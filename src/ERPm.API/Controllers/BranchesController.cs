using ERPm.Application.Common;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class BranchesController : BaseApiController
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    /// <summary>
    /// Lấy danh sách tất cả các chi nhánh công ty
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranches(CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchesAsync(cancellationToken);
        return Ok(ApiResponse<List<BranchDto>>.Ok(result));
    }

    /// <summary>
    /// Lấy cây phân cấp chi nhánh (hierarchical tree)
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranchTree(CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchTreeAsync(cancellationToken);
        return Ok(ApiResponse<List<BranchDto>>.Ok(result));
    }

    /// <summary>
    /// Lấy chi tiết một chi nhánh theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetBranchById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchByIdAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<BranchDto>.Fail($"Branch with ID '{id}' was not found."));
        }
        return Ok(ApiResponse<BranchDto>.Ok(result));
    }

    /// <summary>
    /// Lấy danh sách ID của chi nhánh được chọn và tất cả chi nhánh con cháu (descendants)
    /// </summary>
    [HttpGet("{id:guid}/descendants")]
    public async Task<ActionResult<ApiResponse<List<Guid>>>> GetBranchDescendants(Guid id, CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchAndDescendantIdsAsync(id, cancellationToken);
        return Ok(ApiResponse<List<Guid>>.Ok(result));
    }

    /// <summary>
    /// Tạo mới một chi nhánh
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BranchDto>>> CreateBranch([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var result = await _branchService.CreateBranchAsync(request, cancellationToken);
        return Ok(ApiResponse<BranchDto>.Ok(result, "Branch created successfully."));
    }
}
