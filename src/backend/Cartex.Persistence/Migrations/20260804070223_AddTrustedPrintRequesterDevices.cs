using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustedPrintRequesterDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_trusted_requester_device",
                table: "print_routing_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "print_requester_devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_trusted = table.Column<bool>(type: "boolean", nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_user_id = table.Column<long>(type: "bigint", nullable: true),
                    last_ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_requester_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_print_requester_devices_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_print_requester_devices_users_last_user_id",
                        column: x => x.last_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_print_requester_devices_branch_id_device_id",
                table: "print_requester_devices",
                columns: new[] { "branch_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_requester_devices_branch_id_is_trusted",
                table: "print_requester_devices",
                columns: new[] { "branch_id", "is_trusted" });

            migrationBuilder.CreateIndex(
                name: "ix_print_requester_devices_last_user_id",
                table: "print_requester_devices",
                column: "last_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_requester_devices");

            migrationBuilder.DropColumn(
                name: "require_trusted_requester_device",
                table: "print_routing_policies");
        }
    }
}
