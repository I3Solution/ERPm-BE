using System.Text.Json;
using System.Text.Json.Nodes;
using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Localization;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Purchase;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ERPm.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUser? _currentUser;
    public bool IsUnrestricted => _currentUser == null || _currentUser.IsSystem || _currentUser.IsAdmin || _currentUser.HasUnrestrictedScope;
    public int[] AllowedBranchIds => _currentUser?.BranchIds ?? [];
    public int[] AllowedDepartmentIds => _currentUser?.DepartmentIds ?? [];
    public string? OwnRecordsUserId => _currentUser is { IncludeOwnRecords: true } ? _currentUser.UserId : null;
    internal bool AllowCancellation { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUser? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<IamRole> IamRoles => Set<IamRole>();
    public DbSet<JobPositionRole> JobPositionRoles => Set<JobPositionRole>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<DepartmentRole> DepartmentRoles => Set<DepartmentRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DepartmentLevel> DepartmentLevels => Set<DepartmentLevel>();
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WorkHistory> WorkHistories => Set<WorkHistory>();
    public DbSet<AppLanguage> AppLanguages => Set<AppLanguage>();
    public DbSet<AppDictionary> AppDictionaries => Set<AppDictionary>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemAttribute> ItemAttributes => Set<ItemAttribute>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptItem> StockReceiptItems => Set<StockReceiptItem>();
    public DbSet<StockIssue> StockIssues => Set<StockIssue>();
    public DbSet<StockIssueItem> StockIssueItems => Set<StockIssueItem>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<NotificationOutbox> NotificationOutboxes => Set<NotificationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var dictionaryComparer = new ValueComparer<Dictionary<string, string>>(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
            c => new Dictionary<string, string>(c));

        var objectDictionaryComparer = new ValueComparer<Dictionary<string, object>>(
            (c1, c2) => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(c1, (JsonSerializerOptions?)null), JsonSerializer.SerializeToNode(c2, (JsonSerializerOptions?)null)),
            c => 0,
            c => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        modelBuilder.HasSequence<long>("erpm_document_number");
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("tbl_SYS_User");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.UserName).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.Property(e => e.DisplayName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired();
            entity.Property(e => e.BranchIds).HasColumnType("jsonb");
            entity.Property(e => e.InlinePolicy).HasColumnType("jsonb").HasConversion(JsonConverter<IamPolicyDocument?>(), JsonComparer<IamPolicyDocument?>());
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => new List<string>(c));

        // 1. System / Organization & Localization
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("tbl_SYS_Branch");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.TaxCode).HasMaxLength(50);
            entity.Property(e => e.OrgUnitType).HasDefaultValue(OrgUnitType.Branch).HasSentinel((OrgUnitType)(-1));

            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 1.1 SYS - Dynamic IAM roles and assignments
        modelBuilder.Entity<IamRole>(entity =>
        {
            entity.ToTable("tbl_SYS_Role");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Translations).HasColumnType("jsonb").HasConversion(JsonConverter<Dictionary<string, string>>()).Metadata.SetValueComparer(dictionaryComparer);
            entity.Property(e => e.PolicyDocument).HasColumnType("jsonb").HasConversion(JsonConverter<IamPolicyDocument>(), JsonComparer<IamPolicyDocument>());
            entity.Property(e => e.Version).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            // Codes are unique per company; group templates (null company) share one namespace.
            entity.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique().AreNullsDistinct(false).HasFilter("is_deleted = false");
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<JobPositionRole>(entity =>
        {
            entity.ToTable("tbl_SYS_JobPositionRole");
            ConfigureDualKeyEntity(entity);
            entity.HasOne(e => e.JobPosition).WithMany(e => e.Roles).HasForeignKey(e => e.JobPositionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.JobPositionId, e.RoleId }).IsUnique();
        });

        modelBuilder.Entity<DepartmentRole>(entity =>
        {
            entity.ToTable("tbl_SYS_DepartmentRole");
            ConfigureDualKeyEntity(entity);
            entity.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.DepartmentId, e.RoleId }).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("tbl_SYS_UserRole");
            ConfigureDualKeyEntity(entity);
            entity.Property(e => e.ScopeOverride).HasColumnType("jsonb").HasConversion(JsonConverter<IamDataScope?>(), JsonComparer<IamDataScope?>());
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
        });

        // 1.2 HRM - Departments, job positions, employees and work history
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("tbl_HRM_Department");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Translations).HasColumnType("jsonb").HasConversion(JsonConverter<Dictionary<string, string>>()).Metadata.SetValueComparer(dictionaryComparer);
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Level).WithMany().HasForeignKey(e => e.LevelId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Path).HasMaxLength(1000).IsRequired();
            // Prefix searches (path LIKE '/1/5/%') use this index without recursion.
            entity.HasIndex(e => e.Path).HasOperators("text_pattern_ops");
            entity.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique().HasFilter("is_deleted = false");
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<DepartmentLevel>(entity =>
        {
            entity.ToTable("tbl_HRM_DepartmentLevel");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.StandardLevelCode).HasMaxLength(50);
            entity.Property(e => e.Translations).HasColumnType("jsonb").HasConversion(JsonConverter<Dictionary<string, string>>()).Metadata.SetValueComparer(dictionaryComparer);
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique().AreNullsDistinct(false).HasFilter("is_deleted = false");
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<JobPosition>(entity =>
        {
            entity.ToTable("tbl_HRM_JobPosition");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Translations).HasColumnType("jsonb").HasConversion(JsonConverter<Dictionary<string, string>>()).Metadata.SetValueComparer(dictionaryComparer);
            entity.Property(e => e.DefaultScopeMode).HasMaxLength(30).IsRequired();
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique().AreNullsDistinct(false).HasFilter("is_deleted = false");
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("tbl_HRM_Employee");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JobPosition).WithMany().HasForeignKey(e => e.JobPositionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.Code).IsUnique().HasFilter("is_deleted = false");
            entity.HasIndex(e => e.UserId).IsUnique().HasFilter("is_deleted = false");
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<WorkHistory>(entity =>
        {
            entity.ToTable("tbl_HRM_WorkHistory");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasOne(e => e.Employee).WithMany(e => e.WorkHistories).HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JobPosition).WithMany().HasForeignKey(e => e.JobPositionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.EmployeeId, e.DateEffective });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<AppLanguage>(entity =>
        {
            entity.ToTable("tbl_SYS_Language");
            entity.HasKey(e => e.Code);
            entity.Ignore(e => e.Id);
            entity.Ignore(e => e.ExternalId);
            entity.Property(e => e.Code).HasMaxLength(10);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FlagIcon).HasMaxLength(50);
        });

        modelBuilder.Entity<AppDictionary>(entity =>
        {
            entity.ToTable("tbl_SYS_Dictionary");
            ConfigureDualKeyEntity(entity);
            entity.Property(e => e.LanguageCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Key).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Value).IsRequired();

            entity.HasOne(e => e.Language)
                .WithMany(l => l.Dictionaries)
                .HasForeignKey(e => e.LanguageCode)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.LanguageCode, e.Key }).IsUnique();
        });

        // 1.1 Enterprise Notification Engine (Step 21)
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("tbl_SYS_NotificationTemplate");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DefaultSeverity).HasMaxLength(20).IsRequired();
            entity.Property(e => e.SupportedChannels).HasColumnType("jsonb").HasConversion(JsonConverter<List<string>>(), JsonComparer<List<string>>());
            entity.Property(e => e.TitleTemplate).HasMaxLength(255).IsRequired();
            entity.Property(e => e.BodyTemplate).IsRequired();
            entity.Property(e => e.Translations).HasColumnType("jsonb").HasConversion(JsonConverter<Dictionary<string, NotificationTemplateTranslation>>(), JsonComparer<Dictionary<string, NotificationTemplateTranslation>>());
            entity.Property(e => e.ActionUrlPattern).HasMaxLength(500);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("tbl_SYS_Notification");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.TemplateCode).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.DataPayload).HasColumnType("jsonb").HasConversion(JsonConverter<Dictionary<string, object>>(), objectDictionaryComparer);
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.TargetType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SenderUser).WithMany().HasForeignKey(e => e.SenderUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<NotificationRecipient>(entity =>
        {
            entity.ToTable("tbl_SYS_NotificationRecipient");
            ConfigureDualKeyAuditable(entity);
            entity.HasOne(e => e.Notification).WithMany(n => n.Recipients).HasForeignKey(e => e.NotificationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.ToTable("tbl_SYS_UserNotificationPreference");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Channel).HasMaxLength(30).IsRequired();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.Category, e.Channel }).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<NotificationOutbox>(entity =>
        {
            entity.ToTable("tbl_SYS_NotificationOutbox");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Channel).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Destination).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("jsonb").HasConversion(JsonConverter<Dictionary<string, object>>(), objectDictionaryComparer);
            entity.HasOne(e => e.Notification).WithMany(n => n.Outboxes).HasForeignKey(e => e.NotificationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.Status, e.NextRetryUtc });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 2. WMS - Catalog / Master Data
        modelBuilder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("tbl_WMS_UnitOfMeasure");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Translations)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(dictionaryComparer);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("tbl_WMS_Category");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Translations)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(dictionaryComparer);

            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("tbl_WMS_Item");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Barcode).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Translations)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(dictionaryComparer);

            entity.Property(e => e.Attributes)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
                .Metadata.SetValueComparer(objectDictionaryComparer);

            entity.Property(e => e.SalePrice).HasPrecision(18, 4);
            entity.Property(e => e.CostPrice).HasPrecision(18, 4);
            entity.Property(e => e.MinStockAlert).HasPrecision(18, 4);
            entity.Property(e => e.MaxStockAlert).HasPrecision(18, 4);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.BaseUom)
                .WithMany(u => u.Items)
                .HasForeignKey(e => e.BaseUomId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Barcode);
            entity.HasIndex(e => e.Attributes).HasMethod("gin");
            entity.HasIndex(e => e.Translations).HasMethod("gin");
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<ItemAttribute>(entity =>
        {
            entity.ToTable("tbl_WMS_ItemAttribute");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Options)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.Code);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 3. CRM - Partner
        modelBuilder.Entity<Partner>(entity =>
        {
            entity.ToTable("tbl_CRM_Partner");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.TaxCode).HasMaxLength(50);

            entity.Property(e => e.CreditLimit).HasPrecision(18, 4);
            entity.Property(e => e.DebtBalance).HasPrecision(18, 4);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 4. WMS - Inventory
        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("tbl_WMS_Warehouse");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Translations)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(dictionaryComparer);

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.Warehouses)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.ToTable("tbl_WMS_StockBalance");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.QuantityOnHand).HasPrecision(18, 4);
            entity.Property(e => e.AverageCostPrice).HasPrecision(18, 4);
            entity.Property(e => e.InventoryValue).HasPrecision(18, 4);

            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.WarehouseId, e.ItemId }).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<StockReceipt>(entity =>
        {
            entity.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("tbl_WMS_StockReceipt");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TotalAmount).HasPrecision(18, 4);
            entity.HasIndex(e => e.RequestId).IsUnique();
            entity.HasOne(e => e.JournalEntry).WithMany().HasForeignKey(e => e.JournalEntryId).OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.StockReceipts)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<StockReceiptItem>(entity =>
        {
            entity.ToTable("tbl_WMS_StockReceiptItem");
            ConfigureDualKeyEntity(entity);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 4);
            entity.Property(e => e.Amount).HasPrecision(18, 4);

            entity.HasOne(e => e.StockReceipt)
                .WithMany(r => r.Items)
                .HasForeignKey(e => e.StockReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockIssue>(entity =>
        {
            entity.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("tbl_WMS_StockIssue");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TotalCostAmount).HasPrecision(18, 4);

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.StockIssues)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<StockIssueItem>(entity =>
        {
            entity.ToTable("tbl_WMS_StockIssueItem");
            ConfigureDualKeyEntity(entity);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitCost).HasPrecision(18, 4);
            entity.Property(e => e.TotalCost).HasPrecision(18, 4);

            entity.HasOne(e => e.StockIssue)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.StockIssueId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. SALE - Invoicing
        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("tbl_SALE_Invoice");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(255).IsRequired();

            entity.Property(e => e.SubTotal).HasPrecision(18, 4);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 4);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 4);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 4);
            entity.Property(e => e.PaidAmount).HasPrecision(18, 4);
            entity.Property(e => e.TenderedAmount).HasPrecision(18, 4);
            entity.Property(e => e.ChangeAmount).HasPrecision(18, 4);
            entity.HasIndex(e => e.RequestId).IsUnique();
            entity.Property(e => e.RemainingDebt).HasPrecision(18, 4);

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.SalesInvoices)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.StockIssue)
                .WithMany()
                .HasForeignKey(e => e.StockIssueId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.InvoiceDate);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<SalesInvoiceItem>(entity =>
        {
            entity.ToTable("tbl_SALE_InvoiceItem");
            ConfigureDualKeyEntity(entity);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 4);
            entity.Property(e => e.DiscountPercent).HasPrecision(18, 4);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.CostPrice).HasPrecision(18, 4);
            entity.Property(e => e.CostAmount).HasPrecision(18, 4);

            entity.HasOne(e => e.SalesInvoice)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5.1 SALE - Orders
        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("tbl_SALE_Order");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

            entity.Property(e => e.SubTotal).HasPrecision(18, 4);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 4);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 4);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 4);
            entity.Property(e => e.PaidAmount).HasPrecision(18, 4);

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<SalesOrderItem>(entity =>
        {
            entity.ToTable("tbl_SALE_OrderItem");
            ConfigureDualKeyEntity(entity);
            entity.Property(e => e.ItemCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ItemName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.UnitName).HasMaxLength(50);

            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 4);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 4);
            entity.Property(e => e.LineTotal).HasPrecision(18, 4);

            entity.HasOne(e => e.SalesOrder)
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5.2 PURCHASE - Orders
        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("tbl_PURCHASE_Order");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SupplierName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

            entity.Property(e => e.SubTotal).HasPrecision(18, 4);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 4);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 4);
            entity.Property(e => e.PaidAmount).HasPrecision(18, 4);

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.ToTable("tbl_PURCHASE_OrderItem");
            ConfigureDualKeyEntity(entity);
            entity.Property(e => e.ItemCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ItemName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.UnitName).HasMaxLength(50);

            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 4);
            entity.Property(e => e.LineTotal).HasPrecision(18, 4);

            entity.HasOne(e => e.PurchaseOrder)
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. ACC - Accounting Foundation
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("tbl_ACC_Account");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Translations)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(dictionaryComparer);

            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("tbl_ACC_JournalEntry");
            ConfigureDualKeyAuditable(entity);
            entity.Property(e => e.VoucherNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.VoucherType).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.JournalEntries)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.VoucherNumber).IsUnique();
            entity.HasIndex(e => e.ReversesJournalEntryId).IsUnique();
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(e => e.ReversesJournalEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.PostingDate);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<JournalEntryLine>(entity =>
        {
            entity.ToTable("tbl_ACC_JournalEntryLine");
            ConfigureDualKeyEntity(entity);

            entity.Property(e => e.DebitAmount).HasPrecision(18, 4);
            entity.Property(e => e.CreditAmount).HasPrecision(18, 4);

            entity.HasOne(e => e.JournalEntry)
                .WithMany(j => j.Lines)
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Partner)
                .WithMany()
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // A user's branch grants are enforced independently of client-side filtering.
        modelBuilder.Entity<Branch>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.Id)));
        modelBuilder.Entity<Warehouse>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.BranchId)));
        modelBuilder.Entity<SalesInvoice>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.BranchId)
            || (e.DepartmentId != null && AllowedDepartmentIds.Contains(e.DepartmentId.Value)) || (OwnRecordsUserId != null && e.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<StockReceipt>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.BranchId)
            || (e.DepartmentId != null && AllowedDepartmentIds.Contains(e.DepartmentId.Value)) || (OwnRecordsUserId != null && e.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<StockIssue>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.BranchId)
            || (e.DepartmentId != null && AllowedDepartmentIds.Contains(e.DepartmentId.Value)) || (OwnRecordsUserId != null && e.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<JournalEntry>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.BranchId)));
        modelBuilder.Entity<SalesOrder>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.BranchId)
            || (e.DepartmentId != null && AllowedDepartmentIds.Contains(e.DepartmentId.Value)) || (OwnRecordsUserId != null && e.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.BranchId)
            || (e.DepartmentId != null && AllowedDepartmentIds.Contains(e.DepartmentId.Value)) || (OwnRecordsUserId != null && e.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<StockBalance>().HasQueryFilter(e => !e.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.Warehouse!.BranchId)));
        modelBuilder.Entity<SalesInvoiceItem>().HasQueryFilter(e => !e.SalesInvoice!.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.SalesInvoice.BranchId)
            || (e.SalesInvoice.DepartmentId != null && AllowedDepartmentIds.Contains(e.SalesInvoice.DepartmentId.Value)) || (OwnRecordsUserId != null && e.SalesInvoice.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<StockReceiptItem>().HasQueryFilter(e => !e.StockReceipt!.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.StockReceipt.BranchId)
            || (e.StockReceipt.DepartmentId != null && AllowedDepartmentIds.Contains(e.StockReceipt.DepartmentId.Value)) || (OwnRecordsUserId != null && e.StockReceipt.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<StockIssueItem>().HasQueryFilter(e => !e.StockIssue!.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.StockIssue.BranchId)
            || (e.StockIssue.DepartmentId != null && AllowedDepartmentIds.Contains(e.StockIssue.DepartmentId.Value)) || (OwnRecordsUserId != null && e.StockIssue.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<JournalEntryLine>().HasQueryFilter(e => !e.JournalEntry!.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.JournalEntry.BranchId)));
        modelBuilder.Entity<SalesOrderItem>().HasQueryFilter(e => !e.SalesOrder!.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.SalesOrder.BranchId)
            || (e.SalesOrder.DepartmentId != null && AllowedDepartmentIds.Contains(e.SalesOrder.DepartmentId.Value)) || (OwnRecordsUserId != null && e.SalesOrder.CreatedBy == OwnRecordsUserId)));
        modelBuilder.Entity<PurchaseOrderItem>().HasQueryFilter(e => !e.PurchaseOrder!.IsDeleted && (IsUnrestricted || AllowedBranchIds.Contains(e.PurchaseOrder.BranchId)
            || (e.PurchaseOrder.DepartmentId != null && AllowedDepartmentIds.Contains(e.PurchaseOrder.DepartmentId.Value)) || (OwnRecordsUserId != null && e.PurchaseOrder.CreatedBy == OwnRecordsUserId)));
    }

    private static readonly JsonSerializerOptions JsonColumnOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Stores a value object as a jsonb document (PascalCase keys, nulls omitted).</summary>
    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> JsonConverter<T>() => new(
        v => JsonSerializer.Serialize(v, JsonColumnOptions),
        v => JsonSerializer.Deserialize<T>(v, JsonColumnOptions)!);

    private static ValueComparer<T> JsonComparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, JsonColumnOptions) == JsonSerializer.Serialize(b, JsonColumnOptions),
        v => JsonSerializer.Serialize(v, JsonColumnOptions).GetHashCode(),
        v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonColumnOptions), JsonColumnOptions)!);

    /// <summary>
    /// Configures dual-key architecture for auditable entities:
    /// - Id: PostgreSQL auto-increment INT IDENTITY primary key
    /// - ExternalId: Unique B-Tree covering index on UUIDv7 with INCLUDE (Id, IsDeleted) for Index-Only Scans
    /// </summary>
    private static void ConfigureDualKeyAuditable<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : BaseAuditableEntity
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.ExternalId).IsRequired();
        entity.HasIndex(e => e.ExternalId)
            .IsUnique()
            .IncludeProperties(e => new { e.Id, e.IsDeleted });
    }

    /// <summary>
    /// Configures dual-key architecture for non-auditable detail/lookup entities:
    /// - Id: PostgreSQL auto-increment INT IDENTITY primary key
    /// - ExternalId: Unique B-Tree covering index on UUIDv7 with INCLUDE (Id) for Index-Only Scans
    /// </summary>
    private static void ConfigureDualKeyEntity<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : BaseEntity
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.ExternalId).IsRequired();
        entity.HasIndex(e => e.ExternalId)
            .IsUnique()
            .IncludeProperties(e => e.Id);
    }

    public override int SaveChanges() => throw new NotSupportedException("Use SaveChangesAsync to run posting and audit guards.");
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("Use SaveChangesAsync to run posting and audit guards.");
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (!AllowCancellation)
        {
            foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
            {
                var immutable = entry.Entity switch
                {
                    SalesInvoiceItem => await SalesInvoices.IgnoreQueryFilters().AnyAsync(i => i.Id == entry.OriginalValues.GetValue<int>("SalesInvoiceId") && i.Status != VoucherStatus.Draft, cancellationToken),
                    StockReceiptItem => await StockReceipts.IgnoreQueryFilters().AnyAsync(i => i.Id == entry.OriginalValues.GetValue<int>("StockReceiptId") && i.Status != VoucherStatus.Draft, cancellationToken),
                    StockIssueItem => await StockIssues.IgnoreQueryFilters().AnyAsync(i => i.Id == entry.OriginalValues.GetValue<int>("StockIssueId") && i.Status != VoucherStatus.Draft, cancellationToken),
                    JournalEntryLine => await JournalEntries.IgnoreQueryFilters().AnyAsync(i => i.Id == entry.OriginalValues.GetValue<int>("JournalEntryId") && i.Status != VoucherStatus.Draft, cancellationToken),
                    _ => false
                };
                if (immutable) throw new BusinessException("error.accounting.posted_immutable", 409);
            }
        }
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is SalesInvoice or StockReceipt or StockIssue or JournalEntry)
            {
                var originalStatus = entry.OriginalValues.GetValue<VoucherStatus>("Status");
                if (originalStatus != VoucherStatus.Draft && !AllowCancellation)
                    throw new BusinessException("error.accounting.posted_immutable", 409);
            }
        }
        foreach (var journal in ChangeTracker.Entries<JournalEntry>().Where(e => e.State == EntityState.Added && e.Entity.Status == VoucherStatus.Posted))
            ERPm.Infrastructure.Services.Common.PostingTransaction.ValidateJournal(journal.Entity);
        var baseEntries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in baseEntries)
        {
            if (entry.State == EntityState.Added && entry.Entity.ExternalId == Guid.Empty)
            {
                entry.Entity.ExternalId = Uuid7Generator.NewUuid7();
            }
        }

        var entries = ChangeTracker.Entries<IAuditable>();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = _currentUser?.UserId ?? entry.Entity.CreatedBy;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.LastModifiedAtUtc = now;
                entry.Entity.LastModifiedBy = _currentUser?.UserId ?? entry.Entity.LastModifiedBy;
            }
        }

        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
