using System.Security.Claims;
using ERPm.Application.Common;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERPm.API.Security;

public sealed record LoginRequest(string UserName, string Password);
public sealed record UserRequest(string UserName, string DisplayName, string Password, string Role, int[] BranchIds);
public sealed record UserAccessRequest(string DisplayName, string Role, int[] BranchIds, bool IsActive, string? Password);
public sealed record AccountDto(int Id, string UserName, string DisplayName, string Role, int[] BranchIds);

public sealed class AccountService(ApplicationDbContext context, PostingTransaction transaction)
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public Task<ClaimsPrincipal> LoginAsync(LoginRequest request, CancellationToken ct) => transaction.RunAsync(async () =>
    {
        var name = (request.UserName ?? "").Trim().ToUpperInvariant();
        PostingTransaction.Require(name.Length is > 0 and <= 100 && request.Password is { Length: > 0 and <= 256 }, "error.auth.invalid", 401);
        await transaction.LockAsync($"login:{name}", ct);
        var user = await context.AppUsers.SingleOrDefaultAsync(u => u.UserName == name, ct);
        if (user == null || !user.IsActive || user.LockedUntilUtc > DateTime.UtcNow)
            throw new BusinessException("error.auth.invalid", 401);
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5) user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
            await context.SaveChangesAsync(ct);
            // Commit failed-attempt state before returning the authentication error.
            return new ClaimsPrincipal();
        }
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = _hasher.HashPassword(user, request.Password);
        return Principal(user);
    }, ct);

    public async Task<AccountDto> CreateAsync(UserRequest request, CancellationToken ct)
    {
        var name = (request.UserName ?? "").Trim().ToUpperInvariant();
        PostingTransaction.Require(name.Length is > 0 and <= 100 && !string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Length <= 255, "error.validation.request");
        PostingTransaction.Require(request.Password is { Length: >= 12 and <= 256 } && request.Role is "Admin" or "Operator" or "Viewer", "error.auth.user_request");
        var ids = request.BranchIds.Distinct().ToArray();
        PostingTransaction.Require(request.Role == "Admin" || ids.Length > 0, "error.auth.branch_required");
        PostingTransaction.Require(await context.Branches.IgnoreQueryFilters().CountAsync(b => !b.IsDeleted && b.IsActive && ids.Contains(b.Id), ct) == ids.Length, "error.inventory.branch_mismatch");
        var user = new AppUser { UserName = name, DisplayName = request.DisplayName.Trim(), Role = request.Role, BranchIds = ids.ToList() };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        context.AppUsers.Add(user);
        await context.SaveChangesAsync(ct);
        return new AccountDto(user.Id, user.UserName, user.DisplayName, user.Role, ids);
    }

    public static ClaimsPrincipal Principal(AppUser user) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.DisplayName),
        new Claim(ClaimTypes.Role, user.Role), new Claim("stamp", user.SecurityStamp)
    ], "ERPmBearer"));

    public Task<AccountDto> UpdateAsync(int id, UserAccessRequest request, CancellationToken ct) => transaction.RunAsync<AccountDto>(async () =>
    {
        await transaction.LockAsync("user-management", ct);
        var user = await context.AppUsers.SingleOrDefaultAsync(u => u.Id == id, ct);
        PostingTransaction.Require(user != null, "error.order.not_found", 404);
        PostingTransaction.Require(!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Length <= 255 && request.Role is "Admin" or "Operator" or "Viewer", "error.auth.user_request");
        var ids = request.BranchIds.Distinct().ToArray();
        PostingTransaction.Require(request.Role == "Admin" || ids.Length > 0, "error.auth.branch_required");
        PostingTransaction.Require(await context.Branches.IgnoreQueryFilters().CountAsync(b => !b.IsDeleted && b.IsActive && ids.Contains(b.Id), ct) == ids.Length, "error.inventory.branch_mismatch");
        if (user!.Role == "Admin" && (!request.IsActive || request.Role != "Admin"))
            PostingTransaction.Require(await context.AppUsers.CountAsync(u => u.IsActive && u.Role == "Admin", ct) > 1, "error.access.denied", 409);
        if (!string.IsNullOrEmpty(request.Password))
        {
            PostingTransaction.Require(request.Password.Length is >= 12 and <= 256, "error.auth.user_request");
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
        }
        user.DisplayName = request.DisplayName.Trim(); user.Role = request.Role; user.BranchIds = ids.ToList(); user.IsActive = request.IsActive;
        user.FailedLoginCount = 0; user.LockedUntilUtc = null; user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(ct);
        return new(user.Id, user.UserName, user.DisplayName, user.Role, ids);
    }, ct);
}
