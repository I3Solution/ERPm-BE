using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

/// <summary>
/// Legal entity catalogue. Blocks removal while branches, org units or HRM rows still reference the entity.
/// </summary>
public class CompanyService(ApplicationDbContext context)
    : BaseCrudService<Company, CompanyDto, CreateCompanyRequest, UpdateCompanyRequest>(context)
{
    protected override Task BeforeCreateAsync(Company entity, CreateCompanyRequest dto, CancellationToken cancellationToken)
    {
        Normalize(dto);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        return Task.CompletedTask;
    }

    protected override Task BeforeUpdateAsync(Company entity, UpdateCompanyRequest dto, CancellationToken cancellationToken)
    {
        Normalize(dto);
        return Task.CompletedTask;
    }

    protected override async Task BeforeDeleteAsync(Company entity, CancellationToken cancellationToken)
    {
        var inUse = await Context.Branches.IgnoreQueryFilters().AnyAsync(b => b.CompanyId == entity.Id && !b.IsDeleted, cancellationToken)
            || await Context.OrgStructures.AnyAsync(o => o.CompanyId == entity.Id, cancellationToken)
            || await Context.WorkHistories.AnyAsync(w => w.CompanyId == entity.Id, cancellationToken)
            || await Context.HrmDataPermissions.AnyAsync(p => p.CompanyId == entity.Id, cancellationToken);
        PostingTransaction.Require(!inUse, "error.validation.reference", 409);
    }

    private static void Normalize(CreateCompanyRequest dto)
    {
        dto.Code = dto.Code.Trim();
        dto.Name = dto.Name.Trim();
        PostingTransaction.Require(dto.Code.Length > 0 && dto.Name.Length > 0, "error.validation.required");
    }
}
