using ERPm.Application.Common.Interfaces;
using ERPm.Application.Common.Notifications;
using ERPm.Application.Features.System;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>What to announce about one document and which permission decides who may hear it.</summary>
public sealed record DocumentNotice(
    string Template, string Category, string Severity, string ViewAction,
    int BranchId, int? DepartmentId, string? CreatedBy, IDictionary<string, object> Parameters, string ActionUrl);

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

    /// <summary>
    /// Sends the notice to each recipient on its own (target USER). Call it only after the change is committed, so a
    /// rolled back change never notifies anybody. Without an engine or IAM service nothing is sent.
    /// </summary>
    public static async Task PublishAsync(ApplicationDbContext context, IIamService? iam, INotificationEngine? engine, ICurrentUser? actor, DocumentNotice? notice, CancellationToken ct)
    {
        if (engine == null || iam == null || notice == null) return;
        int? actorId = int.TryParse(actor?.UserId, out var parsed) && parsed != 0 ? parsed : null;
        var recipients = await new PermissionRecipients(context, iam).ForDocumentAsync(notice.ViewAction, notice.BranchId, notice.DepartmentId, notice.CreatedBy, actorId, ct);
        foreach (var userId in recipients)
            _ = engine.PublishAsync(new ERPm.Domain.Events.NotificationEvent(
                notice.Template, notice.Category, notice.Severity, ERPm.Domain.Enums.NotificationTargetType.User,
                targetId: userId, branchId: notice.BranchId, parameters: notice.Parameters, actionUrl: notice.ActionUrl));
    }
}
