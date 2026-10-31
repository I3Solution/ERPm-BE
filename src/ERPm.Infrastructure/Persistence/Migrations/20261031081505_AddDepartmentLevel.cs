using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "depth",
                table: "tbl_HRM_Department",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_from",
                table: "tbl_HRM_Department",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_to",
                table: "tbl_HRM_Department",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "level_id",
                table: "tbl_HRM_Department",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "path",
                table: "tbl_HRM_Department",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "tbl_HRM_DepartmentLevel",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    translations = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    standard_level_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_hrm_department_level", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_department_level_tbl_sys_branch_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_level_id",
                table: "tbl_HRM_Department",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_path",
                table: "tbl_HRM_Department",
                column: "path")
                .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_level_company_id_code",
                table: "tbl_HRM_DepartmentLevel",
                columns: new[] { "company_id", "code" },
                unique: true,
                filter: "is_deleted = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_level_external_id",
                table: "tbl_HRM_DepartmentLevel",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            // Seed the group default levels, attach existing departments to DEPARTMENT and back-fill Path/Depth.
            migrationBuilder.Sql("""
                INSERT INTO "tbl_HRM_DepartmentLevel" (code, name, translations, sort_order, company_id, is_active, external_id, created_at_utc, is_deleted)
                VALUES ('DIVISION', 'Division', '{"vi":"Khối","en":"Division"}'::jsonb, 10, NULL, true, gen_random_uuid(), NOW(), false),
                       ('DEPARTMENT', 'Department', '{"vi":"Phòng","en":"Department"}'::jsonb, 20, NULL, true, gen_random_uuid(), NOW(), false),
                       ('SECTION', 'Section', '{"vi":"Bộ phận","en":"Section"}'::jsonb, 30, NULL, true, gen_random_uuid(), NOW(), false),
                       ('TEAM', 'Team', '{"vi":"Tổ","en":"Team"}'::jsonb, 40, NULL, true, gen_random_uuid(), NOW(), false);
                UPDATE "tbl_HRM_Department" SET level_id = (SELECT id FROM "tbl_HRM_DepartmentLevel" WHERE code = 'DEPARTMENT' AND company_id IS NULL);
                WITH RECURSIVE tree AS (
                    SELECT id, '/' || id || '/' AS path, 1 AS depth FROM "tbl_HRM_Department" WHERE parent_id IS NULL
                    UNION ALL
                    SELECT d.id, t.path || d.id || '/', t.depth + 1 FROM "tbl_HRM_Department" d JOIN tree t ON d.parent_id = t.id
                )
                UPDATE "tbl_HRM_Department" d SET path = tree.path, depth = tree.depth FROM tree WHERE d.id = tree.id;
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_hrm_department_department_levels_level_id",
                table: "tbl_HRM_Department",
                column: "level_id",
                principalTable: "tbl_HRM_DepartmentLevel",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_hrm_department_department_levels_level_id",
                table: "tbl_HRM_Department");

            migrationBuilder.DropTable(
                name: "tbl_HRM_DepartmentLevel");

            migrationBuilder.DropIndex(
                name: "ix_tbl_hrm_department_level_id",
                table: "tbl_HRM_Department");

            migrationBuilder.DropIndex(
                name: "ix_tbl_hrm_department_path",
                table: "tbl_HRM_Department");

            migrationBuilder.DropColumn(
                name: "depth",
                table: "tbl_HRM_Department");

            migrationBuilder.DropColumn(
                name: "effective_from",
                table: "tbl_HRM_Department");

            migrationBuilder.DropColumn(
                name: "effective_to",
                table: "tbl_HRM_Department");

            migrationBuilder.DropColumn(
                name: "level_id",
                table: "tbl_HRM_Department");

            migrationBuilder.DropColumn(
                name: "path",
                table: "tbl_HRM_Department");
        }
    }
}
