using ERPm.Application.DTOs.System;

namespace ERPm.Application.Features.System;

public interface IBranchService
{
    Task<List<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
    Task<List<BranchDto>> GetBranchTreeAsync(CancellationToken cancellationToken = default);
    Task<BranchDto?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetBranchAndDescendantIdsAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
}
