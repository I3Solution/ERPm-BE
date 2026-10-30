namespace ERPm.Domain.Services.Iam;

/// <summary>
/// Rows a permission reaches: everything, or any row whose branch OR department is listed, OR that the
/// user created (SELF). Several Allow statements are merged by union.
/// </summary>
public sealed class EffectiveDataScope
{
    public static readonly EffectiveDataScope None = new(false, new HashSet<int>(), new HashSet<int>(), false);
    public static readonly EffectiveDataScope Everything = new(true, new HashSet<int>(), new HashSet<int>(), false);

    public EffectiveDataScope(bool unrestricted, IReadOnlySet<int> branchIds, IReadOnlySet<int> departmentIds, bool ownRecords)
    {
        Unrestricted = unrestricted;
        BranchIds = branchIds;
        DepartmentIds = departmentIds;
        OwnRecords = ownRecords;
    }

    public bool Unrestricted { get; }
    public IReadOnlySet<int> BranchIds { get; }
    public IReadOnlySet<int> DepartmentIds { get; }
    public bool OwnRecords { get; }
    public bool IsEmpty => !Unrestricted && BranchIds.Count == 0 && DepartmentIds.Count == 0 && !OwnRecords;

    public EffectiveDataScope Union(EffectiveDataScope other)
    {
        if (Unrestricted || other.IsEmpty) return this;
        if (other.Unrestricted || IsEmpty) return other;
        return new(false, BranchIds.Union(other.BranchIds).ToHashSet(), DepartmentIds.Union(other.DepartmentIds).ToHashSet(),
            OwnRecords || other.OwnRecords);
    }

    public bool CoversBranch(int branchId) => Unrestricted || BranchIds.Contains(branchId);
}

public sealed record PermissionDecision(bool Allowed, bool ExplicitDeny, EffectiveDataScope Scope)
{
    public static readonly PermissionDecision Denied = new(false, false, EffectiveDataScope.None);
}
