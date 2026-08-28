using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchemaCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_barcodes_product_packs_pack_id",
                table: "barcodes");

            migrationBuilder.DropTable(
                name: "sms_messages");

            migrationBuilder.DropIndex(
                name: "ix_sales_branch_id",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_print_attempts_printer_endpoint_id_status_started_at",
                table: "print_attempts");

            migrationBuilder.DropIndex(
                name: "ix_barcodes_pack_id",
                table: "barcodes");

            migrationBuilder.DropColumn(
                name: "sold_sale_id",
                table: "prepacks");

            migrationBuilder.DropColumn(
                name: "pack_id",
                table: "barcodes");

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "license_states",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_branch_id_created_at",
                table: "transactions",
                columns: new[] { "branch_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_operation_type_created_at",
                table: "transactions",
                columns: new[] { "operation_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_branch_id_status_created_at",
                table: "sales",
                columns: new[] { "branch_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_print_attempts_printer_endpoint_id",
                table: "print_attempts",
                column: "printer_endpoint_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_license_states_singleton",
                table: "license_states",
                sql: "\"id\" = 1");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_type_balance",
                table: "accounts",
                columns: new[] { "type", "balance" },
                filter: "\"customer_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transactions_branch_id_created_at",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_operation_type_created_at",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_sales_branch_id_status_created_at",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_print_attempts_printer_endpoint_id",
                table: "print_attempts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_license_states_singleton",
                table: "license_states");

            migrationBuilder.DropIndex(
                name: "ix_accounts_type_balance",
                table: "accounts");

            migrationBuilder.AddColumn<long>(
                name: "sold_sale_id",
                table: "prepacks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "license_states",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "pack_id",
                table: "barcodes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sms_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    segments = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sms_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_branch_id",
                table: "sales",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_attempts_printer_endpoint_id_status_started_at",
                table: "print_attempts",
                columns: new[] { "printer_endpoint_id", "status", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_barcodes_pack_id",
                table: "barcodes",
                column: "pack_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_messages_status_created_at",
                table: "sms_messages",
                columns: new[] { "status", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_barcodes_product_packs_pack_id",
                table: "barcodes",
                column: "pack_id",
                principalTable: "product_packs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
