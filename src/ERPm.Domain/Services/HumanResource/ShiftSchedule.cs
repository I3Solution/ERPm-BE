namespace ERPm.Domain.Services.HumanResource;

[Flags]
public enum WorkDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    MonToFri = Monday | Tuesday | Wednesday | Thursday | Friday,
    MonToSat = MonToFri | Saturday,
    All = MonToSat | Sunday,
}

/// <summary>
/// Timing rules of a work shift used by <see cref="TimesheetCalculator"/>. Kept as a value object so the
/// calculator does not depend on how the shift catalogue is persisted.
/// </summary>
public sealed record ShiftSchedule(
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int LateGraceMinutes,
    WorkDays WorkDays)
{
    public bool IsOvernight => EndTime <= StartTime;

    public int StandardMinutes
    {
        get
        {
            var span = EndTime.ToTimeSpan() - StartTime.ToTimeSpan();
            if (span <= TimeSpan.Zero) span += TimeSpan.FromDays(1);
            return (int)span.TotalMinutes - BreakMinutes;
        }
    }

    public bool IsWorkDay(DateOnly date) => WorkDays.HasFlag(date.DayOfWeek switch
    {
        DayOfWeek.Monday => WorkDays.Monday,
        DayOfWeek.Tuesday => WorkDays.Tuesday,
        DayOfWeek.Wednesday => WorkDays.Wednesday,
        DayOfWeek.Thursday => WorkDays.Thursday,
        DayOfWeek.Friday => WorkDays.Friday,
        DayOfWeek.Saturday => WorkDays.Saturday,
        _ => WorkDays.Sunday,
    });

    /// <summary>Punches inside this window are attributed to the shift starting on <paramref name="date"/>.</summary>
    public (DateTime From, DateTime To) Window(DateOnly date)
    {
        var start = date.ToDateTime(StartTime);
        var end = date.ToDateTime(EndTime);
        if (IsOvernight) end = end.AddDays(1);
        return (start - TimesheetCalculator.WindowBefore, end + TimesheetCalculator.WindowAfter);
    }
}
