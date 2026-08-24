using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiSimRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sms_gateway_devices_branch_id_device_id_sim_subscription_id",
                table: "sms_gateway_devices");

            migrationBuilder.AddColumn<DateTime>(
                name: "available_at",
                table: "sms_gateway_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "notification_delivery_attempt_id",
                table: "sms_gateway_jobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "notification_delivery_id",
                table: "sms_gateway_jobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "retry_of_job_id",
                table: "sms_gateway_jobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "sticky_device_id",
                table: "sms_gateway_jobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "waiting_reason",
                table: "sms_gateway_jobs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_sms_routes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    last_device_id = table.Column<long>(type: "bigint", nullable: false),
                    last_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_sms_routes", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_sms_routes_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_sms_routes_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_sms_routes_sms_gateway_devices_last_device_id",
                        column: x => x.last_device_id,
                        principalTable: "sms_gateway_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_branch_id_customer_id_created_at",
                table: "sms_gateway_jobs",
                columns: new[] { "branch_id", "customer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_notification_delivery_attempt_id",
                table: "sms_gateway_jobs",
                column: "notification_delivery_attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_notification_delivery_id",
                table: "sms_gateway_jobs",
                column: "notification_delivery_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_retry_of_job_id",
                table: "sms_gateway_jobs",
                column: "retry_of_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_jobs_sticky_device_id",
                table: "sms_gateway_jobs",
                column: "sticky_device_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_devices_branch_id_device_id_sim_slot",
                table: "sms_gateway_devices",
                columns: new[] { "branch_id", "device_id", "sim_slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_sms_routes_branch_id_customer_id",
                table: "customer_sms_routes",
                columns: new[] { "branch_id", "customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_sms_routes_customer_id",
                table: "customer_sms_routes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_sms_routes_last_device_id",
                table: "customer_sms_routes",
                column: "last_device_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sms_gateway_jobs_notification_deliveries_notification_deliv",
                table: "sms_gateway_jobs",
                column: "notification_delivery_id",
                principalTable: "notification_deliveries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sms_gateway_jobs_notification_delivery_attempts_notificatio",
                table: "sms_gateway_jobs",
                column: "notification_delivery_attempt_id",
                principalTable: "notification_delivery_attempts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sms_gateway_jobs_sms_gateway_devices_sticky_device_id",
                table: "sms_gateway_jobs",
                column: "sticky_device_id",
                principalTable: "sms_gateway_devices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_sms_gateway_jobs_sms_gateway_jobs_retry_of_job_id",
                table: "sms_gateway_jobs",
                column: "retry_of_job_id",
                principalTable: "sms_gateway_jobs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sms_gateway_jobs_notification_deliveries_notification_deliv",
                table: "sms_gateway_jobs");

            migrationBuilder.DropForeignKey(
                name: "fk_sms_gateway_jobs_notification_delivery_attempts_notificatio",
                table: "sms_gateway_jobs");

            migrationBuilder.DropForeignKey(
                name: "fk_sms_gateway_jobs_sms_gateway_devices_sticky_device_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropForeignKey(
                name: "fk_sms_gateway_jobs_sms_gateway_jobs_retry_of_job_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropTable(
                name: "customer_sms_routes");

            migrationBuilder.DropIndex(
                name: "ix_sms_gateway_jobs_branch_id_customer_id_created_at",
                table: "sms_gateway_jobs");

            migrationBuilder.DropIndex(
                name: "ix_sms_gateway_jobs_notification_delivery_attempt_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropIndex(
                name: "ix_sms_gateway_jobs_notification_delivery_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropIndex(
                name: "ix_sms_gateway_jobs_retry_of_job_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropIndex(
                name: "ix_sms_gateway_jobs_sticky_device_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropIndex(
                name: "ix_sms_gateway_devices_branch_id_device_id_sim_slot",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "available_at",
                table: "sms_gateway_jobs");

            migrationBuilder.DropColumn(
                name: "notification_delivery_attempt_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropColumn(
                name: "notification_delivery_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropColumn(
                name: "retry_of_job_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropColumn(
                name: "sticky_device_id",
                table: "sms_gateway_jobs");

            migrationBuilder.DropColumn(
                name: "waiting_reason",
                table: "sms_gateway_jobs");

            migrationBuilder.CreateIndex(
                name: "ix_sms_gateway_devices_branch_id_device_id_sim_subscription_id",
                table: "sms_gateway_devices",
                columns: new[] { "branch_id", "device_id", "sim_subscription_id" },
                unique: true);
        }
    }
}
