using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyPrintDeviceTrust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_print_nodes_branch_id_is_enabled_status",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "require_trusted_node",
                table: "print_routing_policies");

            migrationBuilder.DropColumn(
                name: "require_trusted_requester_device",
                table: "print_routing_policies");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "print_nodes");

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

            migrationBuilder.AddColumn<bool>(
                name: "auto_trust_print_devices",
                table: "branches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_print_nodes_branch_id_is_trusted_status",
                table: "print_nodes",
                columns: new[] { "branch_id", "is_trusted", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_print_nodes_branch_id_is_trusted_status",
                table: "print_nodes");

            migrationBuilder.DropColumn(
                name: "auto_trust_print_devices",
                table: "branches");

            migrationBuilder.AddColumn<bool>(
                name: "require_trusted_node",
                table: "print_routing_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "require_trusted_requester_device",
                table: "print_routing_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "print_nodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.CreateIndex(
                name: "ix_print_nodes_branch_id_is_enabled_status",
                table: "print_nodes",
                columns: new[] { "branch_id", "is_enabled", "status" });
        }
    }
}
