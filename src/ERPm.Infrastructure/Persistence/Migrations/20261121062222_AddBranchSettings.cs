using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_SYS_BranchSettingHistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    setting_key = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sys_branch_setting_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sys_branch_setting_history_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SYS_SettingDefinition",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    setting_key = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    group_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    labels_json = table.Column<string>(type: "jsonb", nullable: false),
                    constraints_json = table.Column<string>(type: "jsonb", nullable: false),
                    default_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    storage_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    editor_key = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_tbl_sys_setting_definition", x => x.id);
                    table.UniqueConstraint("ak_tbl_sys_setting_definition_setting_key", x => x.setting_key);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SYS_BranchSetting",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    setting_key = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_tbl_sys_branch_setting", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sys_branch_setting_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sys_branch_setting_tbl_sys_setting_definition_setting_key",
                        column: x => x.setting_key,
                        principalTable: "tbl_SYS_SettingDefinition",
                        principalColumn: "setting_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_branch_setting_branch_id_setting_key",
                table: "tbl_SYS_BranchSetting",
                columns: new[] { "branch_id", "setting_key" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_branch_setting_external_id",
                table: "tbl_SYS_BranchSetting",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_branch_setting_setting_key",
                table: "tbl_SYS_BranchSetting",
                column: "setting_key");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_branch_setting_history_branch_id_setting_key_id",
                table: "tbl_SYS_BranchSettingHistory",
                columns: new[] { "branch_id", "setting_key", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_branch_setting_history_external_id",
                table: "tbl_SYS_BranchSettingHistory",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sys_setting_definition_external_id",
                table: "tbl_SYS_SettingDefinition",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_SYS_BranchSetting");

            migrationBuilder.DropTable(
                name: "tbl_SYS_BranchSettingHistory");

            migrationBuilder.DropTable(
                name: "tbl_SYS_SettingDefinition");
        }
    }
}
