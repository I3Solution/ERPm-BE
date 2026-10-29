using Microsoft.EntityFrameworkCore.Migrations;
namespace ERPm.Infrastructure.Persistence.Migrations;
public partial class StabilizeMvp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = typeof(StabilizeMvp).Assembly.GetManifestResourceStream("ERPm.Infrastructure.Persistence.Migrations.Sql.StabilizeMvp.sql")!;
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Forward-only migration. Restore a verified backup to roll back.");
}
