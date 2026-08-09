using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "default_allow_amount_entry",
                table: "units",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "default_quantity_step",
                table: "units",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity_step_override",
                table: "products",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            // Preserve the pre-policy behaviour for existing measurable units.
            // Count remains integer-only, while weight/volume/length keep 0.001 precision
            // and their existing amount-entry capability.
            migrationBuilder.Sql(
                """
                UPDATE "units"
                SET "default_quantity_step" = 0.001,
                    "default_allow_amount_entry" = TRUE
                WHERE "dimension" <> 'Count';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_units_default_quantity_step",
                table: "units",
                sql: "\"default_quantity_step\" >= 0.001 AND \"default_quantity_step\" <= 1000000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_quantity_step_override",
                table: "products",
                sql: "\"quantity_step_override\" IS NULL OR (\"quantity_step_override\" >= 0.001 AND \"quantity_step_override\" <= 1000000)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_units_default_quantity_step",
                table: "units");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_quantity_step_override",
                table: "products");

            migrationBuilder.DropColumn(
                name: "default_allow_amount_entry",
                table: "units");

            migrationBuilder.DropColumn(
                name: "default_quantity_step",
                table: "units");

            migrationBuilder.DropColumn(
                name: "quantity_step_override",
                table: "products");
        }
    }
}
