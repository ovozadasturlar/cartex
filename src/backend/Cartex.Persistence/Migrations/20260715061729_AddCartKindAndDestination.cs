using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartKindAndDestination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cart_destination",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cart_destination",
                table: "roles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "carts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Queue");

            migrationBuilder.CreateIndex(
                name: "ix_carts_kind_status",
                table: "carts",
                columns: new[] { "kind", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_carts_kind_status",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "cart_destination",
                table: "users");

            migrationBuilder.DropColumn(
                name: "cart_destination",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "carts");
        }
    }
}
