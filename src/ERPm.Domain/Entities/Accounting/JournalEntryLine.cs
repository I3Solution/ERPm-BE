using ERPm.Domain.Common;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Accounting;

public class JournalEntryLine : BaseEntity<Guid>
{
    public JournalEntryLine()
    {
        Id = Guid.NewGuid();
    }

    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }

    // Số tiền Nợ & Có (Bắt buộc decimal(18, 4))
    public decimal DebitAmount { get; set; } = 0m;     // Phát sinh Nợ
    public decimal CreditAmount { get; set; } = 0m;    // Phát sinh Có

    public string? Description { get; set; }           // Diễn giải dòng bút toán
    public Guid? PartnerId { get; set; }               // Đối tượng công nợ (Khách hàng / NCC) nếu có

    // Navigation
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual Account? Account { get; set; }
    public virtual PartnerEntity? Partner { get; set; }
}
