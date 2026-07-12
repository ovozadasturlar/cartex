using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "carts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "note",
                table: "carts");
        }
    }
}
