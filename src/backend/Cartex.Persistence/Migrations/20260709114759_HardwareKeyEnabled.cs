using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardwareKeyEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "hardware_keys",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "hardware_keys");
        }
    }
}
