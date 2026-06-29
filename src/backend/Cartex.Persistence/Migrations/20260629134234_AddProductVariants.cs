using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_barcodes_products_product_id",
                table: "barcodes");

            migrationBuilder.DropForeignKey(
                name: "fk_cart_items_products_product_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "fk_product_prices_products_product_id",
                table: "product_prices");

            migrationBuilder.DropForeignKey(
                name: "fk_sale_items_products_product_id",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "fk_sale_items_stocks_stock_id",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "fk_stock_transfers_products_product_id",
                table: "stock_transfers");

            migrationBuilder.DropForeignKey(
                name: "fk_stocks_products_product_id",
                table: "stocks");

            migrationBuilder.DropForeignKey(
                name: "fk_supply_items_products_product_id",
                table: "supply_items");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "supply_items",
                newName: "variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_supply_items_product_id",
                table: "supply_items",
                newName: "ix_supply_items_variant_id");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "stocks",
                newName: "variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_stocks_warehouse_id_product_id",
                table: "stocks",
                newName: "ix_stocks_warehouse_id_variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_stocks_product_id",
                table: "stocks",
                newName: "ix_stocks_variant_id");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "stock_transfers",
                newName: "variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_stock_transfers_product_id",
                table: "stock_transfers",
                newName: "ix_stock_transfers_variant_id");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "sale_items",
                newName: "variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_sale_items_product_id",
                table: "sale_items",
                newName: "ix_sale_items_variant_id");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "product_prices",
                newName: "variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_product_prices_product_id_warehouse_id",
                table: "product_prices",
                newName: "ix_product_prices_variant_id_warehouse_id");

            migrationBuilder.RenameIndex(
                name: "ix_product_prices_product_id",
                table: "product_prices",
                newName: "ix_product_prices_variant_id");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "cart_items",
                newName: "variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_cart_items_product_id",
                table: "cart_items",
                newName: "ix_cart_items_variant_id");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "barcodes",
                newName: "variant_id");

            migrationBuilder.RenameIndex(
                name: "ix_barcodes_product_id",
                table: "barcodes",
                newName: "ix_barcodes_variant_id");

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    attributes = table.Column<string>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("pk_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_code",
                table: "product_variants",
                column: "code",
                unique: true,
                filter: "\"code\" IS NOT NULL AND \"is_deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_product_id",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_barcodes_product_variants_variant_id",
                table: "barcodes",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_cart_items_product_variants_variant_id",
                table: "cart_items",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_product_prices_product_variants_variant_id",
                table: "product_prices",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sale_items_product_variants_variant_id",
                table: "sale_items",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sale_items_stocks_stock_id",
                table: "sale_items",
                column: "stock_id",
                principalTable: "stocks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stock_transfers_product_variants_variant_id",
                table: "stock_transfers",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stocks_product_variants_variant_id",
                table: "stocks",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_supply_items_product_variants_variant_id",
                table: "supply_items",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_barcodes_product_variants_variant_id",
                table: "barcodes");

            migrationBuilder.DropForeignKey(
                name: "fk_cart_items_product_variants_variant_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "fk_product_prices_product_variants_variant_id",
                table: "product_prices");

            migrationBuilder.DropForeignKey(
                name: "fk_sale_items_product_variants_variant_id",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "fk_sale_items_stocks_stock_id",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "fk_stock_transfers_product_variants_variant_id",
                table: "stock_transfers");

            migrationBuilder.DropForeignKey(
                name: "fk_stocks_product_variants_variant_id",
                table: "stocks");

            migrationBuilder.DropForeignKey(
                name: "fk_supply_items_product_variants_variant_id",
                table: "supply_items");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.RenameColumn(
                name: "variant_id",
                table: "supply_items",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_supply_items_variant_id",
                table: "supply_items",
                newName: "ix_supply_items_product_id");

            migrationBuilder.RenameColumn(
                name: "variant_id",
                table: "stocks",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_stocks_warehouse_id_variant_id",
                table: "stocks",
                newName: "ix_stocks_warehouse_id_product_id");

            migrationBuilder.RenameIndex(
                name: "ix_stocks_variant_id",
                table: "stocks",
                newName: "ix_stocks_product_id");

            migrationBuilder.RenameColumn(
                name: "variant_id",
                table: "stock_transfers",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_stock_transfers_variant_id",
                table: "stock_transfers",
                newName: "ix_stock_transfers_product_id");

            migrationBuilder.RenameColumn(
                name: "variant_id",
                table: "sale_items",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_sale_items_variant_id",
                table: "sale_items",
                newName: "ix_sale_items_product_id");

            migrationBuilder.RenameColumn(
                name: "variant_id",
                table: "product_prices",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_product_prices_variant_id_warehouse_id",
                table: "product_prices",
                newName: "ix_product_prices_product_id_warehouse_id");

            migrationBuilder.RenameIndex(
                name: "ix_product_prices_variant_id",
                table: "product_prices",
                newName: "ix_product_prices_product_id");

            migrationBuilder.RenameColumn(
                name: "variant_id",
                table: "cart_items",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_cart_items_variant_id",
                table: "cart_items",
                newName: "ix_cart_items_product_id");

            migrationBuilder.RenameColumn(
                name: "variant_id",
                table: "barcodes",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_barcodes_variant_id",
                table: "barcodes",
                newName: "ix_barcodes_product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_barcodes_products_product_id",
                table: "barcodes",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_cart_items_products_product_id",
                table: "cart_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_product_prices_products_product_id",
                table: "product_prices",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sale_items_products_product_id",
                table: "sale_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sale_items_stocks_stock_id",
                table: "sale_items",
                column: "stock_id",
                principalTable: "stocks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_stock_transfers_products_product_id",
                table: "stock_transfers",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_stocks_products_product_id",
                table: "stocks",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_supply_items_products_product_id",
                table: "supply_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
