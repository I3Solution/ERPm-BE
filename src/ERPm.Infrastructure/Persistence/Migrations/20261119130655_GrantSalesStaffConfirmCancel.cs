using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantSalesStaffConfirmCancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Group templates are only seeded when missing, so an existing SALES_STAFF role is granted the S27.4
            // defaults here once; actions already present are not repeated and other statements are left as they are.
            migrationBuilder.Sql("""
                UPDATE "tbl_SYS_Role" r
                SET policy_document = jsonb_set(r.policy_document, '{Statements}', (
                    SELECT jsonb_agg(CASE WHEN s.value->>'Sid' = 'OwnOrders'
                        THEN jsonb_set(s.value, '{Actions}', (s.value->'Actions') || COALESCE((
                            SELECT jsonb_agg(a.value) FROM jsonb_array_elements_text('["sd:order:confirm","sd:order:cancel"]'::jsonb) a
                            WHERE NOT (s.value->'Actions') ? a.value), '[]'::jsonb))
                        ELSE s.value END ORDER BY s.ordinality)
                    FROM jsonb_array_elements(r.policy_document->'Statements') WITH ORDINALITY s))
                WHERE r.code = 'SALES_STAFF' AND r.company_id IS NULL
                  AND r.policy_document->'Statements' @> '[{"Sid": "OwnOrders"}]'::jsonb;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data only: the earlier grants of the role are not known, so nothing is taken back.
        }
    }
}
