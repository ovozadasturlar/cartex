using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrintNodeEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pending_client",
                table: "print_nodes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_credential_hash",
                table: "print_nodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_ip_address",
                table: "print_nodes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pending_requested_at",
                table: "print_nodes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pending_client",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "pending_credential_hash",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "pending_ip_address",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "pending_requested_at",
                table: "print_nodes");
        }
    }
}
