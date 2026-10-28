using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Partner;

public class Partner : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // Mã đối tác (vd: KH0001, NCC0001)
    public string Name { get; set; } = string.Empty;       // Tên công ty hoặc họ tên
    public PartnerType PartnerType { get; set; } = PartnerType.Customer;

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }                   // Mã số thuế

    // Công nợ (Bắt buộc decimal(18, 4))
    public decimal CreditLimit { get; set; } = 0m;         // Hạn mức nợ tối đa cho phép
    public decimal DebtBalance { get; set; } = 0m;         // Dư nợ hiện tại (dương: phải thu, âm: phải trả)

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
