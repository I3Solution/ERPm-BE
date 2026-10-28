using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Localization;

public class AppLanguage : BaseEntity<string>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FlagIcon { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public virtual ICollection<AppDictionary> Dictionaries { get; set; } = new List<AppDictionary>();
}
