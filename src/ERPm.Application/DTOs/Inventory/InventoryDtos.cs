namespace ERPm.Application.DTOs.Inventory;

public class WarehouseDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
}

public class StockBalanceDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? UomName { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal AverageCostPrice { get; set; }
    public decimal TotalValue => QuantityOnHand * AverageCostPrice;
    public bool IsLowStock { get; set; }
}

public class CreateStockReceiptRequest
{
    public Guid? BranchId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Notes { get; set; }
    public List<StockReceiptItemRequest> Items { get; set; } = new();
}

public class StockReceiptItemRequest
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
