using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCreditAndReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "quantity_reserved",
                table: "tbl_WMS_StockBalance",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "invoiced_quantity",
                table: "tbl_SALE_OrderItem",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "tbl_SALE_Order",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_approval_reason",
                table: "tbl_SALE_Order",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_check_status",
                table: "tbl_SALE_Order",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Passed");

            migrationBuilder.AddColumn<string>(
                name: "delivery_address",
                table: "tbl_SALE_Order",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expected_delivery_date",
                table: "tbl_SALE_Order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "price_list_id",
                table: "tbl_SALE_Order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quotation_id",
                table: "tbl_SALE_Order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reservation_status",
                table: "tbl_SALE_Order",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "salesperson_id",
                table: "tbl_SALE_Order",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_SD_StockReservation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_order_id = table.Column<int>(type: "integer", nullable: false),
                    sales_order_item_id = table.Column<int>(type: "integer", nullable: false),
                    warehouse_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    released_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sd_stock_reservation", x => x.id);
                    table.CheckConstraint("CK_tbl_SD_StockReservation_Quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_tbl_sd_stock_reservation_sales_order_items_sales_order_item_",
                        column: x => x.sales_order_item_id,
                        principalTable: "tbl_SALE_OrderItem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_sd_stock_reservation_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "tbl_SALE_Order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_sd_stock_reservation_tbl_wms_item_item_id",
                        column: x => x.item_id,
                        principalTable: "tbl_WMS_Item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_stock_reservation_tbl_wms_warehouse_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "tbl_WMS_Warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_WMS_StockBalance_Reserved",
                table: "tbl_WMS_StockBalance",
                sql: "quantity_reserved >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_order_price_list_id",
                table: "tbl_SALE_Order",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_order_quotation_id",
                table: "tbl_SALE_Order",
                column: "quotation_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_order_salesperson_id",
                table: "tbl_SALE_Order",
                column: "salesperson_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_order_status",
                table: "tbl_SALE_Order",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_stock_reservation_external_id",
                table: "tbl_SD_StockReservation",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_stock_reservation_item_id",
                table: "tbl_SD_StockReservation",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_stock_reservation_sales_order_id_status",
                table: "tbl_SD_StockReservation",
                columns: new[] { "sales_order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_stock_reservation_sales_order_item_id",
                table: "tbl_SD_StockReservation",
                column: "sales_order_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_stock_reservation_warehouse_id_item_id_status",
                table: "tbl_SD_StockReservation",
                columns: new[] { "warehouse_id", "item_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_order_tbl_hrm_employee_salesperson_id",
                table: "tbl_SALE_Order",
                column: "salesperson_id",
                principalTable: "tbl_HRM_Employee",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_order_tbl_sd_price_list_price_list_id",
                table: "tbl_SALE_Order",
                column: "price_list_id",
                principalTable: "tbl_SD_PriceList",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_order_tbl_sd_sales_quotation_quotation_id",
                table: "tbl_SALE_Order",
                column: "quotation_id",
                principalTable: "tbl_SD_SalesQuotation",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_order_tbl_hrm_employee_salesperson_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_order_tbl_sd_price_list_price_list_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_order_tbl_sd_sales_quotation_quotation_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropTable(
                name: "tbl_SD_StockReservation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_WMS_StockBalance_Reserved",
                table: "tbl_WMS_StockBalance");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_order_price_list_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_order_quotation_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_order_salesperson_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_order_status",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "quantity_reserved",
                table: "tbl_WMS_StockBalance");

            migrationBuilder.DropColumn(
                name: "invoiced_quantity",
                table: "tbl_SALE_OrderItem");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "credit_approval_reason",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "credit_check_status",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "delivery_address",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "expected_delivery_date",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "price_list_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "quotation_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "reservation_status",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "salesperson_id",
                table: "tbl_SALE_Order");
        }
    }
}
