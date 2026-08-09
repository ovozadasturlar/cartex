using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "trade_case_id",
                table: "transactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "trade_case_id",
                table: "sales",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "trade_case_id",
                table: "customer_payment_documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trade_cases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    case_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    site_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    workflow = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    price_policy = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    settled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trade_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_trade_cases_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_cases_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_cases_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_issue_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    trade_case_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    estimated_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_issue_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_goods_issue_documents_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_issue_documents_trade_cases_trade_case_id",
                        column: x => x.trade_case_id,
                        principalTable: "trade_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_issue_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_issue_documents_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_return_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    trade_case_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_return_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_goods_return_documents_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_return_documents_trade_cases_trade_case_id",
                        column: x => x.trade_case_id,
                        principalTable: "trade_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_return_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_return_documents_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trade_case_settlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    trade_case_id = table.Column<long>(type: "bigint", nullable: false),
                    sale_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trade_case_settlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_trade_case_settlements_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_case_settlements_trade_cases_trade_case_id",
                        column: x => x.trade_case_id,
                        principalTable: "trade_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_case_settlements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_issue_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    goods_issue_document_id = table.Column<long>(type: "bigint", nullable: false),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    stock_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    returned_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    settled_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    purchase_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_issue_lines", x => x.id);
                    table.CheckConstraint("ck_goods_issue_lines_balances", "\"returned_quantity\" >= 0 AND \"settled_quantity\" >= 0 AND \"returned_quantity\" + \"settled_quantity\" <= \"quantity\"");
                    table.CheckConstraint("ck_goods_issue_lines_quantity", "\"quantity\" > 0");
                    table.ForeignKey(
                        name: "fk_goods_issue_lines_goods_issue_documents_goods_issue_documen",
                        column: x => x.goods_issue_document_id,
                        principalTable: "goods_issue_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_goods_issue_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_issue_lines_stocks_stock_id",
                        column: x => x.stock_id,
                        principalTable: "stocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_return_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    goods_return_document_id = table.Column<long>(type: "bigint", nullable: false),
                    goods_issue_line_id = table.Column<long>(type: "bigint", nullable: false),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    condition = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    disposition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_return_lines", x => x.id);
                    table.CheckConstraint("ck_goods_return_lines_quantity", "\"quantity\" > 0");
                    table.ForeignKey(
                        name: "fk_goods_return_lines_goods_issue_lines_goods_issue_line_id",
                        column: x => x.goods_issue_line_id,
                        principalTable: "goods_issue_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_return_lines_goods_return_documents_goods_return_docu",
                        column: x => x.goods_return_document_id,
                        principalTable: "goods_return_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_goods_return_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_trade_case_id",
                table: "transactions",
                column: "trade_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_trade_case_id",
                table: "sales",
                column: "trade_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_documents_trade_case_id",
                table: "customer_payment_documents",
                column: "trade_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_documents_branch_id_idempotency_key",
                table: "goods_issue_documents",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_documents_customer_id",
                table: "goods_issue_documents",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_documents_document_number",
                table: "goods_issue_documents",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_documents_trade_case_id_business_date_id",
                table: "goods_issue_documents",
                columns: new[] { "trade_case_id", "business_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_documents_user_id",
                table: "goods_issue_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_documents_warehouse_id",
                table: "goods_issue_documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_lines_goods_issue_document_id_variant_id",
                table: "goods_issue_lines",
                columns: new[] { "goods_issue_document_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_lines_stock_id",
                table: "goods_issue_lines",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_lines_variant_id",
                table: "goods_issue_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_documents_branch_id_idempotency_key",
                table: "goods_return_documents",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_documents_customer_id",
                table: "goods_return_documents",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_documents_document_number",
                table: "goods_return_documents",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_documents_trade_case_id_business_date_id",
                table: "goods_return_documents",
                columns: new[] { "trade_case_id", "business_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_documents_user_id",
                table: "goods_return_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_documents_warehouse_id",
                table: "goods_return_documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_lines_goods_issue_line_id",
                table: "goods_return_lines",
                column: "goods_issue_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_lines_goods_return_document_id",
                table: "goods_return_lines",
                column: "goods_return_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_return_lines_variant_id",
                table: "goods_return_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_settlements_branch_id_idempotency_key",
                table: "trade_case_settlements",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_settlements_document_number",
                table: "trade_case_settlements",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_settlements_sale_id",
                table: "trade_case_settlements",
                column: "sale_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_settlements_trade_case_id",
                table: "trade_case_settlements",
                column: "trade_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_settlements_user_id",
                table: "trade_case_settlements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_cases_branch_id_idempotency_key",
                table: "trade_cases",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trade_cases_branch_id_status_id",
                table: "trade_cases",
                columns: new[] { "branch_id", "status", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_trade_cases_case_number",
                table: "trade_cases",
                column: "case_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_cases_customer_id_status_business_date",
                table: "trade_cases",
                columns: new[] { "customer_id", "status", "business_date" });

            migrationBuilder.CreateIndex(
                name: "ix_trade_cases_warehouse_id",
                table: "trade_cases",
                column: "warehouse_id");

            migrationBuilder.AddForeignKey(
                name: "fk_customer_payment_documents_trade_cases_trade_case_id",
                table: "customer_payment_documents",
                column: "trade_case_id",
                principalTable: "trade_cases",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sales_trade_cases_trade_case_id",
                table: "sales",
                column: "trade_case_id",
                principalTable: "trade_cases",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_trade_cases_trade_case_id",
                table: "transactions",
                column: "trade_case_id",
                principalTable: "trade_cases",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customer_payment_documents_trade_cases_trade_case_id",
                table: "customer_payment_documents");

            migrationBuilder.DropForeignKey(
                name: "fk_sales_trade_cases_trade_case_id",
                table: "sales");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_trade_cases_trade_case_id",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "goods_return_lines");

            migrationBuilder.DropTable(
                name: "trade_case_settlements");

            migrationBuilder.DropTable(
                name: "goods_issue_lines");

            migrationBuilder.DropTable(
                name: "goods_return_documents");

            migrationBuilder.DropTable(
                name: "goods_issue_documents");

            migrationBuilder.DropTable(
                name: "trade_cases");

            migrationBuilder.DropIndex(
                name: "ix_transactions_trade_case_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_sales_trade_case_id",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_customer_payment_documents_trade_case_id",
                table: "customer_payment_documents");

            migrationBuilder.DropColumn(
                name: "trade_case_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "trade_case_id",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "trade_case_id",
                table: "customer_payment_documents");
        }
    }
}
