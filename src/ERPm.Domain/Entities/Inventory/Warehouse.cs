using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Inventory;

public class Warehouse : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // e.g. KHO01
    public string Name { get; set; } = string.Empty;       // Warehouse name
    public Dictionary<string, string> Translations { get; set; } = new(); // Multi-lingual names
    
    public int BranchId { get; set; }                      // Owning branch ID
    public string? Address { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Branch { get; set; }
}

