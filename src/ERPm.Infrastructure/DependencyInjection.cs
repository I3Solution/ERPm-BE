using ERPm.Application.Common.Crud;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Features.Catalog;
using ERPm.Application.Features.Dashboard;
using ERPm.Application.Features.Inventory;
using ERPm.Application.Features.Localization;
using ERPm.Application.Features.Partner;
using ERPm.Application.Features.Sales;
using ERPm.Application.Features.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services;
using ERPm.Infrastructure.Services.Common;
using ERPm.Infrastructure.Services.HumanResource;
using ERPm.Infrastructure.Services.Iam;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERPm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(3);
            });
            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<PostingTransaction>();
        services.AddScoped<StockReceiptPostingService>();

        // Register Generic Base CRUD Service for open generic types (Type-safe & Zero boilerplate for Solo Dev)
        services.AddScoped(typeof(IBaseCrudService<,,,,>), typeof(BaseCrudService<,,,,>));
        services.AddScoped(typeof(IBaseCrudService<,,,>), typeof(BaseCrudService<,,,>));
        services.AddScoped<SalesOrderService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<IBaseCrudService<ERPm.Domain.Entities.Sales.SalesOrder, ERPm.Application.DTOs.Sales.SalesOrderDto, ERPm.Application.DTOs.Sales.CreateSalesOrderRequest, ERPm.Application.DTOs.Sales.UpdateSalesOrderRequest>>(p => p.GetRequiredService<SalesOrderService>());
        services.AddScoped<IBaseCrudService<ERPm.Domain.Entities.Purchase.PurchaseOrder, ERPm.Application.DTOs.Purchase.PurchaseOrderDto, ERPm.Application.DTOs.Purchase.CreatePurchaseOrderRequest, ERPm.Application.DTOs.Purchase.UpdatePurchaseOrderRequest>>(p => p.GetRequiredService<PurchaseOrderService>());
        services.AddScoped<IOrderWorkflow<ERPm.Application.DTOs.Sales.SalesOrderDto>>(p => p.GetRequiredService<SalesOrderService>());
        services.AddScoped<IOrderWorkflow<ERPm.Application.DTOs.Purchase.PurchaseOrderDto>>(p => p.GetRequiredService<PurchaseOrderService>());

        // Register All Domain & Feature Services
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
        services.AddScoped<IPartnerService, PartnerService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // IAM (dynamic roles, policy evaluation) and HRM organization
        services.AddMemoryCache();
        services.AddScoped<IIamService, IamService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IBaseCrudService<Department, DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest>, DepartmentService>();
        services.AddScoped<IBaseCrudService<JobPosition, JobPositionDto, CreateJobPositionRequest, UpdateJobPositionRequest>, JobPositionService>();
        services.AddScoped<IBaseCrudService<Employee, EmployeeDto, CreateEmployeeRequest, UpdateEmployeeRequest>, EmployeeService>();
        services.AddScoped<IBaseCrudService<WorkHistory, WorkHistoryDto, CreateWorkHistoryRequest, UpdateWorkHistoryRequest>, WorkHistoryService>();

        return services;
    }
}
