using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "entry_price",
                table: "supply_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "entry_quantity",
                table: "supply_items",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "pack_id",
                table: "supply_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "price_basis",
                table: "supply_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "pack_id",
                table: "barcodes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_packs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    size = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_packs", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_packs_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supply_items_pack_id",
                table: "supply_items",
                column: "pack_id");

            migrationBuilder.CreateIndex(
                name: "ix_barcodes_pack_id",
                table: "barcodes",
                column: "pack_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_packs_product_id",
                table: "product_packs",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_packs_product_id_name",
                table: "product_packs",
                columns: new[] { "product_id", "name" },
                unique: true,
                filter: "\"is_deleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "fk_barcodes_product_packs_pack_id",
                table: "barcodes",
                column: "pack_id",
                principalTable: "product_packs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_supply_items_product_packs_pack_id",
                table: "supply_items",
                column: "pack_id",
                principalTable: "product_packs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Eski qatorlar saqlash birligida kiritilgan deb hisoblanadi: kirim ko'rinishi = normallashgan qiymat.
            migrationBuilder.Sql("""
                UPDATE supply_items
                SET entry_quantity = quantity,
                    entry_price = purchase_price,
                    price_basis = 'PerEntry'
                WHERE entry_quantity = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_barcodes_product_packs_pack_id",
                table: "barcodes");

            migrationBuilder.DropForeignKey(
                name: "fk_supply_items_product_packs_pack_id",
                table: "supply_items");

            migrationBuilder.DropTable(
                name: "product_packs");

            migrationBuilder.DropIndex(
                name: "ix_supply_items_pack_id",
                table: "supply_items");

            migrationBuilder.DropIndex(
                name: "ix_barcodes_pack_id",
                table: "barcodes");

            migrationBuilder.DropColumn(
                name: "entry_price",
                table: "supply_items");

            migrationBuilder.DropColumn(
                name: "entry_quantity",
                table: "supply_items");

            migrationBuilder.DropColumn(
                name: "pack_id",
                table: "supply_items");

            migrationBuilder.DropColumn(
                name: "price_basis",
                table: "supply_items");

            migrationBuilder.DropColumn(
                name: "pack_id",
                table: "barcodes");
        }
    }
}
