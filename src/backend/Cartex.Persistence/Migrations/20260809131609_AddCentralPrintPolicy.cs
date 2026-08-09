using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralPrintPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "auto_print_on_sale",
                table: "print_routing_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "default_copies",
                table: "print_routing_policies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "receipt_settings_override_json",
                table: "print_routing_policies",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "print_routing_policies",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_print_routing_default_copies",
                table: "print_routing_policies",
                sql: "default_copies BETWEEN 1 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_print_routing_revision",
                table: "print_routing_policies",
                sql: "revision > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_print_routing_default_copies",
                table: "print_routing_policies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_print_routing_revision",
                table: "print_routing_policies");

            migrationBuilder.DropColumn(
                name: "auto_print_on_sale",
                table: "print_routing_policies");

            migrationBuilder.DropColumn(
                name: "default_copies",
                table: "print_routing_policies");

            migrationBuilder.DropColumn(
                name: "receipt_settings_override_json",
                table: "print_routing_policies");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "print_routing_policies");
        }
    }
}
