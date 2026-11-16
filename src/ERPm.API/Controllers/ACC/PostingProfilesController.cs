using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.ACC;

[Route("api/v1/accounting/posting-profiles")]
public class PostingProfilesController(IPostingProfileService postingProfileService) : BaseApiController
{
    [RequirePermission("acc:posting-profile:view")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PostingProfileDto>>>> GetProfiles(
        [FromQuery] string? module,
        [FromQuery] string? transactionType,
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await postingProfileService.GetProfilesAsync(module, transactionType, branchId, ct);
        return Ok(ApiResponse<List<PostingProfileDto>>.Ok(result));
    }

    [RequirePermission("acc:posting-profile:edit")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PostingProfileDto>>> UpsertProfile(
        [FromBody] UpsertPostingProfileRequest request,
        CancellationToken ct)
    {
        var result = await postingProfileService.UpsertProfileAsync(request, ct);
        return Ok(ApiResponse<PostingProfileDto>.Ok(result, "common.saved"));
    }
}
