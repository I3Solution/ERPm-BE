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

/// <summary>
/// Without <see cref="Mode"/> (legacy client) it replaces the branch's whole list and an empty list means the branch's own
/// warehouses. With a mode (<c>fixed_list</c>, <c>own_warehouses</c>, <c>inherit</c>) a reason is required.
/// </summary>
public class UpdateServingWarehousesRequest
{
    public string? Mode { get; set; }
    public List<ServingWarehouseLine> Warehouses { get; set; } = new();
    public string? Reason { get; set; }
}

/// <summary>Branch Settings view of the module key <c>wms.so_warehouse_policy</c> (contract section 4).</summary>
public class ServingPolicyView
{
    public int BranchId { get; set; }
    public string Key { get; set; } = "wms.so_warehouse_policy";
    public string StorageKind { get; set; } = "module";
    public string EditorKey { get; set; } = "wms.so_warehouse_policy";
    public ServingPolicyLocal Local { get; set; } = new();
    public ServingPolicyEffective Effective { get; set; } = new();
    public bool CanEdit { get; set; }
    public int Revision { get; set; }
}

public class ServingPolicyLocal
{
    public string Mode { get; set; } = "inherit";          // inherit | override
    public ServingPolicyValue? Value { get; set; }
}

public class ServingPolicyValue
{
    public string Mode { get; set; } = "own_warehouses";   // fixed_list | own_warehouses
    public List<ServingWarehouseLine>? Warehouses { get; set; }
}

public class ServingPolicyEffective
{
    public string Status { get; set; } = "resolved";       // resolved | invalid
    public string? ErrorCode { get; set; }
    public string Mode { get; set; } = "own_warehouses";
    public List<ServingWarehouseDto> Warehouses { get; set; } = new();
    public int? DefaultWarehouseId { get; set; }
    public ServingPolicySource Source { get; set; } = new();
}

public class ServingPolicySource
{
    public string Kind { get; set; } = "module_fallback";  // local | ancestor | module_fallback
    public int? BranchId { get; set; }                     // Only when the caller may see the source unit
    public string? BranchName { get; set; }
}

/// <summary>What a save would change in the unit and the units inheriting from it.</summary>
public class ServingPolicyPreview
{
    public bool Blocked { get; set; }
    public int AffectedBranchCount { get; set; }
    public List<ServingPolicyChange> Changes { get; set; } = new();
    public List<ServingPolicyBlocker> Blockers { get; set; } = new();
}

public class ServingPolicyChange
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public ServingPolicySide Before { get; set; } = new();
    public ServingPolicySide After { get; set; } = new();
}

public class ServingPolicySide
{
    public List<int> WarehouseIds { get; set; } = new();
    public int? DefaultWarehouseId { get; set; }
    public string SourceKind { get; set; } = "module_fallback";
    public string Status { get; set; } = "resolved";
}

public class ServingPolicyBlocker
{
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Stock of one item in one warehouse: available is on hand less what orders hold.</summary>
public class StockAvailabilityDto
{
    public int ItemId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityAvailable { get; set; }
}
