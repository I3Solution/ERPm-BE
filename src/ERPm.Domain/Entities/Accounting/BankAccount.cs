using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Accounting;

public class BankAccount : BaseAuditableEntity
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string? BankBranch { get; set; }

    public int BranchId { get; set; }
    public int AccountId { get; set; }                 // Linked GL account (112x)

    public decimal CurrentBalance { get; set; } = 0m;
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Account? Account { get; set; }
}
