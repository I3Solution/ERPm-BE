using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceSalesOrderLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sales_order_id",
                table: "tbl_SALE_Invoice",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_invoice_sales_order_id",
                table: "tbl_SALE_Invoice",
                column: "sales_order_id",
                unique: true,
                filter: "sales_order_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_invoice_sales_orders_sales_order_id",
                table: "tbl_SALE_Invoice",
                column: "sales_order_id",
                principalTable: "tbl_SALE_Order",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_invoice_sales_orders_sales_order_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_invoice_sales_order_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropColumn(
                name: "sales_order_id",
                table: "tbl_SALE_Invoice");
        }
    }
}
