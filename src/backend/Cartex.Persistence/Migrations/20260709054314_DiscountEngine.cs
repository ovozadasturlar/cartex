using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiscountEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "manufacturer_id",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_combine_mode",
                table: "loyalty_programs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Priority");

            migrationBuilder.CreateTable(
                name: "discount_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<long>(type: "bigint", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    min_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discount_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_discount_rules_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "manufacturers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manufacturers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "discount_rule_exceptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    discount_rule_id = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discount_rule_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_discount_rule_exceptions_discount_rules_discount_rule_id",
                        column: x => x.discount_rule_id,
                        principalTable: "discount_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_discount_rule_exceptions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_manufacturer_id",
                table: "products",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_discount_rule_exceptions_discount_rule_id_product_id",
                table: "discount_rule_exceptions",
                columns: new[] { "discount_rule_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_discount_rule_exceptions_product_id",
                table: "discount_rule_exceptions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_discount_rules_customer_id",
                table: "discount_rules",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_discount_rules_scope_target_id",
                table: "discount_rules",
                columns: new[] { "scope", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_manufacturers_name",
                table: "manufacturers",
                column: "name",
                unique: true,
                filter: "NOT \"is_deleted\"");

            migrationBuilder.AddForeignKey(
                name: "fk_products_manufacturers_manufacturer_id",
                table: "products",
                column: "manufacturer_id",
                principalTable: "manufacturers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_products_manufacturers_manufacturer_id",
                table: "products");

            migrationBuilder.DropTable(
                name: "discount_rule_exceptions");

            migrationBuilder.DropTable(
                name: "manufacturers");

            migrationBuilder.DropTable(
                name: "discount_rules");

            migrationBuilder.DropIndex(
                name: "ix_products_manufacturer_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "manufacturer_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "discount_combine_mode",
                table: "loyalty_programs");
        }
    }
}
