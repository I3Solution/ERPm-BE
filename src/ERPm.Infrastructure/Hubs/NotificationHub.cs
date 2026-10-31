using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ERPm.Infrastructure.Hubs;

/// <summary>
/// Real-time WebSocket SignalR Hub delivering notifications and sync events to connected clients.
/// Route: /hubs/notifications
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeBranch(int branchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Branch_{branchId}");
    }

    public async Task UnsubscribeBranch(int branchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Branch_{branchId}");
    }

    public async Task SubscribeCompany(int companyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyId}");
    }

    public async Task UnsubscribeCompany(int companyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Company_{companyId}");
    }

    private string? GetCurrentUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst("uid")?.Value;
    }
}
