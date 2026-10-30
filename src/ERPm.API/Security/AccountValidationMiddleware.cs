using System.Security.Claims;
using ERPm.Application.Common;
using ERPm.Application.Features.System;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.API.Security;

/// <summary>
/// Re-validates the bearer token against the account (active, security stamp) and attaches the cached IAM
/// principal used by <see cref="PermissionFilter"/> and <see cref="CurrentUser"/>.
/// </summary>
public sealed class AccountValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ApplicationDbContext context, IIamService iam)
    {
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var id = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await context.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, http.RequestAborted);
            if (user == null || !user.IsActive || user.SecurityStamp != http.User.FindFirstValue("stamp"))
                throw new BusinessException("error.auth.expired", 401);
            http.User = AccountService.Principal(user);
            http.Items[IamHttpContext.PrincipalKey] = await iam.GetPrincipalAsync(user.Id, http.RequestAborted);
        }
        await next(http);
    }
}
