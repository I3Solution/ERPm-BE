using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERPm.API;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=erpm_design;Username=erpm")
        .UseSnakeCaseNamingConvention().Options);
}
