using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveInventoryCarryingValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "inventory_value",
                table: "tbl_WMS_StockBalance",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("UPDATE \"tbl_WMS_StockBalance\" SET inventory_value = ROUND(quantity_on_hand * average_cost_price, 4);");

            migrationBuilder.AddColumn<decimal>(
                name: "cost_amount",
                table: "tbl_SALE_InvoiceItem",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
            migrationBuilder.Sql("UPDATE \"tbl_SALE_InvoiceItem\" SET cost_amount = ROUND(quantity * cost_price, 4);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Inventory carrying values are financial history. Restore a verified backup instead of downgrading this migration.");
        }
    }
}
