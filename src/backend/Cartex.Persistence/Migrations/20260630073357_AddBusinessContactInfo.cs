using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessContactInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_image_key",
                table: "businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "businesses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "logo_image_key",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "businesses");
        }
    }
}
