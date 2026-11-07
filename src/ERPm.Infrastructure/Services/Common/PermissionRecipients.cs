using ERPm.Application.Features.System;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>
/// Finds the people who should hear about a document: active accounts that hold a permission and whose data scope
/// reaches the document, never the person who just acted. Used instead of broadcasting to a whole branch tree, which
/// also reached people who may not open the document.
/// </summary>
public sealed class PermissionRecipients(ApplicationDbContext context, IIamService iam)
{
    public async Task<List<int>> ForDocumentAsync(string action, int branchId, int? departmentId, string? createdBy, int? excludeUserId, CancellationToken ct)
    {
        var userIds = await context.AppUsers.AsNoTracking().Where(u => u.IsActive && !u.IsDeleted && u.Id != excludeUserId).Select(u => u.Id).ToListAsync(ct);
        var recipients = new List<int>();
        foreach (var userId in userIds)
        {
            var principal = await iam.GetPrincipalAsync(userId, ct);
            var decision = principal.Evaluate(action);
            if (!decision.Allowed) continue;
            var scope = decision.Scope;
            var reaches = scope.Unrestricted
                || scope.BranchIds.Contains(branchId)
                || (departmentId.HasValue && scope.DepartmentIds.Contains(departmentId.Value))
                || (scope.OwnRecords && createdBy != null && createdBy == principal.Context.UserKey);
            if (reaches) recipients.Add(userId);
        }
        return recipients;
    }
}
