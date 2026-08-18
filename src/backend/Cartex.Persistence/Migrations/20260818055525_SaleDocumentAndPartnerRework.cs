using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleDocumentAndPartnerRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rounding_amount",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "rounding_amount",
                table: "carts");

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                table: "sales",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "public_about",
                table: "partner_profiles",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "public_consent",
                table: "partner_profiles",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "public_consent_at",
                table: "partner_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "public_consent_by_user_id",
                table: "partner_profiles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "public_consent_source",
                table: "partner_profiles",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "public_display_name",
                table: "partner_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "public_phone_visible",
                table: "partner_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "public_visible",
                table: "partner_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "owner_enabled",
                table: "features",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "credit_limit",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_number",
                table: "sales",
                column: "document_number",
                unique: true,
                filter: "\"document_number\" <> ''");

            migrationBuilder.CreateIndex(
                name: "ix_partner_profiles_public_visible",
                table: "partner_profiles",
                column: "public_visible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_document_number",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_partner_profiles_public_visible",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "document_number",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "public_about",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "public_consent",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "public_consent_at",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "public_consent_by_user_id",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "public_consent_source",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "public_display_name",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "public_phone_visible",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "public_visible",
                table: "partner_profiles");

            migrationBuilder.DropColumn(
                name: "owner_enabled",
                table: "features");

            migrationBuilder.AddColumn<decimal>(
                name: "rounding_amount",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "credit_limit",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rounding_amount",
                table: "carts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
