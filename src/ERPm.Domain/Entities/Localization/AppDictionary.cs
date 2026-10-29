using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.Localization;

public class AppDictionary : BaseEntity
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Category { get; set; } = "COMMON"; // COMMON, CATALOG, SALES, INVENTORY, ACCOUNTING, ERROR
    public string Key { get; set; } = string.Empty;   // e.g. "sales.invoice.create"
    public string Value { get; set; } = string.Empty; // Translated string

    // Navigation
    public virtual AppLanguage? Language { get; set; }
}
