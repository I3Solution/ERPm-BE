using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerAddressType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_partner_delivery_address_default",
                table: "tbl_CRM_PartnerDeliveryAddress");

            migrationBuilder.AddColumn<int>(
                name: "address_type",
                table: "tbl_CRM_PartnerDeliveryAddress",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_tbl_crm_partner_delivery_address_partner_id",
                table: "tbl_CRM_PartnerDeliveryAddress",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ux_partner_delivery_address_default",
                table: "tbl_CRM_PartnerDeliveryAddress",
                columns: new[] { "partner_id", "address_type" },
                unique: true,
                filter: "is_default = true AND is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_CRM_PartnerDeliveryAddress_AddressType",
                table: "tbl_CRM_PartnerDeliveryAddress",
                sql: "address_type IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tbl_crm_partner_delivery_address_partner_id",
                table: "tbl_CRM_PartnerDeliveryAddress");

            migrationBuilder.DropIndex(
                name: "ux_partner_delivery_address_default",
                table: "tbl_CRM_PartnerDeliveryAddress");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_CRM_PartnerDeliveryAddress_AddressType",
                table: "tbl_CRM_PartnerDeliveryAddress");

            migrationBuilder.DropColumn(
                name: "address_type",
                table: "tbl_CRM_PartnerDeliveryAddress");

            migrationBuilder.CreateIndex(
                name: "ux_partner_delivery_address_default",
                table: "tbl_CRM_PartnerDeliveryAddress",
                column: "partner_id",
                unique: true,
                filter: "is_default = true AND is_deleted = false");
        }
    }
}
