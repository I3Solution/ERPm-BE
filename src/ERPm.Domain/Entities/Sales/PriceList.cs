using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Sales;

/// <summary>
/// A selling price list for a customer segment (retail, dealer, wholesale, VIP) or a season/branch.
/// </summary>
public class PriceList : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // Unique, e.g. PL-RETAIL
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";          // ISO 4217

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }             // Open-ended when null

    public int? BranchId { get; set; }                     // Null = applies to every branch
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<PriceListItem> Items { get; set; } = new List<PriceListItem>();
}

/// <summary>
/// One price line. Several lines for the same item express quantity tiers (MinQuantity).
/// </summary>
public class PriceListItem : BaseEntity
{
    public int PriceListId { get; set; }
    public int ItemId { get; set; }
    public int? UomId { get; set; }                        // Null = the item's base unit

    public decimal UnitPrice { get; set; } = 0m;           // decimal(18, 4)
    public decimal MinQuantity { get; set; } = 0m;         // Tier threshold, decimal(18, 4)
    public decimal DiscountPercent { get; set; } = 0m;     // 0..100, decimal(18, 4)

    // Navigation
    public virtual PriceList? PriceList { get; set; }
    public virtual Item? Item { get; set; }
    public virtual UnitOfMeasure? Uom { get; set; }
}
