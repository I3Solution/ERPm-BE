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
                OrgUnitType = b.OrgUnitType,
                CompanyId = b.CompanyId,
                TaxCode = b.TaxCode,
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
                OrgUnitType = b.OrgUnitType,
                CompanyId = b.CompanyId,
                TaxCode = b.TaxCode,
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
            OrgUnitType = b.OrgUnitType,
            CompanyId = b.CompanyId,
            TaxCode = b.TaxCode,
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
        ERPm.Infrastructure.Services.Common.PostingTransaction.Require(!string.IsNullOrWhiteSpace(request.Code) && request.Code.Length <= 50 && !string.IsNullOrWhiteSpace(request.Name) && request.Name.Length <= 255, "error.validation.request");
        ERPm.Infrastructure.Services.Common.PostingTransaction.Require(request.ParentId == null || await _context.Branches.AnyAsync(b => b.Id == request.ParentId && b.IsActive, cancellationToken), "error.validation.reference");
        // Group -> Company -> Branch: a company sits under the group, a branch under a company or another branch.
        // A branch directly under the group stays allowed for single-company setups (no owning company).
        var parent = request.ParentId == null ? null : await _context.Branches.Where(b => b.Id == request.ParentId)
            .Select(b => new { b.Id, b.OrgUnitType, b.CompanyId }).SingleAsync(cancellationToken);
        ERPm.Infrastructure.Services.Common.PostingTransaction.Require(request.OrgUnitType switch
        {
            OrgUnitType.Group => parent == null && !await _context.Branches.AnyAsync(b => b.OrgUnitType == OrgUnitType.Group, cancellationToken),
            OrgUnitType.Company => parent?.OrgUnitType == OrgUnitType.Group,
            OrgUnitType.Branch => parent != null,
            _ => false
        } && (request.TaxCode?.Length ?? 0) <= 50, "error.validation.request");
        var branch = new Branch
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            OrgUnitType = request.OrgUnitType,
            CompanyId = request.OrgUnitType == OrgUnitType.Branch ? parent!.CompanyId : null,
            TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim(),
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = true
        };

        await _context.Branches.AddAsync(branch, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (branch.OrgUnitType == OrgUnitType.Company)
        {
            branch.CompanyId = branch.Id;
            await _context.SaveChangesAsync(cancellationToken);
        }

        string? parentName = null;
        if (branch.ParentId.HasValue)
        {
            var parentBranch = await _context.Branches.FindAsync(new object[] { branch.ParentId.Value }, cancellationToken);
            parentName = parentBranch?.Name;
        }

        return new BranchDto
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name,
            ParentId = branch.ParentId,
            OrgUnitType = branch.OrgUnitType,
            CompanyId = branch.CompanyId,
            TaxCode = branch.TaxCode,
            ParentName = parentName,
            Address = branch.Address,
            Phone = branch.Phone,
            Email = branch.Email,
            IsDefault = branch.IsDefault,
            IsActive = branch.IsActive
        };
    }
}
