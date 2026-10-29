namespace ERPm.Domain.Services.HumanResource;

/// <summary>
/// One approved work-history row of an employee (hire, transfer or termination).
/// A null <see cref="CompanyId"/> inherits the legal entity of the previous row.
/// </summary>
public sealed record TimelineEntry(
    int Id,
    DateOnly DateEffective,
    int? OrgStructureId,
    int? CompanyId,
    bool IsTermination,
    DateTime CreatedAt);

public sealed record CompanySegment(DateOnly From, DateOnly To, int CompanyId, int? OrgStructureId);

public sealed record EmploymentSpan(DateOnly From, DateOnly To, int? CompanyId, int? OrgStructureId);

public sealed record CompanyBoundary(DateOnly Date, int FromCompanyId, int ToCompanyId);

/// <summary>
/// Work-history (QTCT) rules: who may see or edit an employee's dated records, and how a period splits
/// by legal entity for attendance and payroll. Pure logic, no database access.
/// </summary>
public sealed class WorkTimeline
{
    private readonly TimelineEntry[] _entries;

    public WorkTimeline(IEnumerable<TimelineEntry> approvedEntries)
    {
        int? company = null;
        _entries = approvedEntries
            .OrderBy(e => e.DateEffective)
            .ThenBy(e => e.CreatedAt)
            .Select(e =>
            {
                company = e.CompanyId ?? company;
                return e with { CompanyId = company };
            })
            .ToArray();
    }

    public IReadOnlyList<TimelineEntry> Entries => _entries;

    /// <summary>Row in force on <paramref name="date"/>; on the same day the last created row wins.</summary>
    public TimelineEntry? AssignmentAt(DateOnly date)
    {
        for (var i = _entries.Length - 1; i >= 0; i--)
            if (_entries[i].DateEffective <= date) return _entries[i];
        return null;
    }

    /// <summary>
    /// Exclusive end date of the window in which the scope owns the employee: the date of the row that follows
    /// the last row (up to today) covered by the scope. MaxValue = still owned; null = never owned.
    /// </summary>
    public DateOnly? VisibleBefore(DataScope scope, DateOnly today)
    {
        DateOnly? lastIn = null;
        foreach (var e in _entries)
        {
            if (e.DateEffective > today) break;
            if (!e.IsTermination && scope.Covers(e.OrgStructureId, e.CompanyId)) lastIn = e.DateEffective;
        }

        if (lastIn is null) return null;
        foreach (var e in _entries)
            if (e.DateEffective > lastIn) return e.DateEffective;
        return DateOnly.MaxValue;
    }

    public bool CanSee(DataScope scope, int profileId, DateOnly anchor, DateOnly today) =>
        scope.AlwaysSees(profileId) || VisibleBefore(scope, today) is { } before && anchor < before;

    /// <summary>Records may only be edited by the scope that owned the employee on the record's anchor date.</summary>
    public bool CanWrite(DataScope scope, int profileId, DateOnly anchor, DateOnly today)
    {
        if (scope.All || scope.GrantedProfileIds.Contains(profileId)) return true;
        if (scope.OwnProfileId == profileId) return Covers(scope, AssignmentAt(today));
        if (VisibleBefore(scope, today) is not { } before || anchor >= before) return false;
        return Covers(scope, AssignmentAt(anchor));
    }

    /// <summary>Keeps a transferred-out employee selectable for <paramref name="daysAfterTransfer"/> days.</summary>
    public bool IsWithinSelectorWindow(DataScope scope, int profileId, DateOnly today, int daysAfterTransfer)
    {
        if (scope.AlwaysSees(profileId)) return true;
        if (VisibleBefore(scope, today) is not { } before) return false;
        return before > today || daysAfterTransfer > 0 && today.DayNumber - before.DayNumber < daysAfterTransfer;
    }

    public CompanyBoundary? FirstCompanyBoundary(DateOnly from, DateOnly to)
    {
        var company = AssignmentAt(from) is { IsTermination: false } start ? start.CompanyId : null;
        foreach (var e in _entries)
        {
            if (e.DateEffective <= from || e.IsTermination) continue;
            if (e.DateEffective > to) break;
            if (company is { } c && e.CompanyId is { } next && next != c) return new CompanyBoundary(e.DateEffective, c, next);
            company = e.CompanyId ?? company;
        }
        return null;
    }

    /// <summary>Employed days of the period grouped by legal entity; rows without a legal entity are skipped.</summary>
    public IReadOnlyList<CompanySegment> CompanySegments(DateOnly from, DateOnly to)
    {
        var points = _entries.Select(e => e.DateEffective).Where(d => d > from && d <= to).Distinct().Prepend(from).ToList();
        var segments = new List<CompanySegment>();

        for (var i = 0; i < points.Count; i++)
        {
            var start = points[i];
            var end = i + 1 < points.Count ? points[i + 1].AddDays(-1) : to;
            var at = AssignmentAt(start);
            if (at is null || at.IsTermination || at.CompanyId is not { } company) continue;

            if (segments.Count > 0 && segments[^1] is var last && last.CompanyId == company && last.To.AddDays(1) == start)
                segments[^1] = last with { To = end, OrgStructureId = at.OrgStructureId ?? last.OrgStructureId };
            else
                segments.Add(new CompanySegment(start, end, company, at.OrgStructureId));
        }

        return segments;
    }

    /// <summary>Employed days of the period; a change of legal entity or a rehire starts a new span.</summary>
    public IReadOnlyList<EmploymentSpan> EmploymentSegments(DateOnly from, DateOnly to)
    {
        var points = _entries.Select(e => e.DateEffective).Where(d => d > from && d <= to).Distinct().Prepend(from).ToList();
        var spans = new List<EmploymentSpan>();

        for (var i = 0; i < points.Count; i++)
        {
            var start = points[i];
            var end = i + 1 < points.Count ? points[i + 1].AddDays(-1) : to;
            var at = AssignmentAt(start);
            if (at is null || at.IsTermination) continue;

            if (spans.Count > 0 && spans[^1] is var last && last.CompanyId == at.CompanyId && last.To.AddDays(1) == start)
                spans[^1] = last with { To = end, OrgStructureId = at.OrgStructureId ?? last.OrgStructureId };
            else
                spans.Add(new EmploymentSpan(start, end, at.CompanyId, at.OrgStructureId));
        }

        return spans;
    }

    private static bool Covers(DataScope scope, TimelineEntry? at) =>
        at is { IsTermination: false } && scope.Covers(at.OrgStructureId, at.CompanyId);
}
