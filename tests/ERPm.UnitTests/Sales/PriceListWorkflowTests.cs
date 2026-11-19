using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.Sales;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Events;
using ERPm.Domain.Services.Iam;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERPm.UnitTests.Sales;

public sealed class PriceListWorkflowTests
{
    private static ApplicationDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Branch BranchA, Branch BranchB, Item Item)> SeedCoreAsync(ApplicationDbContext db)
    {
        var uom = new UnitOfMeasure { Id = 1, Code = "PCS", Name = "Piece", IsActive = true };
        var branchA = new Branch { Id = 1, Code = "BR-A", Name = "Branch A", IsActive = true };
        var branchB = new Branch { Id = 2, Code = "BR-B", Name = "Branch B", IsActive = true };
        var item = new Item { Id = 10, Code = "SP01", Name = "Product 1", BaseUomId = 1, SalePrice = 100m, VatRate = 10m, IsActive = true };
        db.UnitsOfMeasure.Add(uom);
        db.Branches.AddRange(branchA, branchB);
        db.Items.Add(item);
        db.AppUsers.AddRange(
            new AppUser { Id = 101, UserName = "EDITOR", DisplayName = "Editor User", IsActive = true },
            new AppUser { Id = 102, UserName = "APPROVER", DisplayName = "Approver User", IsActive = true },
            new AppUser { Id = 103, UserName = "ACTIVATOR", DisplayName = "Activator User", IsActive = true },
            new AppUser { Id = 104, UserName = "PURMGR", DisplayName = "Purchase Manager", IsActive = true },
            new AppUser { Id = 105, UserName = "BRANCH_A", DisplayName = "Branch A User", IsActive = true });
        await db.SaveChangesAsync();
        return (branchA, branchB, item);
    }

    private static IamPrincipal MakePrincipal(int userId, string[] actions, string scopeMode = DataScopeModes.Group, int[]? customBranches = null)
    {
        var scope = customBranches != null
            ? new IamDataScope { Mode = scopeMode, CustomBranchIds = [.. customBranches], IncludeDescendants = false }
            : new IamDataScope { Mode = scopeMode };
        var statement = new IamStatement { Sid = "Allow", Effect = IamStatement.Allow, Actions = [.. actions], DataScope = scope };
        var tree = new OrgTree([(1, null), (2, null)]);
        var ctx = new OrgContext { UserKey = userId.ToString(), Branches = tree };
        return new IamPrincipal([statement], ctx, false);
    }

    [Fact]
    public void PermissionCatalog_ContainsSeparatedSalesAndPurchaseWorkflowVerbs()
    {
        string[] verbs = ["view", "create", "edit", "delete", "submit", "withdraw", "approve", "reject", "activate", "close", "archive"];
        foreach (var verb in verbs)
        {
            Assert.True(PermissionCatalog.Contains($"sd:pricelist:{verb}"), $"Missing sd:pricelist:{verb}");
            Assert.True(PermissionCatalog.Contains($"pur:pricelist:{verb}"), $"Missing pur:pricelist:{verb}");
        }
    }

    [Fact]
    public async Task FullLifecycle_Draft_Submit_Withdraw_Reject_Approve_Activate_Close_Archive_RecordsAuditAndNotifications()
    {
        await using var db = CreateDb(nameof(FullLifecycle_Draft_Submit_Withdraw_Reject_Approve_Activate_Close_Archive_RecordsAuditAndNotifications));
        var (branchA, _, item) = await SeedCoreAsync(db);
        var notifier = new StubNotifier();
        var iam = new StubIam(new Dictionary<int, IamPrincipal>
        {
            [101] = MakePrincipal(101, ["sd:pricelist:view", "sd:pricelist:create", "sd:pricelist:edit", "sd:pricelist:submit", "sd:pricelist:withdraw"]),
            [102] = MakePrincipal(102, ["sd:pricelist:view", "sd:pricelist:approve", "sd:pricelist:reject"]),
            [103] = MakePrincipal(103, ["sd:pricelist:view", "sd:pricelist:activate", "sd:pricelist:close", "sd:pricelist:archive"])
        });

        var editorSvc = new PriceListService(db, null, new StubUser(101), iam, notifier);
        var approverSvc = new PriceListService(db, null, new StubUser(102), iam, notifier);
        var activatorSvc = new PriceListService(db, null, new StubUser(103), iam, notifier);

        var from = DateTime.UtcNow.AddDays(-1);
        var created = await editorSvc.CreateAsync(new CreatePriceListRequest
        {
            Code = "PL-WF",
            Name = "Workflow Sales List",
            Currency = "VND",
            Type = PriceListType.Sales,
            BranchId = branchA.Id,
            EffectiveFrom = from,
            EffectiveTo = from.AddDays(30),
            IsActive = true,
            Items = [new() { ItemId = item.Id, UnitPrice = 90m, MinQuantity = 1m }]
        });

        // New price list starts V1 in Draft, so active Items preview is empty until activated.
        Assert.Empty(created.Items);
        var v1 = Assert.Single(await editorSvc.GetVersionsAsync(created.Id));
        Assert.Equal(PriceListVersionStatus.Draft, v1.Status);

        // Editor cannot approve or activate.
        await Assert.ThrowsAsync<BusinessException>(() => editorSvc.ApproveVersionAsync(created.Id, v1.Id));
        await Assert.ThrowsAsync<BusinessException>(() => editorSvc.ActivateVersionAsync(created.Id, v1.Id));

        // Submit -> Withdraw -> Submit -> Reject (requires reason) -> Submit -> Approve -> Withdraw -> Submit -> Approve
        var submitted = await editorSvc.SubmitVersionAsync(created.Id, v1.Id);
        Assert.Equal(PriceListVersionStatus.PendingApproval, submitted.Status);
        Assert.Contains(notifier.Events, e => e.TemplateCode == "SD_PRICELIST_SUBMITTED" && e.TargetId == 102);

        var withdrawn1 = await editorSvc.WithdrawVersionAsync(created.Id, v1.Id, new() { Reason = "Recheck" });
        Assert.Equal(PriceListVersionStatus.Draft, withdrawn1.Status);

        await editorSvc.SubmitVersionAsync(created.Id, v1.Id);
        await Assert.ThrowsAsync<BusinessException>(() => approverSvc.RejectVersionAsync(created.Id, v1.Id, new() { Reason = "   " }));
        var rejected = await approverSvc.RejectVersionAsync(created.Id, v1.Id, new() { Reason = "Margin too low" });
        Assert.Equal(PriceListVersionStatus.Draft, rejected.Status);
        Assert.Equal("Margin too low", rejected.RejectReason);

        await editorSvc.SubmitVersionAsync(created.Id, v1.Id);
        var approved = await approverSvc.ApproveVersionAsync(created.Id, v1.Id);
        Assert.Equal(PriceListVersionStatus.Approved, approved.Status);
        Assert.Contains(notifier.Events, e => e.TemplateCode == "SD_PRICELIST_APPROVED" && e.TargetId == 103);

        var withdrawn2 = await editorSvc.WithdrawVersionAsync(created.Id, v1.Id);
        Assert.Equal(PriceListVersionStatus.Draft, withdrawn2.Status);

        await editorSvc.SubmitVersionAsync(created.Id, v1.Id);
        await approverSvc.ApproveVersionAsync(created.Id, v1.Id);

        // Approver cannot activate; Activator activates -> header PUT cannot mutate Active lines.
        await Assert.ThrowsAsync<BusinessException>(() => approverSvc.ActivateVersionAsync(created.Id, v1.Id));
        var active = await activatorSvc.ActivateVersionAsync(created.Id, v1.Id);
        Assert.Equal(PriceListVersionStatus.Active, active.Status);

        var headerUpdated = await editorSvc.UpdateAsync(created.Id, new UpdatePriceListRequest
        {
            Code = "PL-WF",
            Name = "Workflow Sales List Renamed",
            Currency = "VND",
            Type = PriceListType.Sales,
            BranchId = branchA.Id,
            EffectiveFrom = from,
            EffectiveTo = from.AddDays(30),
            IsActive = true,
            Items = [new() { ItemId = item.Id, UnitPrice = 1m, MinQuantity = 1m }]
        });
        Assert.Equal("Workflow Sales List Renamed", headerUpdated!.Name);
        Assert.Equal(90m, Assert.Single(headerUpdated.Items).UnitPrice);

        // Close requires reason -> Expired -> Archive -> Archived; audit log records every step.
        await Assert.ThrowsAsync<BusinessException>(() => activatorSvc.CloseVersionAsync(created.Id, v1.Id, new() { Reason = "" }));
        var closed = await activatorSvc.CloseVersionAsync(created.Id, v1.Id, new() { Reason = "Campaign ended" });
        Assert.Equal(PriceListVersionStatus.Expired, closed.Status);
        Assert.Equal("Campaign ended", closed.CloseReason);

        var archived = await activatorSvc.ArchiveVersionAsync(created.Id, v1.Id, new() { Reason = "Archived" });
        Assert.Equal(PriceListVersionStatus.Archived, archived.Status);

        var audits = await editorSvc.GetVersionAuditAsync(created.Id, v1.Id);
        Assert.Equal(12, audits.Count);
        Assert.Contains(audits, a => a.Action == "REJECT" && a.Reason == "Margin too low" && a.ActorName == "Approver User");
        Assert.Contains(audits, a => a.Action == "CLOSE" && a.Reason == "Campaign ended" && a.ActorName == "Activator User");
        Assert.Contains(audits, a => a.Action == "ARCHIVE");
    }

    [Fact]
    public async Task Activate_RejectsExpiredOrOutsideHeaderRange_And_AllowsSelfApprovalWhenPermitted()
    {
        await using var db = CreateDb(nameof(Activate_RejectsExpiredOrOutsideHeaderRange_And_AllowsSelfApprovalWhenPermitted));
        var (branchA, _, item) = await SeedCoreAsync(db);
        var iam = new StubIam(new Dictionary<int, IamPrincipal>
        {
            [101] = MakePrincipal(101, ["sd:pricelist:view", "sd:pricelist:create", "sd:pricelist:edit", "sd:pricelist:submit", "sd:pricelist:approve", "sd:pricelist:activate"])
        });
        var svc = new PriceListService(db, null, new StubUser(101), iam);
        var from = DateTime.UtcNow.AddDays(-5);

        var pl = await svc.CreateAsync(new CreatePriceListRequest
        {
            Code = "PL-BOUND",
            Name = "Bounded",
            Currency = "VND",
            Type = PriceListType.Sales,
            BranchId = branchA.Id,
            EffectiveFrom = from,
            EffectiveTo = from.AddDays(20),
            IsActive = true
        });

        // Expired version (EffectiveTo < UtcNow) cannot be activated.
        var expiredVer = await svc.CreateVersionAsync(pl.Id, new CreatePriceListVersionRequest
        {
            VersionCode = "EXP",
            EffectiveFrom = from,
            EffectiveTo = from.AddDays(1),
            Items = [new() { ItemId = item.Id, UnitPrice = 80m, MinQuantity = 1m }]
        });
        await svc.SubmitVersionAsync(pl.Id, expiredVer.Id);
        await svc.ApproveVersionAsync(pl.Id, expiredVer.Id); // Same user is allowed when granted both permissions.
        var expiredEx = await Assert.ThrowsAsync<BusinessException>(() => svc.ActivateVersionAsync(pl.Id, expiredVer.Id));
        Assert.Equal("error.sd.pricelist_version_expired", expiredEx.Code);

        // Version exceeding header EffectiveTo cannot be activated.
        var outsideVer = await svc.CreateVersionAsync(pl.Id, new CreatePriceListVersionRequest
        {
            VersionCode = "OUT",
            EffectiveFrom = from,
            EffectiveTo = from.AddDays(40),
            Items = [new() { ItemId = item.Id, UnitPrice = 85m, MinQuantity = 1m }]
        });
        await svc.SubmitVersionAsync(pl.Id, outsideVer.Id);
        await svc.ApproveVersionAsync(pl.Id, outsideVer.Id);
        var outsideEx = await Assert.ThrowsAsync<BusinessException>(() => svc.ActivateVersionAsync(pl.Id, outsideVer.Id));
        Assert.Equal("error.sd.pricelist_version_outside_header_range", outsideEx.Code);
    }

    [Fact]
    public async Task SalesPurchaseIsolation_And_BranchDataScope_EnforcedOnServer()
    {
        await using var db = CreateDb(nameof(SalesPurchaseIsolation_And_BranchDataScope_EnforcedOnServer));
        var (branchA, branchB, item) = await SeedCoreAsync(db);
        var adminSvc = new PriceListService(db);
        var from = DateTime.UtcNow.AddDays(-1);

        var salesA = await adminSvc.CreateAsync(new CreatePriceListRequest { Code = "SD-A", Name = "Sales A", Currency = "VND", Type = PriceListType.Sales, BranchId = branchA.Id, EffectiveFrom = from, IsActive = true, Items = [new() { ItemId = item.Id, UnitPrice = 50m }] });
        var salesB = await adminSvc.CreateAsync(new CreatePriceListRequest { Code = "SD-B", Name = "Sales B", Currency = "VND", Type = PriceListType.Sales, BranchId = branchB.Id, EffectiveFrom = from, IsActive = true, Items = [new() { ItemId = item.Id, UnitPrice = 55m }] });
        var salesGlobal = await adminSvc.CreateAsync(new CreatePriceListRequest { Code = "SD-ALL", Name = "Sales Global", Currency = "VND", Type = PriceListType.Sales, BranchId = null, EffectiveFrom = from, IsActive = true, Items = [new() { ItemId = item.Id, UnitPrice = 60m }] });
        var purA = await adminSvc.CreateAsync(new CreatePriceListRequest { Code = "PUR-A", Name = "Purchase A", Currency = "VND", Type = PriceListType.Purchase, BranchId = branchA.Id, EffectiveFrom = from, IsActive = true, Items = [new() { ItemId = item.Id, UnitPrice = 40m }] });

        var iam = new StubIam(new Dictionary<int, IamPrincipal>
        {
            [104] = MakePrincipal(104, ["pur:pricelist:view", "pur:pricelist:edit", "pur:pricelist:submit", "pur:pricelist:approve", "pur:pricelist:activate"]),
            [105] = MakePrincipal(105, ["sd:pricelist:view", "sd:pricelist:edit", "sd:pricelist:submit"], DataScopeModes.CustomBranches, [branchA.Id])
        });

        var purSvc = new PriceListService(db, null, new StubUser(104), iam);
        var purPage = await purSvc.GetPagedAsync(new PriceListFilterQuery { PageSize = 50 });
        Assert.Single(purPage.Items);
        Assert.Equal(purA.Id, purPage.Items[0].Id);
        await Assert.ThrowsAsync<BusinessException>(() => purSvc.GetByIdAsync(salesA.Id));
        await Assert.ThrowsAsync<BusinessException>(() => purSvc.GetPagedAsync(new PriceListFilterQuery { Type = PriceListType.Sales }));

        var purV1 = Assert.Single(await purSvc.GetVersionsAsync(purA.Id));
        await purSvc.SubmitVersionAsync(purA.Id, purV1.Id);
        await purSvc.ApproveVersionAsync(purA.Id, purV1.Id);
        var purActive = await purSvc.ActivateVersionAsync(purA.Id, purV1.Id);
        Assert.Equal(PriceListVersionStatus.Active, purActive.Status);

        var branchASvc = new PriceListService(db, null, new StubUser(105), iam);
        var branchPage = await branchASvc.GetPagedAsync(new PriceListFilterQuery { PageSize = 50 });
        Assert.Equal(2, branchPage.TotalCount);
        Assert.Contains(branchPage.Items, p => p.Id == salesA.Id);
        Assert.Contains(branchPage.Items, p => p.Id == salesGlobal.Id);
        Assert.DoesNotContain(branchPage.Items, p => p.Id == salesB.Id);

        // Branch A user can view global PL, cannot edit global PL or Branch B PL, can submit Branch A PL.
        Assert.NotNull(await branchASvc.GetByIdAsync(salesGlobal.Id));
        await Assert.ThrowsAsync<BusinessException>(() => branchASvc.GetByIdAsync(salesB.Id));
        await Assert.ThrowsAsync<BusinessException>(() => branchASvc.UpdateAsync(salesGlobal.Id, new UpdatePriceListRequest { Code = "SD-ALL", Name = "Hacked", Currency = "VND", Type = PriceListType.Sales, BranchId = null, EffectiveFrom = from, IsActive = true }));
        var salesAV1 = Assert.Single(await branchASvc.GetVersionsAsync(salesA.Id));
        var submittedA = await branchASvc.SubmitVersionAsync(salesA.Id, salesAV1.Id);
        Assert.Equal(PriceListVersionStatus.PendingApproval, submittedA.Status);
    }

    private sealed class StubUser(int id) : ICurrentUser
    {
        public string? UserId => id.ToString();
        public bool IsSystem => false;
        public bool IsAdmin => false;
        public int[] BranchIds => [];
    }

    private sealed class StubIam(Dictionary<int, IamPrincipal> map) : IIamService
    {
        private static readonly IamPrincipal Empty = new([], new OrgContext(), false);
        public Task<IamPrincipal> GetPrincipalAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(map.TryGetValue(userId, out var principal) ? principal : Empty);
        public Task<IamPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<List<EffectivePermissionDto>> GetEffectiveAsync(int userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task EnsureCanGrantAsync(IEnumerable<IamStatement> statements, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public void Invalidate() { }
    }

    private sealed class StubNotifier : INotificationEngine
    {
        public List<INotificationEvent> Events { get; } = [];
        public Task<NotificationResult> PublishAsync(INotificationEvent notifEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(notifEvent);
            return Task.FromResult(new NotificationResult(true, null, 1));
        }
        public Task<NotificationResult> BroadcastAsync(BroadcastNotificationRequest request, int? senderUserId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
