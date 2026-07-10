using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyItemUnitPack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "pack_size",
                table: "supply_items",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<long>(
                name: "unit_id",
                table: "supply_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_supply_items_unit_id",
                table: "supply_items",
                column: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "fk_supply_items_units_unit_id",
                table: "supply_items",
                column: "unit_id",
                principalTable: "units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_supply_items_units_unit_id",
                table: "supply_items");

            migrationBuilder.DropIndex(
                name: "ix_supply_items_unit_id",
                table: "supply_items");

            migrationBuilder.DropColumn(
                name: "pack_size",
                table: "supply_items");

            migrationBuilder.DropColumn(
                name: "unit_id",
                table: "supply_items");
        }
    }
}
