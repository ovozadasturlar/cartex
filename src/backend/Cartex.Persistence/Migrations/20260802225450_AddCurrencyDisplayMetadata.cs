using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyDisplayMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "decimal_digits",
                table: "currencies",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "symbol",
                table: "currencies",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "symbol_position",
                table: "currencies",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Suffix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "decimal_digits",
                table: "currencies");

            migrationBuilder.DropColumn(
                name: "symbol",
                table: "currencies");

            migrationBuilder.DropColumn(
                name: "symbol_position",
                table: "currencies");
        }
    }
}
