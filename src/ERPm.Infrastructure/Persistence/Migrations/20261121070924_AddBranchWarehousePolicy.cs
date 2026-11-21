using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchWarehousePolicy : Migration
    {
        /// <summary>
        /// Keeps every unit's effective serving warehouses as S27.1 computed them. Units with a list get a fixed list
        /// (rows that no longer serve are dropped when a serving row remains, and a missing default goes to the lowest
        /// warehouse ID). Units without a list below an ancestor with one get "own warehouses" instead of inheriting it.
        /// Only units without a live policy are touched, so it can be re-run.
        /// </summary>
        public const string BackfillSql = """
            WITH pending AS (
                SELECT DISTINCT s.branch_id FROM "tbl_WMS_BranchServingWarehouse" s
                WHERE NOT s.is_deleted AND NOT EXISTS (SELECT 1 FROM "tbl_WMS_BranchWarehousePolicy" p WHERE p.branch_id = s.branch_id AND NOT p.is_deleted)
            ), serving AS (
                SELECT s.id, s.branch_id FROM "tbl_WMS_BranchServingWarehouse" s
                JOIN "tbl_SYS_Branch" b ON b.id = s.branch_id
                JOIN "tbl_WMS_Warehouse" w ON w.id = s.warehouse_id
                JOIN "tbl_SYS_Branch" wb ON wb.id = w.branch_id
                WHERE NOT s.is_deleted AND NOT w.is_deleted AND w.is_active AND NOT wb.is_deleted AND wb.is_active
                  AND wb.company_id IS NOT DISTINCT FROM b.company_id
            )
            UPDATE "tbl_WMS_BranchServingWarehouse" s
            SET is_deleted = true, is_default = false, deleted_at_utc = now(), deleted_by = 'migration'
            WHERE NOT s.is_deleted AND s.branch_id IN (SELECT branch_id FROM pending)
              AND s.id NOT IN (SELECT id FROM serving) AND EXISTS (SELECT 1 FROM serving v WHERE v.branch_id = s.branch_id);

            UPDATE "tbl_WMS_BranchServingWarehouse" SET is_default = true, last_modified_at_utc = now(), last_modified_by = 'migration'
            WHERE id IN (
                SELECT DISTINCT ON (x.branch_id) x.id FROM "tbl_WMS_BranchServingWarehouse" x
                WHERE NOT x.is_deleted
                  AND NOT EXISTS (SELECT 1 FROM "tbl_WMS_BranchWarehousePolicy" p WHERE p.branch_id = x.branch_id AND NOT p.is_deleted)
                  AND NOT EXISTS (SELECT 1 FROM "tbl_WMS_BranchServingWarehouse" y WHERE y.branch_id = x.branch_id AND NOT y.is_deleted AND y.is_default)
                ORDER BY x.branch_id, x.warehouse_id);

            INSERT INTO "tbl_WMS_BranchWarehousePolicy" (branch_id, mode, revision, external_id, created_at_utc, created_by, is_deleted)
            SELECT l.branch_id, 1, 1, gen_random_uuid(), now(), 'migration', false
            FROM (SELECT DISTINCT s.branch_id FROM "tbl_WMS_BranchServingWarehouse" s
                  WHERE NOT s.is_deleted AND NOT EXISTS (SELECT 1 FROM "tbl_WMS_BranchWarehousePolicy" p WHERE p.branch_id = s.branch_id AND NOT p.is_deleted)) l;

            WITH RECURSIVE up AS (
                SELECT b.id AS branch_id, b.parent_id AS ancestor_id, b.company_id FROM "tbl_SYS_Branch" b
                WHERE NOT b.is_deleted AND b.org_unit_type IN (1, 2) AND b.company_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "tbl_WMS_BranchWarehousePolicy" p WHERE p.branch_id = b.id AND NOT p.is_deleted)
                UNION
                SELECT up.branch_id, a.parent_id, up.company_id FROM up JOIN "tbl_SYS_Branch" a ON a.id = up.ancestor_id
                WHERE a.company_id = up.company_id AND a.parent_id IS DISTINCT FROM a.id
            )
            INSERT INTO "tbl_WMS_BranchWarehousePolicy" (branch_id, mode, revision, external_id, created_at_utc, created_by, is_deleted)
            SELECT u.branch_id, 2, 1, gen_random_uuid(), now(), 'migration', false
            FROM (SELECT DISTINCT up.branch_id FROM up
                  JOIN "tbl_SYS_Branch" a ON a.id = up.ancestor_id AND a.company_id = up.company_id AND NOT a.is_deleted
                  JOIN "tbl_WMS_BranchWarehousePolicy" p ON p.branch_id = a.id AND NOT p.is_deleted AND p.mode = 1) u;
            """;

        /// <summary>Registers the module key so the Branch Settings page lists it and hands editing to the WMS editor.</summary>
        public const string DefinitionSql = """
            INSERT INTO "tbl_SYS_SettingDefinition" (setting_key, group_key, value_type, labels_json, constraints_json, storage_kind, editor_key, sort_order, external_id, created_at_utc, created_by, is_deleted)
            SELECT 'wms.so_warehouse_policy', 'warehouse', 'object', '{"vi": "Kho xuất đơn bán", "en": "Sales order shipping warehouses"}'::jsonb, '{}'::jsonb,
                   'module', 'wms.so_warehouse_policy', 10, gen_random_uuid(), now(), 'migration', false
            WHERE NOT EXISTS (SELECT 1 FROM "tbl_SYS_SettingDefinition" WHERE setting_key = 'wms.so_warehouse_policy' AND NOT is_deleted);
            """;

        /// <summary>
        /// Saving serving warehouses now also needs sys:branch-setting:edit: every Allow statement that grants
        /// sys:branch:edit (roles and user inline policies) gets it too, with the same data scope.
        /// </summary>
        public const string GrantSql = """
            UPDATE "tbl_SYS_Role" r
            SET policy_document = jsonb_set(r.policy_document, '{Statements}', (
                SELECT jsonb_agg(CASE
                    WHEN lower(COALESCE(stmt->>'Effect', '')) = 'allow' AND jsonb_typeof(stmt->'Actions') = 'array'
                         AND stmt->'Actions' @> '["sys:branch:edit"]'::jsonb AND NOT stmt->'Actions' @> '["sys:branch-setting:edit"]'::jsonb
                    THEN jsonb_set(stmt, '{Actions}', (stmt->'Actions') || '["sys:branch-setting:edit"]'::jsonb)
                    ELSE stmt END ORDER BY ord)
                FROM jsonb_array_elements(r.policy_document->'Statements') WITH ORDINALITY AS s(stmt, ord)))
            WHERE jsonb_typeof(r.policy_document->'Statements') = 'array'
              AND EXISTS (SELECT 1 FROM jsonb_array_elements(r.policy_document->'Statements') AS s(stmt)
                          WHERE lower(COALESCE(stmt->>'Effect', '')) = 'allow' AND jsonb_typeof(stmt->'Actions') = 'array'
                            AND stmt->'Actions' @> '["sys:branch:edit"]'::jsonb AND NOT stmt->'Actions' @> '["sys:branch-setting:edit"]'::jsonb);

            UPDATE "tbl_SYS_User" r
            SET inline_policy = jsonb_set(r.inline_policy, '{Statements}', (
                SELECT jsonb_agg(CASE
                    WHEN lower(COALESCE(stmt->>'Effect', '')) = 'allow' AND jsonb_typeof(stmt->'Actions') = 'array'
                         AND stmt->'Actions' @> '["sys:branch:edit"]'::jsonb AND NOT stmt->'Actions' @> '["sys:branch-setting:edit"]'::jsonb
                    THEN jsonb_set(stmt, '{Actions}', (stmt->'Actions') || '["sys:branch-setting:edit"]'::jsonb)
                    ELSE stmt END ORDER BY ord)
                FROM jsonb_array_elements(r.inline_policy->'Statements') WITH ORDINALITY AS s(stmt, ord)))
            WHERE jsonb_typeof(r.inline_policy->'Statements') = 'array'
              AND EXISTS (SELECT 1 FROM jsonb_array_elements(r.inline_policy->'Statements') AS s(stmt)
                          WHERE lower(COALESCE(stmt->>'Effect', '')) = 'allow' AND jsonb_typeof(stmt->'Actions') = 'array'
                            AND stmt->'Actions' @> '["sys:branch:edit"]'::jsonb AND NOT stmt->'Actions' @> '["sys:branch-setting:edit"]'::jsonb);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_WMS_BranchWarehousePolicy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_tbl_wms_branch_warehouse_policy", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_wms_branch_warehouse_policy_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_wms_branch_warehouse_policy_branch_id",
                table: "tbl_WMS_BranchWarehousePolicy",
                column: "branch_id",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_wms_branch_warehouse_policy_external_id",
                table: "tbl_WMS_BranchWarehousePolicy",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.Sql(BackfillSql);
            migrationBuilder.Sql(DefinitionSql);
            migrationBuilder.Sql(GrantSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Roles keep sys:branch-setting:edit (harmless without the key); S27.1 rows are kept, so S27.1 code reads the same lists.
            migrationBuilder.Sql("DELETE FROM \"tbl_SYS_SettingDefinition\" WHERE setting_key = 'wms.so_warehouse_policy';");
            migrationBuilder.DropTable(
                name: "tbl_WMS_BranchWarehousePolicy");
        }
    }
}
