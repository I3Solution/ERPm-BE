namespace ERPm.Domain.Entities.System;

/// <summary>
/// AWS IAM style permission document stored as jsonb on roles and as a per-user inline policy.
/// Actions use the <c>module:resource:action</c> format and accept <c>*</c> wildcards.
/// </summary>
public sealed class IamPolicyDocument
{
    public const string CurrentVersion = "2026-10";

    public string Version { get; set; } = CurrentVersion;
    public List<IamStatement> Statements { get; set; } = [];
}

public sealed class IamStatement
{
    public const string Allow = "Allow";
    public const string Deny = "Deny";

    public string? Sid { get; set; }
    public string Effect { get; set; } = Allow;
    public List<string> Actions { get; set; } = [];
    public List<string> Resources { get; set; } = ["*"];
    public IamDataScope? DataScope { get; set; }            // Null falls back to the holder's default scope
}

/// <summary>
/// Data range granted by an Allow statement. Modes are listed in <see cref="DataScopeModes"/>.
/// </summary>
public sealed class IamDataScope
{
    public string Mode { get; set; } = DataScopeModes.BranchTree;
    public bool IncludeDescendants { get; set; } = true;
    public List<int> CustomBranchIds { get; set; } = [];
    public List<int> CustomDepartmentIds { get; set; } = [];
}

public static class DataScopeModes
{
    public const string Self = "SELF";
    public const string Department = "DEPARTMENT";
    public const string DepartmentTree = "DEPARTMENT_TREE";
    public const string Branch = "BRANCH";
    public const string BranchTree = "BRANCH_TREE";
    public const string CustomBranches = "CUSTOM_BRANCHES";
    public const string Company = "COMPANY";
    public const string Group = "GROUP";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Self, Department, DepartmentTree, Branch, BranchTree, CustomBranches, Company, Group]);
}
