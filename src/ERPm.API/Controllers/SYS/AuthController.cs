using System.Security.Claims;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ERPm.API.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController(AccountService accounts, ApplicationDbContext context) : BaseApiController
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var principal = await accounts.LoginAsync(request, ct);
        if (principal.Identity?.IsAuthenticated != true) throw new BusinessException("error.auth.invalid", 401);
        return SignIn(principal, "ERPmBearer");
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await context.AppUsers.AsNoTracking().SingleAsync(u => u.Id == id, ct);
        return Ok(ApiResponse<AccountDto>.Ok(new(user.Id, user.UserName, user.DisplayName, user.Role,
            User.FindAll("branch").Select(c => int.Parse(c.Value)).ToArray())));
    }

    [Authorize(Policy = "Admin"), HttpPost("users")]
    public async Task<IActionResult> CreateUser(UserRequest request, CancellationToken ct) => Ok(ApiResponse<AccountDto>.Ok(await accounts.CreateAsync(request, ct)));

    [Authorize(Policy = "Admin"), HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(ApiResponse<List<AccountDto>>.Ok(await context.AppUsers.AsNoTracking()
        .Select(u => new AccountDto(u.Id, u.UserName, u.DisplayName, u.Role, u.BranchIds.ToArray())).ToListAsync(ct)));

    [Authorize(Policy = "Admin"), HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, UserAccessRequest request, CancellationToken ct)
        => Ok(ApiResponse<AccountDto>.Ok(await accounts.UpdateAsync(id, request, ct)));
}
