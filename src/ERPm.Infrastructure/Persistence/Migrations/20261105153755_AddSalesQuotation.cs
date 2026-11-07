using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesQuotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_SD_SalesQuotation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quotation_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quotation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    customer_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    salesperson_id = table.Column<int>(type: "integer", nullable: true),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    price_list_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sub_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    terms_and_conditions = table.Column<string>(type: "text", nullable: true),
                    converted_sales_order_id = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_tbl_sd_sales_quotation", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_sales_orders_converted_sales_order_id",
                        column: x => x.converted_sales_order_id,
                        principalTable: "tbl_SALE_Order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_tbl_crm_partner_customer_id",
                        column: x => x.customer_id,
                        principalTable: "tbl_CRM_Partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_tbl_hrm_department_department_id",
                        column: x => x.department_id,
                        principalTable: "tbl_HRM_Department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_tbl_hrm_employee_salesperson_id",
                        column: x => x.salesperson_id,
                        principalTable: "tbl_HRM_Employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_tbl_sd_price_list_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "tbl_SD_PriceList",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SD_SalesQuotationItem",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_quotation_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    unit_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tbl_sd_sales_quotation_item", x => x.id);
                    table.CheckConstraint("CK_tbl_SD_SalesQuotationItem_Values", "quantity > 0 AND unit_price >= 0 AND discount_amount >= 0 AND line_total >= 0");
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_item_tbl_sd_sales_quotation_sales_quota",
                        column: x => x.sales_quotation_id,
                        principalTable: "tbl_SD_SalesQuotation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tbl_sd_sales_quotation_item_tbl_wms_item_item_id",
                        column: x => x.item_id,
                        principalTable: "tbl_WMS_Item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_branch_id",
                table: "tbl_SD_SalesQuotation",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_converted_sales_order_id",
                table: "tbl_SD_SalesQuotation",
                column: "converted_sales_order_id",
                unique: true,
                filter: "converted_sales_order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_customer_id",
                table: "tbl_SD_SalesQuotation",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_department_id",
                table: "tbl_SD_SalesQuotation",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_external_id",
                table: "tbl_SD_SalesQuotation",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_price_list_id",
                table: "tbl_SD_SalesQuotation",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_quotation_number",
                table: "tbl_SD_SalesQuotation",
                column: "quotation_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_salesperson_id",
                table: "tbl_SD_SalesQuotation",
                column: "salesperson_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_status_valid_until",
                table: "tbl_SD_SalesQuotation",
                columns: new[] { "status", "valid_until" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_item_external_id",
                table: "tbl_SD_SalesQuotationItem",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_item_item_id",
                table: "tbl_SD_SalesQuotationItem",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sd_sales_quotation_item_sales_quotation_id",
                table: "tbl_SD_SalesQuotationItem",
                column: "sales_quotation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_SD_SalesQuotationItem");

            migrationBuilder.DropTable(
                name: "tbl_SD_SalesQuotation");
        }
    }
}
