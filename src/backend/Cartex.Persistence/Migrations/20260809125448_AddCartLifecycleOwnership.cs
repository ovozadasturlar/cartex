using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartLifecycleOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "carts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "carts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cancelled_by_user_id",
                table: "carts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "claimed_at",
                table: "carts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "claimed_by_user_id",
                table: "carts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "requeued_from_cart_id",
                table: "carts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "sale_id",
                table: "carts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "carts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_carts_cancelled_by_user_id",
                table: "carts",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_claimed_by_user_id",
                table: "carts",
                column: "claimed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_requeued_from_cart_id",
                table: "carts",
                column: "requeued_from_cart_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_sale_id",
                table: "carts",
                column: "sale_id",
                unique: true,
                filter: "\"sale_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_carts_status_claimed_by_user_id_claimed_at",
                table: "carts",
                columns: new[] { "status", "claimed_by_user_id", "claimed_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_carts_carts_requeued_from_cart_id",
                table: "carts",
                column: "requeued_from_cart_id",
                principalTable: "carts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_carts_sales_sale_id",
                table: "carts",
                column: "sale_id",
                principalTable: "sales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_carts_users_cancelled_by_user_id",
                table: "carts",
                column: "cancelled_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_carts_users_claimed_by_user_id",
                table: "carts",
                column: "claimed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_carts_carts_requeued_from_cart_id",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "fk_carts_sales_sale_id",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "fk_carts_users_cancelled_by_user_id",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "fk_carts_users_claimed_by_user_id",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_cancelled_by_user_id",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_claimed_by_user_id",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_requeued_from_cart_id",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_sale_id",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_status_claimed_by_user_id_claimed_at",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "claimed_by_user_id",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "requeued_from_cart_id",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "sale_id",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "version",
                table: "carts");
        }
    }
}
