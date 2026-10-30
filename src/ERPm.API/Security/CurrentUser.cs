using System.Security.Claims;
using ERPm.Application.Common.Interfaces;

namespace ERPm.API.Security;

/// <summary>
/// Signed-in account for the current request. Data scope comes from the permission checked on the endpoint,
/// or the union of all Allow scopes when the endpoint declares none.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public bool IsSystem => accessor.HttpContext == null;
    public bool IsAdmin => accessor.HttpContext?.User.IsInRole("Admin") == true || accessor.HttpContext?.Principal()?.IsSuperAdmin == true;
    public int[] BranchIds => accessor.HttpContext?.Scope()?.BranchIds.ToArray() ?? [];
    public bool HasUnrestrictedScope => accessor.HttpContext?.Scope()?.Unrestricted == true;
    public int[] DepartmentIds => accessor.HttpContext?.Scope()?.DepartmentIds.ToArray() ?? [];
    public bool IncludeOwnRecords => accessor.HttpContext?.Scope()?.OwnRecords == true;
}
