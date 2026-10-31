namespace ERPm.Application.Common.Notifications;

/// <summary>
/// Evaluates organization hierarchy and IAM to resolve target recipients into user IDs.
/// </summary>
public interface INotificationRouter
{
    Task<IReadOnlyList<int>> ResolveRecipientUserIdsAsync(string targetType, int? targetId, int? branchId, int? companyId, CancellationToken cancellationToken = default);
}
