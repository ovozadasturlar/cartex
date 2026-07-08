using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_carts_customer_id",
                table: "carts");

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "carts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_carts_customer_id_idempotency_key",
                table: "carts",
                columns: new[] { "customer_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_carts_customer_id_idempotency_key",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "carts");

            migrationBuilder.CreateIndex(
                name: "ix_carts_customer_id",
                table: "carts",
                column: "customer_id");
        }
    }
}
