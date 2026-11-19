using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceListVersionWorkflowAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_SD_PriceListVersion_Status",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.AddColumn<DateTime>(
                name: "activated_at_utc",
                table: "tbl_SD_PriceListVersion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "activated_by",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at_utc",
                table: "tbl_SD_PriceListVersion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at_utc",
                table: "tbl_SD_PriceListVersion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "archived_by",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "close_reason",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "closed_at_utc",
                table: "tbl_SD_PriceListVersion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_by",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reject_reason",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at_utc",
                table: "tbl_SD_PriceListVersion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejected_by",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at_utc",
                table: "tbl_SD_PriceListVersion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submitted_by",
                table: "tbl_SD_PriceListVersion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_SD_PriceListVersionAudit",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    price_list_version_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    actor_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sd_price_list_version_audit", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sd_price_list_version_audit_version_id",
                        column: x => x.price_list_version_id,
                        principalTable: "tbl_SD_PriceListVersion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_SD_PriceListVersion_Status",
                table: "tbl_SD_PriceListVersion",
                sql: "status BETWEEN 1 AND 6");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_version_audit_external_id",
                table: "tbl_SD_PriceListVersionAudit",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_version_audit_version_occurred",
                table: "tbl_SD_PriceListVersionAudit",
                columns: new[] { "price_list_version_id", "occurred_at_utc" });

            // Upgrade existing custom role & user inline Allow statements that explicitly grant sd:pricelist:edit
            // by appending sd:pricelist:submit, sd:pricelist:approve, sd:pricelist:activate and sd:pricelist:close.
            migrationBuilder.Sql(@"
UPDATE ""tbl_SYS_Role"" r
SET policy_document = jsonb_set(
    r.policy_document,
    '{Statements}',
    (
        SELECT COALESCE(jsonb_agg(
            CASE
                WHEN lower(COALESCE(stmt->>'Effect', '')) = 'allow'
                     AND jsonb_typeof(stmt->'Actions') = 'array'
                     AND EXISTS (
                         SELECT 1 FROM jsonb_array_elements_text(stmt->'Actions') AS a(val)
                         WHERE lower(a.val) = 'sd:pricelist:edit'
                     )
                THEN jsonb_set(
                    stmt,
                    '{Actions}',
                    (
                        SELECT jsonb_agg(val ORDER BY ord)
                        FROM (
                            SELECT val, MIN(ord) AS ord
                            FROM (
                                SELECT val, ord FROM jsonb_array_elements_text(stmt->'Actions') WITH ORDINALITY AS existing(val, ord)
                                UNION ALL
                                SELECT extra.val, 1000000 + extra.ord
                                FROM unnest(ARRAY['sd:pricelist:submit', 'sd:pricelist:approve', 'sd:pricelist:activate', 'sd:pricelist:close']) WITH ORDINALITY AS extra(val, ord)
                            ) merged
                            GROUP BY val
                        ) dedup
                    )
                )
                ELSE stmt
            END
            ORDER BY stmt_ord
        ), '[]'::jsonb)
        FROM jsonb_array_elements(r.policy_document->'Statements') WITH ORDINALITY AS s(stmt, stmt_ord)
    )
)
WHERE r.policy_document IS NOT NULL
  AND jsonb_typeof(r.policy_document->'Statements') = 'array'
  AND EXISTS (
      SELECT 1
      FROM jsonb_array_elements(r.policy_document->'Statements') AS s(stmt)
      WHERE lower(COALESCE(stmt->>'Effect', '')) = 'allow'
        AND jsonb_typeof(stmt->'Actions') = 'array'
        AND EXISTS (
            SELECT 1 FROM jsonb_array_elements_text(stmt->'Actions') AS a(val)
            WHERE lower(a.val) = 'sd:pricelist:edit'
        )
  );

UPDATE ""tbl_SYS_User"" u
SET inline_policy = jsonb_set(
    u.inline_policy,
    '{Statements}',
    (
        SELECT COALESCE(jsonb_agg(
            CASE
                WHEN lower(COALESCE(stmt->>'Effect', '')) = 'allow'
                     AND jsonb_typeof(stmt->'Actions') = 'array'
                     AND EXISTS (
                         SELECT 1 FROM jsonb_array_elements_text(stmt->'Actions') AS a(val)
                         WHERE lower(a.val) = 'sd:pricelist:edit'
                     )
                THEN jsonb_set(
                    stmt,
                    '{Actions}',
                    (
                        SELECT jsonb_agg(val ORDER BY ord)
                        FROM (
                            SELECT val, MIN(ord) AS ord
                            FROM (
                                SELECT val, ord FROM jsonb_array_elements_text(stmt->'Actions') WITH ORDINALITY AS existing(val, ord)
                                UNION ALL
                                SELECT extra.val, 1000000 + extra.ord
                                FROM unnest(ARRAY['sd:pricelist:submit', 'sd:pricelist:approve', 'sd:pricelist:activate', 'sd:pricelist:close']) WITH ORDINALITY AS extra(val, ord)
                            ) merged
                            GROUP BY val
                        ) dedup
                    )
                )
                ELSE stmt
            END
            ORDER BY stmt_ord
        ), '[]'::jsonb)
        FROM jsonb_array_elements(u.inline_policy->'Statements') WITH ORDINALITY AS s(stmt, stmt_ord)
    )
)
WHERE u.inline_policy IS NOT NULL
  AND jsonb_typeof(u.inline_policy->'Statements') = 'array'
  AND EXISTS (
      SELECT 1
      FROM jsonb_array_elements(u.inline_policy->'Statements') AS s(stmt)
      WHERE lower(COALESCE(stmt->>'Effect', '')) = 'allow'
        AND jsonb_typeof(stmt->'Actions') = 'array'
        AND EXISTS (
            SELECT 1 FROM jsonb_array_elements_text(stmt->'Actions') AS a(val)
            WHERE lower(a.val) = 'sd:pricelist:edit'
        )
  );

SELECT nextval('erpm_permission_version');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""tbl_SD_PriceListVersion"" SET status = 1 WHERE status IN (5, 6);");

            migrationBuilder.DropTable(
                name: "tbl_SD_PriceListVersionAudit");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_SD_PriceListVersion_Status",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "activated_at_utc",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "activated_by",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "approved_at_utc",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "archived_by",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "close_reason",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "closed_at_utc",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "closed_by",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "reject_reason",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "rejected_at_utc",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "rejected_by",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "submitted_at_utc",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.DropColumn(
                name: "submitted_by",
                table: "tbl_SD_PriceListVersion");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_SD_PriceListVersion_Status",
                table: "tbl_SD_PriceListVersion",
                sql: "status BETWEEN 1 AND 4");
        }
    }
}
