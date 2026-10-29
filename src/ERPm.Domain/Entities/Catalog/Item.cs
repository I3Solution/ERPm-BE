using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Catalog;

public class Item : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // SKU (e.g. SP0001)
    public string? Barcode { get; set; }                   // Barcode / QR (e.g. 8935001812345)
    public string Name { get; set; } = string.Empty;       // Primary item name
    public Dictionary<string, string> Translations { get; set; } = new(); // Multi-lingual names {"vi": "...", "en": "..."}

    public ItemType ItemType { get; set; } = ItemType.Goods;

    public int? CategoryId { get; set; }
    public int BaseUomId { get; set; }

    // Pricing and stock thresholds (Required decimal(18, 4) per accounting discipline)
    public decimal SalePrice { get; set; } = 0m;
    public decimal CostPrice { get; set; } = 0m;
    public decimal MinStockAlert { get; set; } = 0m;
    public decimal MaxStockAlert { get; set; } = 0m;

    /// <summary>
    /// Dynamic semi-structured attributes stored in PostgreSQL JSONB.
    /// Supports arbitrary custom fields (e.g. color, size, warranty_months, ram, origin).
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Category? Category { get; set; }
    public virtual UnitOfMeasure? BaseUom { get; set; }
}
