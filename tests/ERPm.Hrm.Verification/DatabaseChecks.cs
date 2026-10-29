using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.HumanResource;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Persistence.Seeders;
using ERPm.Infrastructure.Services;
using ERPm.Infrastructure.Services.HumanResource;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// HRM foundation checks against a fresh PostgreSQL database: migration, SYS organization catalogues,
/// HRM services validation and the server-side data scope. The database is kept for inspection.
/// </summary>
static class DatabaseChecks
{
    public static async Task RunAsync(string connection, Action<bool, string> check)
    {
        var source = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", IncludeErrorDetail = true };
        var name = "erpm_hrm_verify_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(source.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await command.ExecuteNonQueryAsync();
        }
        Console.WriteLine($"Isolated HRM verification database: {name}");
        source.Database = name;
        ApplicationDbContext Db(ICurrentUser? user = null) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(source.ConnectionString).UseSnakeCaseNamingConvention().Options, user);

        async Task Reject(Func<Task> action, string code, string label)
        {
            try { await action(); }
            catch (BusinessException e) { check(e.Code == code, $"{label} ({code})"); return; }
            check(false, $"{label}: expected rejection {code}");
        }
        async Task Duplicate(Func<Task> action, string label)
        {
            try { await action(); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { check(true, label); return; }
            check(false, label);
        }

        await using (var db = Db())
        {
            await db.Database.MigrateAsync();
            await EssentialDataSeeder.SeedAsync(db);
            check(!db.Database.HasPendingModelChanges(), "HRM migration matches the current model");
            check(await db.Branches.AllAsync(b => b.CompanyId == null), "existing branches keep working without a legal entity");
        }

        // SYS organization catalogues
        int companyA, companyB, root, child, other;
        await using (var db = Db())
        {
            var companies = new CompanyService(db);
            companyA = (await companies.CreateAsync(new CreateCompanyRequest { Code = " CTY_A ", Name = "Company A", TaxCode = "0100000001" })).Id;
            companyB = (await companies.CreateAsync(new CreateCompanyRequest { Code = "CTY_B", Name = "Company B" })).Id;
            check((await db.Companies.SingleAsync(c => c.Id == companyA)).Code == "CTY_A", "company code is trimmed");
        }
        await using (var db = Db())
            await Duplicate(() => new CompanyService(db).CreateAsync(new CreateCompanyRequest { Code = "CTY_A", Name = "Again" }), "duplicate company code is rejected by the unique index");

        await using (var db = Db())
        {
            var orgs = new OrgStructureService(db);
            root = (await orgs.CreateAsync(new CreateOrgStructureRequest { Code = "HQ", Name = "Head office", CompanyId = companyA })).Id;
            child = (await orgs.CreateAsync(new CreateOrgStructureRequest { Code = "HR", Name = "HR department", ParentId = root })).Id;
            other = (await orgs.CreateAsync(new CreateOrgStructureRequest { Code = "PLANT", Name = "Plant", CompanyId = companyB })).Id;
            check((await db.OrgStructures.SingleAsync(o => o.Id == child)).CompanyId == companyA, "org unit inherits the parent's legal entity");
            await Reject(() => orgs.UpdateAsync(root, new UpdateOrgStructureRequest { Code = "HQ", Name = "Head office", ParentId = child, CompanyId = companyA }), "error.validation.reference", "org unit cannot become a child of its own descendant");
            await Reject(() => orgs.CreateAsync(new CreateOrgStructureRequest { Code = "X", Name = "Orphan", ParentId = 999999 }), "error.validation.reference", "org unit parent must exist");
        }

        // HRM employees, accounts and work history
        int userId, employeeId;
        await using (var db = Db())
        {
            var account = new AppUser { UserName = "HR_MANAGER", DisplayName = "HR manager", Role = "Operator" };
            db.AppUsers.Add(account);
            await db.SaveChangesAsync();
            userId = account.Id;

            var employees = new EmployeeService(db);
            employeeId = (await employees.CreateAsync(new CreateEmployeeRequest { Code = "NV001", Name = "Employee One", UserId = userId, HireDate = new(2025, 1, 1) })).Id;
            await Reject(() => employees.CreateAsync(new CreateEmployeeRequest { Code = "NV404", Name = "Ghost account", UserId = 999999 }), "error.validation.reference", "employee account must exist");
        }
        await using (var db = Db())
            await Duplicate(() => new EmployeeService(db).CreateAsync(new CreateEmployeeRequest { Code = "NV002", Name = "Same account", UserId = userId }), "an account links to one employee only");

        await using (var db = Db())
        {
            var history = new WorkHistoryService(db);
            var hire = await history.CreateAsync(new CreateWorkHistoryRequest { EmployeeId = employeeId, DateEffective = new(2025, 1, 1), OrgStructureId = child });
            check(hire.CompanyId == companyA, "work history takes the legal entity of its org unit");
            await history.CreateAsync(new CreateWorkHistoryRequest { EmployeeId = employeeId, DateEffective = new(2026, 4, 16), OrgStructureId = other });
            await history.CreateAsync(new CreateWorkHistoryRequest { EmployeeId = employeeId, DateEffective = new(2026, 5, 1), OrgStructureId = root, IsApproved = false });
            await Reject(() => history.CreateAsync(new CreateWorkHistoryRequest { EmployeeId = employeeId, DateEffective = new(2026, 6, 1) }), "error.validation.required", "non-termination work history needs an org unit");
            await Reject(() => history.CreateAsync(new CreateWorkHistoryRequest { EmployeeId = employeeId, DateEffective = new(2026, 6, 1), OrgStructureId = other, CompanyId = companyA }), "error.validation.reference", "work history legal entity must match its org unit");
        }

        await using (var db = Db())
        {
            await Reject(() => new OrgStructureService(db).DeleteAsync(root), "error.validation.reference", "org unit with children or history cannot be deleted");
            await Reject(() => new CompanyService(db).DeleteAsync(companyA), "error.validation.reference", "legal entity in use cannot be deleted");
        }

        // Timeline and data scope resolved from the database
        await using (var db = Db())
        {
            var timeline = await new HrmDataScopeService(db).GetTimelineAsync(employeeId);
            var spans = timeline.EmploymentSegments(new(2026, 4, 1), new(2026, 5, 31));
            check(timeline.Entries.Count == 2 && spans.Count == 2 && spans[0].CompanyId == companyA && spans[1].CompanyId == companyB
                && spans[1].From == new DateOnly(2026, 4, 16), "timeline uses approved rows only and splits by legal entity");

            var permissions = new HrmDataPermissionService(db);
            await Reject(() => permissions.CreateAsync(new CreateHrmDataPermissionRequest { UserId = userId }), "error.validation.required", "data permission needs an org unit or a legal entity");
            await permissions.CreateAsync(new CreateHrmDataPermissionRequest { UserId = userId, OrgStructureId = root, CompanyId = companyA });
        }

        var today = new DateOnly(2026, 10, 30);
        await using (var db = Db())
        {
            var scope = await new HrmDataScopeService(db, new HrmTestUser(userId, admin: false)).GetCurrentScopeAsync();
            var timeline = await new HrmDataScopeService(db).GetTimelineAsync(employeeId);
            check(!scope.All && scope.Covers(child, companyA) && !scope.Covers(other, companyB) && scope.OwnProfileId == employeeId,
                "account scope expands the org subtree within its legal entity and resolves its own employee");
            check(timeline.VisibleBefore(scope, today) == new DateOnly(2026, 4, 16)
                && timeline.CanWrite(scope, -1, new(2026, 3, 1), today) && !timeline.CanWrite(scope, -1, new(2026, 5, 1), today),
                "old owner sees and edits only the period before the transfer");

            var none = await new HrmDataScopeService(db, new HrmTestUser(999999, admin: false)).GetCurrentScopeAsync();
            var adminScope = await new HrmDataScopeService(db, new HrmTestUser(userId, admin: true)).GetCurrentScopeAsync();
            check(none.IsEmpty && adminScope.All, "accounts without rows see nothing; Admin is unrestricted");
        }

        // Shift catalogue feeds the timesheet calculator
        await using (var db = Db())
        {
            var shifts = new ShiftService(db);
            await Reject(() => shifts.CreateAsync(new CreateShiftRequest { Code = "BAD", Name = "Bad", StartTime = new(8, 0), EndTime = new(8, 0) }), "error.validation.request", "shift start and end cannot be equal");
            await Reject(() => shifts.CreateAsync(new CreateShiftRequest { Code = "BAD", Name = "Bad", StartTime = new(8, 0), EndTime = new(9, 0), BreakMinutes = 60 }), "error.validation.request", "shift break cannot exceed its duration");
            var office = await shifts.CreateAsync(new CreateShiftRequest { Code = "hc", Name = "Office", StartTime = new(8, 0), EndTime = new(17, 0), BreakMinutes = 60, LateGraceMinutes = 5, WorkDays = WorkDays.MonToSat });
            var stored = await db.Shifts.SingleAsync(s => s.Id == office.Id);
            var day = TimesheetCalculator.CalculateDay(stored.ToSchedule(), new(2026, 10, 5), [new DateTime(2026, 10, 5, 7, 55, 0), new DateTime(2026, 10, 5, 17, 2, 0)], true, null, new DateTime(2026, 12, 1));
            check(stored.Code == "HC" && stored.StartTime == new TimeOnly(8, 0) && day.Credit == 1m, "stored shift round-trips into a full-day timesheet credit");
        }

        // Soft delete keeps history but hides the profile
        await using (var db = Db())
        {
            var employees = new EmployeeService(db);
            var temp = await employees.CreateAsync(new CreateEmployeeRequest { Code = "NV900", Name = "Temporary" });
            await employees.DeleteAsync(temp.Id);
            var page = await employees.GetPagedAsync(new ERPm.Application.Common.Crud.BaseFilterQuery { PageSize = 200 });
            check(page.Items.All(e => e.Id != temp.Id) && await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.Id == temp.Id && e.IsDeleted),
                "deleted employee is hidden but retained");
        }
    }
}

sealed class HrmTestUser(int id, bool admin) : ICurrentUser
{
    public string? UserId => id.ToString();
    public bool IsSystem => false;
    public bool IsAdmin => admin;
    public int[] BranchIds => [];
}
