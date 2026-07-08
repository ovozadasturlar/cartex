using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropMeasureModeAndPakUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "measure_mode",
                table: "product_types");

            migrationBuilder.Sql("""
                UPDATE units SET is_deleted = true, deleted_at = now()
                WHERE short_name = 'pak' AND is_system AND NOT is_deleted
                  AND NOT EXISTS (SELECT 1 FROM products p WHERE p.unit_id = units.id AND NOT p.is_deleted);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "measure_mode",
                table: "product_types",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
