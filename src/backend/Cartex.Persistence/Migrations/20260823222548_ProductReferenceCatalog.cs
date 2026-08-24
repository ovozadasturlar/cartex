using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductReferenceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_reference",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    barcode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    search_fold = table.Column<string>(type: "text", nullable: true),
                    unit_hint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    category_hint = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    manufacturer_hint = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    pack_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    suggested_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    source_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_reference", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_reference_barcode",
                table: "product_reference",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_reference_name_trgm",
                table: "product_reference",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_product_reference_search_fold_trgm",
                table: "product_reference",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_reference");
        }
    }
}
