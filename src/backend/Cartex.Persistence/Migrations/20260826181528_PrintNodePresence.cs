using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrintNodePresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_print_nodes_branch_id_is_trusted_status",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "last_connected_at",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "last_disconnected_at",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "status",
                table: "print_nodes");

            migrationBuilder.CreateIndex(
                name: "ix_print_nodes_branch_id_is_trusted",
                table: "print_nodes",
                columns: new[] { "branch_id", "is_trusted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_print_nodes_branch_id_is_trusted",
                table: "print_nodes");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_connected_at",
                table: "print_nodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_disconnected_at",
                table: "print_nodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "print_nodes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_print_nodes_branch_id_is_trusted_status",
                table: "print_nodes",
                columns: new[] { "branch_id", "is_trusted", "status" });
        }
    }
}
