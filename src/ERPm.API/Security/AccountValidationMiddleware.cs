using System.Security.Claims;
using ERPm.Application.Common;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.API.Security;

public sealed class AccountValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ApplicationDbContext context)
    {
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var id = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await context.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, http.RequestAborted);
            if (user == null || !user.IsActive || user.SecurityStamp != http.User.FindFirstValue("stamp"))
                throw new BusinessException("error.auth.expired", 401);
            http.User = AccountService.Principal(user);
            if (user.Role != "Admin")
            {
                var all = await context.Branches.IgnoreQueryFilters().Where(b => !b.IsDeleted && b.IsActive)
                    .Select(b => new { b.Id, b.ParentId }).ToListAsync(http.RequestAborted);
                var allowed = new HashSet<int>(user.BranchIds.Where(id => all.Any(b => b.Id == id)));
                bool added;
                do { added = false; foreach (var b in all) if (b.ParentId.HasValue && allowed.Contains(b.ParentId.Value)) added |= allowed.Add(b.Id); } while (added);
                ((ClaimsIdentity)http.User.Identity!).AddClaims(allowed.Select(b => new Claim("branch", b.ToString())));
            }
        }
        await next(http);
    }
}
