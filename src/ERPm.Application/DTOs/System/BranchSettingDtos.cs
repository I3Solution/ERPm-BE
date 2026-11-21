using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERPm.Application.DTOs.System;

public class SettingDefinitionDto
{
    public string Key { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public Dictionary<string, string> Labels { get; set; } = new();
    public JsonElement Constraints { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public JsonElement? DefaultValue { get; set; }
    public string StorageKind { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EditorKey { get; set; }
    public int SortOrder { get; set; }
}

public class CreateSettingDefinitionRequest
{
    public string? Key { get; set; }
    public string? GroupKey { get; set; }
    public string? ValueType { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    /// <summary>Undefined when the property is absent, Null when sent as JSON null.</summary>
    public JsonElement DefaultValue { get; set; }
    public JsonElement Constraints { get; set; }
    public int SortOrder { get; set; }
}

public class SettingLocalDto
{
    public string Mode { get; set; } = "inherit";              // inherit | override
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public JsonElement? Value { get; set; }
}

public class SettingSourceDto
{
    public string Kind { get; set; } = string.Empty;          // local | ancestor | system_default | module_fallback
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BranchId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BranchName { get; set; }
}

public class SettingEffectiveDto
{
    public string Status { get; set; } = "resolved";          // resolved | invalid
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public JsonElement? Value { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ErrorCode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SettingSourceDto? Source { get; set; }
}

public class SettingViewDto
{
    public int BranchId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string StorageKind { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EditorKey { get; set; }
    public SettingLocalDto Local { get; set; } = new();
    public SettingEffectiveDto Effective { get; set; } = new();
    public bool CanEdit { get; set; }
    public string Etag { get; set; } = string.Empty;
}

public class SettingListDto
{
    public List<SettingViewDto> Items { get; set; } = new();
}

public class UpdateBranchSettingRequest
{
    public string? Mode { get; set; }
    /// <summary>Undefined when the property is absent, Null when sent as JSON null.</summary>
    public JsonElement Value { get; set; }
    public string? Reason { get; set; }
}

public class SettingImpactSideDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public JsonElement? Value { get; set; }
    public string SourceKind { get; set; } = string.Empty;
}

public class SettingImpactChangeDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public SettingImpactSideDto Before { get; set; } = new();
    public SettingImpactSideDto After { get; set; } = new();
}

public class SettingBlockerDto
{
    public string Code { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Count { get; set; }
}

public class SettingImpactDto
{
    public bool Blocked { get; set; }
    public int AffectedBranchCount { get; set; }
    public List<SettingImpactChangeDto> Changes { get; set; } = new();
    public List<SettingBlockerDto> Blockers { get; set; } = new();
}

public class SettingHistoryItemDto
{
    public int BranchId { get; set; }
    public string Key { get; set; } = string.Empty;
    public SettingLocalDto PreviousLocal { get; set; } = new();
    public SettingLocalDto NewLocal { get; set; } = new();
    public string? ChangedBy { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? Reason { get; set; }
    public int Revision { get; set; }
}
