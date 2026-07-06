using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoneyIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_supplier_id",
                table: "accounts");

            migrationBuilder.AddColumn<decimal>(
                name: "change_amount",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_bonus",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_card",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_cash",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_cashback",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_debt",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_supplier_id_type",
                table: "accounts",
                columns: new[] { "supplier_id", "type" },
                unique: true,
                filter: "\"supplier_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_supplier_id_type",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "change_amount",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "refunded_bonus",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "refunded_card",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "refunded_cash",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "refunded_cashback",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "refunded_debt",
                table: "sales");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_supplier_id",
                table: "accounts",
                column: "supplier_id");
        }
    }
}
