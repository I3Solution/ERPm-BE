using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly IApplicationDbContext _context;

    public BranchService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .Include(b => b.Parent)
            .OrderBy(b => b.Code)
            .Select(b => new BranchDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                ParentId = b.ParentId,
                ParentName = b.Parent != null ? b.Parent.Name : null,
                Address = b.Address,
                Phone = b.Phone,
                Email = b.Email,
                IsDefault = b.IsDefault,
                IsActive = b.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BranchDto>> GetBranchTreeAsync(CancellationToken cancellationToken = default)
    {
        var allBranches = await _context.Branches
            .OrderBy(b => b.Code)
            .ToListAsync(cancellationToken);

        var lookup = allBranches.ToDictionary(
            b => b.Id,
            b => new BranchDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                ParentId = b.ParentId,
                Address = b.Address,
                Phone = b.Phone,
                Email = b.Email,
                IsDefault = b.IsDefault,
                IsActive = b.IsActive,
                Children = new List<BranchDto>()
            });

        var rootBranches = new List<BranchDto>();

        foreach (var branch in allBranches)
        {
            var dto = lookup[branch.Id];
            if (branch.ParentId.HasValue && lookup.TryGetValue(branch.ParentId.Value, out var parentDto))
            {
                dto.ParentName = parentDto.Name;
                parentDto.Children.Add(dto);
            }
            else
            {
                rootBranches.Add(dto);
            }
        }

        return rootBranches;
    }

    public async Task<List<int>> GetBranchAndDescendantIdsAsync(int branchId, CancellationToken cancellationToken = default)
    {
        var allBranches = await _context.Branches.AsNoTracking().ToListAsync(cancellationToken);
        var targetBranch = allBranches.FirstOrDefault(b => b.Id == branchId);
        if (targetBranch == null)
        {
            return new List<int> { branchId };
        }

        var result = new HashSet<int> { branchId };
        var queue = new Queue<int>();
        queue.Enqueue(branchId);

        // Traverse down the infinite recursive hierarchy (BFS)
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var children = allBranches.Where(b => b.ParentId == currentId).ToList();
            foreach (var child in children)
            {
                if (result.Add(child.Id))
                {
                    queue.Enqueue(child.Id);
                }
            }
        }

        return result.ToList();
    }

    public async Task<BranchDto?> GetBranchByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var b = await _context.Branches
            .Include(x => x.Parent)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (b == null) return null;

        return new BranchDto
        {
            Id = b.Id,
            Code = b.Code,
            Name = b.Name,
            ParentId = b.ParentId,
            ParentName = b.Parent?.Name,
            Address = b.Address,
            Phone = b.Phone,
            Email = b.Email,
            IsDefault = b.IsDefault,
            IsActive = b.IsActive
        };
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = new Branch
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = true
        };

        await _context.Branches.AddAsync(branch, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        string? parentName = null;
        if (branch.ParentId.HasValue)
        {
            var parent = await _context.Branches.FindAsync(new object[] { branch.ParentId.Value }, cancellationToken);
            parentName = parent?.Name;
        }

        return new BranchDto
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name,
            ParentId = branch.ParentId,
            ParentName = parentName,
            Address = branch.Address,
            Phone = branch.Phone,
            Email = branch.Email,
            IsDefault = branch.IsDefault,
            IsActive = branch.IsActive
        };
    }
}
