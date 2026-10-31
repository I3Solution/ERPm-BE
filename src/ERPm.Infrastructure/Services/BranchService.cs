using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly ApplicationDbContext _context;
    private readonly IIamService? _iam;

    public BranchService(ApplicationDbContext context, IIamService? iam = null)
    {
        _context = context;
        _iam = iam;
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

    /// <summary>
    /// Edits a node. The type never changes; a new parent must be visible to the caller, keep the tree acyclic and
    /// match the type rules (company under the group, branch under a company or branch). Moving a branch recomputes
    /// CompanyId of its whole subtree and is refused when that would strand departments or employees in the old company.
    /// </summary>
    public async Task<BranchDto?> UpdateBranchAsync(int id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches.Include(b => b.Parent).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null) return null;
        var code = (request.Code ?? "").Trim();
        var name = (request.Name ?? "").Trim();
        PostingTransaction.Require(code.Length is > 0 and <= 50 && name.Length is > 0 and <= 255
            && (request.TaxCode?.Length ?? 0) <= 50 && (request.Address?.Length ?? 0) <= 500
            && (request.Phone?.Length ?? 0) <= 50 && (request.Email?.Length ?? 0) <= 100, "error.validation.request");
        // The unique code index also covers soft-deleted rows.
        PostingTransaction.Require(!await _context.Branches.IgnoreQueryFilters().AnyAsync(b => b.Id != id && b.Code == code, cancellationToken), "error.validation.duplicate", 409);
        PostingTransaction.Require(request.IsActive || (branch.OrgUnitType != OrgUnitType.Group && !branch.IsDefault), "error.validation.reference", 409);

        var parentChanged = request.ParentId != branch.ParentId;
        if (parentChanged)
        {
            PostingTransaction.Require(branch.OrgUnitType != OrgUnitType.Group && request.ParentId != null && request.ParentId != id, "error.validation.request");
            var parent = await _context.Branches.Where(b => b.Id == request.ParentId && b.IsActive)
                .Select(b => new { b.Id, b.OrgUnitType, b.CompanyId }).FirstOrDefaultAsync(cancellationToken);
            PostingTransaction.Require(parent != null, "error.validation.reference");
            PostingTransaction.Require(branch.OrgUnitType == OrgUnitType.Company
                ? parent!.OrgUnitType == OrgUnitType.Group
                : parent!.OrgUnitType is OrgUnitType.Company or OrgUnitType.Branch, "error.validation.request");

            // The whole tree (ignoring the caller's scope) decides cycles: the new parent may not sit inside this subtree.
            var all = await _context.Branches.IgnoreQueryFilters().Where(b => !b.IsDeleted).ToListAsync(cancellationToken);
            var subtree = CollectSubtree(all, id);
            PostingTransaction.Require(subtree.All(b => b.Id != parent!.Id), "error.validation.reference");

            if (branch.OrgUnitType == OrgUnitType.Branch)
            {
                var newCompanyId = parent!.OrgUnitType == OrgUnitType.Company ? parent.Id : parent.CompanyId;
                var ids = subtree.Select(b => b.Id).ToArray();
                if (newCompanyId != branch.CompanyId)
                    PostingTransaction.Require(!await _context.Departments.AnyAsync(d => ids.Contains(d.BranchId), cancellationToken)
                        && !await _context.Employees.AnyAsync(e => ids.Contains(e.BranchId), cancellationToken)
                        && !await _context.WorkHistories.AnyAsync(w => ids.Contains(w.BranchId), cancellationToken), "error.validation.reference", 409);
                foreach (var node in subtree.Where(b => b.OrgUnitType == OrgUnitType.Branch)) node.CompanyId = newCompanyId;
            }
            branch.ParentId = request.ParentId;
        }

        branch.Code = code;
        branch.Name = name;
        branch.TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim();
        branch.Address = request.Address;
        branch.Phone = request.Phone;
        branch.Email = request.Email;
        var activeChanged = branch.IsActive != request.IsActive;
        branch.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        if (parentChanged || activeChanged) _iam?.Invalidate();
        return await GetBranchByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Soft-deletes a node only when nothing references it: no child node, department, employee, work history,
    /// warehouse, voucher, job position, role, department level or account branch grant. Otherwise use IsActive = false.
    /// </summary>
    public async Task<bool> DeleteBranchAsync(int id, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null) return false;
        PostingTransaction.Require(branch.OrgUnitType != OrgUnitType.Group && !branch.IsDefault, "error.validation.reference", 409);
        var db = _context;
        var referenced = await db.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.ParentId == id, cancellationToken)
            || await db.Departments.AnyAsync(d => d.BranchId == id || d.CompanyId == id, cancellationToken)
            || await db.Employees.AnyAsync(e => e.BranchId == id || e.CompanyId == id, cancellationToken)
            || await db.WorkHistories.AnyAsync(w => w.BranchId == id || w.CompanyId == id, cancellationToken)
            || await db.JobPositions.AnyAsync(p => p.CompanyId == id, cancellationToken)
            || await db.IamRoles.AnyAsync(r => r.CompanyId == id, cancellationToken)
            || await db.DepartmentLevels.AnyAsync(l => l.CompanyId == id, cancellationToken)
            || await db.Warehouses.IgnoreQueryFilters().AnyAsync(w => !w.IsDeleted && w.BranchId == id, cancellationToken)
            || await db.SalesOrders.IgnoreQueryFilters().AnyAsync(v => !v.IsDeleted && v.BranchId == id, cancellationToken)
            || await db.SalesInvoices.IgnoreQueryFilters().AnyAsync(v => !v.IsDeleted && v.BranchId == id, cancellationToken)
            || await db.PurchaseOrders.IgnoreQueryFilters().AnyAsync(v => !v.IsDeleted && v.BranchId == id, cancellationToken)
            || await db.StockReceipts.IgnoreQueryFilters().AnyAsync(v => !v.IsDeleted && v.BranchId == id, cancellationToken)
            || await db.StockIssues.IgnoreQueryFilters().AnyAsync(v => !v.IsDeleted && v.BranchId == id, cancellationToken)
            || await db.JournalEntries.IgnoreQueryFilters().AnyAsync(v => !v.IsDeleted && v.BranchId == id, cancellationToken)
            || (await db.AppUsers.IgnoreQueryFilters().Where(u => !u.IsDeleted).Select(u => u.BranchIds).ToListAsync(cancellationToken)).Any(ids => ids.Contains(id));
        PostingTransaction.Require(!referenced, "error.validation.reference", 409);
        branch.IsDeleted = true;
        branch.DeletedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _iam?.Invalidate();
        return true;
    }

    private static List<Branch> CollectSubtree(List<Branch> all, int rootId)
    {
        var result = new List<Branch>();
        var seen = new HashSet<int>();
        var stack = new Stack<int>([rootId]);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current)) continue;
            var node = all.FirstOrDefault(b => b.Id == current);
            if (node != null) result.Add(node);
            foreach (var child in all.Where(b => b.ParentId == current)) stack.Push(child.Id);
        }
        return result;
    }
}
