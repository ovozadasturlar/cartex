using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SearchFoldColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "search_fold",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_fold",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_fold",
                table: "manufacturers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_fold",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_fold",
                table: "categories",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_search_fold_trgm",
                table: "suppliers",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_products_search_fold_trgm",
                table: "products",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_manufacturers_search_fold_trgm",
                table: "manufacturers",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_customers_search_fold_trgm",
                table: "customers",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_search_fold_trgm",
                table: "categories",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_suppliers_search_fold_trgm",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "ix_products_search_fold_trgm",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_manufacturers_search_fold_trgm",
                table: "manufacturers");

            migrationBuilder.DropIndex(
                name: "ix_customers_search_fold_trgm",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ix_categories_search_fold_trgm",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "search_fold",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "search_fold",
                table: "products");

            migrationBuilder.DropColumn(
                name: "search_fold",
                table: "manufacturers");

            migrationBuilder.DropColumn(
                name: "search_fold",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "search_fold",
                table: "categories");
        }
    }
}
