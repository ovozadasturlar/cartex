using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customers_users_agent_id",
                table: "customers");

            migrationBuilder.RenameColumn(
                name: "agent_id",
                table: "customers",
                newName: "assigned_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_customers_agent_id",
                table: "customers",
                newName: "ix_customers_assigned_user_id");

            migrationBuilder.CreateSequence(
                name: "document_number_seq");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "transactions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<long>(
                name: "customer_payment_document_id",
                table: "transactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "customer_return_document_id",
                table: "transactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "sale_payments",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "cashback_earned",
                table: "sale_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "returned_cashback",
                table: "sale_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "balance",
                table: "accounts",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            // Customer advances used to be represented as a negative debt balance.
            // Move them to a dedicated liability account without losing native currency.
            migrationBuilder.Sql(
                """
                INSERT INTO accounts
                    (customer_id, name, type, currency, balance, created_at, is_deleted)
                SELECT customer_id, 'Mijoz avansi', 'CustomerAdvance', currency, -balance, NOW(), FALSE
                FROM accounts
                WHERE customer_id IS NOT NULL AND type = 'Debt' AND balance < 0 AND NOT is_deleted
                ON CONFLICT (customer_id, type, currency) WHERE customer_id IS NOT NULL
                DO UPDATE SET balance = accounts.balance + EXCLUDED.balance,
                              updated_at = NOW();

                UPDATE accounts
                SET balance = 0, updated_at = NOW()
                WHERE customer_id IS NOT NULL AND type = 'Debt' AND balance < 0 AND NOT is_deleted;
                """);

            // Historical sales did not persist line-level cashback. A proportional
            // snapshot preserves the sale total and enables deterministic future returns.
            migrationBuilder.Sql(
                """
                UPDATE sale_items AS item
                SET cashback_earned = ROUND(
                    sale.cashback_earned * (item.quantity * item.unit_price)
                    / NULLIF(lines.gross_amount, 0), 2)
                FROM sales AS sale
                JOIN (
                    SELECT sale_id, SUM(quantity * unit_price) AS gross_amount
                    FROM sale_items
                    GROUP BY sale_id
                ) AS lines ON lines.sale_id = sale.id
                WHERE item.sale_id = sale.id AND sale.cashback_earned > 0;
                """);

            migrationBuilder.CreateTable(
                name: "customer_payment_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    total_base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    allocated_base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    advance_base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_payment_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_payment_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_payment_documents_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_payment_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_return_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    sale_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cashback_reversed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_full_return = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_return_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_return_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_return_documents_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_return_documents_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_return_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_return_documents_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_location_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_location_id = table.Column<long>(type: "bigint", nullable: false),
                    to_location_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_location_id = table.Column<long>(type: "bigint", nullable: false),
                    source_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_movements", x => x.id);
                    table.CheckConstraint("ck_inventory_movements_quantity", "\"quantity\" > 0");
                    table.ForeignKey(
                        name: "fk_inventory_movements_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_movements_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_movements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_positions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    location_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    location_id = table.Column<long>(type: "bigint", nullable: false),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "customer_payment_allocations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_payment_document_id = table.Column<long>(type: "bigint", nullable: false),
                    sale_id = table.Column<long>(type: "bigint", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    amount_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_payment_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_payment_allocations_customer_payment_documents_cus",
                        column: x => x.customer_payment_document_id,
                        principalTable: "customer_payment_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_payment_allocations_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_payment_tenders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_payment_document_id = table.Column<long>(type: "bigint", nullable: false),
                    method = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    amount_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_payment_tenders", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_payment_tenders_customer_payment_documents_custome",
                        column: x => x.customer_payment_document_id,
                        principalTable: "customer_payment_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_return_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_return_document_id = table.Column<long>(type: "bigint", nullable: false),
                    sale_item_id = table.Column<long>(type: "bigint", nullable: false),
                    variant_id = table.Column<long>(type: "bigint", nullable: false),
                    stock_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    line_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cashback_reversed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    condition = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    disposition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_return_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_return_lines_customer_return_documents_customer_re",
                        column: x => x.customer_return_document_id,
                        principalTable: "customer_return_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_return_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_return_lines_sale_items_sale_item_id",
                        column: x => x.sale_item_id,
                        principalTable: "sale_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_return_settlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_return_document_id = table.Column<long>(type: "bigint", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    amount_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_return_settlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_return_settlements_customer_return_documents_custo",
                        column: x => x.customer_return_document_id,
                        principalTable: "customer_return_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_customer_payment_document_id",
                table: "transactions",
                column: "customer_payment_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_customer_return_document_id",
                table: "transactions",
                column: "customer_return_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_allocations_customer_payment_document_id",
                table: "customer_payment_allocations",
                column: "customer_payment_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_allocations_sale_id",
                table: "customer_payment_allocations",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_documents_branch_id_idempotency_key",
                table: "customer_payment_documents",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_documents_customer_id_business_date_id",
                table: "customer_payment_documents",
                columns: new[] { "customer_id", "business_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_documents_document_number",
                table: "customer_payment_documents",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_documents_user_id",
                table: "customer_payment_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_payment_tenders_customer_payment_document_id",
                table: "customer_payment_tenders",
                column: "customer_payment_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_documents_branch_id_idempotency_key",
                table: "customer_return_documents",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_documents_customer_id_business_date_id",
                table: "customer_return_documents",
                columns: new[] { "customer_id", "business_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_documents_document_number",
                table: "customer_return_documents",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_documents_sale_id_id",
                table: "customer_return_documents",
                columns: new[] { "sale_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_documents_user_id",
                table: "customer_return_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_documents_warehouse_id",
                table: "customer_return_documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_lines_customer_return_document_id",
                table: "customer_return_lines",
                column: "customer_return_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_lines_sale_item_id",
                table: "customer_return_lines",
                column: "sale_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_lines_variant_id",
                table: "customer_return_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_return_settlements_customer_return_document_id",
                table: "customer_return_settlements",
                column: "customer_return_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_branch_id_occurred_at",
                table: "inventory_movements",
                columns: new[] { "branch_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_source_type_source_id",
                table: "inventory_movements",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_to_location_kind_to_location_id_variant",
                table: "inventory_movements",
                columns: new[] { "to_location_kind", "to_location_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_user_id",
                table: "inventory_movements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_variant_id",
                table: "inventory_movements",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_positions_branch_id_location_kind_location_id_var",
                table: "inventory_positions",
                columns: new[] { "branch_id", "location_kind", "location_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_positions_variant_id",
                table: "inventory_positions",
                column: "variant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_customers_users_assigned_user_id",
                table: "customers",
                column: "assigned_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_customer_payment_documents_customer_payment_do",
                table: "transactions",
                column: "customer_payment_document_id",
                principalTable: "customer_payment_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_customer_return_documents_customer_return_docu",
                table: "transactions",
                column: "customer_return_document_id",
                principalTable: "customer_return_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE accounts AS debt
                SET balance = debt.balance - advance.balance,
                    updated_at = NOW()
                FROM accounts AS advance
                WHERE debt.customer_id = advance.customer_id
                  AND debt.currency = advance.currency
                  AND debt.type = 'Debt'
                  AND advance.type = 'CustomerAdvance'
                  AND NOT debt.is_deleted
                  AND NOT advance.is_deleted;

                UPDATE accounts
                SET is_deleted = TRUE, deleted_at = NOW(), balance = 0
                WHERE type = 'CustomerAdvance' AND NOT is_deleted;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_customers_users_assigned_user_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_customer_payment_documents_customer_payment_do",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_customer_return_documents_customer_return_docu",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "customer_payment_allocations");

            migrationBuilder.DropTable(
                name: "customer_payment_tenders");

            migrationBuilder.DropTable(
                name: "customer_return_lines");

            migrationBuilder.DropTable(
                name: "customer_return_settlements");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "inventory_positions");

            migrationBuilder.DropTable(
                name: "customer_payment_documents");

            migrationBuilder.DropTable(
                name: "customer_return_documents");

            migrationBuilder.DropIndex(
                name: "ix_transactions_customer_payment_document_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_customer_return_document_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "customer_payment_document_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "customer_return_document_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "cashback_earned",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "returned_cashback",
                table: "sale_items");

            migrationBuilder.DropSequence(
                name: "document_number_seq");

            migrationBuilder.RenameColumn(
                name: "assigned_user_id",
                table: "customers",
                newName: "agent_id");

            migrationBuilder.RenameIndex(
                name: "ix_customers_assigned_user_id",
                table: "customers",
                newName: "ix_customers_agent_id");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "sale_payments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "balance",
                table: "accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddForeignKey(
                name: "fk_customers_users_agent_id",
                table: "customers",
                column: "agent_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
