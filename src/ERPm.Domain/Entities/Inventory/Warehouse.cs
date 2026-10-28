using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Inventory;

public class Warehouse : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // KHO01
    public string Name { get; set; } = string.Empty;       // Kho Tổng
    public Dictionary<string, string> Translations { get; set; } = new(); // {"vi": "Kho Tổng", "en": "Main Warehouse"}
    
    public Guid BranchId { get; set; }                     // Chi nhánh sở hữu kho
    public string? Address { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Branch { get; set; }
}

