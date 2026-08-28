using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockWriteOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "stock_write_off_document_id",
                table: "transactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "accepts_returns",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "stock_write_off_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reverses_document_id = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_write_off_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_write_off_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_write_off_documents_stock_write_off_documents_reverse",
                        column: x => x.reverses_document_id,
                        principalTable: "stock_write_off_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_write_off_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_write_off_documents_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_write_off_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    stock_write_off_document_id = table.Column<long>(type: "bigint", nullable: false),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    stock_id = table.Column<long>(type: "bigint", nullable: false),
                    supplier_id = table.Column<long>(type: "bigint", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    claim_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    claim_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reason = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    disposition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_write_off_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_write_off_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_write_off_lines_stock_write_off_documents_stock_write",
                        column: x => x.stock_write_off_document_id,
                        principalTable: "stock_write_off_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stock_write_off_lines_stocks_stock_id",
                        column: x => x.stock_id,
                        principalTable: "stocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_write_off_lines_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_stock_write_off_document_id",
                table: "transactions",
                column: "stock_write_off_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_documents_branch_id_business_date_id",
                table: "stock_write_off_documents",
                columns: new[] { "branch_id", "business_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_documents_branch_id_idempotency_key",
                table: "stock_write_off_documents",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_documents_document_number",
                table: "stock_write_off_documents",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_documents_reverses_document_id",
                table: "stock_write_off_documents",
                column: "reverses_document_id",
                unique: true,
                filter: "\"reverses_document_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_documents_user_id",
                table: "stock_write_off_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_documents_warehouse_id",
                table: "stock_write_off_documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_lines_stock_id",
                table: "stock_write_off_lines",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_lines_stock_write_off_document_id",
                table: "stock_write_off_lines",
                column: "stock_write_off_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_lines_supplier_id",
                table: "stock_write_off_lines",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_write_off_lines_variant_id_reason",
                table: "stock_write_off_lines",
                columns: new[] { "variant_id", "reason" });

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_stock_write_off_documents_stock_write_off_docu",
                table: "transactions",
                column: "stock_write_off_document_id",
                principalTable: "stock_write_off_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_stock_write_off_documents_stock_write_off_docu",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "stock_write_off_lines");

            migrationBuilder.DropTable(
                name: "stock_write_off_documents");

            migrationBuilder.DropIndex(
                name: "ix_transactions_stock_write_off_document_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "stock_write_off_document_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "accepts_returns",
                table: "suppliers");
        }
    }
}
