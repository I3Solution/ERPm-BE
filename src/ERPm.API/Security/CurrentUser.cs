using System.Security.Claims;
using ERPm.Application.Common.Interfaces;

namespace ERPm.API.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public bool IsSystem => accessor.HttpContext == null;
    public bool IsAdmin => accessor.HttpContext?.User.IsInRole("Admin") == true;
    public int[] BranchIds => accessor.HttpContext?.User.FindAll("branch").Select(c => int.Parse(c.Value)).ToArray() ?? [];
}
