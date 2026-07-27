using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchCatalogPolicyAndSaleResilience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stocks_warehouse_id_variant_id",
                table: "stocks");

            migrationBuilder.DropIndex(
                name: "ix_carts_customer_id_idempotency_key",
                table: "carts");

            migrationBuilder.AddColumn<bool>(
                name: "is_deficit",
                table: "stocks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "branch_catalog_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    first_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activation_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    visibility_override = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_catalog_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_branch_catalog_entries_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_catalog_entries_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stocks_warehouse_id_variant_id",
                table: "stocks",
                columns: new[] { "warehouse_id", "variant_id" },
                unique: true,
                filter: "\"is_deficit\" AND NOT \"is_deleted\"");

            migrationBuilder.CreateIndex(
                name: "ix_carts_customer_id",
                table: "carts",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_idempotency_key",
                table: "carts",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_branch_catalog_entries_branch_id_variant_id",
                table: "branch_catalog_entries",
                columns: new[] { "branch_id", "variant_id" },
                unique: true,
                filter: "NOT \"is_deleted\"");

            migrationBuilder.CreateIndex(
                name: "ix_branch_catalog_entries_branch_id_visibility_override",
                table: "branch_catalog_entries",
                columns: new[] { "branch_id", "visibility_override" });

            migrationBuilder.CreateIndex(
                name: "ix_branch_catalog_entries_variant_id",
                table: "branch_catalog_entries",
                column: "variant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_catalog_entries");

            migrationBuilder.DropIndex(
                name: "ix_stocks_warehouse_id_variant_id",
                table: "stocks");

            migrationBuilder.DropIndex(
                name: "ix_carts_customer_id",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_idempotency_key",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "is_deficit",
                table: "stocks");

            migrationBuilder.CreateIndex(
                name: "ix_stocks_warehouse_id_variant_id",
                table: "stocks",
                columns: new[] { "warehouse_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_carts_customer_id_idempotency_key",
                table: "carts",
                columns: new[] { "customer_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }
    }
}
