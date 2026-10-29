using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Partner;

public class Partner : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // Partner code (e.g. KH0001, NCC0001)
    public string Name { get; set; } = string.Empty;       // Company or full name
    public PartnerType PartnerType { get; set; } = PartnerType.Customer;

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }                   // Tax identification number

    // Receivables/Payables (Required decimal(18, 4))
    public decimal CreditLimit { get; set; } = 0m;         // Maximum allowed credit limit
    public decimal DebtBalance { get; set; } = 0m;         // Current debt balance (positive: receivable, negative: payable)

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
