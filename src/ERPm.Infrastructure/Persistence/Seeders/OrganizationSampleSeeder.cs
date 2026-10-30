using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Demo organization for local setups and verification (never run by the production migrate path):
/// Group root -> COMPANY_A -> CA_HN (-> CA_HN_01) and CA_HCM -> departments -> job positions linked to
/// the group role templates.
/// </summary>
public static class OrganizationSampleSeeder
{
    public sealed record SampleOrganization(int CompanyId, int HanoiBranchId, int HanoiSubBranchId, int HcmBranchId,
        int AccountingDepartmentId, int SalesDepartmentId, IReadOnlyDictionary<string, int> JobPositions);

    public static async Task<SampleOrganization> SeedAsync(ApplicationDbContext context)
    {
        var root = await context.Branches.IgnoreQueryFilters().SingleAsync(b => b.Code == "root");
        var company = await BranchAsync(context, "COMPANY_A", "Company A", root.Id, OrgUnitType.Company, null, "0100000001");
        var hanoi = await BranchAsync(context, "CA_HN", "Company A - Ha Noi", company.Id, OrgUnitType.Branch, company.Id, null);
        var hanoiSub = await BranchAsync(context, "CA_HN_01", "Company A - Ha Noi store 01", hanoi.Id, OrgUnitType.Branch, company.Id, null);
        var hcm = await BranchAsync(context, "CA_HCM", "Company A - Ho Chi Minh", company.Id, OrgUnitType.Branch, company.Id, null);

        var accounting = await DepartmentAsync(context, "ACC", "Accounting", company.Id, hanoi.Id);
        var sales = await DepartmentAsync(context, "SALES", "Sales", company.Id, hanoi.Id);

        var positions = new Dictionary<string, int>();
        foreach (var (code, name, rank, mode, departmentId, roleCode) in new (string, string, int, string, int?, string)[]
        {
            ("CHIEF_ACC", "Chief accountant", 2, DataScopeModes.Company, accounting.Id, "SALES_ACCOUNTANT"),
            ("SALES_ACC", "Sales accountant", 4, DataScopeModes.BranchTree, accounting.Id, "SALES_ACCOUNTANT"),
            ("BRANCH_DIR", "Branch director", 1, DataScopeModes.BranchTree, null, "BRANCH_DIRECTOR"),
            ("SALES_STAFF", "Sales staff", 5, DataScopeModes.Self, sales.Id, "SALES_STAFF"),
        })
        {
            var position = await context.JobPositions.FirstOrDefaultAsync(p => p.CompanyId == company.Id && p.Code == code);
            if (position == null)
            {
                context.JobPositions.Add(position = new JobPosition { Code = code, Name = name, CompanyId = company.Id, DepartmentId = departmentId, RankOrder = rank, DefaultScopeMode = mode });
                await context.SaveChangesAsync();
            }
            var role = await context.IamRoles.SingleAsync(r => r.CompanyId == null && r.Code == roleCode);
            if (!await context.JobPositionRoles.AnyAsync(l => l.JobPositionId == position.Id && l.RoleId == role.Id))
            {
                context.JobPositionRoles.Add(new JobPositionRole { JobPositionId = position.Id, RoleId = role.Id });
                await context.SaveChangesAsync();
            }
            positions[code] = position.Id;
        }
        return new(company.Id, hanoi.Id, hanoiSub.Id, hcm.Id, accounting.Id, sales.Id, positions);
    }

    private static async Task<Branch> BranchAsync(ApplicationDbContext context, string code, string name, int parentId, OrgUnitType type, int? companyId, string? taxCode)
    {
        var branch = await context.Branches.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Code == code);
        if (branch != null) return branch;
        context.Branches.Add(branch = new Branch { Code = code, Name = name, ParentId = parentId, OrgUnitType = type, CompanyId = companyId, TaxCode = taxCode });
        await context.SaveChangesAsync();
        if (type == OrgUnitType.Company)
        {
            branch.CompanyId = branch.Id;
            await context.SaveChangesAsync();
        }
        return branch;
    }

    private static async Task<Department> DepartmentAsync(ApplicationDbContext context, string code, string name, int companyId, int branchId)
    {
        var department = await context.Departments.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.Code == code);
        if (department != null) return department;
        context.Departments.Add(department = new Department { Code = code, Name = name, CompanyId = companyId, BranchId = branchId });
        await context.SaveChangesAsync();
        return department;
    }
}
