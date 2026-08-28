using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CartItemPrepack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "prepack_id",
                table: "cart_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_prepack_id",
                table: "cart_items",
                column: "prepack_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cart_items_prepacks_prepack_id",
                table: "cart_items",
                column: "prepack_id",
                principalTable: "prepacks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cart_items_prepacks_prepack_id",
                table: "cart_items");

            migrationBuilder.DropIndex(
                name: "ix_cart_items_prepack_id",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "prepack_id",
                table: "cart_items");
        }
    }
}
