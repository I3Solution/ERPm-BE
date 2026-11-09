using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCreditApprovalAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "credit_approved_at_utc",
                table: "tbl_SALE_Order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_approved_by",
                table: "tbl_SALE_Order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credit_approved_at_utc",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "credit_approved_by",
                table: "tbl_SALE_Order");
        }
    }
}
