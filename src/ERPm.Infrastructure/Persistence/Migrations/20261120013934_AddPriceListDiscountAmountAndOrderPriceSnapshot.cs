using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceListDiscountAmountAndOrderPriceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_SD_PriceListItem_Values",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "tbl_SD_PriceListItem",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "applied_price_list_id",
                table: "tbl_SALE_OrderItem",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_price_list_name",
                table: "tbl_SALE_OrderItem",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "applied_price_list_version_id",
                table: "tbl_SALE_OrderItem",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_version_code",
                table: "tbl_SALE_OrderItem",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_percent",
                table: "tbl_SALE_OrderItem",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "fixed_discount_amount",
                table: "tbl_SALE_OrderItem",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "original_price",
                table: "tbl_SALE_OrderItem",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "uom_id",
                table: "tbl_SALE_OrderItem",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_SD_PriceListItem_Values",
                table: "tbl_SD_PriceListItem",
                sql: "unit_price >= 0 AND min_quantity >= 0 AND discount_percent BETWEEN 0 AND 100 AND discount_amount >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_SD_PriceListItem_Values",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "tbl_SD_PriceListItem");

            migrationBuilder.DropColumn(
                name: "applied_price_list_id",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "applied_price_list_name",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "applied_price_list_version_id",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "applied_version_code",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "discount_percent",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "fixed_discount_amount",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "original_price",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "uom_id",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_SD_PriceListItem_Values",
                table: "tbl_SD_PriceListItem",
                sql: "unit_price >= 0 AND min_quantity >= 0 AND discount_percent BETWEEN 0 AND 100");
        }
    }
}
