namespace ERPm.Application.DTOs.Localization;

public class LanguageDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FlagIcon { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class DictionaryBundleDto
{
    public string LanguageCode { get; set; } = string.Empty;
    public Dictionary<string, string> Entries { get; set; } = new();
}

public class UpsertDictionaryRequest
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Category { get; set; } = "COMMON";
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
