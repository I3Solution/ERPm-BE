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
    /// <summary>Held for confirmed sales orders.</summary>
    public decimal QuantityReserved { get; set; }
    /// <summary>On hand minus reserved: what can still be sold.</summary>
    public decimal QuantityAvailable { get; set; }
    public decimal AverageCostPrice { get; set; }
    public decimal TotalValue { get; set; }
    public bool IsLowStock { get; set; }
}

public class CreateStockReceiptRequest
{
    public Guid RequestId { get; set; }
    public bool IsOpeningBalance { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
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

/// <summary>A warehouse a selling branch may ship sales orders from.</summary>
public class ServingWarehouseDto
{
    public int WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BranchId { get; set; }                      // Branch that owns the warehouse
    public string? BranchName { get; set; }
    public bool IsDefault { get; set; }
    /// <summary>False when the branch has no list and ships from its own warehouses.</summary>
    public bool IsConfigured { get; set; }
}

public class ServingWarehouseLine
{
    public int WarehouseId { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>Replaces the branch's whole list. An empty list goes back to the branch's own warehouses.</summary>
public class UpdateServingWarehousesRequest
{
    public List<ServingWarehouseLine> Warehouses { get; set; } = new();
}

/// <summary>Stock of one item in one warehouse: available is on hand less what orders hold.</summary>
public class StockAvailabilityDto
{
    public int ItemId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityAvailable { get; set; }
}
