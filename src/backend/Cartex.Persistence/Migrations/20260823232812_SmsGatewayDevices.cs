using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmsGatewayDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_marketing_sms",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "sms_gateway_devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    credential_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    credential_issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sim_slot = table.Column<int>(type: "integer", nullable: false),
                    sim_operator = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sim_subscription_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_trusted = table.Column<bool>(type: "boolean", nullable: false),
                    is_consented = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    monthly_quota = table.Column<int>(type: "integer", nullable: true),
                    quota_reset_day = table.Column<int>(type: "integer", nullable: false),
                    sent_this_period = table.Column<int>(type: "integer", nullable: false),
                    period_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    max_per_hour = table.Column<int>(type: "integer", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    last_user_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sms_gateway_devices", x => x.id);
                    table.CheckConstraint("ck_sms_gateway_max_hour", "max_per_hour BETWEEN 1 AND 300");
                    table.CheckConstraint("ck_sms_gateway_min_interval", "min_interval_seconds >= 2");
                    table.CheckConstraint("ck_sms_gateway_monthly_quota", "monthly_quota IS NULL OR monthly_quota >= 0");
                    table.CheckConstraint("ck_sms_gateway_quota_reset_day", "quota_reset_day BETWEEN 1 AND 28");
                    table.CheckConstraint("ck_sms_gateway_sent_period", "sent_this_period >= 0");
                    table.ForeignKey(
                        name: "fk_sms_gateway_devices_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sms_gateway_devices_users_last_user_id",
                        column: x => x.last_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sms_gateway_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    assigned_device_id = table.Column<long>(type: "bigint", nullable: true),
                    lease_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    segment_count = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fallback_provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fallback_message_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sms_gateway_jobs", x => x.id);
                    table.CheckConstraint("ck_sms_gateway_job_segments", "segment_count > 0");
                    table.ForeignKey(
                        name: "fk_sms_gateway_jobs_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sms_gateway_jobs_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sms_gateway_jobs_sms_gateway_devices_assigned_device_id",
                        column: x => x.assigned_device_id,
                        principalTable: "sms_gateway_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_devices_branch_id_device_id_sim_subscription_id",
                table: "sms_gateway_devices",
                columns: new[] { "branch_id", "device_id", "sim_subscription_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_devices_branch_id_is_trusted_is_consented_is_en",
                table: "sms_gateway_devices",
                columns: new[] { "branch_id", "is_trusted", "is_consented", "is_enabled", "last_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_devices_last_user_id",
                table: "sms_gateway_devices",
                column: "last_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_assigned_device_id_status",
                table: "sms_gateway_jobs",
                columns: new[] { "assigned_device_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_branch_id_status_created_at",
                table: "sms_gateway_jobs",
                columns: new[] { "branch_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_customer_id",
                table: "sms_gateway_jobs",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_idempotency_key",
                table: "sms_gateway_jobs",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sms_gateway_jobs");

            migrationBuilder.DropTable(
                name: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "allow_marketing_sms",
                table: "customers");
        }
    }
}
