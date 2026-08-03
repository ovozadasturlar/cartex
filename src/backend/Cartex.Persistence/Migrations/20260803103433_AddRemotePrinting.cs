using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemotePrinting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "print_nodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    credential_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    credential_issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_trusted = table.Column<bool>(type: "boolean", nullable: false),
                    host_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_disconnected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_user_id = table.Column<long>(type: "bigint", nullable: true),
                    last_client = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    last_ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_print_nodes_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_nodes_users_last_user_id",
                        column: x => x.last_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "printer_endpoints",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    print_node_id = table.Column<long>(type: "bigint", nullable: false),
                    stable_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    system_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    capabilities = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    profile_json = table.Column<string>(type: "jsonb", nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_success_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_failure_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_printer_endpoints", x => x.id);
                    table.ForeignKey(
                        name: "fk_printer_endpoints_print_nodes_print_node_id",
                        column: x => x.print_node_id,
                        principalTable: "print_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "print_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    copies = table.Column<int>(type: "integer", nullable: false),
                    is_reprint = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requested_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    requested_device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_client = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    requested_ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    requested_user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    origin_node_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_node_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_endpoint_id = table.Column<long>(type: "bigint", nullable: true),
                    lease_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_print_jobs_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_jobs_print_nodes_assigned_node_id",
                        column: x => x.assigned_node_id,
                        principalTable: "print_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_print_jobs_print_nodes_origin_node_id",
                        column: x => x.origin_node_id,
                        principalTable: "print_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_print_jobs_printer_endpoints_assigned_endpoint_id",
                        column: x => x.assigned_endpoint_id,
                        principalTable: "printer_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_print_jobs_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "print_routing_policies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    routing_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    allow_fallback = table.Column<bool>(type: "boolean", nullable: false),
                    sticky_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sticky_duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    sticky_endpoint_id = table.Column<long>(type: "bigint", nullable: true),
                    sticky_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    max_copies = table.Column<int>(type: "integer", nullable: false),
                    max_jobs_per_minute = table.Column<int>(type: "integer", nullable: false),
                    max_copies_per_minute = table.Column<int>(type: "integer", nullable: false),
                    assignment_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    require_trusted_node = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_routing_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_print_routing_policies_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_print_routing_policies_printer_endpoints_sticky_endpoint_id",
                        column: x => x.sticky_endpoint_id,
                        principalTable: "printer_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "print_attempts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    print_job_id = table.Column<long>(type: "bigint", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    print_node_id = table.Column<long>(type: "bigint", nullable: false),
                    printer_endpoint_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    lease_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    spool_job_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_print_attempts_print_jobs_print_job_id",
                        column: x => x.print_job_id,
                        principalTable: "print_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_print_attempts_print_nodes_print_node_id",
                        column: x => x.print_node_id,
                        principalTable: "print_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_attempts_printer_endpoints_printer_endpoint_id",
                        column: x => x.printer_endpoint_id,
                        principalTable: "printer_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "print_route_targets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    print_routing_policy_id = table.Column<long>(type: "bigint", nullable: false),
                    printer_endpoint_id = table.Column<long>(type: "bigint", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_route_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_print_route_targets_print_routing_policies_print_routing_po",
                        column: x => x.print_routing_policy_id,
                        principalTable: "print_routing_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_print_route_targets_printer_endpoints_printer_endpoint_id",
                        column: x => x.printer_endpoint_id,
                        principalTable: "printer_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_print_attempts_print_job_id_attempt_number",
                table: "print_attempts",
                columns: new[] { "print_job_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_attempts_print_node_id",
                table: "print_attempts",
                column: "print_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_attempts_printer_endpoint_id_status_started_at",
                table: "print_attempts",
                columns: new[] { "printer_endpoint_id", "status", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_assigned_endpoint_id",
                table: "print_jobs",
                column: "assigned_endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_assigned_node_id_status",
                table: "print_jobs",
                columns: new[] { "assigned_node_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_branch_id_kind_idempotency_key",
                table: "print_jobs",
                columns: new[] { "branch_id", "kind", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_branch_id_kind_status_created_at",
                table: "print_jobs",
                columns: new[] { "branch_id", "kind", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_origin_node_id",
                table: "print_jobs",
                column: "origin_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_requested_by_user_id",
                table: "print_jobs",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_nodes_branch_id_is_enabled_status",
                table: "print_nodes",
                columns: new[] { "branch_id", "is_enabled", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_print_nodes_device_id",
                table: "print_nodes",
                column: "device_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_nodes_last_user_id",
                table: "print_nodes",
                column: "last_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_route_targets_print_routing_policy_id_printer_endpoin",
                table: "print_route_targets",
                columns: new[] { "print_routing_policy_id", "printer_endpoint_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_route_targets_print_routing_policy_id_priority",
                table: "print_route_targets",
                columns: new[] { "print_routing_policy_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_print_route_targets_printer_endpoint_id",
                table: "print_route_targets",
                column: "printer_endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_routing_policies_branch_id_kind",
                table: "print_routing_policies",
                columns: new[] { "branch_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_routing_policies_sticky_endpoint_id",
                table: "print_routing_policies",
                column: "sticky_endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_printer_endpoints_is_enabled_status",
                table: "printer_endpoints",
                columns: new[] { "is_enabled", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_printer_endpoints_print_node_id_stable_key",
                table: "printer_endpoints",
                columns: new[] { "print_node_id", "stable_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_attempts");

            migrationBuilder.DropTable(
                name: "print_route_targets");

            migrationBuilder.DropTable(
                name: "print_jobs");

            migrationBuilder.DropTable(
                name: "print_routing_policies");

            migrationBuilder.DropTable(
                name: "printer_endpoints");

            migrationBuilder.DropTable(
                name: "print_nodes");
        }
    }
}
