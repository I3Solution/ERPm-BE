using System.Globalization;
using ERPm.Domain.Services.HumanResource;

// HRM domain rule verification (no database). Same style as ERPm.Verification: each Check prints PASS or throws.
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); checks++; Console.WriteLine("PASS: " + name); }

// ---------------------------------------------------------------------------------------------
// Work-history (QTCT) visibility and edit rights
// ---------------------------------------------------------------------------------------------
const int hq = 1, gl = 2, dl = 3;            // legal entities
const int hrDept = 11, bap = 12, bo = 13;    // org units
const int profile = 100;
var today = new DateOnly(2026, 10, 28);
var nextEntryId = 0;

TimelineEntry Entry(DateOnly date, int? org, int? company, bool stop = false, int order = 0) =>
    new(++nextEntryId, date, org, company, stop, new DateTime(2026, 1, 1).AddMinutes(order));
DataScope Company(int company) => DataScope.Create([new ScopeRow(null, company)], id => new HashSet<int> { id }, [], null);
WorkTimeline Demo() => new([
    Entry(new(2022, 1, 1), hrDept, hq),
    Entry(new(2026, 8, 1), bap, gl),
    Entry(new(2026, 9, 1), bo, dl),
    Entry(new(2026, 10, 1), hrDept, hq),
]);

{
    var timeline = Demo(); var scope = Company(gl);
    Check(timeline.VisibleBefore(scope, today) == new DateOnly(2026, 9, 1)
        && timeline.CanSee(scope, profile, new(2026, 8, 31), today)
        && !timeline.CanSee(scope, profile, new(2026, 9, 1), today)
        && timeline.CanWrite(scope, profile, new(2026, 8, 15), today)
        && !timeline.CanWrite(scope, profile, new(2026, 7, 1), today),
        "old owner sees rows before transfer-out and edits only its own period");
}
{
    var timeline = Demo(); var scope = Company(hq);
    Check(timeline.VisibleBefore(scope, today) == DateOnly.MaxValue
        && timeline.CanSee(scope, profile, new(2026, 8, 10), today)
        && !timeline.CanWrite(scope, profile, new(2026, 8, 10), today)
        && timeline.CanWrite(scope, profile, new(2025, 3, 1), today)
        && timeline.CanWrite(scope, profile, new(2026, 10, 5), today),
        "current owner sees everything but cannot edit other entities' periods");
}
{
    var day = new DateOnly(2026, 5, 1);
    var timeline = new WorkTimeline([Entry(day, bap, gl, order: 2), Entry(day, bo, dl, order: 1)]);
    Check(timeline.AssignmentAt(day)!.CompanyId == gl, "same-day entries: last created wins");
}
{
    var quit = new DateOnly(2026, 10, 15);
    var timeline = new WorkTimeline([Entry(new(2024, 1, 1), bap, gl), Entry(quit, bap, gl, stop: true)]);
    var scope = Company(gl);
    Check(timeline.VisibleBefore(scope, today) == quit
        && timeline.CanSee(scope, profile, quit.AddDays(-1), today)
        && !timeline.CanSee(scope, profile, quit, today),
        "terminated employee visible only before the quit date");
}
{
    var timeline = new WorkTimeline([Entry(new(2024, 1, 1), bap, gl), Entry(new(2026, 11, 10), bo, dl)]);
    var scope = Company(gl);
    Check(timeline.VisibleBefore(scope, today) == new DateOnly(2026, 11, 10)
        && timeline.IsWithinSelectorWindow(scope, profile, today, 30)
        && !timeline.CanWrite(scope, profile, new(2026, 11, 12), today),
        "future approved transfer limits the current owner from that date");
}
foreach (var (days, since, visible) in new[] { (30, 20, true), (30, 30, false), (0, 1, false) })
{
    var timeline = new WorkTimeline([Entry(new(2024, 1, 1), bap, gl), Entry(today.AddDays(-since), bo, dl)]);
    Check(timeline.IsWithinSelectorWindow(Company(gl), profile, today, days) == visible,
        $"selector window {days} days, transferred {since} days ago -> visible={visible}");
}
{
    var boundary = Demo().FirstCompanyBoundary(new(2026, 9, 29), new(2026, 10, 2));
    Check(boundary == new CompanyBoundary(new(2026, 10, 1), dl, hq)
        && Demo().FirstCompanyBoundary(new(2026, 10, 2), new(2026, 10, 9)) is null,
        "detects a legal-entity boundary inside a date range");
}
{
    var timeline = new WorkTimeline([
        Entry(new(2024, 1, 1), bap, gl),
        Entry(new(2026, 9, 10), bap, null),
        Entry(new(2026, 9, 16), bo, dl),
        Entry(new(2026, 9, 25), bo, dl, stop: true),
    ]);
    var segments = timeline.CompanySegments(new(2026, 9, 1), new(2026, 9, 30));
    Check(segments.Count == 2
        && (segments[0].From, segments[0].To, segments[0].CompanyId) == (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15), gl)
        && (segments[1].From, segments[1].To, segments[1].CompanyId) == (new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 24), dl),
        "splits a period into legal-entity segments and inherits a missing legal entity");
}
{
    var scope = DataScope.Create([new ScopeRow(hrDept, hq), new ScopeRow(null, gl)], id => new HashSet<int> { id }, [], null);
    Check(scope.Covers(hrDept, hq) && !scope.Covers(bap, gl) && !scope.Covers(hrDept, gl),
        "paired scope requires org + legal entity and drops single-dimension rows");
}
{
    const int own = 200, granted = 201;
    var scope = DataScope.Create([new ScopeRow(null, gl)], id => new HashSet<int> { id }, [granted], own);
    var timeline = new WorkTimeline([Entry(new(2024, 1, 1), bo, dl)]);
    Check(timeline.CanSee(scope, granted, new(2025, 1, 1), today)
        && timeline.CanSee(scope, own, new(2025, 1, 1), today)
        && !timeline.CanWrite(scope, own, new(2025, 1, 1), today)
        && timeline.CanWrite(scope, granted, new(2025, 1, 1), today),
        "granted profile and own profile are always visible; own profile is not writable outside scope");
}
{
    var scope = DataScope.Create([new ScopeRow(hrDept, null)], id => id == hrDept ? new HashSet<int> { hrDept, bap } : new HashSet<int> { id }, [], null);
    Check(scope.Covers(bap, gl) && !scope.Covers(bo, gl) && !scope.Covers(null, gl) && !scope.IsEmpty,
        "org scope covers the expanded subtree only");
}

// ---------------------------------------------------------------------------------------------
// Employment segments by legal entity (attendance / payroll lines)
// ---------------------------------------------------------------------------------------------
{
    const int org = 21, org2 = 22;
    DateOnly from = new(2026, 4, 1), to = new(2026, 4, 30);
    TimelineEntry Seg(DateOnly date, int? company, bool stop = false, int? o = null, int order = 0) => Entry(date, o ?? org, company, stop, order);

    var transfer = new WorkTimeline([Seg(new(2025, 1, 1), gl), Seg(new(2026, 4, 16), dl)]).EmploymentSegments(from, to);
    Check(transfer.SequenceEqual([new EmploymentSpan(from, new(2026, 4, 15), gl, org), new EmploymentSpan(new(2026, 4, 16), to, dl, org)]),
        "transfer between legal entities mid-period gives two segments");

    var deptChange = new WorkTimeline([Seg(new(2025, 1, 1), gl), Seg(new(2026, 4, 10), gl, o: org2)]).EmploymentSegments(from, to);
    Check(deptChange.SequenceEqual([new EmploymentSpan(from, to, gl, org2)]),
        "department change inside the same legal entity does not split");

    var rehire = new WorkTimeline([Seg(new(2025, 1, 1), gl), Seg(new(2026, 4, 10), gl, stop: true), Seg(new(2026, 4, 20), gl, order: 1)]).EmploymentSegments(from, to);
    Check(rehire.SequenceEqual([new EmploymentSpan(from, new(2026, 4, 9), gl, org), new EmploymentSpan(new(2026, 4, 20), to, gl, org)]),
        "days after termination are excluded and a rehire starts a new line");

    var hire = new WorkTimeline([Seg(new(2026, 4, 5), gl)]).EmploymentSegments(from, to);
    Check(hire.SequenceEqual([new EmploymentSpan(new(2026, 4, 5), to, gl, org)]), "hire mid-period excludes days before hire");

    var noCompany = new WorkTimeline([Seg(new(2025, 1, 1), null)]);
    Check(noCompany.EmploymentSegments(from, to) is [{ CompanyId: null }] && noCompany.CompanySegments(from, to).Count == 0,
        "employee without a legal entity keeps the span with a null legal entity");

    var xyx = new WorkTimeline([Seg(new(2025, 1, 1), gl), Seg(new(2026, 4, 10), dl), Seg(new(2026, 4, 20), gl)]).EmploymentSegments(from, to);
    Check(xyx.Select(s => s.CompanyId).SequenceEqual(new int?[] { gl, dl, gl }), "X -> Y -> X gives three lines");
}

// ---------------------------------------------------------------------------------------------
// Timesheet calculation
// ---------------------------------------------------------------------------------------------
{
    var office = new ShiftSchedule(new(8, 0), new(17, 0), 60, 5, WorkDays.MonToSat);
    var night = new ShiftSchedule(new(22, 0), new(6, 0), 30, 5, WorkDays.All);
    var monday = new DateOnly(2026, 10, 5);
    var later = new DateTime(2026, 12, 1);
    AttendanceDay Day(ShiftSchedule shift, DateOnly date, params string[] times) =>
        TimesheetCalculator.CalculateDay(shift, date,
            times.Select(t => DateTime.Parse(t, CultureInfo.InvariantCulture)).Order().ToArray(), true, null, later);

    var full = Day(office, monday, "2026-10-05 07:52", "2026-10-05 17:05");
    Check(full.Status == DayStatus.Present && full.WorkedMinutes == 480 && full.Credit == 1m, "full day on time gives one credit");

    var late = Day(office, monday, "2026-10-05 08:20", "2026-10-05 17:00");
    Check(late.Status == DayStatus.Late && late.LateMinutes == 20, "late beyond grace is marked late with full minutes");

    Check(Day(office, monday, "2026-10-05 08:04", "2026-10-05 17:00").Status == DayStatus.Present, "within grace is not late");
    Check(Day(office, monday, "2026-10-05 07:55").Status == DayStatus.MissingPunch, "single punch in the past is a missing punch");

    var overnight = Day(night, monday, "2026-10-05 21:50", "2026-10-06 06:02");
    Check(overnight.Status == DayStatus.Present && overnight.WorkedMinutes == 450 && overnight.Credit == 1m,
        "overnight shift pairs punches across midnight");

    Check(Day(office, new(2026, 10, 4)).Status == DayStatus.DayOff && Day(office, monday).Status == DayStatus.Absent,
        "Sunday is a day off for the office shift; no punches on a workday is absent");

    const int leaveType = 31;
    var paid = TimesheetCalculator.CalculateDay(office, monday, [], true, new LeaveOnDay(leaveType, true), later);
    var unpaid = TimesheetCalculator.CalculateDay(office, monday.AddDays(1), [], true, new LeaveOnDay(leaveType, false), later);
    var summary = TimesheetCalculator.Summarize([paid, unpaid], office);
    Check(paid.Status == DayStatus.Leave && summary.PaidLeaveDays == 1m && summary.UnpaidLeaveDays == 1m && summary.StandardDays == 2,
        "approved leave overrides punches and the summary splits paid and unpaid leave");

    Check(Day(office, monday, "2026-10-05 08:00", "2026-10-05 12:00").Credit == 0.5m, "half day rounds to half credit");

    var notEmployed = TimesheetCalculator.CalculateDay(office, monday, [], false, null, later);
    var future = TimesheetCalculator.CalculateDay(office, monday, [], true, null, new DateTime(2026, 10, 5, 9, 0, 0));
    Check(notEmployed.Status == DayStatus.NotEmployed && future.Status == DayStatus.Future,
        "days outside employment are not counted and unfinished shifts are future");
}

Console.WriteLine($"VERIFIED {checks} HRM checks.");
