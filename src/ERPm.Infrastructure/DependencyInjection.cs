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

        // Register Generic Base CRUD Service for open generic types (Type-safe & Zero boilerplate for Solo Dev)
        services.AddScoped(typeof(IBaseCrudService<,,,,>), typeof(BaseCrudService<,,,,>));
        services.AddScoped(typeof(IBaseCrudService<,,,>), typeof(BaseCrudService<,,,>));

        // Register All Domain & Feature Services
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
        services.AddScoped<IPartnerService, PartnerService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
