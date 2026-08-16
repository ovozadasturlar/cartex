using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartnerPublicity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "ix_partner_profiles_public_visible",
                table: "partner_profiles",
                column: "public_visible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_partner_profiles_public_visible",
                table: "partner_profiles");

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
        }
    }
}
