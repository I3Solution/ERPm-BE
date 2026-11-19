using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchServingWarehouseAndOrderWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "warehouse_id",
                table: "tbl_SALE_Order",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_WMS_BranchServingWarehouse",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    warehouse_id = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_tbl_wms_branch_serving_warehouse", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_wms_branch_serving_warehouse_tbl_sys_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "tbl_SYS_Branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tbl_wms_branch_serving_warehouse_tbl_wms_warehouse_warehouse_",
                        column: x => x.warehouse_id,
                        principalTable: "tbl_WMS_Warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_sale_order_warehouse_id",
                table: "tbl_SALE_Order",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_wms_branch_serving_warehouse_branch_id_warehouse_id",
                table: "tbl_WMS_BranchServingWarehouse",
                columns: new[] { "branch_id", "warehouse_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tbl_wms_branch_serving_warehouse_external_id",
                table: "tbl_WMS_BranchServingWarehouse",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_wms_branch_serving_warehouse_warehouse_id",
                table: "tbl_WMS_BranchServingWarehouse",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ux_branch_serving_warehouse_default",
                table: "tbl_WMS_BranchServingWarehouse",
                column: "branch_id",
                unique: true,
                filter: "is_default = true AND is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "fk_tbl_sale_order_tbl_wms_warehouse_warehouse_id",
                table: "tbl_SALE_Order",
                column: "warehouse_id",
                principalTable: "tbl_WMS_Warehouse",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tbl_sale_order_tbl_wms_warehouse_warehouse_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropTable(
                name: "tbl_WMS_BranchServingWarehouse");

            migrationBuilder.DropIndex(
                name: "ix_tbl_sale_order_warehouse_id",
                table: "tbl_SALE_Order");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "tbl_SALE_Order");
        }
    }
}
