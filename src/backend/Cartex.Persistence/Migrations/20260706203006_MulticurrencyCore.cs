using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MulticurrencyCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_branch_id_type",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_customer_id_type",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_supplier_id_type",
                table: "accounts");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "transactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "rate",
                table: "transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "supplies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "rate",
                table: "supplies",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "debt_currency",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "debt_rate",
                table: "sales",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "price_currency",
                table: "sale_items",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "price_rate",
                table: "sale_items",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "product_prices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "accounts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    effective_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchange_rates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sale_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sale_id = table.Column<long>(type: "bigint", nullable: false),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    amount_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_payments_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_branch_id_type_currency",
                table: "accounts",
                columns: new[] { "branch_id", "type", "currency" },
                unique: true,
                filter: "\"branch_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_customer_id_type_currency",
                table: "accounts",
                columns: new[] { "customer_id", "type", "currency" },
                unique: true,
                filter: "\"customer_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_supplier_id_type_currency",
                table: "accounts",
                columns: new[] { "supplier_id", "type", "currency" },
                unique: true,
                filter: "\"supplier_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_code_effective_at",
                table: "exchange_rates",
                columns: new[] { "code", "effective_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sale_payments_sale_id",
                table: "sale_payments",
                column: "sale_id");

            migrationBuilder.Sql("""
                UPDATE accounts SET currency = COALESCE((SELECT currency FROM businesses LIMIT 1), 'UZS') WHERE currency = '';
                UPDATE transactions SET currency = COALESCE((SELECT currency FROM businesses LIMIT 1), 'UZS'), rate = 1 WHERE currency = '';
                UPDATE sale_items SET price_currency = COALESCE((SELECT currency FROM businesses LIMIT 1), 'UZS'), price_rate = 1 WHERE price_currency = '';
                UPDATE sales SET debt_currency = COALESCE((SELECT currency FROM businesses LIMIT 1), 'UZS'), debt_rate = 1 WHERE debt_currency = '';
                UPDATE product_prices SET currency = COALESCE((SELECT currency FROM businesses LIMIT 1), 'UZS') WHERE currency = '';
                UPDATE supplies SET currency = COALESCE((SELECT currency FROM businesses LIMIT 1), 'UZS'), rate = 1 WHERE currency = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropTable(
                name: "sale_payments");

            migrationBuilder.DropIndex(
                name: "ix_accounts_branch_id_type_currency",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_customer_id_type_currency",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_supplier_id_type_currency",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "rate",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "supplies");

            migrationBuilder.DropColumn(
                name: "rate",
                table: "supplies");

            migrationBuilder.DropColumn(
                name: "debt_currency",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "debt_rate",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "price_currency",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "price_rate",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "product_prices");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_branch_id_type",
                table: "accounts",
                columns: new[] { "branch_id", "type" },
                unique: true,
                filter: "\"branch_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_customer_id_type",
                table: "accounts",
                columns: new[] { "customer_id", "type" },
                unique: true,
                filter: "\"customer_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_supplier_id_type",
                table: "accounts",
                columns: new[] { "supplier_id", "type" },
                unique: true,
                filter: "\"supplier_id\" IS NOT NULL");
        }
    }
}
