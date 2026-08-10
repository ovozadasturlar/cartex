using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BooleanQuantityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_units_default_quantity_step",
                table: "units");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_quantity_step_override",
                table: "products");

            migrationBuilder.DropColumn(
                name: "default_quantity_step",
                table: "units");

            migrationBuilder.DropColumn(
                name: "quantity_step_override",
                table: "products");

            migrationBuilder.AddColumn<bool>(
                name: "allow_fractional",
                table: "units",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "fractional_override",
                table: "products",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allow_fractional",
                table: "units");

            migrationBuilder.DropColumn(
                name: "fractional_override",
                table: "products");

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_units_default_quantity_step",
                table: "units",
                sql: "\"default_quantity_step\" >= 0.001 AND \"default_quantity_step\" <= 1000000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_quantity_step_override",
                table: "products",
                sql: "\"quantity_step_override\" IS NULL OR (\"quantity_step_override\" >= 0.001 AND \"quantity_step_override\" <= 1000000)");
        }
    }
}
