using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerRefundDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "customer_refund_document_id",
                table: "transactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_refund_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    trade_case_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    total_base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_refund_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_refund_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_refund_documents_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_refund_documents_trade_cases_trade_case_id",
                        column: x => x.trade_case_id,
                        principalTable: "trade_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_refund_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_refund_tenders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_refund_document_id = table.Column<long>(type: "bigint", nullable: false),
                    method = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    amount_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_refund_tenders", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_refund_tenders_customer_refund_documents_customer_",
                        column: x => x.customer_refund_document_id,
                        principalTable: "customer_refund_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_customer_refund_document_id",
                table: "transactions",
                column: "customer_refund_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_refund_documents_branch_id_idempotency_key",
                table: "customer_refund_documents",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_refund_documents_customer_id_business_date_id",
                table: "customer_refund_documents",
                columns: new[] { "customer_id", "business_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_refund_documents_document_number",
                table: "customer_refund_documents",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_refund_documents_trade_case_id",
                table: "customer_refund_documents",
                column: "trade_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_refund_documents_user_id",
                table: "customer_refund_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_refund_tenders_customer_refund_document_id",
                table: "customer_refund_tenders",
                column: "customer_refund_document_id");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_customer_refund_documents_customer_refund_docu",
                table: "transactions",
                column: "customer_refund_document_id",
                principalTable: "customer_refund_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_customer_refund_documents_customer_refund_docu",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "customer_refund_tenders");

            migrationBuilder.DropTable(
                name: "customer_refund_documents");

            migrationBuilder.DropIndex(
                name: "ix_transactions_customer_refund_document_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "customer_refund_document_id",
                table: "transactions");
        }
    }
}
