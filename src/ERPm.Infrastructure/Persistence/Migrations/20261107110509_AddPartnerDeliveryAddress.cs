using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerDeliveryAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_unlimited_credit",
                table: "tbl_CRM_Partner",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "tbl_CRM_PartnerDeliveryAddress",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partner_id = table.Column<int>(type: "integer", nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ward = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_tbl_crm_partner_delivery_address", x => x.id);
                    table.ForeignKey(
                        name: "fk_tbl_crm_partner_delivery_address_tbl_crm_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "tbl_CRM_Partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tbl_crm_partner_delivery_address_external_id",
                table: "tbl_CRM_PartnerDeliveryAddress",
                column: "external_id",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ux_partner_delivery_address_default",
                table: "tbl_CRM_PartnerDeliveryAddress",
                column: "partner_id",
                unique: true,
                filter: "is_default = true AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_CRM_PartnerDeliveryAddress");

            migrationBuilder.DropColumn(
                name: "is_unlimited_credit",
                table: "tbl_CRM_Partner");
        }
    }
}
