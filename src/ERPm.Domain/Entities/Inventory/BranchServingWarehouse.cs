using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Inventory;

/// <summary>
/// A warehouse a selling branch may ship its sales orders from. It may belong to another branch of the same company.
/// A branch without rows ships from its own active warehouses, as before.
/// </summary>
public class BranchServingWarehouse : BaseAuditableEntity
{
    public int BranchId { get; set; }                      // Selling branch
    public int WarehouseId { get; set; }
    public bool IsDefault { get; set; }                    // Picked for an order that names no warehouse

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
}
