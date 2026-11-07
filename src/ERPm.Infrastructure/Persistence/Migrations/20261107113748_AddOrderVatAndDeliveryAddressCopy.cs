using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderVatAndDeliveryAddressCopy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "tax_amount",
                table: "tbl_SALE_OrderItem",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "tbl_SALE_OrderItem",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "delivery_address_id",
                table: "tbl_SALE_Order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_address_text",
                table: "tbl_SALE_Order",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_phone",
                table: "tbl_SALE_Order",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_recipient_name",
                table: "tbl_SALE_Order",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_order_delivery_address_id",
                table: "tbl_SALE_Order",
                column: "delivery_address_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_order_tbl_crm_partner_delivery_address_delivery_addr",
                table: "tbl_SALE_Order",
                column: "delivery_address_id",
                principalTable: "tbl_CRM_PartnerDeliveryAddress",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_order_tbl_crm_partner_delivery_address_delivery_addr",
                table: "tbl_SALE_Order");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_order_delivery_address_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "tax_amount",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "delivery_address_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "delivery_address_text",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "delivery_phone",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "delivery_recipient_name",
                table: "tbl_SALE_Order");
        }
    }
}
