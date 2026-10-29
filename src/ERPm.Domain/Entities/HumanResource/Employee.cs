using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.HumanResource;

public enum Gender
{
    Unspecified = 0,
    Male = 1,
    Female = 2
}

/// <summary>
/// Employee profile (HRM9: Hre_Profile). Current org unit / legal entity are derived from approved
/// <see cref="WorkHistory"/> rows, not stored here.
/// </summary>
public class Employee : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;          // Full name
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? IdentityNumber { get; set; }               // Citizen ID / passport
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? HireDate { get; set; }
    public int? UserId { get; set; }                          // Linked internal account (one employee per account)
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual AppUser? User { get; set; }
    public virtual ICollection<WorkHistory> WorkHistories { get; set; } = new List<WorkHistory>();
}
