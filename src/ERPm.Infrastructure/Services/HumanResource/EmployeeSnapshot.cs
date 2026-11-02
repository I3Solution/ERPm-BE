using ERPm.Application.Features.System;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.HumanResource;

/// <summary>
/// Keeps the organization fields cached on an employee (company, branch, department, job position, active flag)
/// equal to the approved work-history row in effect on a given day. Work history is the source of truth; the
/// cache exists only so lists and filters need no join.
/// </summary>
internal static class EmployeeSnapshot
{
    /// <summary>
    /// Copies the approved row in effect on <paramref name="today"/> onto the employee; a termination deactivates the
    /// profile and leaves the last placement untouched. Returns true when the employee changed.
    /// </summary>
    public static async Task<bool> ApplyAsync(ApplicationDbContext context, int employeeId, DateOnly today, CancellationToken cancellationToken)
    {
        // Soft-deleted rows never count, and the caller's data scope must not hide a row from the cache.
        var current = await context.WorkHistories.IgnoreQueryFilters().AsNoTracking()
            .Where(w => w.EmployeeId == employeeId && !w.IsDeleted && w.IsApproved && w.DateEffective <= today)
            .OrderByDescending(w => w.DateEffective).ThenByDescending(w => w.CreatedAtUtc).ThenByDescending(w => w.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (current == null) return false;

        var employee = await context.Employees.IgnoreQueryFilters().FirstAsync(e => e.Id == employeeId && !e.IsDeleted, cancellationToken);
        var changed = employee.IsActive == current.IsTermination;
        employee.IsActive = !current.IsTermination;
        if (!current.IsTermination)
        {
            changed |= employee.CompanyId != current.CompanyId || employee.BranchId != current.BranchId
                || employee.DepartmentId != current.DepartmentId || employee.JobPositionId != current.JobPositionId;
            employee.CompanyId = current.CompanyId;
            employee.BranchId = current.BranchId;
            employee.DepartmentId = current.DepartmentId;
            employee.JobPositionId = current.JobPositionId;
        }
        await context.SaveChangesAsync(cancellationToken);
        return changed;
    }
}

/// <summary>
/// Daily catch-up of the employee cache: a work-history row dated in the future changes nothing when it is saved,
/// so once its date arrives the cache is stale until someone edits the employee. Finds those employees and
/// re-applies the row in effect today. Safe to run repeatedly and from several instances: applying is idempotent.
/// </summary>
public class EmployeeSnapshotRefresher(ApplicationDbContext context, IIamService iam)
{
    /// <summary>Re-applies the row in effect on <paramref name="today"/> for every employee whose cache differs. Returns how many were fixed.</summary>
    public async Task<int> RefreshStaleAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var rows = await context.WorkHistories.IgnoreQueryFilters().AsNoTracking()
            .Where(w => !w.IsDeleted && w.IsApproved && w.DateEffective <= today)
            .Select(w => new { w.Id, w.EmployeeId, w.DateEffective, w.CreatedAtUtc, w.CompanyId, w.BranchId, w.DepartmentId, w.JobPositionId, w.IsTermination })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return 0;

        // Same ordering as the server uses everywhere: date, then creation time, then id.
        var latest = rows.GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.DateEffective).ThenByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id).First());
        var employees = await context.Employees.IgnoreQueryFilters().AsNoTracking().Where(e => !e.IsDeleted)
            .Select(e => new { e.Id, e.CompanyId, e.BranchId, e.DepartmentId, e.JobPositionId, e.IsActive })
            .ToListAsync(cancellationToken);

        var stale = employees.Where(e => latest.TryGetValue(e.Id, out var row)
            && (e.IsActive == row.IsTermination
                || (!row.IsTermination && (e.CompanyId != row.CompanyId || e.BranchId != row.BranchId
                    || e.DepartmentId != row.DepartmentId || e.JobPositionId != row.JobPositionId))))
            .Select(e => e.Id).ToList();

        foreach (var employeeId in stale) await EmployeeSnapshot.ApplyAsync(context, employeeId, today, cancellationToken);
        if (stale.Count > 0) iam.Invalidate();
        return stale.Count;
    }
}
