using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

/// <summary>Branch settings with inheritance (docs/BRANCH_SETTINGS_API_CONTRACT.md).</summary>
[Route("api/v1/sys")]
[PermissionResource("sys:branch-setting")]
public class BranchSettingsController(IBranchSettingService settings) : BaseApiController
{
    [RequirePermission(":view")]
    [HttpGet("branch-setting-definitions")]
    public async Task<ActionResult<ApiResponse<List<SettingDefinitionDto>>>> GetDefinitions([FromQuery] string? group, CancellationToken ct)
        => Ok(ApiResponse<List<SettingDefinitionDto>>.Ok(await settings.GetDefinitionsAsync(group, ct)));

    [RequirePermission(":manage_definition")]
    [HttpPost("branch-setting-definitions")]
    public async Task<ActionResult<ApiResponse<SettingDefinitionDto>>> CreateDefinition([FromBody] CreateSettingDefinitionRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<SettingDefinitionDto>.Ok(await settings.CreateDefinitionAsync(request, ct), "common.saved"));

    [RequirePermission(":view")]
    [HttpGet("branches/{branchId:int}/settings")]
    public async Task<ActionResult<ApiResponse<SettingListDto>>> GetSettings(int branchId, [FromQuery] string? group, CancellationToken ct)
        => Ok(ApiResponse<SettingListDto>.Ok(await settings.GetSettingsAsync(branchId, group, ct)));

    [RequirePermission(":view")]
    [HttpGet("branches/{branchId:int}/settings/{settingKey}")]
    public async Task<ActionResult<ApiResponse<SettingViewDto>>> GetSetting(int branchId, string settingKey, CancellationToken ct)
        => WithEtag(await settings.GetSettingAsync(branchId, settingKey, ct));

    [RequirePermission(":edit")]
    [HttpPost("branches/{branchId:int}/settings/{settingKey}/preview")]
    public async Task<ActionResult<ApiResponse<SettingImpactDto>>> Preview(int branchId, string settingKey, [FromBody] UpdateBranchSettingRequest request, CancellationToken ct)
        => Ok(ApiResponse<SettingImpactDto>.Ok(await settings.PreviewAsync(branchId, settingKey, request, Request.Headers.IfMatch.ToString(), ct)));

    [RequirePermission(":edit")]
    [HttpPut("branches/{branchId:int}/settings/{settingKey}")]
    public async Task<ActionResult<ApiResponse<SettingViewDto>>> Update(int branchId, string settingKey, [FromBody] UpdateBranchSettingRequest request, CancellationToken ct)
        => WithEtag(await settings.UpdateAsync(branchId, settingKey, request, Request.Headers.IfMatch.ToString(), ct), "common.saved");

    [RequirePermission(":view")]
    [HttpGet("branches/{branchId:int}/settings/{settingKey}/history")]
    public async Task<ActionResult<ApiResponse<PagedResult<SettingHistoryItemDto>>>> GetHistory(int branchId, string settingKey,
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<SettingHistoryItemDto>>.Ok(await settings.GetHistoryAsync(branchId, settingKey, pageIndex, pageSize, ct)));

    private ActionResult<ApiResponse<SettingViewDto>> WithEtag(SettingViewDto view, string? message = null)
    {
        Response.Headers.ETag = view.Etag;
        return Ok(ApiResponse<SettingViewDto>.Ok(view, message));
    }
}
