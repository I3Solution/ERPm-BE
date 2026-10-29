using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHrmFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "company_id",
                table: "tbl_SYS_Branch",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_HRM_Employee",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    identity_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_tbl_hrm_employee", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_employee_tbl_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "tbl_SYS_User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_HRM_Shift",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    break_minutes = table.Column<int>(type: "integer", nullable: false),
                    late_grace_minutes = table.Column<int>(type: "integer", nullable: false),
                    work_days = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_tbl_hrm_shift", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SYS_Company",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tax_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    social_insurance_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("pk_tbl_sys_company", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SYS_OrgStructure",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    branch_id = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_tbl_sys_org_structure", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sys_org_structure_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sys_org_structure_tbl_sys_company_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sys_org_structure_tbl_sys_org_structure_parent_id",
                        column: x => x.parent_id,
                        principalTable: "tbl_SYS_OrgStructure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_HRM_DataPermission",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    org_structure_id = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_tbl_hrm_data_permission", x => x.id);
                    table.CheckConstraint("ck_tbl_hrm_data_permission_dimension", "org_structure_id IS NOT NULL OR company_id IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_tbl_hrm_data_permission_tbl_sys_company_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_data_permission_tbl_sys_org_structure_org_structure_id",
                        column: x => x.org_structure_id,
                        principalTable: "tbl_SYS_OrgStructure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_data_permission_tbl_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "tbl_SYS_User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_HRM_WorkHistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    date_effective = table.Column<DateOnly>(type: "date", nullable: false),
                    org_structure_id = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    job_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_termination = table.Column<bool>(type: "boolean", nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_tbl_hrm_work_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_work_history_tbl_hrm_employee_employee_id",
                        column: x => x.employee_id,
                        principalTable: "tbl_HRM_Employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_work_history_tbl_sys_company_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_work_history_tbl_sys_org_structure_org_structure_id",
                        column: x => x.org_structure_id,
                        principalTable: "tbl_SYS_OrgStructure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_branch_company_id",
                table: "tbl_SYS_Branch",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_data_permission_company_id",
                table: "tbl_HRM_DataPermission",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_data_permission_external_id",
                table: "tbl_HRM_DataPermission",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_data_permission_org_structure_id",
                table: "tbl_HRM_DataPermission",
                column: "org_structure_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_data_permission_user_id",
                table: "tbl_HRM_DataPermission",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_code",
                table: "tbl_HRM_Employee",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_external_id",
                table: "tbl_HRM_Employee",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_user_id",
                table: "tbl_HRM_Employee",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_shift_code",
                table: "tbl_HRM_Shift",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_shift_external_id",
                table: "tbl_HRM_Shift",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_company_id",
                table: "tbl_HRM_WorkHistory",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_employee_id_date_effective",
                table: "tbl_HRM_WorkHistory",
                columns: new[] { "employee_id", "date_effective" })
                .Annotation("Npgsql:IndexInclude", new[] { "org_structure_id", "company_id", "is_termination", "is_approved" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_external_id",
                table: "tbl_HRM_WorkHistory",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_org_structure_id",
                table: "tbl_HRM_WorkHistory",
                column: "org_structure_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_company_code",
                table: "tbl_SYS_Company",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_company_external_id",
                table: "tbl_SYS_Company",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_org_structure_branch_id",
                table: "tbl_SYS_OrgStructure",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_org_structure_code",
                table: "tbl_SYS_OrgStructure",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_org_structure_company_id",
                table: "tbl_SYS_OrgStructure",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_org_structure_external_id",
                table: "tbl_SYS_OrgStructure",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_org_structure_parent_id",
                table: "tbl_SYS_OrgStructure",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sys_branch_companies_company_id",
                table: "tbl_SYS_Branch",
                column: "company_id",
                principalTable: "tbl_SYS_Company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sys_branch_companies_company_id",
                table: "tbl_SYS_Branch");

            migrationBuilder.DropTable(
                name: "tbl_HRM_DataPermission");

            migrationBuilder.DropTable(
                name: "tbl_HRM_Shift");

            migrationBuilder.DropTable(
                name: "tbl_HRM_WorkHistory");

            migrationBuilder.DropTable(
                name: "tbl_HRM_Employee");

            migrationBuilder.DropTable(
                name: "tbl_SYS_OrgStructure");

            migrationBuilder.DropTable(
                name: "tbl_SYS_Company");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sys_branch_company_id",
                table: "tbl_SYS_Branch");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "tbl_SYS_Branch");
        }
    }
}
