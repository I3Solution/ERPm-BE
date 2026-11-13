using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// S25.1 - price list Type / IsTaxInclusive and PriceListVersions.
    /// Existing data is preserved: every price list gets one version "V1" that takes over its date range and all of its lines.
    /// </summary>
    public partial class AddPriceListTypeTaxAndVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Header columns. Every existing list stays a tax-exclusive Sales list (Type 1 = Sales, 2 = Purchase).
            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "tbl_SD_PriceList",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_tax_inclusive",
                table: "tbl_SD_PriceList",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_SD_PriceList_Type",
                table: "tbl_SD_PriceList",
                sql: "type IN (1, 2)");

            // 2. Version table.
            migrationBuilder.CreateTable(
                name: "tbl_SD_PriceListVersion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    price_list_id = table.Column<int>(type: "integer", nullable: false),
                    version_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_tbl_sd_price_list_version", x => x.id);
                    table.CheckConstraint("CK_tbl_SD_PriceListVersion_Range", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("CK_tbl_SD_PriceListVersion_Status", "status BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_tbl_sd_price_list_version_tbl_sd_price_list_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "tbl_SD_PriceList",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_version_external_id",
                table: "tbl_SD_PriceListVersion",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_version_price_list_id_status",
                table: "tbl_SD_PriceListVersion",
                columns: new[] { "price_list_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_version_price_list_id_version_code",
                table: "tbl_SD_PriceListVersion",
                columns: new[] { "price_list_id", "version_code" },
                unique: true,
                filter: "is_deleted = false");

            // 3. Back-fill: one version "V1" per existing list, soft-deleted lists included so their lines keep a parent.
            //    Status keeps today's behaviour: Expired (3) once the list range has ended, otherwise Active (2).
            //    The list's own is_active flag still gates pricing, so inactive lists stay inactive.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "tbl_SD_PriceList" WHERE effective_to IS NOT NULL AND effective_to < effective_from) THEN
                        RAISE EXCEPTION 'S25.1 migration aborted: a price list has effective_to earlier than effective_from. Fix those rows first.';
                    END IF;
                END $$;

                INSERT INTO "tbl_SD_PriceListVersion"
                    (price_list_id, version_code, effective_from, effective_to, status, external_id, created_at_utc, is_deleted, deleted_at_utc, deleted_by, tenant_id)
                SELECT pl.id, 'V1', pl.effective_from, pl.effective_to,
                       CASE WHEN pl.effective_to IS NOT NULL AND pl.effective_to < NOW() THEN 3 ELSE 2 END,
                       gen_random_uuid(), NOW(), pl.is_deleted, pl.deleted_at_utc, pl.deleted_by, pl.tenant_id
                FROM "tbl_SD_PriceList" pl;
                """);

            // 4. Re-point the lines from the list to its version. The column is added nullable, filled, then made required.
            migrationBuilder.AddColumn<int>(
                name: "price_list_version_id",
                table: "tbl_SD_PriceListItem",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "tbl_SD_PriceListItem" i
                SET price_list_version_id = v.id
                FROM "tbl_SD_PriceListVersion" v
                WHERE v.price_list_id = i.price_list_id AND v.version_code = 'V1';

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "tbl_SD_PriceListItem" WHERE price_list_version_id IS NULL) THEN
                        RAISE EXCEPTION 'S25.1 migration aborted: price list lines without a version remain.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sd_price_list_item_tbl_sd_price_list_price_list_id",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sd_price_list_item_price_list_id_item_id_uom_id_min_quant",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.DropColumn(
                name: "price_list_id",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.AlterColumn<int>(
                name: "price_list_version_id",
                table: "tbl_SD_PriceListItem",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_item_version_item_uom_min_quantity",
                table: "tbl_SD_PriceListItem",
                columns: new[] { "price_list_version_id", "item_id", "uom_id", "min_quantity" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sd_price_list_item_price_list_version_id",
                table: "tbl_SD_PriceListItem",
                column: "price_list_version_id",
                principalTable: "tbl_SD_PriceListVersion",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy by design: the old schema holds one set of lines per list, so only the lines of the
            // newest Active version (else the newest version) of each list are kept; lines of other versions are deleted.
            migrationBuilder.AddColumn<int>(
                name: "price_list_id",
                table: "tbl_SD_PriceListItem",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "tbl_SD_PriceListItem" i
                SET price_list_id = v.price_list_id
                FROM "tbl_SD_PriceListVersion" v
                WHERE v.id = i.price_list_version_id
                  AND v.id IN (
                      SELECT DISTINCT ON (price_list_id) id
                      FROM "tbl_SD_PriceListVersion"
                      ORDER BY price_list_id, (status = 2) DESC, effective_from DESC, id DESC);

                DELETE FROM "tbl_SD_PriceListItem" WHERE price_list_id IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sd_price_list_item_price_list_version_id",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sd_price_list_item_version_item_uom_min_quantity",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.DropColumn(
                name: "price_list_version_id",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.AlterColumn<int>(
                name: "price_list_id",
                table: "tbl_SD_PriceListItem",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_item_price_list_id_item_id_uom_id_min_quant",
                table: "tbl_SD_PriceListItem",
                columns: new[] { "price_list_id", "item_id", "uom_id", "min_quantity" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sd_price_list_item_tbl_sd_price_list_price_list_id",
                table: "tbl_SD_PriceListItem",
                column: "price_list_id",
                principalTable: "tbl_SD_PriceList",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                name: "tbl_SD_PriceListVersion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_SD_PriceList_Type",
                table: "tbl_SD_PriceList");

            migrationBuilder.DropColumn(
                name: "is_tax_inclusive",
                table: "tbl_SD_PriceList");

            migrationBuilder.DropColumn(
                name: "type",
                table: "tbl_SD_PriceList");
        }
    }
}
