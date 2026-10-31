using System.Security.Claims;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ERPm.API.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController(AccountService accounts, ApplicationDbContext context, ICurrentUser currentUser, IIamService iam) : BaseApiController
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var principal = await accounts.LoginAsync(request, ct);
        if (principal.Identity?.IsAuthenticated != true) throw new BusinessException("error.auth.invalid", 401);
        return SignIn(principal, "ERPmBearer");
    }

    /// <summary>
    /// Signed-in account with its org position, effective permission list and read scope (drives FE <c>can()</c>).
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await context.AppUsers.AsNoTracking().SingleAsync(u => u.Id == id, ct);
        var principal = await iam.GetPrincipalAsync(id, ct);
        var scope = principal.ReadScope;
        return Ok(ApiResponse<CurrentAccountDto>.Ok(new(user.Id, user.UserName, user.DisplayName, user.Role, currentUser.BranchIds,
            principal.IsSuperAdmin, principal.EmployeeId, principal.Context.CompanyId, principal.Context.BranchId, principal.Context.DepartmentId,
            principal.JobPositionId, principal.AllowedActions().ToArray(),
            new DataScopeDto(scope.Unrestricted, scope.BranchIds.Order().ToArray(), scope.DepartmentIds.Order().ToArray(), scope.OwnRecords))));
    }

    [RequirePermission("sys:user:create"), HttpPost("users")]
    public async Task<IActionResult> CreateUser(UserRequest request, CancellationToken ct) => Ok(ApiResponse<AccountDto>.Ok(await accounts.CreateAsync(request, ct)));

    /// <summary>Accounts list; a company administrator only sees accounts linked to its own company's employees.</summary>
    [RequirePermission("sys:user:view"), HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var principal = HttpContext.Principal()!;
        var companyId = principal.Context.CompanyId;
        var all = principal.IsSuperAdmin;
        return Ok(ApiResponse<List<AccountDto>>.Ok(await context.AppUsers.AsNoTracking()
            .Where(u => all || context.Employees.Any(e => e.UserId == u.Id && e.CompanyId == companyId))
            .Select(u => new
            {
                User = u,
                Employee = context.Employees.Where(e => e.UserId == u.Id).Select(e => new { e.Id, e.Code, e.FullName }).FirstOrDefault()
            })
            .Select(x => new AccountDto(x.User.Id, x.User.UserName, x.User.DisplayName, x.User.Role, x.User.BranchIds.ToArray(),
                x.Employee == null ? null : x.Employee.Id, x.Employee == null ? null : x.Employee.Code, x.Employee == null ? null : x.Employee.FullName))
            .ToListAsync(ct)));
    }

    [RequirePermission("sys:user:edit"), HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, UserAccessRequest request, CancellationToken ct)
        => Ok(ApiResponse<AccountDto>.Ok(await accounts.UpdateAsync(id, request, ct)));
}
