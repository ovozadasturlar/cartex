using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleItemPopularityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sale_items_sale_id",
                table: "sale_items");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_sale_id_variant_id",
                table: "sale_items",
                columns: new[] { "sale_id", "variant_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sale_items_sale_id_variant_id",
                table: "sale_items");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_sale_id",
                table: "sale_items",
                column: "sale_id");
        }
    }
}
