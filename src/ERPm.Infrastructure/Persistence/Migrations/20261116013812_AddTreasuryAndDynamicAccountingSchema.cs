using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryAndDynamicAccountingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_postable",
                table: "tbl_ACC_Account",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "nature",
                table: "tbl_ACC_Account",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "track_branch",
                table: "tbl_ACC_Account",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "track_partner",
                table: "tbl_ACC_Account",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "tbl_ACC_AccountPostingProfile",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: true),
                    dimension_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    debit_account_id = table.Column<int>(type: "integer", nullable: false),
                    credit_account_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("pk_tbl_acc_account_posting_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_acc_account_posting_profile_tbl_acc_account_credit_accoun",
                        column: x => x.credit_account_id,
                        principalTable: "tbl_ACC_Account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_account_posting_profile_tbl_acc_account_debit_account",
                        column: x => x.debit_account_id,
                        principalTable: "tbl_ACC_Account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_account_posting_profile_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ACC_BankAccount",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bank_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_holder = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    bank_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "VND"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("pk_tbl_acc_bank_account", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_acc_bank_account_tbl_acc_account_account_id",
                        column: x => x.account_id,
                        principalTable: "tbl_ACC_Account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_bank_account_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ACC_CashFund",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    custodian_employee_id = table.Column<int>(type: "integer", nullable: true),
                    current_balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "VND"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("pk_tbl_acc_cash_fund", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_acc_cash_fund_tbl_acc_account_account_id",
                        column: x => x.account_id,
                        principalTable: "tbl_ACC_Account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_cash_fund_tbl_hrm_employee_custodian_employee_id",
                        column: x => x.custodian_employee_id,
                        principalTable: "tbl_HRM_Employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tbl_acc_cash_fund_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ACC_PaymentVoucher",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    voucher_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    cash_fund_id = table.Column<int>(type: "integer", nullable: true),
                    bank_account_id = table.Column<int>(type: "integer", nullable: true),
                    partner_id = table.Column<int>(type: "integer", nullable: true),
                    recipient_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_type = table.Column<int>(type: "integer", nullable: false),
                    expense_account_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    journal_entry_id = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_tbl_acc_payment_voucher", x => x.id);
                    table.CheckConstraint("CK_tbl_ACC_PaymentVoucher_Amount", "amount > 0");
                    table.CheckConstraint("CK_tbl_ACC_PaymentVoucher_Method", "payment_method IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_tbl_acc_payment_voucher_tbl_acc_account_expense_account_id",
                        column: x => x.expense_account_id,
                        principalTable: "tbl_ACC_Account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_payment_voucher_tbl_acc_bank_account_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "tbl_ACC_BankAccount",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_payment_voucher_tbl_acc_cash_fund_cash_fund_id",
                        column: x => x.cash_fund_id,
                        principalTable: "tbl_ACC_CashFund",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_payment_voucher_tbl_acc_journal_entry_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "tbl_ACC_JournalEntry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_payment_voucher_tbl_crm_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "tbl_CRM_Partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_payment_voucher_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ACC_ReceiptVoucher",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    voucher_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    cash_fund_id = table.Column<int>(type: "integer", nullable: true),
                    bank_account_id = table.Column<int>(type: "integer", nullable: true),
                    partner_id = table.Column<int>(type: "integer", nullable: true),
                    payer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    payer_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    receipt_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    journal_entry_id = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_tbl_acc_receipt_voucher", x => x.id);
                    table.CheckConstraint("CK_tbl_ACC_ReceiptVoucher_Amount", "amount > 0");
                    table.CheckConstraint("CK_tbl_ACC_ReceiptVoucher_Method", "payment_method IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_tbl_acc_receipt_voucher_tbl_acc_bank_account_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "tbl_ACC_BankAccount",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_receipt_voucher_tbl_acc_cash_fund_cash_fund_id",
                        column: x => x.cash_fund_id,
                        principalTable: "tbl_ACC_CashFund",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_receipt_voucher_tbl_acc_journal_entry_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "tbl_ACC_JournalEntry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_receipt_voucher_tbl_crm_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "tbl_CRM_Partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_acc_receipt_voucher_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ACC_ReceiptVoucherAllocation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    receipt_voucher_id = table.Column<int>(type: "integer", nullable: false),
                    sales_invoice_id = table.Column<int>(type: "integer", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_acc_receipt_voucher_allocation", x => x.id);
                    table.CheckConstraint("CK_tbl_ACC_ReceiptVoucherAllocation_Amount", "allocated_amount > 0");
                    table.ForeignKey(
                        name: "fk_tbl_acc_receipt_voucher_allocation_tbl_acc_receipt_voucher_rec",
                        column: x => x.receipt_voucher_id,
                        principalTable: "tbl_ACC_ReceiptVoucher",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_acc_receipt_voucher_allocation_tbl_sale_invoice_sales_inv",
                        column: x => x.sales_invoice_id,
                        principalTable: "tbl_SALE_Invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_ACC_Account_Nature",
                table: "tbl_ACC_Account",
                sql: "nature IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_account_posting_profile_branch_id",
                table: "tbl_ACC_AccountPostingProfile",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_account_posting_profile_credit_account_id",
                table: "tbl_ACC_AccountPostingProfile",
                column: "credit_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_account_posting_profile_debit_account_id",
                table: "tbl_ACC_AccountPostingProfile",
                column: "debit_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_account_posting_profile_external_id",
                table: "tbl_ACC_AccountPostingProfile",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "UQ_PostingProfile_Key",
                table: "tbl_ACC_AccountPostingProfile",
                columns: new[] { "module", "transaction_type", "branch_id", "dimension_key" },
                unique: true,
                filter: "is_deleted = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_bank_account_account_id",
                table: "tbl_ACC_BankAccount",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_bank_account_branch_id",
                table: "tbl_ACC_BankAccount",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_bank_account_external_id",
                table: "tbl_ACC_BankAccount",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "UQ_BankAccount_Number",
                table: "tbl_ACC_BankAccount",
                columns: new[] { "bank_name", "account_number" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_cash_fund_account_id",
                table: "tbl_ACC_CashFund",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_cash_fund_branch_id",
                table: "tbl_ACC_CashFund",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_cash_fund_code",
                table: "tbl_ACC_CashFund",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_cash_fund_custodian_employee_id",
                table: "tbl_ACC_CashFund",
                column: "custodian_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_cash_fund_external_id",
                table: "tbl_ACC_CashFund",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_bank_account_id",
                table: "tbl_ACC_PaymentVoucher",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_branch_id_voucher_date",
                table: "tbl_ACC_PaymentVoucher",
                columns: new[] { "branch_id", "voucher_date" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_cash_fund_id",
                table: "tbl_ACC_PaymentVoucher",
                column: "cash_fund_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_expense_account_id",
                table: "tbl_ACC_PaymentVoucher",
                column: "expense_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_external_id",
                table: "tbl_ACC_PaymentVoucher",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_journal_entry_id",
                table: "tbl_ACC_PaymentVoucher",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_partner_id",
                table: "tbl_ACC_PaymentVoucher",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_payment_voucher_voucher_number",
                table: "tbl_ACC_PaymentVoucher",
                column: "voucher_number",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_bank_account_id",
                table: "tbl_ACC_ReceiptVoucher",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_branch_id_voucher_date",
                table: "tbl_ACC_ReceiptVoucher",
                columns: new[] { "branch_id", "voucher_date" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_cash_fund_id",
                table: "tbl_ACC_ReceiptVoucher",
                column: "cash_fund_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_external_id",
                table: "tbl_ACC_ReceiptVoucher",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_journal_entry_id",
                table: "tbl_ACC_ReceiptVoucher",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_partner_id",
                table: "tbl_ACC_ReceiptVoucher",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_voucher_number",
                table: "tbl_ACC_ReceiptVoucher",
                column: "voucher_number",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_allocation_external_id",
                table: "tbl_ACC_ReceiptVoucherAllocation",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_allocation_receipt_voucher_id_sales_i",
                table: "tbl_ACC_ReceiptVoucherAllocation",
                columns: new[] { "receipt_voucher_id", "sales_invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_acc_receipt_voucher_allocation_sales_invoice_id",
                table: "tbl_ACC_ReceiptVoucherAllocation",
                column: "sales_invoice_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_ACC_AccountPostingProfile");

            migrationBuilder.DropTable(
                name: "tbl_ACC_PaymentVoucher");

            migrationBuilder.DropTable(
                name: "tbl_ACC_ReceiptVoucherAllocation");

            migrationBuilder.DropTable(
                name: "tbl_ACC_ReceiptVoucher");

            migrationBuilder.DropTable(
                name: "tbl_ACC_BankAccount");

            migrationBuilder.DropTable(
                name: "tbl_ACC_CashFund");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_ACC_Account_Nature",
                table: "tbl_ACC_Account");

            migrationBuilder.DropColumn(
                name: "is_postable",
                table: "tbl_ACC_Account");

            migrationBuilder.DropColumn(
                name: "nature",
                table: "tbl_ACC_Account");

            migrationBuilder.DropColumn(
                name: "track_branch",
                table: "tbl_ACC_Account");

            migrationBuilder.DropColumn(
                name: "track_partner",
                table: "tbl_ACC_Account");
        }
    }
}
