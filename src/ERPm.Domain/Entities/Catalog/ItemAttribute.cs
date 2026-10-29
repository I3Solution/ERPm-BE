using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Catalog;

public enum AttributeDataType
{
    Text = 1,
    Number = 2,
    Select = 3,
    Date = 4,
    Boolean = 5
}

/// <summary>
/// Defines dynamic attribute metadata for items, optionally bound to a specific category or global.
/// </summary>
public class ItemAttribute : BaseAuditableEntity
{
    public int? CategoryId { get; set; } // Nullable: applies to all items if null
    public string Code { get; set; } = string.Empty; // e.g. "color", "size", "ram", "warranty_months"
    public string Name { get; set; } = string.Empty; // Display label
    public AttributeDataType DataType { get; set; } = AttributeDataType.Text;
    public List<string> Options { get; set; } = new(); // Predefined options for Select data type
    public string? Unit { get; set; } // Measurement unit (e.g. "GB", "inch", "months", "kg")
    public bool IsRequired { get; set; } = false;
    public bool IsFilterable { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public virtual Category? Category { get; set; }
}
