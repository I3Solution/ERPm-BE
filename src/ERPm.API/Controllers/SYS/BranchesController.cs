using ERPm.API.Security;
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
    /// Gets a flat list of all company branches.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranches(CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchesAsync(cancellationToken);
        return Ok(ApiResponse<List<BranchDto>>.Ok(result));
    }

    /// <summary>
    /// Gets the hierarchical branch tree.
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranchTree(CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchTreeAsync(cancellationToken);
        return Ok(ApiResponse<List<BranchDto>>.Ok(result));
    }

    /// <summary>
    /// Gets details of a single branch by its internal integer ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetBranchById(int id, CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchByIdAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<BranchDto>.Fail("error.validation.reference"));
        }
        return Ok(ApiResponse<BranchDto>.Ok(result));
    }

    /// <summary>
    /// Gets the list of IDs for the selected branch and all of its descendants.
    /// </summary>
    [HttpGet("{id:int}/descendants")]
    public async Task<ActionResult<ApiResponse<List<int>>>> GetBranchDescendants(int id, CancellationToken cancellationToken)
    {
        var result = await _branchService.GetBranchAndDescendantIdsAsync(id, cancellationToken);
        return Ok(ApiResponse<List<int>>.Ok(result));
    }

    /// <summary>
    /// Creates a new branch.
    /// </summary>
    [RequirePermission("sys:branch:create")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BranchDto>>> CreateBranch([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var result = await _branchService.CreateBranchAsync(request, cancellationToken);
        return Ok(ApiResponse<BranchDto>.Ok(result, "common.saved"));
    }

    /// <summary>
    /// Edits a node of the org tree (code, name, tax code, contact, active flag, parent). The node type never changes.
    /// </summary>
    [RequirePermission("sys:branch:edit")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateBranch(int id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        var result = await _branchService.UpdateBranchAsync(id, request, cancellationToken);
        return result == null ? NotFound(ApiResponse<BranchDto>.Fail("error.validation.reference")) : Ok(ApiResponse<BranchDto>.Ok(result, "common.saved"));
    }

    /// <summary>
    /// Soft-deletes a node that nothing references any more; otherwise deactivate it with IsActive = false.
    /// </summary>
    [RequirePermission("sys:branch:delete")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteBranch(int id, CancellationToken cancellationToken) =>
        await _branchService.DeleteBranchAsync(id, cancellationToken)
            ? Ok(ApiResponse<bool>.Ok(true, "common.saved"))
            : NotFound(ApiResponse<bool>.Fail("error.validation.reference"));
}
