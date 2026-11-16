using ERPm.Domain.Common;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Accounting;

public class CashFund : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int BranchId { get; set; }
    public int AccountId { get; set; }                 // Linked GL account (111x)
    public int? CustodianEmployeeId { get; set; }

    public decimal CurrentBalance { get; set; } = 0m;
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Account? Account { get; set; }
    public virtual Employee? CustodianEmployee { get; set; }
}
