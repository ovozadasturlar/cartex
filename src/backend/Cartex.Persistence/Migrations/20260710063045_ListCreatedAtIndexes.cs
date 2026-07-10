using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ListCreatedAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_supplies_created_at",
                table: "supplies",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfers_created_at",
                table: "stock_transfers",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_created_at",
                table: "accounts",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplies_created_at",
                table: "supplies");

            migrationBuilder.DropIndex(
                name: "ix_stock_transfers_created_at",
                table: "stock_transfers");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_created_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_accounts_created_at",
                table: "accounts");
        }
    }
}
