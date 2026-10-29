namespace ERPm.Domain.Services.HumanResource;

/// <summary>
/// One data-permission row granted to a user: an org unit (expanded to its subtree), a legal entity, or both.
/// </summary>
public sealed record ScopeRow(int? OrgStructureId, int? CompanyId);

/// <summary>
/// Resolved HRM data scope of the current user. Pure rule object (no database access) so it can be
/// unit-verified and reused by query builders that translate it into SQL.
/// </summary>
public sealed class DataScope
{
    private readonly IReadOnlyList<(IReadOnlySet<int>? Orgs, int? Company)> _pairs;

    private DataScope(bool all, IReadOnlyList<(IReadOnlySet<int>? Orgs, int? Company)> pairs,
        IReadOnlySet<int> grantedProfiles, int? ownProfileId)
    {
        All = all;
        _pairs = pairs;
        GrantedProfileIds = grantedProfiles;
        OwnProfileId = ownProfileId;
    }

    public bool All { get; }
    public IReadOnlySet<int> GrantedProfileIds { get; }
    public int? OwnProfileId { get; }
    public bool IsEmpty => !All && _pairs.Count == 0;
    public IReadOnlyList<(IReadOnlySet<int>? Orgs, int? Company)> Pairs => _pairs;

    public static DataScope Unrestricted(int? ownProfileId = null) =>
        new(true, [], new HashSet<int>(), ownProfileId);

    /// <summary>
    /// Builds a scope from permission rows. When any row pairs an org unit with a legal entity, only paired rows
    /// apply and single-dimension rows are ignored (HRM9 rule). <paramref name="expandOrg"/> returns the org unit
    /// together with all of its descendants.
    /// </summary>
    public static DataScope Create(IEnumerable<ScopeRow> rows, Func<int, IReadOnlySet<int>> expandOrg,
        IEnumerable<int> grantedProfiles, int? ownProfileId)
    {
        var effective = rows.Where(r => r.OrgStructureId is not null || r.CompanyId is not null).Distinct().ToList();
        if (effective.Any(r => r.OrgStructureId is not null && r.CompanyId is not null))
            effective = effective.Where(r => r.OrgStructureId is not null && r.CompanyId is not null).ToList();

        var pairs = effective
            .Select(r => (r.OrgStructureId is { } org ? expandOrg(org) : null, r.CompanyId))
            .ToList();
        return new DataScope(false, pairs, grantedProfiles.ToHashSet(), ownProfileId);
    }

    public bool Covers(int? orgStructureId, int? companyId)
    {
        if (All) return true;
        if (orgStructureId is null) return false;
        foreach (var (orgs, company) in _pairs)
        {
            if (orgs is not null && !orgs.Contains(orgStructureId.Value)) continue;
            if (company is not null && company != companyId) continue;
            return true;
        }
        return false;
    }

    public bool AlwaysSees(int profileId) => All || GrantedProfileIds.Contains(profileId) || OwnProfileId == profileId;
}
