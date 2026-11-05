using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_SD_SalesReturn",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    return_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    return_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    invoice_id = table.Column<int>(type: "integer", nullable: true),
                    customer_id = table.Column<int>(type: "integer", nullable: true),
                    customer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    warehouse_id = table.Column<int>(type: "integer", nullable: false),
                    refund_method = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    total_refund_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_cost_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    stock_receipt_id = table.Column<int>(type: "integer", nullable: true),
                    journal_entry_id = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_tbl_sd_sales_return", x => x.id);
                    table.CheckConstraint("CK_tbl_SD_SalesReturn_Amounts", "total_refund_amount >= 0 AND total_cost_amount >= 0");
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "tbl_ACC_JournalEntry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_tbl_crm_partner_customer_id",
                        column: x => x.customer_id,
                        principalTable: "tbl_CRM_Partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_tbl_hrm_department_department_id",
                        column: x => x.department_id,
                        principalTable: "tbl_HRM_Department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_tbl_sale_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "tbl_SALE_Invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_tbl_wms_stock_receipt_stock_receipt_id",
                        column: x => x.stock_receipt_id,
                        principalTable: "tbl_WMS_StockReceipt",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_tbl_wms_warehouse_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "tbl_WMS_Warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SD_SalesReturnItem",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_return_id = table.Column<int>(type: "integer", nullable: false),
                    invoice_item_id = table.Column<int>(type: "integer", nullable: true),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    unit_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    return_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sd_sales_return_item", x => x.id);
                    table.CheckConstraint("CK_tbl_SD_SalesReturnItem_Values", "return_quantity > 0 AND unit_price >= 0 AND refund_amount >= 0 AND cost_amount >= 0");
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_item_tbl_sale_invoice_item_invoice_item_id",
                        column: x => x.invoice_item_id,
                        principalTable: "tbl_SALE_InvoiceItem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_item_tbl_sd_sales_return_sales_return_id",
                        column: x => x.sales_return_id,
                        principalTable: "tbl_SD_SalesReturn",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_return_item_tbl_wms_item_item_id",
                        column: x => x.item_id,
                        principalTable: "tbl_WMS_Item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_branch_id",
                table: "tbl_SD_SalesReturn",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_customer_id",
                table: "tbl_SD_SalesReturn",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_department_id",
                table: "tbl_SD_SalesReturn",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_external_id",
                table: "tbl_SD_SalesReturn",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_invoice_id",
                table: "tbl_SD_SalesReturn",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_journal_entry_id",
                table: "tbl_SD_SalesReturn",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_return_number",
                table: "tbl_SD_SalesReturn",
                column: "return_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_stock_receipt_id",
                table: "tbl_SD_SalesReturn",
                column: "stock_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_warehouse_id",
                table: "tbl_SD_SalesReturn",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_item_external_id",
                table: "tbl_SD_SalesReturnItem",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_item_invoice_item_id",
                table: "tbl_SD_SalesReturnItem",
                column: "invoice_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_item_item_id",
                table: "tbl_SD_SalesReturnItem",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_return_item_sales_return_id",
                table: "tbl_SD_SalesReturnItem",
                column: "sales_return_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_SD_SalesReturnItem");

            migrationBuilder.DropTable(
                name: "tbl_SD_SalesReturn");
        }
    }
}
