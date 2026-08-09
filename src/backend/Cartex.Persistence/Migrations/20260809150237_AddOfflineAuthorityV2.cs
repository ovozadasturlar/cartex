using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineAuthorityV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offline_authority_leases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    business_id = table.Column<long>(type: "bigint", nullable: false),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    epoch = table.Column<long>(type: "bigint", nullable: false),
                    last_accepted_sequence = table.Column<long>(type: "bigint", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_heartbeat_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offline_authority_leases", x => x.id);
                    table.ForeignKey(
                        name: "fk_offline_authority_leases_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_offline_authority_leases_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_offline_authority_leases_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_offline_authority_leases_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offline_sync_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    offline_authority_lease_id = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_user_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_entity_id = table.Column<long>(type: "bigint", nullable: true),
                    result_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    device_occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offline_sync_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_offline_sync_events_offline_authority_leases_offline_author",
                        column: x => x.offline_authority_lease_id,
                        principalTable: "offline_authority_leases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_offline_sync_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_offline_authority_leases_branch_id",
                table: "offline_authority_leases",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_offline_authority_leases_business_id",
                table: "offline_authority_leases",
                column: "business_id",
                unique: true,
                filter: "\"revoked_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_offline_authority_leases_device_id_revoked_at",
                table: "offline_authority_leases",
                columns: new[] { "device_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_offline_authority_leases_revoked_by_user_id",
                table: "offline_authority_leases",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_offline_authority_leases_warehouse_id_revoked_at",
                table: "offline_authority_leases",
                columns: new[] { "warehouse_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_offline_sync_events_actor_user_id",
                table: "offline_sync_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_offline_sync_events_event_id",
                table: "offline_sync_events",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offline_sync_events_offline_authority_lease_id_processed_at",
                table: "offline_sync_events",
                columns: new[] { "offline_authority_lease_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_offline_sync_events_offline_authority_lease_id_sequence",
                table: "offline_sync_events",
                columns: new[] { "offline_authority_lease_id", "sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offline_sync_events");

            migrationBuilder.DropTable(
                name: "offline_authority_leases");
        }
    }
}
