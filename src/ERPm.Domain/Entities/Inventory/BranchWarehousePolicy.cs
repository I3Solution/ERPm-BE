using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Inventory;

public enum WarehousePolicyMode
{
    FixedList = 1,      // The branch's own rows in tbl_WMS_BranchServingWarehouse
    OwnWarehouses = 2   // Active warehouses of the branch that raises the order
}

/// <summary>
/// Where a company or branch ships its sales orders from (Branch Settings key <c>wms.so_warehouse_policy</c>).
/// A unit without an active row inherits from its nearest active ancestor of the same company.
/// </summary>
public class BranchWarehousePolicy : BaseAuditableEntity
{
    public int BranchId { get; set; }
    public WarehousePolicyMode Mode { get; set; }
    public int Revision { get; set; } = 1;

    // Navigation
    public virtual Branch? Branch { get; set; }
}
