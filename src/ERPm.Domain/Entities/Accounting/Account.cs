using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Accounting;

public class Account : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // Account code (e.g. 111, 112, 131, 156, 331, 511, 632)
    public string Name { get; set; } = string.Empty;       // Account name
    public Dictionary<string, string> Translations { get; set; } = new(); // Multi-lingual names

    public AccountType AccountType { get; set; } = AccountType.Asset;

    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Account? Parent { get; set; }
    public virtual ICollection<Account> Children { get; set; } = new List<Account>();
}
