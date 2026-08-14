using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeletablePrintDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_print_attempts_print_nodes_print_node_id",
                table: "print_attempts");

            migrationBuilder.DropForeignKey(
                name: "fk_print_attempts_printer_endpoints_printer_endpoint_id",
                table: "print_attempts");

            migrationBuilder.AlterColumn<long>(
                name: "printer_endpoint_id",
                table: "print_attempts",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "print_node_id",
                table: "print_attempts",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddForeignKey(
                name: "fk_print_attempts_print_nodes_print_node_id",
                table: "print_attempts",
                column: "print_node_id",
                principalTable: "print_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_print_attempts_printer_endpoints_printer_endpoint_id",
                table: "print_attempts",
                column: "printer_endpoint_id",
                principalTable: "printer_endpoints",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_print_attempts_print_nodes_print_node_id",
                table: "print_attempts");

            migrationBuilder.DropForeignKey(
                name: "fk_print_attempts_printer_endpoints_printer_endpoint_id",
                table: "print_attempts");

            migrationBuilder.AlterColumn<long>(
                name: "printer_endpoint_id",
                table: "print_attempts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "print_node_id",
                table: "print_attempts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_print_attempts_print_nodes_print_node_id",
                table: "print_attempts",
                column: "print_node_id",
                principalTable: "print_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_print_attempts_printer_endpoints_printer_endpoint_id",
                table: "print_attempts",
                column: "printer_endpoint_id",
                principalTable: "printer_endpoints",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
