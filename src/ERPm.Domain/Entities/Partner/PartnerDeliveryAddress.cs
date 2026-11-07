using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Partner;

/// <summary>
/// One delivery address of a partner. A partner can have many; at most one is the default.
/// </summary>
public class PartnerDeliveryAddress : BaseAuditableEntity
{
    public int PartnerId { get; set; }
    public string RecipientName { get; set; } = string.Empty;   // Person or site receiving the goods
    public string? Phone { get; set; }
    public string Address { get; set; } = string.Empty;         // Street line
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public bool IsDefault { get; set; }

    // Navigation
    public virtual Partner? Partner { get; set; }
}
