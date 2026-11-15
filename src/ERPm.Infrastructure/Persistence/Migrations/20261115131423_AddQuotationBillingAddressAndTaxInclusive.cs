using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationBillingAddressAndTaxInclusive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "billing_address_id",
                table: "tbl_SD_SalesQuotation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "billing_address_text",
                table: "tbl_SD_SalesQuotation",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_tax_inclusive",
                table: "tbl_SD_SalesQuotation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_billing_address_id",
                table: "tbl_SD_SalesQuotation",
                column: "billing_address_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tbl_sd_sales_quotation_billing_address_id",
                table: "tbl_SD_SalesQuotation");

            migrationBuilder.DropColumn(
                name: "billing_address_id",
                table: "tbl_SD_SalesQuotation");

            migrationBuilder.DropColumn(
                name: "billing_address_text",
                table: "tbl_SD_SalesQuotation");

            migrationBuilder.DropColumn(
                name: "is_tax_inclusive",
                table: "tbl_SD_SalesQuotation");
        }
    }
}
