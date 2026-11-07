using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesPriceList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "price_list_id",
                table: "tbl_CRM_Partner",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_SD_PriceList",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    branch_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_tbl_sd_price_list", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sd_price_list_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SD_PriceListItem",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    price_list_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    uom_id = table.Column<int>(type: "integer", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    min_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sd_price_list_item", x => x.id);
                    table.CheckConstraint("CK_tbl_SD_PriceListItem_Values", "unit_price >= 0 AND min_quantity >= 0 AND discount_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_tbl_sd_price_list_item_tbl_sd_price_list_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "tbl_SD_PriceList",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_sd_price_list_item_tbl_wms_item_item_id",
                        column: x => x.item_id,
                        principalTable: "tbl_WMS_Item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_price_list_item_tbl_wms_unit_of_measure_uom_id",
                        column: x => x.uom_id,
                        principalTable: "tbl_WMS_UnitOfMeasure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_crm_partner_price_list_id",
                table: "tbl_CRM_Partner",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_branch_id",
                table: "tbl_SD_PriceList",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_code",
                table: "tbl_SD_PriceList",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_external_id",
                table: "tbl_SD_PriceList",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_is_active_effective_from_effective_to",
                table: "tbl_SD_PriceList",
                columns: new[] { "is_active", "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_item_external_id",
                table: "tbl_SD_PriceListItem",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_item_item_id",
                table: "tbl_SD_PriceListItem",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_item_price_list_id_item_id_uom_id_min_quant",
                table: "tbl_SD_PriceListItem",
                columns: new[] { "price_list_id", "item_id", "uom_id", "min_quantity" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_price_list_item_uom_id",
                table: "tbl_SD_PriceListItem",
                column: "uom_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_crm_partner_price_lists_price_list_id",
                table: "tbl_CRM_Partner",
                column: "price_list_id",
                principalTable: "tbl_SD_PriceList",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_crm_partner_price_lists_price_list_id",
                table: "tbl_CRM_Partner");

            migrationBuilder.DropTable(
                name: "tbl_SD_PriceListItem");

            migrationBuilder.DropTable(
                name: "tbl_SD_PriceList");

            migrationBuilder.DropIndex(
                name: "ix_tbl_crm_partner_price_list_id",
                table: "tbl_CRM_Partner");

            migrationBuilder.DropColumn(
                name: "price_list_id",
                table: "tbl_CRM_Partner");
        }
    }
}
