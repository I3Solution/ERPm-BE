using ERPm.Application.DTOs.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IPostingProfileService
{
    Task<List<PostingProfileDto>> GetProfilesAsync(
        string? module = null,
        string? transactionType = null,
        int? branchId = null,
        CancellationToken ct = default);

    Task<PostingProfileDto> UpsertProfileAsync(
        UpsertPostingProfileRequest request,
        CancellationToken ct = default);
}
