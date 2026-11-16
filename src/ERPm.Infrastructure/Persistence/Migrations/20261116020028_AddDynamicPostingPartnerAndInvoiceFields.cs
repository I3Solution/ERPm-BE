using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicPostingPartnerAndInvoiceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "bank_account_id",
                table: "tbl_SALE_Invoice",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cash_fund_id",
                table: "tbl_SALE_Invoice",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "tbl_SALE_Invoice",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "receivable_account_id",
                table: "tbl_CRM_Partner",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_invoice_bank_account_id",
                table: "tbl_SALE_Invoice",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_invoice_cash_fund_id",
                table: "tbl_SALE_Invoice",
                column: "cash_fund_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_crm_partner_receivable_account_id",
                table: "tbl_CRM_Partner",
                column: "receivable_account_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_crm_partner_accounts_receivable_account_id",
                table: "tbl_CRM_Partner",
                column: "receivable_account_id",
                principalTable: "tbl_ACC_Account",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_invoice_bank_accounts_bank_account_id",
                table: "tbl_SALE_Invoice",
                column: "bank_account_id",
                principalTable: "tbl_ACC_BankAccount",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_invoice_cash_funds_cash_fund_id",
                table: "tbl_SALE_Invoice",
                column: "cash_fund_id",
                principalTable: "tbl_ACC_CashFund",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_crm_partner_accounts_receivable_account_id",
                table: "tbl_CRM_Partner");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_invoice_bank_accounts_bank_account_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_invoice_cash_funds_cash_fund_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_invoice_bank_account_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_invoice_cash_fund_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropIndex(
                name: "ix_tbl_crm_partner_receivable_account_id",
                table: "tbl_CRM_Partner");

            migrationBuilder.DropColumn(
                name: "bank_account_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropColumn(
                name: "cash_fund_id",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropColumn(
                name: "tax_rate",
                table: "tbl_SALE_Invoice");

            migrationBuilder.DropColumn(
                name: "receivable_account_id",
                table: "tbl_CRM_Partner");
        }
    }
}
