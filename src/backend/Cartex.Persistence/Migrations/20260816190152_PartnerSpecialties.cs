using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartnerSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "partner_specialties",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    business_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_specialties", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_specialties_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_specialty_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partner_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    partner_specialty_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_specialty_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_specialty_links_partner_profiles_partner_profile_id",
                        column: x => x.partner_profile_id,
                        principalTable: "partner_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_partner_specialty_links_partner_specialties_partner_special",
                        column: x => x.partner_specialty_id,
                        principalTable: "partner_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_partner_specialties_business_id_is_enabled_sort_order",
                table: "partner_specialties",
                columns: new[] { "business_id", "is_enabled", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_specialties_business_id_name",
                table: "partner_specialties",
                columns: new[] { "business_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_specialty_links_partner_profile_id_partner_specialt",
                table: "partner_specialty_links",
                columns: new[] { "partner_profile_id", "partner_specialty_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_specialty_links_partner_specialty_id",
                table: "partner_specialty_links",
                column: "partner_specialty_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partner_specialty_links");

            migrationBuilder.DropTable(
                name: "partner_specialties");
        }
    }
}
