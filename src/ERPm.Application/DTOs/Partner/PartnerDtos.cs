using ERPm.Domain.Entities.Partner;

namespace ERPm.Application.DTOs.Partner;

public class PartnerDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PartnerType PartnerType { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal DebtBalance { get; set; }
    public int? PriceListId { get; set; }
    public bool IsUnlimitedCredit { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePartnerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PartnerType PartnerType { get; set; } = PartnerType.Customer;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public decimal CreditLimit { get; set; } = 0m;
    public int? PriceListId { get; set; }
    public bool IsUnlimitedCredit { get; set; }
    public string? Notes { get; set; }
}
