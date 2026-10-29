namespace ERPm.Domain.Services.HumanResource;

public enum DayStatus
{
    NotEmployed,
    Future,
    DayOff,
    Leave,
    Present,
    Late,
    MissingPunch,
    Absent,
}

public sealed record AttendanceDay(
    DateOnly Date,
    DayStatus Status,
    DateTime? FirstIn,
    DateTime? LastOut,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyMinutes,
    decimal Credit,
    int? LeaveTypeId,
    bool PaidLeave);

public sealed record TimesheetSummary(
    decimal WorkCredits,
    int PresentDays,
    int LateCount,
    int LateMinutes,
    int EarlyMinutes,
    int MissingPunchDays,
    int AbsentDays,
    decimal PaidLeaveDays,
    decimal UnpaidLeaveDays,
    int StandardDays);

public sealed record LeaveOnDay(int LeaveTypeId, bool IsPaid);

/// <summary>
/// Turns raw punches of one day into an attendance result (status, worked minutes, late/early minutes,
/// work credit rounded to half days) and aggregates days into a timesheet summary. Pure logic.
/// </summary>
public static class TimesheetCalculator
{
    public static readonly TimeSpan WindowBefore = TimeSpan.FromHours(4);
    public static readonly TimeSpan WindowAfter = TimeSpan.FromHours(6);
    public const int BreakAppliesAfterMinutes = 240;

    /// <param name="sortedLogs">Punch times of the employee sorted ascending.</param>
    /// <param name="now">Current time; shifts not yet finished are reported as Future/Present instead of Absent.</param>
    public static AttendanceDay CalculateDay(ShiftSchedule shift, DateOnly date, ReadOnlySpan<DateTime> sortedLogs,
        bool employed, LeaveOnDay? leave, DateTime now)
    {
        if (!employed) return Empty(date, DayStatus.NotEmployed);

        var start = date.ToDateTime(shift.StartTime);
        var end = date.ToDateTime(shift.EndTime);
        if (shift.IsOvernight) end = end.AddDays(1);
        var (windowFrom, windowTo) = shift.Window(date);

        DateTime? first = null, last = null;
        var count = 0;
        foreach (var log in sortedLogs)
        {
            if (log < windowFrom) continue;
            if (log >= windowTo) break;
            first ??= log;
            last = log;
            count++;
        }

        if (leave is not null)
            return new AttendanceDay(date, DayStatus.Leave, first, last, 0, 0, 0, 0, leave.LeaveTypeId, leave.IsPaid);

        if (!shift.IsWorkDay(date))
        {
            var offMinutes = count >= 2 ? (int)(last!.Value - first!.Value).TotalMinutes : 0;
            return new AttendanceDay(date, DayStatus.DayOff, first, count >= 2 ? last : null, offMinutes, 0, 0, 0, null, false);
        }

        if (count == 0) return Empty(date, end > now ? DayStatus.Future : DayStatus.Absent);

        var late = LateOf(shift, start, first!.Value);
        if (count == 1)
            return new AttendanceDay(date, end > now ? DayStatus.Present : DayStatus.MissingPunch, first, null, 0, late,
                0, 0, null, false);

        var from = first.Value > start ? first.Value : start;
        var to = last!.Value < end ? last.Value : end;
        var raw = Math.Max(0, (int)(to - from).TotalMinutes);
        var worked = raw > BreakAppliesAfterMinutes ? raw - shift.BreakMinutes : raw;
        var early = last.Value < end ? (int)Math.Ceiling((end - last.Value).TotalMinutes) : 0;

        return new AttendanceDay(date, late > 0 ? DayStatus.Late : DayStatus.Present, first, last, worked, late, early,
            Credit(worked, shift.StandardMinutes), null, false);
    }

    public static TimesheetSummary Summarize(IEnumerable<AttendanceDay> days, ShiftSchedule shift)
    {
        decimal credits = 0, paidLeave = 0, unpaidLeave = 0;
        int present = 0, lateCount = 0, lateMinutes = 0, early = 0, missing = 0, absent = 0, standard = 0;

        foreach (var d in days)
        {
            if (d.Status != DayStatus.NotEmployed && shift.IsWorkDay(d.Date)) standard++;
            switch (d.Status)
            {
                case DayStatus.Present or DayStatus.Late:
                    present++;
                    credits += d.Credit;
                    early += d.EarlyMinutes;
                    if (d.Status == DayStatus.Late)
                    {
                        lateCount++;
                        lateMinutes += d.LateMinutes;
                    }
                    break;
                case DayStatus.MissingPunch:
                    missing++;
                    break;
                case DayStatus.Absent:
                    absent++;
                    break;
                case DayStatus.Leave when d.PaidLeave:
                    paidLeave++;
                    break;
                case DayStatus.Leave:
                    unpaidLeave++;
                    break;
            }
        }

        return new TimesheetSummary(credits, present, lateCount, lateMinutes, early, missing, absent, paidLeave,
            unpaidLeave, standard);
    }

    private static int LateOf(ShiftSchedule shift, DateTime start, DateTime firstIn)
    {
        var late = (int)Math.Floor((firstIn - start).TotalMinutes);
        return late > shift.LateGraceMinutes ? late : 0;
    }

    private static decimal Credit(int worked, int standard)
    {
        if (standard <= 0) return 0;
        var ratio = Math.Min(1m, (decimal)worked / standard);
        return Math.Round(ratio * 2, MidpointRounding.AwayFromZero) / 2;
    }

    private static AttendanceDay Empty(DateOnly date, DayStatus status) =>
        new(date, status, null, null, 0, 0, 0, 0, null, false);
}
