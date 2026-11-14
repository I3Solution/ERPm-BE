using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261113170000_AddQuotationDeliveryAddressSnapshot")]
public partial class AddQuotationDeliveryAddressSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "delivery_address_id",
            table: "tbl_SD_SalesQuotation",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "delivery_recipient_name",
            table: "tbl_SD_SalesQuotation",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "delivery_phone",
            table: "tbl_SD_SalesQuotation",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "delivery_address_text",
            table: "tbl_SD_SalesQuotation",
            type: "character varying(800)",
            maxLength: 800,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_tbl_sd_sales_quotation_delivery_address_id",
            table: "tbl_SD_SalesQuotation",
            column: "delivery_address_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_tbl_sd_sales_quotation_delivery_address_id",
            table: "tbl_SD_SalesQuotation");

        migrationBuilder.DropColumn(name: "delivery_address_id", table: "tbl_SD_SalesQuotation");
        migrationBuilder.DropColumn(name: "delivery_recipient_name", table: "tbl_SD_SalesQuotation");
        migrationBuilder.DropColumn(name: "delivery_phone", table: "tbl_SD_SalesQuotation");
        migrationBuilder.DropColumn(name: "delivery_address_text", table: "tbl_SD_SalesQuotation");
    }
}
