using ERPm.Domain.Common;
using ERPm.Domain.Services.HumanResource;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>
/// Work shift catalogue (HRM9: Cat_Shift). Timing rules are evaluated through <see cref="ShiftSchedule"/>.
/// </summary>
public class Shift : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public int LateGraceMinutes { get; set; }
    public WorkDays WorkDays { get; set; } = WorkDays.MonToFri;
    public bool IsActive { get; set; } = true;

    public ShiftSchedule ToSchedule() => new(StartTime, EndTime, BreakMinutes, LateGraceMinutes, WorkDays);
}
