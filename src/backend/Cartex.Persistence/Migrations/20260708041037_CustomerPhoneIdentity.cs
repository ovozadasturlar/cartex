using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPhoneIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customers_phone",
                table: "customers");

            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                table: "customers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE customers SET phone = CASE
                    WHEN regexp_replace(phone, '\D', '', 'g') = '' THEN NULL
                    WHEN length(regexp_replace(phone, '\D', '', 'g')) = 9 THEN '+998' || regexp_replace(phone, '\D', '', 'g')
                    ELSE '+' || regexp_replace(phone, '\D', '', 'g')
                END
                WHERE phone IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_customers_phone",
                table: "customers",
                column: "phone",
                unique: true,
                filter: "\"phone\" IS NOT NULL AND NOT \"is_deleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customers_phone",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "preferred_language",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "ix_customers_phone",
                table: "customers",
                column: "phone",
                unique: true,
                filter: "\"phone\" IS NOT NULL");
        }
    }
}
