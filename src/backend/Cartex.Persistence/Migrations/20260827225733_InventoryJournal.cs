using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_positions");

            migrationBuilder.DropIndex(
                name: "ix_inventory_movements_branch_id_occurred_at",
                table: "inventory_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_movements_quantity",
                table: "inventory_movements");

            migrationBuilder.AlterColumn<long>(
                name: "user_id",
                table: "inventory_movements",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "source_id",
                table: "inventory_movements",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "stock_id",
                table: "inventory_movements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "warehouse_id",
                table: "inventory_movements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_branch_id_variant_id_warehouse_id_occur",
                table: "inventory_movements",
                columns: new[] { "branch_id", "variant_id", "warehouse_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_stock_id",
                table: "inventory_movements",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_warehouse_id",
                table: "inventory_movements",
                column: "warehouse_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_movements_quantity",
                table: "inventory_movements",
                sql: "\"quantity\" <> 0");

            migrationBuilder.AddForeignKey(
                name: "fk_inventory_movements_stocks_stock_id",
                table: "inventory_movements",
                column: "stock_id",
                principalTable: "stocks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_inventory_movements_warehouses_warehouse_id",
                table: "inventory_movements",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_inventory_movements_stocks_stock_id",
                table: "inventory_movements");

            migrationBuilder.DropForeignKey(
                name: "fk_inventory_movements_warehouses_warehouse_id",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_inventory_movements_branch_id_variant_id_warehouse_id_occur",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_inventory_movements_stock_id",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_inventory_movements_warehouse_id",
                table: "inventory_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_movements_quantity",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "stock_id",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "inventory_movements");

            migrationBuilder.AlterColumn<long>(
                name: "user_id",
                table: "inventory_movements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "source_id",
                table: "inventory_movements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "inventory_positions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    location_id = table.Column<long>(type: "bigint", nullable: false),
                    location_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_positions", x => x.id);
                    table.CheckConstraint("ck_inventory_positions_quantity", "\"quantity\" >= 0");
                    table.ForeignKey(
                        name: "fk_inventory_positions_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_positions_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_branch_id_occurred_at",
                table: "inventory_movements",
                columns: new[] { "branch_id", "occurred_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_movements_quantity",
                table: "inventory_movements",
                sql: "\"quantity\" > 0");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_positions_branch_id_location_kind_location_id_var",
                table: "inventory_positions",
                columns: new[] { "branch_id", "location_kind", "location_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_positions_variant_id",
                table: "inventory_positions",
                column: "variant_id");
        }
    }
}
