using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Accounting;

public class Account : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // Số hiệu tài khoản (vd: 111, 112, 131, 156, 331, 511, 632)
    public string Name { get; set; } = string.Empty;       // Tên tài khoản (vd: Tiền mặt, Doanh thu bán hàng)
    public Dictionary<string, string> Translations { get; set; } = new(); // {"vi": "Tiền mặt", "en": "Cash on Hand"}

    public AccountType AccountType { get; set; } = AccountType.Asset;

    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Account? Parent { get; set; }
    public virtual ICollection<Account> Children { get; set; } = new List<Account>();
}
