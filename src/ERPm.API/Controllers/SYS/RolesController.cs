using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Domain.Services.Iam;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SYS;

/// <summary>
/// Dynamic IAM roles (SYS Module): company-owned roles, default roles of job positions and direct
/// (exception) roles / inline policies of accounts. Every grant passes the privilege escalation guard.
/// </summary>
[Route("api/v1/sys/roles")]
public class RolesController(IRoleService roles, IIamService iam) : BaseApiController
{
    [RequirePermission("sys:role:view"), HttpGet]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetRoles(CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<RoleDto>>.Ok(await roles.GetRolesAsync(cancellationToken)));

    [RequirePermission("sys:role:view"), HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<RoleDto>>> GetRole(int id, CancellationToken cancellationToken)
    {
        var role = await roles.GetRoleAsync(id, cancellationToken);
        return role == null ? NotFound(ApiResponse<RoleDto>.Fail("error.validation.reference")) : Ok(ApiResponse<RoleDto>.Ok(role));
    }

    [RequirePermission("sys:role:create"), HttpPost]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Create([FromBody] SaveRoleRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<RoleDto>.Ok(await roles.CreateRoleAsync(request, cancellationToken), "common.saved"));

    [RequirePermission("sys:role:edit"), HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Update(int id, [FromBody] SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await roles.UpdateRoleAsync(id, request, cancellationToken);
        return role == null ? NotFound(ApiResponse<RoleDto>.Fail("error.validation.reference")) : Ok(ApiResponse<RoleDto>.Ok(role, "common.saved"));
    }

    [RequirePermission("sys:role:delete"), HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id, CancellationToken cancellationToken) =>
        await roles.DeleteRoleAsync(id, cancellationToken)
            ? Ok(ApiResponse<bool>.Ok(true, "common.saved"))
            : NotFound(ApiResponse<bool>.Fail("error.validation.reference"));

    [RequirePermission("sys:role:view"), HttpGet("job-positions/{jobPositionId:int}")]
    public async Task<ActionResult<ApiResponse<int[]>>> GetJobPositionRoles(int jobPositionId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<int[]>.Ok(await roles.GetJobPositionRolesAsync(jobPositionId, cancellationToken)));

    [RequirePermission("sys:role:edit"), HttpPut("job-positions/{jobPositionId:int}")]
    public async Task<ActionResult<ApiResponse<int[]>>> SetJobPositionRoles(int jobPositionId, [FromBody] JobPositionRolesRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<int[]>.Ok(await roles.SetJobPositionRolesAsync(jobPositionId, request.RoleIds ?? [], cancellationToken), "common.saved"));

    [RequirePermission("sys:role:view"), HttpGet("departments/{departmentId:int}")]
    public async Task<ActionResult<ApiResponse<List<DepartmentRoleGrantDto>>>> GetDepartmentRoles(int departmentId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<DepartmentRoleGrantDto>>.Ok(await roles.GetDepartmentRolesAsync(departmentId, cancellationToken)));

    [RequirePermission("sys:role:edit"), HttpPut("departments/{departmentId:int}")]
    public async Task<ActionResult<ApiResponse<List<DepartmentRoleGrantDto>>>> SetDepartmentRoles(int departmentId, [FromBody] List<DepartmentRoleGrantDto> grants, CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<DepartmentRoleGrantDto>>.Ok(await roles.SetDepartmentRolesAsync(departmentId, grants, cancellationToken), "common.saved"));

    [RequirePermission("sys:role:view"), HttpGet("users/{userId:int}")]
    public async Task<ActionResult<ApiResponse<UserAccessPolicyDto>>> GetUserAccess(int userId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<UserAccessPolicyDto>.Ok(await roles.GetUserAccessAsync(userId, cancellationToken)));

    [RequirePermission("sys:role:view"), HttpGet("users/{userId:int}/effective")]
    public async Task<ActionResult<ApiResponse<List<EffectivePermissionDto>>>> GetUserEffective(int userId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<EffectivePermissionDto>>.Ok(await iam.GetEffectiveAsync(userId, cancellationToken)));

    [RequirePermission("sys:role:edit"), HttpPut("users/{userId:int}")]
    public async Task<ActionResult<ApiResponse<UserAccessPolicyDto>>> SetUserAccess(int userId, [FromBody] UserAccessPolicyDto request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<UserAccessPolicyDto>.Ok(await roles.SetUserAccessAsync(userId, request, cancellationToken), "common.saved"));
}

/// <summary>Catalog of every grantable IAM action, used by the role designer.</summary>
[Route("api/v1/sys/permissions")]
public class PermissionsController : BaseApiController
{
    [RequirePermission("sys:role:view"), HttpGet("catalog")]
    public ActionResult<ApiResponse<List<PermissionCatalogItemDto>>> Catalog() =>
        Ok(ApiResponse<List<PermissionCatalogItemDto>>.Ok(PermissionCatalog.All
            .Select(p => new PermissionCatalogItemDto(p.Action, p.Module, p.Resource, p.Verb)).ToList()));
}
