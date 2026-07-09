using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiscountExceptionKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_discount_rule_exceptions_products_product_id",
                table: "discount_rule_exceptions");

            migrationBuilder.DropIndex(
                name: "ix_discount_rule_exceptions_discount_rule_id_product_id",
                table: "discount_rule_exceptions");

            migrationBuilder.DropIndex(
                name: "ix_discount_rule_exceptions_product_id",
                table: "discount_rule_exceptions");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "discount_rule_exceptions",
                newName: "target_id");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "discount_rule_exceptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_discount_rule_exceptions_discount_rule_id_scope_target_id",
                table: "discount_rule_exceptions",
                columns: new[] { "discount_rule_id", "scope", "target_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_discount_rule_exceptions_discount_rule_id_scope_target_id",
                table: "discount_rule_exceptions");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "discount_rule_exceptions");

            migrationBuilder.RenameColumn(
                name: "target_id",
                table: "discount_rule_exceptions",
                newName: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_discount_rule_exceptions_discount_rule_id_product_id",
                table: "discount_rule_exceptions",
                columns: new[] { "discount_rule_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_discount_rule_exceptions_product_id",
                table: "discount_rule_exceptions",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_discount_rule_exceptions_products_product_id",
                table: "discount_rule_exceptions",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
