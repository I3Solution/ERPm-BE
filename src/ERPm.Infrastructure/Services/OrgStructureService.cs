using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

/// <summary>
/// Organization unit tree. Validates references, rejects cycles and inherits the parent's legal entity
/// when none is given.
/// </summary>
public class OrgStructureService(ApplicationDbContext context)
    : BaseCrudService<OrgStructure, OrgStructureDto, CreateOrgStructureRequest, UpdateOrgStructureRequest>(context)
{
    protected override async Task BeforeCreateAsync(OrgStructure entity, CreateOrgStructureRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(null, dto, cancellationToken);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.CompanyId = dto.CompanyId;
    }

    protected override Task BeforeUpdateAsync(OrgStructure entity, UpdateOrgStructureRequest dto, CancellationToken cancellationToken) =>
        ValidateAsync(entity.Id, dto, cancellationToken);

    protected override async Task BeforeDeleteAsync(OrgStructure entity, CancellationToken cancellationToken)
    {
        var inUse = await Context.OrgStructures.AnyAsync(o => o.ParentId == entity.Id, cancellationToken)
            || await Context.WorkHistories.AnyAsync(w => w.OrgStructureId == entity.Id, cancellationToken)
            || await Context.HrmDataPermissions.AnyAsync(p => p.OrgStructureId == entity.Id, cancellationToken);
        PostingTransaction.Require(!inUse, "error.validation.reference", 409);
    }

    private async Task ValidateAsync(int? id, CreateOrgStructureRequest dto, CancellationToken cancellationToken)
    {
        dto.Code = dto.Code.Trim();
        dto.Name = dto.Name.Trim();
        PostingTransaction.Require(dto.Code.Length > 0 && dto.Name.Length > 0, "error.validation.required");
        PostingTransaction.Require(dto.CompanyId == null || await Context.Companies.AnyAsync(c => c.Id == dto.CompanyId, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(dto.BranchId == null || await Context.Branches.IgnoreQueryFilters().AnyAsync(b => b.Id == dto.BranchId && !b.IsDeleted, cancellationToken), "error.validation.reference");
        if (dto.ParentId is not { } parentId) return;

        var parents = await Context.OrgStructures.AsNoTracking().Select(o => new { o.Id, o.ParentId, o.CompanyId }).ToDictionaryAsync(o => o.Id, cancellationToken);
        PostingTransaction.Require(parents.ContainsKey(parentId), "error.validation.reference");
        // Walk up from the new parent; reaching the edited unit would close a cycle.
        int? cursor = parentId;
        var steps = 0;
        while (cursor is { } current)
        {
            PostingTransaction.Require(current != id && steps++ <= parents.Count, "error.validation.reference");
            cursor = parents.TryGetValue(current, out var node) ? node.ParentId : null;
        }
        dto.CompanyId ??= parents[parentId].CompanyId;
    }
}
