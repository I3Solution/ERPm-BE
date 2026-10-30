using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIamOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "tbl_WMS_StockReceipt",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "tbl_WMS_StockIssue",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inline_policy",
                table: "tbl_SYS_User",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_super_admin",
                table: "tbl_SYS_User",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "company_id",
                table: "tbl_SYS_Branch",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "org_unit_type",
                table: "tbl_SYS_Branch",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "tbl_SYS_Branch",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "tbl_SALE_Order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "tbl_SALE_Invoice",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "tbl_PURCHASE_Order",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_HRM_Department",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_tbl_hrm_department", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_department_tbl_hrm_department_parent_id",
                        column: x => x.parent_id,
                        principalTable: "tbl_HRM_Department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_department_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_department_tbl_sys_branch_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SYS_Role",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false),
                    policy_document = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_tbl_sys_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sys_role_tbl_sys_branch_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_HRM_JobPosition",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    rank_order = table.Column<int>(type: "integer", nullable: false),
                    default_scope_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("pk_tbl_hrm_job_position", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_job_position_tbl_hrm_department_department_id",
                        column: x => x.department_id,
                        principalTable: "tbl_HRM_Department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_job_position_tbl_sys_branch_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SYS_UserRole",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    scope_override = table.Column<string>(type: "jsonb", nullable: true),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sys_user_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sys_user_role_tbl_sys_role_role_id",
                        column: x => x.role_id,
                        principalTable: "tbl_SYS_Role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_sys_user_role_tbl_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "tbl_SYS_User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_HRM_Employee",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    job_position_id = table.Column<int>(type: "integer", nullable: false),
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
                        name: "fk_tbl_hrm_employee_tbl_hrm_department_department_id",
                        column: x => x.department_id,
                        principalTable: "tbl_HRM_Department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_employee_tbl_hrm_job_position_job_position_id",
                        column: x => x.job_position_id,
                        principalTable: "tbl_HRM_JobPosition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_employee_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_employee_tbl_sys_branch_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_employee_tbl_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "tbl_SYS_User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SYS_JobPositionRole",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_position_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sys_job_position_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sys_job_position_role_job_positions_job_position_id",
                        column: x => x.job_position_id,
                        principalTable: "tbl_HRM_JobPosition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_sys_job_position_role_tbl_sys_role_role_id",
                        column: x => x.role_id,
                        principalTable: "tbl_SYS_Role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_HRM_WorkHistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    date_effective = table.Column<DateOnly>(type: "date", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    job_position_id = table.Column<int>(type: "integer", nullable: false),
                    is_termination = table.Column<bool>(type: "boolean", nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                        name: "fk_tbl_hrm_work_history_tbl_hrm_department_department_id",
                        column: x => x.department_id,
                        principalTable: "tbl_HRM_Department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_work_history_tbl_hrm_employee_employee_id",
                        column: x => x.employee_id,
                        principalTable: "tbl_HRM_Employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_work_history_tbl_hrm_job_position_job_position_id",
                        column: x => x.job_position_id,
                        principalTable: "tbl_HRM_JobPosition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_work_history_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_hrm_work_history_tbl_sys_branch_company_id",
                        column: x => x.company_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_wms_stock_receipt_department_id",
                table: "tbl_WMS_StockReceipt",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_wms_stock_issue_department_id",
                table: "tbl_WMS_StockIssue",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_branch_company_id",
                table: "tbl_SYS_Branch",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_order_department_id",
                table: "tbl_SALE_Order",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_invoice_department_id",
                table: "tbl_SALE_Invoice",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_purchase_order_department_id",
                table: "tbl_PURCHASE_Order",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_branch_id",
                table: "tbl_HRM_Department",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_company_id_code",
                table: "tbl_HRM_Department",
                columns: new[] { "company_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_external_id",
                table: "tbl_HRM_Department",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_department_parent_id",
                table: "tbl_HRM_Department",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_branch_id",
                table: "tbl_HRM_Employee",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_code",
                table: "tbl_HRM_Employee",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_company_id",
                table: "tbl_HRM_Employee",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_department_id",
                table: "tbl_HRM_Employee",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_external_id",
                table: "tbl_HRM_Employee",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_job_position_id",
                table: "tbl_HRM_Employee",
                column: "job_position_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_employee_user_id",
                table: "tbl_HRM_Employee",
                column: "user_id",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_job_position_company_id_code",
                table: "tbl_HRM_JobPosition",
                columns: new[] { "company_id", "code" },
                unique: true,
                filter: "is_deleted = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_job_position_department_id",
                table: "tbl_HRM_JobPosition",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_job_position_external_id",
                table: "tbl_HRM_JobPosition",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_branch_id",
                table: "tbl_HRM_WorkHistory",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_company_id",
                table: "tbl_HRM_WorkHistory",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_department_id",
                table: "tbl_HRM_WorkHistory",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_employee_id_date_effective",
                table: "tbl_HRM_WorkHistory",
                columns: new[] { "employee_id", "date_effective" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_external_id",
                table: "tbl_HRM_WorkHistory",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_hrm_work_history_job_position_id",
                table: "tbl_HRM_WorkHistory",
                column: "job_position_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_job_position_role_external_id",
                table: "tbl_SYS_JobPositionRole",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_job_position_role_job_position_id_role_id",
                table: "tbl_SYS_JobPositionRole",
                columns: new[] { "job_position_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_job_position_role_role_id",
                table: "tbl_SYS_JobPositionRole",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_role_company_id_code",
                table: "tbl_SYS_Role",
                columns: new[] { "company_id", "code" },
                unique: true,
                filter: "is_deleted = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_role_external_id",
                table: "tbl_SYS_Role",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_user_role_external_id",
                table: "tbl_SYS_UserRole",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_user_role_role_id",
                table: "tbl_SYS_UserRole",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_user_role_user_id_role_id",
                table: "tbl_SYS_UserRole",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_purchase_order_tbl_hrm_department_department_id",
                table: "tbl_PURCHASE_Order",
                column: "department_id",
                principalTable: "tbl_HRM_Department",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_invoice_tbl_hrm_department_department_id",
                table: "tbl_SALE_Invoice",
                column: "department_id",
                principalTable: "tbl_HRM_Department",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_order_tbl_hrm_department_department_id",
                table: "tbl_SALE_Order",
                column: "department_id",
                principalTable: "tbl_HRM_Department",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sys_branch_tbl_sys_branch_company_id",
                table: "tbl_SYS_Branch",
                column: "company_id",
                principalTable: "tbl_SYS_Branch",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_wms_stock_issue_tbl_hrm_department_department_id",
                table: "tbl_WMS_StockIssue",
                column: "department_id",
                principalTable: "tbl_HRM_Department",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_wms_stock_receipt_tbl_hrm_department_department_id",
                table: "tbl_WMS_StockReceipt",
                column: "department_id",
                principalTable: "tbl_HRM_Department",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_purchase_order_tbl_hrm_department_department_id",
                table: "tbl_PURCHASE_Order");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_invoice_tbl_hrm_department_department_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_order_tbl_hrm_department_department_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sys_branch_tbl_sys_branch_company_id",
                table: "tbl_SYS_Branch");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_wms_stock_issue_tbl_hrm_department_department_id",
                table: "tbl_WMS_StockIssue");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_wms_stock_receipt_tbl_hrm_department_department_id",
                table: "tbl_WMS_StockReceipt");

            migrationBuilder.DropTable(
                name: "tbl_HRM_WorkHistory");

            migrationBuilder.DropTable(
                name: "tbl_SYS_JobPositionRole");

            migrationBuilder.DropTable(
                name: "tbl_SYS_UserRole");

            migrationBuilder.DropTable(
                name: "tbl_HRM_Employee");

            migrationBuilder.DropTable(
                name: "tbl_SYS_Role");

            migrationBuilder.DropTable(
                name: "tbl_HRM_JobPosition");

            migrationBuilder.DropTable(
                name: "tbl_HRM_Department");

            migrationBuilder.DropIndex(
                name: "ix_tbl_wms_stock_receipt_department_id",
                table: "tbl_WMS_StockReceipt");

            migrationBuilder.DropIndex(
                name: "ix_tbl_wms_stock_issue_department_id",
                table: "tbl_WMS_StockIssue");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sys_branch_company_id",
                table: "tbl_SYS_Branch");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_order_department_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_invoice_department_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropIndex(
                name: "ix_tbl_purchase_order_department_id",
                table: "tbl_PURCHASE_Order");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "tbl_WMS_StockReceipt");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "tbl_WMS_StockIssue");

            migrationBuilder.DropColumn(
                name: "inline_policy",
                table: "tbl_SYS_User");

            migrationBuilder.DropColumn(
                name: "is_super_admin",
                table: "tbl_SYS_User");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "tbl_SYS_Branch");

            migrationBuilder.DropColumn(
                name: "org_unit_type",
                table: "tbl_SYS_Branch");

            migrationBuilder.DropColumn(
                name: "tax_code",
                table: "tbl_SYS_Branch");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "tbl_PURCHASE_Order");
        }
    }
}
