using System.IO;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ERPm.API;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // 1. Check if connection string passed via command-line args or environment variable
        var basePath = Directory.GetCurrentDirectory();
        
        // Handle when executed from root or from ERPm-BE
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
        {
            var candidate = Path.Combine(basePath, "src", "ERPm.API");
            if (Directory.Exists(candidate))
            {
                basePath = candidate;
            }
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=erpm_design;Username=erpm";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
