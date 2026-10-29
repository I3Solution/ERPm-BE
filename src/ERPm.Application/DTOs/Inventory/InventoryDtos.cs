namespace ERPm.Application.DTOs.Inventory;

public class WarehouseDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
}

public class StockBalanceDto
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? UomName { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal AverageCostPrice { get; set; }
    public decimal TotalValue { get; set; }
    public bool IsLowStock { get; set; }
}

public class CreateStockReceiptRequest
{
    public Guid RequestId { get; set; }
    public bool IsOpeningBalance { get; set; }
    public int? BranchId { get; set; }
    public int WarehouseId { get; set; }
    public int? SupplierId { get; set; }
    public string? Notes { get; set; }
    public List<StockReceiptItemRequest> Items { get; set; } = new();
}

public class StockReceiptItemRequest
{
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
