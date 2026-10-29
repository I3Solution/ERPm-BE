using ERPm.Domain.Common;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Accounting;

public class JournalEntryLine : BaseEntity
{
    public int JournalEntryId { get; set; }
    public int AccountId { get; set; }

    // Debit and Credit amounts (Required decimal(18, 4))
    public decimal DebitAmount { get; set; } = 0m;     // Debit amount
    public decimal CreditAmount { get; set; } = 0m;    // Credit amount

    public string? Description { get; set; }           // Line description
    public int? PartnerId { get; set; }                // Associated partner (Customer / Supplier) if any

    // Navigation
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual Account? Account { get; set; }
    public virtual PartnerEntity? Partner { get; set; }
}
