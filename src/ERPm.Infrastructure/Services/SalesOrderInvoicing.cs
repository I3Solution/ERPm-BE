using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public partial class SalesInvoiceService
{
    /// <summary>
    /// Invoices a whole Confirmed or Processing order in one go: the stock held for the order is given back, then the
    /// goods are issued and the invoice posted at the prices agreed on the order. The order becomes Completed.
    /// </summary>
    public async Task<SalesInvoiceDto> CreateInvoiceFromOrderAsync(int orderId, CreateInvoiceFromOrderRequest request, CancellationToken cancellationToken = default)
    {
        Require(request.WarehouseId > 0 && Enum.IsDefined((PaymentMethod)request.PaymentMethod), "error.validation.payment_method");
        ValidateNumber(request.TenderedAmount);
        ERPm.Domain.Events.NotificationEvent? publish = null;

        var invoice = await posting.RunAsync(async () =>
        {
            await posting.LockAsync($"order:Sales:{orderId}", cancellationToken);
            var order = await context.SalesOrders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            Require(order != null, "error.order.not_found", 404);
            Require(order!.Status is SalesOrderStatus.Confirmed or SalesOrderStatus.Processing && order.Items.All(i => i.InvoicedQuantity == 0), "error.sd.order_invalid_status_for_action", 409);
            Require(order.TaxAmount == 0, "error.sd.order_tax_not_supported", 409);

            // Order discounts become one invoice discount so the invoice total equals the order total exactly.
            var invoiceRequest = new CreateSalesInvoiceRequest
            {
                RequestId = Guid.NewGuid(), BranchId = order.BranchId, DepartmentId = order.DepartmentId, CustomerId = order.CustomerId, CustomerName = order.CustomerName,
                WarehouseId = request.WarehouseId, PaymentMethod = (PaymentMethod)request.PaymentMethod, PaidAmount = request.TenderedAmount,
                DiscountAmount = order.Items.Sum(i => i.DiscountAmount) + order.DiscountAmount,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? order.OrderNumber : $"{order.OrderNumber}: {request.Notes.Trim()}",
                Items = order.Items.Select(i => new SalesInvoiceItemRequest { ItemId = i.ItemId, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList()
            };
            Require(invoiceRequest.Notes!.Length <= 500, "error.validation.request");

            // The order's own reservation would otherwise count against the stock it is about to take.
            await new OrderStockReservations(context, posting).ReleaseAsync(order, cancellationToken);
            var posted = await PostInvoiceAsync(invoiceRequest, Hash(invoiceRequest), order.Id, cancellationToken);

            foreach (var line in order.Items) line.InvoicedQuantity = line.Quantity;
            order.Status = SalesOrderStatus.Completed;

            publish = new ERPm.Domain.Events.NotificationEvent(
                "SD_ORDER_INVOICED", ERPm.Domain.Enums.NotificationCategory.SD, ERPm.Domain.Enums.NotificationSeverity.INFO,
                ERPm.Domain.Enums.NotificationTargetType.BranchTree, branchId: order.BranchId,
                parameters: new Dictionary<string, object> { ["order_code"] = order.OrderNumber, ["invoice_code"] = posted.InvoiceNumber, ["total_amount"] = posted.TotalAmount },
                actionUrl: "/sales/invoices");
            return posted;
        }, cancellationToken);

        if (notificationEngine != null && publish != null) _ = notificationEngine.PublishAsync(publish);
        return invoice;
    }
}
