using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeliveryControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                table: "debt_reminder_log",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "debt_reminder_log",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    purpose = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_deliveries_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery_attempts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    notification_delivery_id = table.Column<long>(type: "bigint", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    units = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_delivery_attempts_notification_deliveries_noti",
                        column: x => x.notification_delivery_id,
                        principalTable: "notification_deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_debt_reminder_log_customer_id_purpose_due_date",
                table: "debt_reminder_log",
                columns: new[] { "customer_id", "purpose", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_channel_status_created_at",
                table: "notification_deliveries",
                columns: new[] { "channel", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_created_at",
                table: "notification_deliveries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_customer_id",
                table: "notification_deliveries",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_notification_delivery_id_att",
                table: "notification_delivery_attempts",
                columns: new[] { "notification_delivery_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_provider_message_id",
                table: "notification_delivery_attempts",
                column: "provider_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_provider_status_started_at",
                table: "notification_delivery_attempts",
                columns: new[] { "provider", "status", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_delivery_attempts");

            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropIndex(
                name: "ix_debt_reminder_log_customer_id_purpose_due_date",
                table: "debt_reminder_log");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "debt_reminder_log");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "debt_reminder_log");
        }
    }
}
