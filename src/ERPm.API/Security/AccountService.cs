using System.Security.Claims;
using ERPm.Application.Common;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.Iam;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERPm.API.Security;

public sealed record LoginRequest(string UserName, string Password);
public sealed record UserRequest(string UserName, string DisplayName, string Password, string Role, int[] BranchIds, int? EmployeeId = null);
public sealed record UserAccessRequest(string DisplayName, string Role, int[] BranchIds, bool IsActive, string? Password, int? EmployeeId = null);
public sealed record AccountDto(int Id, string UserName, string DisplayName, string Role, int[] BranchIds,
    int? EmployeeId = null, string? EmployeeCode = null, string? EmployeeName = null, bool IsActive = true);

public sealed class AccountService(ApplicationDbContext context, PostingTransaction transaction, IIamService iam)
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
        PostingTransaction.Require(request.Password is { Length: >= 12 and <= 256 } && request.Role is "Admin" or "Operator" or "Viewer" or "None", "error.auth.user_request");
        var ids = request.BranchIds.Distinct().ToArray();
        PostingTransaction.Require(request.Role is "Admin" or "None" || ids.Length > 0, "error.auth.branch_required");
        PostingTransaction.Require(await context.Branches.IgnoreQueryFilters().CountAsync(b => !b.IsDeleted && b.IsActive && ids.Contains(b.Id), ct) == ids.Length, "error.inventory.branch_mismatch");
        await EnsureCanAssignAsync(PermissionCatalog.Create, null, request.Role, ids, ct);
        // Company administrators must link the new account to an employee; only super administrators create unlinked (system) accounts.
        var granter = await iam.GetCurrentPrincipalAsync(ct);
        PostingTransaction.Require(granter == null || granter.IsSuperAdmin || request.EmployeeId != null, "error.validation.required");
        var user = new AppUser { UserName = name, DisplayName = request.DisplayName.Trim(), Role = request.Role, BranchIds = ids.ToList() };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        context.AppUsers.Add(user);
        var employee = await LinkEmployeeAsync(user, request.EmployeeId, granter, ct);
        await context.SaveChangesAsync(ct);
        iam.Invalidate();
        return new AccountDto(user.Id, user.UserName, user.DisplayName, user.Role, ids, employee?.Id, employee?.Code, employee?.FullName, user.IsActive);
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
        PostingTransaction.Require(!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Length <= 255 && request.Role is "Admin" or "Operator" or "Viewer" or "None", "error.auth.user_request");
        var ids = request.BranchIds.Distinct().ToArray();
        PostingTransaction.Require(request.Role is "Admin" or "None" || ids.Length > 0, "error.auth.branch_required");
        PostingTransaction.Require(await context.Branches.IgnoreQueryFilters().CountAsync(b => !b.IsDeleted && b.IsActive && ids.Contains(b.Id), ct) == ids.Length, "error.inventory.branch_mismatch");
        await EnsureCanAssignAsync(PermissionCatalog.Edit, user!.Role, request.Role, ids, ct);
        var granter = await iam.GetCurrentPrincipalAsync(ct);
        if (granter is { IsSuperAdmin: false })
            PostingTransaction.Require(await context.Employees.AnyAsync(e => e.UserId == id && e.CompanyId == granter.Context.CompanyId, ct), "error.iam.company_scope", 403);
        if (user.Role == "Admin" && (!request.IsActive || request.Role != "Admin"))
            PostingTransaction.Require(await context.AppUsers.CountAsync(u => u.IsActive && u.Role == "Admin", ct) > 1, "error.access.denied", 409);
        if (!string.IsNullOrEmpty(request.Password))
        {
            PostingTransaction.Require(request.Password.Length is >= 12 and <= 256, "error.auth.user_request");
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
        }
        user.DisplayName = request.DisplayName.Trim(); user.Role = request.Role; user.BranchIds = ids.ToList(); user.IsActive = request.IsActive;
        user.FailedLoginCount = 0; user.LockedUntilUtc = null; user.SecurityStamp = Guid.NewGuid().ToString("N");
        var employee = await LinkEmployeeAsync(user, request.EmployeeId, granter, ct);
        await context.SaveChangesAsync(ct);
        iam.Invalidate();
        return new(user.Id, user.UserName, user.DisplayName, user.Role, ids, employee?.Id, employee?.Code, employee?.FullName, user.IsActive);
    }, ct);

    /// <summary>
    /// Makes <paramref name="employeeId"/> the only employee linked to the account (null unlinks). Employee.UserId
    /// stays the single source of the link. The employee must be free and, for company administrators, in their company.
    /// </summary>
    private async Task<ERPm.Domain.Entities.HumanResource.Employee?> LinkEmployeeAsync(AppUser user, int? employeeId, IamPrincipal? granter, CancellationToken ct)
    {
        var linked = user.Id == 0 ? [] : await context.Employees.Where(e => e.UserId == user.Id).ToListAsync(ct);
        ERPm.Domain.Entities.HumanResource.Employee? employee = null;
        if (employeeId is { } target)
        {
            employee = await context.Employees.FirstOrDefaultAsync(e => e.Id == target, ct);
            PostingTransaction.Require(employee != null, "error.validation.reference");
            PostingTransaction.Require(granter == null || granter.IsSuperAdmin || employee!.CompanyId == granter.Context.CompanyId, "error.iam.company_scope", 403);
            PostingTransaction.Require(employee!.UserId == null || employee.UserId == user.Id, "error.hrm.user_linked", 409);
            employee.User = user;
        }
        foreach (var other in linked.Where(e => e.Id != employee?.Id)) other.UserId = null;
        return employee;
    }

    /// <summary>
    /// Privilege escalation guard for account management: only super administrators touch Admin accounts,
    /// the granted system role must be within the caller's own rights, and the branches within its scope.
    /// </summary>
    private async Task EnsureCanAssignAsync(string verb, string? currentRole, string role, int[] branchIds, CancellationToken ct)
    {
        var granter = await iam.GetCurrentPrincipalAsync(ct);
        if (granter == null || granter.IsSuperAdmin) return;
        PostingTransaction.Require(currentRole != "Admin" && role != "Admin", "error.iam.escalation", 403);
        var code = role.ToUpperInvariant();
        var policy = await context.IamRoles.AsNoTracking().Where(r => r.IsSystemRole && r.CompanyId == null && r.Code == code)
            .Select(r => r.PolicyDocument).FirstOrDefaultAsync(ct);
        if (policy != null) await iam.EnsureCanGrantAsync(policy.Statements, ct);
        var scope = granter.Evaluate("sys:user:" + verb).Scope;
        PostingTransaction.Require(branchIds.All(id => granter.Context.Branches.Subtree(id).All(scope.CoversBranch)), "error.iam.escalation", 403);
    }
}
