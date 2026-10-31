using ERPm.Application.DTOs.System;

namespace ERPm.Application.Features.System;

public interface IBranchService
{
    Task<List<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
    Task<List<BranchDto>> GetBranchTreeAsync(CancellationToken cancellationToken = default);
    Task<BranchDto?> GetBranchByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<int>> GetBranchAndDescendantIdsAsync(int branchId, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto?> UpdateBranchAsync(int id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteBranchAsync(int id, CancellationToken cancellationToken = default);
}
