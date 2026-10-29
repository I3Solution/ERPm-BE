using ERPm.Application.Common.Interfaces;
using ERPm.Application.Features.HumanResource;
using ERPm.Domain.Services.HumanResource;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.HumanResource;

/// <summary>
/// Builds the HRM <see cref="DataScope"/> of the current account from <c>tbl_HRM_DataPermission</c> and the
/// org unit tree, and loads approved work-history timelines. Cached per request (scoped lifetime).
/// </summary>
public class HrmDataScopeService(ApplicationDbContext context, ICurrentUser? currentUser = null) : IHrmDataScopeService
{
    private DataScope? _scope;

    public async Task<DataScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_scope != null) return _scope;
        int? userId = int.TryParse(currentUser?.UserId, out var id) ? id : null;
        int? ownEmployeeId = userId == null ? null
            : await context.Employees.AsNoTracking().Where(e => e.UserId == userId).Select(e => (int?)e.Id).FirstOrDefaultAsync(cancellationToken);

        if (currentUser == null || currentUser.IsSystem || currentUser.IsAdmin)
            return _scope = DataScope.Unrestricted(ownEmployeeId);
        if (userId == null)
            return _scope = DataScope.Create([], _ => new HashSet<int>(), [], null);

        var rows = await context.HrmDataPermissions.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new ScopeRow(p.OrgStructureId, p.CompanyId))
            .ToListAsync(cancellationToken);
        var children = (await context.OrgStructures.AsNoTracking().Where(o => o.ParentId != null).Select(o => new { o.Id, ParentId = o.ParentId!.Value }).ToListAsync(cancellationToken))
            .ToLookup(o => o.ParentId, o => o.Id);

        return _scope = DataScope.Create(rows, root => Subtree(root, children), [], ownEmployeeId);
    }

    public async Task<WorkTimeline> GetTimelineAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var rows = await context.WorkHistories.AsNoTracking()
            .Where(w => w.EmployeeId == employeeId && w.IsApproved)
            .ToListAsync(cancellationToken);
        return new WorkTimeline(rows.Select(w => w.ToTimelineEntry()));
    }

    private static IReadOnlySet<int> Subtree(int root, ILookup<int, int> children)
    {
        var result = new HashSet<int> { root };
        var pending = new Stack<int>([root]);
        while (pending.Count > 0)
            foreach (var child in children[pending.Pop()])
                if (result.Add(child)) pending.Push(child);
        return result;
    }
}
