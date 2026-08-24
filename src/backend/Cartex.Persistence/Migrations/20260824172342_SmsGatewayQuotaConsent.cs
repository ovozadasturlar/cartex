using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmsGatewayQuotaConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "simulated_at",
                table: "sms_gateway_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "quota_reset_day",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "min_interval_seconds",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                defaultValue: 4,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "max_per_hour",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                defaultValue: 60,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "is_trusted",
                table: "sms_gateway_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_enabled",
                table: "sms_gateway_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_consented",
                table: "sms_gateway_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<DateTime>(
                name: "consented_at",
                table: "sms_gateway_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "consented_max_per_hour",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "consented_min_interval_seconds",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "consented_monthly_quota",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "low_quota_warn_percent",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<DateTime>(
                name: "low_quota_warned_period_started_at",
                table: "sms_gateway_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "paused_at",
                table: "sms_gateway_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_gateway_consented_max_hour",
                table: "sms_gateway_devices",
                sql: "consented_max_per_hour BETWEEN 1 AND 300");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_gateway_consented_min_interval",
                table: "sms_gateway_devices",
                sql: "consented_min_interval_seconds >= 2");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_gateway_low_quota_warn",
                table: "sms_gateway_devices",
                sql: "low_quota_warn_percent BETWEEN 1 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_gateway_consented_max_hour",
                table: "sms_gateway_devices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_gateway_consented_min_interval",
                table: "sms_gateway_devices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_gateway_low_quota_warn",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "simulated_at",
                table: "sms_gateway_jobs");

            migrationBuilder.DropColumn(
                name: "consented_at",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "consented_max_per_hour",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "consented_min_interval_seconds",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "consented_monthly_quota",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "low_quota_warn_percent",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "low_quota_warned_period_started_at",
                table: "sms_gateway_devices");

            migrationBuilder.DropColumn(
                name: "paused_at",
                table: "sms_gateway_devices");

            migrationBuilder.AlterColumn<int>(
                name: "quota_reset_day",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "min_interval_seconds",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 4);

            migrationBuilder.AlterColumn<int>(
                name: "max_per_hour",
                table: "sms_gateway_devices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 60);

            migrationBuilder.AlterColumn<bool>(
                name: "is_trusted",
                table: "sms_gateway_devices",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "is_enabled",
                table: "sms_gateway_devices",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "is_consented",
                table: "sms_gateway_devices",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
