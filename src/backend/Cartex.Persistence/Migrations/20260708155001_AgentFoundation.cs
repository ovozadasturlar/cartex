using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transactions_user_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_sales_user_id",
                table: "sales");

            migrationBuilder.AddColumn<long>(
                name: "assigned_user_id",
                table: "warehouses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "sales",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "agent_id",
                table: "customers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_assigned_user_id",
                table: "warehouses",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id_idempotency_key",
                table: "transactions",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_user_id_idempotency_key",
                table: "sales",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customers_agent_id",
                table: "customers",
                column: "agent_id");

            migrationBuilder.AddForeignKey(
                name: "fk_customers_users_agent_id",
                table: "customers",
                column: "agent_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_warehouses_users_assigned_user_id",
                table: "warehouses",
                column: "assigned_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customers_users_agent_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "fk_warehouses_users_assigned_user_id",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "ix_warehouses_assigned_user_id",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "ix_transactions_user_id_idempotency_key",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_sales_user_id_idempotency_key",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_customers_agent_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "assigned_user_id",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "agent_id",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id",
                table: "transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_user_id",
                table: "sales",
                column: "user_id");
        }
    }
}
