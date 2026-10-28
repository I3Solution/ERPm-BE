using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Catalog;

public class UnitOfMeasure : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;   // CAI, HOP, KG
    public string Name { get; set; } = string.Empty;   // Cái
    public Dictionary<string, string> Translations { get; set; } = new(); // {"vi": "Cái", "en": "Piece"}
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
