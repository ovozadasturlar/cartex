using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartCheckoutDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "credit_amount",
                table: "carts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "debt_currency",
                table: "carts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "debt_due_date",
                table: "carts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "use_customer_advance",
                table: "carts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "cart_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cart_id = table.Column<long>(type: "bigint", nullable: false),
                    method = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_payments", x => x.id);
                    table.CheckConstraint("ck_cart_payments_amount", "\"amount\" > 0");
                    table.ForeignKey(
                        name: "fk_cart_payments_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cart_payments_cart_id_method_currency",
                table: "cart_payments",
                columns: new[] { "cart_id", "method", "currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_payments");

            migrationBuilder.DropColumn(
                name: "credit_amount",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "debt_currency",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "debt_due_date",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "use_customer_advance",
                table: "carts");
        }
    }
}
