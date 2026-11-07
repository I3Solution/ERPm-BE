using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Sales;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>Holds and gives back the stock of sales orders. Callers run it inside their own posting transaction.</summary>
internal sealed class OrderStockReservations(ApplicationDbContext context, PostingTransaction posting)
{
    /// <summary>
    /// Holds the goods of every line in the warehouses of the order's branch (largest available first).
    /// All lines are reserved or none: a shortage rejects the whole request.
    /// </summary>
    public async Task ReserveAsync(SalesOrder order, CancellationToken ct)
    {
        var itemIds = order.Items.Select(i => i.ItemId).Distinct().Order().ToArray();
        var goods = await context.Items.Where(i => itemIds.Contains(i.Id) && i.ItemType == ItemType.Goods).Select(i => i.Id).ToListAsync(ct);
        var lines = order.Items.Where(i => goods.Contains(i.ItemId)).ToList();
        if (lines.Count == 0) { order.ReservationStatus = OrderReservationStatus.None; return; }

        var warehouseIds = await context.Warehouses.IgnoreQueryFilters().Where(w => !w.IsDeleted && w.IsActive && w.BranchId == order.BranchId).Select(w => w.Id).OrderBy(w => w).ToListAsync(ct);
        var goodsIds = goods.Order().ToArray();
        foreach (var warehouseId in warehouseIds)
            foreach (var itemId in goodsIds) await posting.LockAsync($"stock:{warehouseId}:{itemId}", ct);

        var balances = await context.StockBalances.IgnoreQueryFilters()
            .Where(b => !b.IsDeleted && warehouseIds.Contains(b.WarehouseId) && goodsIds.Contains(b.ItemId)).ToListAsync(ct);
        foreach (var line in lines)
        {
            var need = line.Quantity;
            foreach (var balance in balances.Where(b => b.ItemId == line.ItemId).OrderByDescending(b => b.QuantityOnHand - b.QuantityReserved).ThenBy(b => b.WarehouseId))
            {
                var take = Math.Min(need, balance.QuantityOnHand - balance.QuantityReserved);
                if (take <= 0) continue;
                balance.QuantityReserved += take;
                context.StockReservations.Add(new StockReservation { SalesOrderId = order.Id, SalesOrderItemId = line.Id, WarehouseId = balance.WarehouseId, ItemId = line.ItemId, Quantity = take });
                need -= take;
                if (need == 0) break;
            }
            Require(need == 0, "error.sd.insufficient_available_stock", 409);
        }
        order.ReservationStatus = OrderReservationStatus.Reserved;
    }

    /// <summary>Gives the order's active reservations back to the available stock.</summary>
    public async Task ReleaseAsync(SalesOrder order, CancellationToken ct)
    {
        var active = await context.StockReservations.Where(r => r.SalesOrderId == order.Id && r.Status == ReservationRecordStatus.Active).ToListAsync(ct);
        if (active.Count == 0) return;
        foreach (var key in active.Select(r => (r.WarehouseId, r.ItemId)).Distinct().Order())
            await posting.LockAsync($"stock:{key.WarehouseId}:{key.ItemId}", ct);

        var warehouseIds = active.Select(r => r.WarehouseId).Distinct().ToArray();
        var itemIds = active.Select(r => r.ItemId).Distinct().ToArray();
        var balances = await context.StockBalances.IgnoreQueryFilters().Where(b => warehouseIds.Contains(b.WarehouseId) && itemIds.Contains(b.ItemId)).ToListAsync(ct);
        foreach (var reservation in active)
        {
            var balance = balances.Single(b => b.WarehouseId == reservation.WarehouseId && b.ItemId == reservation.ItemId);
            balance.QuantityReserved = Math.Max(0m, balance.QuantityReserved - reservation.Quantity);
            reservation.Status = ReservationRecordStatus.Released;
            reservation.ReleasedAtUtc = DateTime.UtcNow;
        }
        order.ReservationStatus = OrderReservationStatus.Released;
    }
}
