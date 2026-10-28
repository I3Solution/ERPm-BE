using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Catalog;

public class Category : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;   // DM01
    public string Name { get; set; } = string.Empty;   // Bút viết
    public Dictionary<string, string> Translations { get; set; } = new(); // {"vi": "Bút viết", "en": "Pens"}
    
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public virtual Category? Parent { get; set; }
    public virtual ICollection<Category> Children { get; set; } = new List<Category>();
    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
