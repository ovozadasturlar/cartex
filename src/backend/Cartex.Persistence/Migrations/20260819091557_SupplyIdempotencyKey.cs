using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplies_user_id",
                table: "supplies");

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "supplies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplies_user_id_idempotency_key",
                table: "supplies",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplies_user_id_idempotency_key",
                table: "supplies");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "supplies");

            migrationBuilder.CreateIndex(
                name: "ix_supplies_user_id",
                table: "supplies",
                column: "user_id");
        }
    }
}
