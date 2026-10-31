using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_SYS_DepartmentRole",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    department_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    apply_to_children = table.Column<bool>(type: "boolean", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sys_department_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sys_department_role_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "tbl_HRM_Department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sys_department_role_tbl_sys_role_role_id",
                        column: x => x.role_id,
                        principalTable: "tbl_SYS_Role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_department_role_department_id_role_id",
                table: "tbl_SYS_DepartmentRole",
                columns: new[] { "department_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_department_role_external_id",
                table: "tbl_SYS_DepartmentRole",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_department_role_role_id",
                table: "tbl_SYS_DepartmentRole",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_SYS_DepartmentRole");
        }
    }
}
