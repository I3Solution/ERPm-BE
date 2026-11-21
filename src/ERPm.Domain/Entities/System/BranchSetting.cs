using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>Catalog entry of a branch setting: stable key, value type, labels, constraints and default (JSON text).</summary>
public class SettingDefinition : BaseAuditableEntity
{
    public const string Scalar = "scalar";
    public const string Module = "module";

    public string Key { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;   // boolean, integer, decimal, time, string, choice
    public string LabelsJson { get; set; } = "{}";
    public string ConstraintsJson { get; set; } = "{}";
    public string? DefaultValueJson { get; set; }
    public string StorageKind { get; set; } = Scalar;      // scalar values live in BranchSetting; module values in the owning module
    public string? EditorKey { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Current override of one scalar key at one branch. Missing row means the branch inherits.</summary>
public class BranchSetting : BaseAuditableEntity
{
    public int BranchId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty;
    public int Revision { get; set; }

    public virtual Branch? Branch { get; set; }
    public virtual SettingDefinition? Definition { get; set; }
}

/// <summary>Append-only change log of a branch setting; a null JSON side means "inherit".</summary>
public class BranchSettingHistory : BaseEntity
{
    public int BranchId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    public int Revision { get; set; }

    public virtual Branch? Branch { get; set; }
}
