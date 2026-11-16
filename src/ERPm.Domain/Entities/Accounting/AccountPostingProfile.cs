using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Accounting;

public class AccountPostingProfile : BaseAuditableEntity
{
    public string Module { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? DimensionKey { get; set; }

    public int DebitAccountId { get; set; }
    public int CreditAccountId { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Account? DebitAccount { get; set; }
    public virtual Account? CreditAccount { get; set; }
}
