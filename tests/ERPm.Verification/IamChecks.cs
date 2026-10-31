using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.Iam;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Persistence.Seeders;
using ERPm.Infrastructure.Services;
using ERPm.Infrastructure.Services.HumanResource;
using ERPm.Infrastructure.Services.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Step-20 IAM verification: pure policy rules, then PostgreSQL checks for job-position inheritance,
/// BRANCH_TREE / CUSTOM_BRANCHES scopes, Explicit Deny and the privilege escalation guard.
/// </summary>
static class IamChecks
{
    public sealed record Context(Func<ICurrentUser?, ApplicationDbContext> Db, Action<bool, string> Check, Func<Func<Task>, string, Task> Reject);

    public static void Pure(Action<bool, string> check)
    {
        check(IamPolicyEvaluator.Matches("*", "sd:order:view") && IamPolicyEvaluator.Matches("sd:*", "sd:order:view")
            && IamPolicyEvaluator.Matches("*:view", "wms:stock:view") && IamPolicyEvaluator.Matches("SD:ORDER:*", "sd:order:edit")
            && !IamPolicyEvaluator.Matches("sd:order:*", "sd:invoice:view") && !IamPolicyEvaluator.Matches("*:view", "sd:order:edit"),
            "IAM wildcards match whole actions, module prefixes and verb suffixes");

        var tree = new OrgTree([(1, null), (2, 1), (3, 2), (4, 2), (5, 1), (6, 5)]);
        var context = new OrgContext { UserKey = "7", CompanyId = 1, BranchId = 2, Branches = tree };
        var statements = new List<IamStatement>
        {
            SystemRoles.Allow("Tree", ["sd:order:view", "sd:order:edit"], new IamDataScope { Mode = DataScopeModes.BranchTree }),
            SystemRoles.Allow("Extra", ["sd:order:view"], new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [6] }),
            SystemRoles.Deny("NoEdit", ["sd:*:edit"]),
        };
        var view = IamPolicyEvaluator.Evaluate(statements, "sd:order:view", context);
        check(view.Allowed && view.Scope.BranchIds.SetEquals([2, 3, 4, 6]), "Allow scopes are merged by union (BRANCH_TREE + CUSTOM_BRANCHES)");
        check(IamPolicyEvaluator.Evaluate(statements, "sd:order:edit", context) is { Allowed: false, ExplicitDeny: true }, "explicit Deny wins over Allow");
        check(!IamPolicyEvaluator.Evaluate(statements, "pur:order:view", context).Allowed, "actions without an Allow are denied by default");
        check(IamPolicyEvaluator.Resolve(new IamDataScope { Mode = DataScopeModes.Self }, context) is { OwnRecords: true, BranchIds.Count: 0 }
            && IamPolicyEvaluator.Resolve(new IamDataScope { Mode = DataScopeModes.Company }, context).BranchIds.SetEquals([1, 2, 3, 4, 5, 6])
            && IamPolicyEvaluator.Resolve(new IamDataScope { Mode = DataScopeModes.Group }, context).Unrestricted,
            "SELF, COMPANY and GROUP scopes resolve against the holder's org position");
        check(IamPolicyEvaluator.Validate(SystemRoles.Policy(SystemRoles.Allow("Typo", ["sd:ordr:view"]))) == "sd:ordr:view"
            && IamPolicyEvaluator.Validate(SystemRoles.Policy([.. SystemRoles.Templates.SelectMany(t => t.Policy.Statements)])) == null,
            "policy validation rejects actions missing from the permission catalog");

        var granter = new IamPrincipal(statements, context, false);
        check(granter.Escalations([SystemRoles.Allow("Same", ["sd:order:view"], new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [3] })]).Count == 0
            && granter.Escalations([SystemRoles.Deny("Restrict", ["*"])]).Count == 0,
            "escalation guard accepts grants inside the granter's own rights");
        check(granter.Escalations([SystemRoles.Allow("More", ["sd:order:edit"])]).Count == 1
            && granter.Escalations([SystemRoles.Allow("Wider", ["sd:order:view"], new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [5] })]).Count == 1
            && granter.Escalations([SystemRoles.Allow("Group", ["sd:order:view"], new IamDataScope { Mode = DataScopeModes.Group })]).Count == 1,
            "escalation guard rejects extra actions, foreign branches and GROUP scope");
    }

    public static async Task DatabaseAsync(Context t)
    {
        await using var db = t.Db(null);
        var org = await OrganizationSampleSeeder.SeedAsync(db);
        t.Check(await db.IamRoles.CountAsync(r => r.IsSystemRole && r.CompanyId == null) == 3
            && await db.Branches.IgnoreQueryFilters().AnyAsync(b => b.Code == "root" && b.OrgUnitType == OrgUnitType.Group),
            "seed provides ADMIN/OPERATOR/VIEWER system roles and a Group root");

        var account = new AppUser { UserName = "IAM_ACCOUNTANT", DisplayName = "Accountant", Role = "None", PasswordHash = "-" };
        db.AppUsers.Add(account); await db.SaveChangesAsync();
        var cache = new MemoryCache(new MemoryCacheOptions());
        IamService Iam(ApplicationDbContext c, ICurrentUser? u = null) => new(c, cache, u);
        var employees = new EmployeeService(db, Iam(db));
        var employee = await employees.CreateAsync(new CreateEmployeeRequest
        {
            Code = "e-iam-01", FullName = "Accountant", CompanyId = org.CompanyId, BranchId = org.HanoiBranchId,
            DepartmentId = org.AccountingDepartmentId, JobPositionId = org.JobPositions["SALES_ACC"], UserId = account.Id, HireDate = new DateOnly(2026, 1, 1)
        });
        t.Check(employee.Code == "E-IAM-01" && await db.WorkHistories.CountAsync(w => w.EmployeeId == employee.Id) == 1, "creating an employee records the hire work-history row");
        await t.Reject(() => employees.CreateAsync(new CreateEmployeeRequest
        {
            Code = "E-IAM-02", FullName = "Duplicate link", CompanyId = org.CompanyId, BranchId = org.HanoiBranchId, JobPositionId = org.JobPositions["SALES_ACC"], UserId = account.Id
        }), "error.hrm.user_linked");

        var principal = await Iam(db).GetPrincipalAsync(account.Id);
        var orders = principal.Evaluate("sd:order:view");
        t.Check(orders.Allowed && orders.Scope.BranchIds.SetEquals([org.HanoiBranchId, org.HanoiSubBranchId]),
            "job position roles are inherited with the position's BRANCH_TREE scope");
        t.Check(principal.Evaluate("sd:invoice:cancel").ExplicitDeny && !principal.Can("wms:receipt:create"), "inherited role keeps its explicit Deny and default deny");

        db.SalesOrders.AddRange(
            new SalesOrder { OrderNumber = "IAM-HN", BranchId = org.HanoiSubBranchId, Status = "Draft" },
            new SalesOrder { OrderNumber = "IAM-HCM", BranchId = org.HcmBranchId, Status = "Draft" });
        await db.SaveChangesAsync();
        async Task<string[]> VisibleOrders(EffectiveDataScope scope)
        {
            await using var scoped = t.Db(new ScopeUser(account.Id, scope));
            return await scoped.SalesOrders.Where(o => o.OrderNumber.StartsWith("IAM-")).Select(o => o.OrderNumber).OrderBy(n => n).ToArrayAsync();
        }
        t.Check((await VisibleOrders(orders.Scope)).SequenceEqual(["IAM-HN"]), "BRANCH_TREE query filter shows only the holder's branch subtree");

        var roles = new RoleService(db, Iam(db));
        var warehouse = await db.IamRoles.SingleAsync(r => r.CompanyId == null && r.Code == "WAREHOUSE_ACCOUNTANT");
        await roles.SetUserAccessAsync(account.Id, new UserAccessPolicyDto
        {
            Roles = [new() { RoleId = warehouse.Id, ScopeOverride = new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [org.HcmBranchId] } }],
            InlinePolicy = SystemRoles.Policy(SystemRoles.Allow("ViewHcmOrders", ["sd:order:view"], new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [org.HcmBranchId] }))
        });
        principal = await Iam(db).GetPrincipalAsync(account.Id);
        t.Check(principal.Evaluate("wms:stock:view").Scope.BranchIds.SetEquals([org.HcmBranchId]) && principal.Can("wms:receipt:create"),
            "extra user role applies with its CUSTOM_BRANCHES scope override");
        t.Check((await VisibleOrders(principal.Evaluate("sd:order:view").Scope)).SequenceEqual(["IAM-HCM", "IAM-HN"]),
            "CUSTOM_BRANCHES inline grant is merged with the position scope");

        await roles.SetUserAccessAsync(account.Id, new UserAccessPolicyDto { InlinePolicy = SystemRoles.Policy(SystemRoles.Deny("NoOrders", ["sd:order:*"])) });
        principal = await Iam(db).GetPrincipalAsync(account.Id);
        t.Check(!principal.Can("sd:order:view") && principal.Can("sd:invoice:view") && !principal.Can("wms:stock:view"),
            "inline Deny overrides the inherited Allow and removed extra roles stop applying");
        await roles.SetUserAccessAsync(account.Id, new UserAccessPolicyDto());

        var workHistories = new WorkHistoryService(db, Iam(db));
        await workHistories.CreateAsync(new CreateWorkHistoryRequest
        {
            EmployeeId = employee.Id, DateEffective = new DateOnly(2026, 6, 1), CompanyId = org.CompanyId, BranchId = org.HcmBranchId, JobPositionId = org.JobPositions["SALES_ACC"]
        });
        principal = await Iam(db).GetPrincipalAsync(account.Id);
        t.Check(principal.Evaluate("sd:order:view").Scope.BranchIds.SetEquals([org.HcmBranchId])
            && (await db.Employees.AsNoTracking().SingleAsync(e => e.Id == employee.Id)).BranchId == org.HcmBranchId,
            "a transfer in work history moves the employee cache and the IAM scope");

        // Privilege escalation guard, acting as the accountant (company A).
        var signedIn = new ScopeUser(account.Id, principal.ReadScope);
        await using var acting = t.Db(signedIn);
        var guarded = new RoleService(acting, Iam(acting, signedIn));
        RoleDto created = await guarded.CreateRoleAsync(new SaveRoleRequest
        {
            Code = "ca_order_viewer", Name = "Order viewer", CompanyId = org.CompanyId,
            PolicyDocument = SystemRoles.Policy(SystemRoles.Allow("Orders", ["sd:order:view"], new IamDataScope { Mode = DataScopeModes.BranchTree }))
        });
        t.Check(created.Code == "CA_ORDER_VIEWER" && created.CompanyId == org.CompanyId, "a company may create its own role within the creator's rights");
        await t.Reject(() => guarded.CreateRoleAsync(new SaveRoleRequest
        {
            Code = "CA_CANCEL", Name = "Cancel", CompanyId = org.CompanyId, PolicyDocument = SystemRoles.Policy(SystemRoles.Allow("Cancel", ["sd:invoice:cancel"]))
        }), "error.iam.escalation");
        await t.Reject(() => guarded.CreateRoleAsync(new SaveRoleRequest
        {
            Code = "CA_GROUP", Name = "Group scope", CompanyId = org.CompanyId,
            PolicyDocument = SystemRoles.Policy(SystemRoles.Allow("All", ["sd:order:view"], new IamDataScope { Mode = DataScopeModes.Group }))
        }), "error.iam.escalation");

        var otherCompany = new Branch { Code = "COMPANY_B", Name = "Company B", ParentId = (await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.Code == "root")).Id, OrgUnitType = OrgUnitType.Company };
        db.Branches.Add(otherCompany); await db.SaveChangesAsync();
        otherCompany.CompanyId = otherCompany.Id; await db.SaveChangesAsync();
        await t.Reject(() => guarded.CreateRoleAsync(new SaveRoleRequest
        {
            Code = "CB_ORDERS", Name = "Other company", CompanyId = otherCompany.Id, PolicyDocument = SystemRoles.Policy(SystemRoles.Allow("Orders", ["sd:order:view"]))
        }), "error.iam.company_scope");
        await t.Reject(() => guarded.SetJobPositionRolesAsync(org.JobPositions["SALES_ACC"], [warehouse.Id]), "error.iam.escalation");
        var admin = await db.IamRoles.SingleAsync(r => r.Code == IamRole.AdminCode);
        await t.Reject(() => guarded.UpdateRoleAsync(admin.Id, new SaveRoleRequest { Code = "ADMIN", Name = "Hijack", PolicyDocument = admin.PolicyDocument }), "error.iam.system_role");

        // CR-20-01: department levels, Path maintenance and department roles.
        var levels = await db.DepartmentLevels.Where(l => l.CompanyId == null).ToDictionaryAsync(l => l.Code, l => l.Id);
        t.Check(levels.Keys.ToHashSet().SetEquals(["DIVISION", "DEPARTMENT", "SECTION", "TEAM"]), "migration seeds the DIVISION/DEPARTMENT/SECTION/TEAM levels");
        var departments = new DepartmentService(db, Iam(db));
        Task<DepartmentDto> Unit(string code, string level, int? parentId, int companyId, int branchId) => departments.CreateAsync(new CreateDepartmentRequest
        {
            Code = code, Name = code, CompanyId = companyId, BranchId = branchId, ParentId = parentId, LevelId = levels[level]
        });
        var divisionA = await Unit("DIV_A", "DIVISION", null, org.CompanyId, org.HanoiBranchId);
        var departmentA = await Unit("DEP_A", "DEPARTMENT", divisionA.Id, org.CompanyId, org.HanoiBranchId);
        var sectionA = await Unit("SEC_A", "SECTION", departmentA.Id, org.CompanyId, org.HanoiBranchId);
        var teamA = await Unit("TEAM_A", "TEAM", sectionA.Id, org.CompanyId, org.HanoiBranchId);
        t.Check(teamA.Path == $"/{divisionA.Id}/{departmentA.Id}/{sectionA.Id}/{teamA.Id}/" && teamA.Depth == 4,
            "Division > Department > Section > Team builds the materialized path");
        await t.Reject(() => Unit("DEP_UNDER_SEC", "DEPARTMENT", sectionA.Id, org.CompanyId, org.HanoiBranchId), "error.hrm.department_level_order");

        var divisionB = await Unit("DIV_B", "DIVISION", null, org.CompanyId, org.HanoiBranchId);
        await departments.UpdateAsync(departmentA.Id, new UpdateDepartmentRequest
        {
            Code = "DEP_A", Name = "DEP_A", CompanyId = org.CompanyId, BranchId = org.HanoiBranchId, ParentId = divisionB.Id, LevelId = levels["DEPARTMENT"]
        });
        var movedTeam = await db.Departments.AsNoTracking().SingleAsync(d => d.Id == teamA.Id);
        var under = await departments.GetPagedAsync(new DepartmentFilterQuery { UnderId = divisionB.Id, PageSize = 50 });
        t.Check(movedTeam.Path == $"/{divisionB.Id}/{departmentA.Id}/{sectionA.Id}/{teamA.Id}/"
            && under.Items.Select(d => d.Id).ToHashSet().SetEquals([divisionB.Id, departmentA.Id, sectionA.Id, teamA.Id]),
            "moving a department re-roots the Path of its whole subtree (underId filter follows)");

        // Employees in the department and in its section; WAREHOUSE_ACCOUNTANT is granted to the department only.
        async Task<int> Staff(string code, int departmentId)
        {
            var login = new AppUser { UserName = "IAM_" + code, DisplayName = code, Role = "None", PasswordHash = "-" };
            db.AppUsers.Add(login); await db.SaveChangesAsync();
            await employees.CreateAsync(new CreateEmployeeRequest
            {
                Code = "E-" + code, FullName = code, CompanyId = org.CompanyId, BranchId = org.HanoiBranchId, DepartmentId = departmentId,
                JobPositionId = org.JobPositions["SALES_ACC"], UserId = login.Id, HireDate = new DateOnly(2026, 1, 1)
            });
            return login.Id;
        }
        var depUser = await Staff("DEP", departmentA.Id);
        var secUser = await Staff("SEC", sectionA.Id);
        await roles.SetDepartmentRolesAsync(departmentA.Id, [new() { RoleId = warehouse.Id, ApplyToChildren = false }]);
        t.Check((await Iam(db).GetPrincipalAsync(depUser)).Can("wms:receipt:create") && !(await Iam(db).GetPrincipalAsync(secUser)).Can("wms:receipt:create"),
            "a department role reaches its own employees, not sub-departments without ApplyToChildren");
        await roles.SetDepartmentRolesAsync(departmentA.Id, [new() { RoleId = warehouse.Id, ApplyToChildren = true }]);
        t.Check((await Iam(db).GetPrincipalAsync(secUser)).Can("wms:receipt:create"), "ApplyToChildren extends the department role to sub-departments");

        var secEmployee = await db.Employees.AsNoTracking().SingleAsync(e => e.UserId == secUser);
        await workHistories.CreateAsync(new CreateWorkHistoryRequest
        {
            EmployeeId = secEmployee.Id, DateEffective = new DateOnly(2026, 6, 1), CompanyId = org.CompanyId, BranchId = org.HanoiBranchId,
            DepartmentId = org.AccountingDepartmentId, JobPositionId = org.JobPositions["SALES_ACC"]
        });
        t.Check(!(await Iam(db).GetPrincipalAsync(secUser)).Can("wms:receipt:create"), "transferring out of the department drops its roles");

        await roles.SetUserAccessAsync(depUser, new UserAccessPolicyDto { InlinePolicy = SystemRoles.Policy(SystemRoles.Deny("NoReceipts", ["wms:receipt:create"])) });
        t.Check(!(await Iam(db).GetPrincipalAsync(depUser)).Can("wms:receipt:create") && (await Iam(db).GetPrincipalAsync(depUser)).Can("wms:stock:view"),
            "a user Deny overrides the Allow inherited from the department");

        var foreignDepartment = await departments.CreateAsync(new CreateDepartmentRequest { Code = "CB_DEP", Name = "Other company", CompanyId = otherCompany.Id, BranchId = otherCompany.Id });
        await t.Reject(() => guarded.SetDepartmentRolesAsync(foreignDepartment.Id, [new() { RoleId = created.Id }]), "error.iam.company_scope");

        // Task 20.3.3: edit, move and delete org tree nodes.
        var branches = new BranchService(db, Iam(db));
        var annex = await branches.CreateBranchAsync(new CreateBranchRequest { Code = "CA_ANNEX", Name = "Annex", ParentId = org.CompanyId });
        var annexStore = await branches.CreateBranchAsync(new CreateBranchRequest { Code = "CA_ANNEX_01", Name = "Annex store", ParentId = annex.Id });
        var renamed = await branches.UpdateBranchAsync(annex.Id, new UpdateBranchRequest { Code = "CA_ANNEX", Name = "Annex renamed", ParentId = org.CompanyId, Phone = "000", IsActive = true });
        t.Check(renamed!.Name == "Annex renamed" && renamed.Phone == "000" && renamed.OrgUnitType == OrgUnitType.Branch, "branch edit changes the fields and never the node type");

        await branches.UpdateBranchAsync(annex.Id, new UpdateBranchRequest { Code = "CA_ANNEX", Name = "Annex renamed", ParentId = otherCompany.Id, IsActive = true });
        var movedIds = new[] { annex.Id, annexStore.Id };
        t.Check((await db.Branches.AsNoTracking().Where(b => movedIds.Contains(b.Id)).ToListAsync()).All(b => b.CompanyId == otherCompany.Id),
            "moving a branch recomputes CompanyId of its whole subtree");

        await t.Reject(() => branches.UpdateBranchAsync(annex.Id, new UpdateBranchRequest { Code = "CA_ANNEX", Name = "Annex", ParentId = annexStore.Id, IsActive = true }), "error.validation.reference");
        await t.Reject(() => branches.UpdateBranchAsync(otherCompany.Id, new UpdateBranchRequest { Code = "COMPANY_B", Name = "Company B", ParentId = annex.Id, IsActive = true }), "error.validation.request");
        await t.Reject(() => branches.UpdateBranchAsync(org.HanoiBranchId, new UpdateBranchRequest { Code = "CA_HN", Name = "Hanoi", ParentId = otherCompany.Id, IsActive = true }), "error.validation.reference");

        var rootBranch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.Code == "root");
        await t.Reject(() => branches.DeleteBranchAsync(otherCompany.Id), "error.validation.reference");
        await t.Reject(() => branches.DeleteBranchAsync(rootBranch.Id), "error.validation.reference");
        t.Check(await branches.DeleteBranchAsync(annexStore.Id) && !await db.Branches.AnyAsync(b => b.Id == annexStore.Id)
            && await db.Branches.IgnoreQueryFilters().AnyAsync(b => b.Id == annexStore.Id && b.IsDeleted),
            "delete soft-deletes an unreferenced leaf and refuses nodes with children or references");

        // Retire the test accounts so the HTTP stage still bootstraps its administrator on an account-free database.
        foreach (var retired in await db.AppUsers.ToListAsync()) { retired.IsDeleted = true; retired.IsActive = false; }
        await db.SaveChangesAsync();
    }
}

sealed class ScopeUser(int userId, EffectiveDataScope scope) : ICurrentUser
{
    public string? UserId => userId.ToString();
    public bool IsSystem => false;
    public bool IsAdmin => false;
    public int[] BranchIds => scope.BranchIds.ToArray();
    public bool HasUnrestrictedScope => scope.Unrestricted;
    public int[] DepartmentIds => scope.DepartmentIds.ToArray();
    public bool IncludeOwnRecords => scope.OwnRecords;
}
