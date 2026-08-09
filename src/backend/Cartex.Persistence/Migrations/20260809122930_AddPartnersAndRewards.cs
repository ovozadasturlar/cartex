using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnersAndRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "partner_redemption_document_id",
                table: "transactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "party_id",
                table: "customers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "participant_role_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    business_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    singular_label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    plural_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    can_equal_buyer = table.Column<bool>(type: "boolean", nullable: false),
                    max_count = table.Column<int>(type: "integer", nullable: false),
                    applies_to_cart = table.Column<bool>(type: "boolean", nullable: false),
                    applies_to_sale = table.Column<bool>(type: "boolean", nullable: false),
                    applies_to_trade_case = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participant_role_definitions", x => x.id);
                    table.CheckConstraint("ck_participant_roles_max_count", "\"max_count\" BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_participant_role_definitions_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    business_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parties", x => x.id);
                    table.ForeignKey(
                        name: "fk_parties_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Customer existed before the neutral Party identity was introduced. Backfill
            // one Party per existing customer before making the relationship mandatory.
            migrationBuilder.Sql("""
                INSERT INTO parties
                    (id, business_id, full_name, phone, email, address, created_at, updated_at,
                     created_by, updated_by, is_deleted, deleted_at)
                SELECT c.id,
                       (SELECT b.id FROM businesses b ORDER BY b.id LIMIT 1),
                       c.full_name, c.phone, c.email, c.address, c.created_at, c.updated_at,
                       c.created_by, c.updated_by, c.is_deleted, c.deleted_at
                FROM customers c;

                SELECT setval(
                    pg_get_serial_sequence('parties', 'id'),
                    COALESCE(MAX(id), 1),
                    MAX(id) IS NOT NULL)
                FROM parties;

                UPDATE customers SET party_id = id;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "party_id",
                table: "customers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "partner_programs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    business_id = table.Column<long>(type: "bigint", nullable: false),
                    branch_id = table.Column<long>(type: "bigint", nullable: true),
                    role_definition_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    mode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    basis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trigger = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cap_per_sale = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    hold_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_programs", x => x.id);
                    table.CheckConstraint("ck_partner_program_hold", "\"hold_days\" BETWEEN 0 AND 3650");
                    table.CheckConstraint("ck_partner_program_value", "\"value\" >= 0");
                    table.ForeignKey(
                        name: "fk_partner_programs_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_programs_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_programs_participant_role_definitions_role_definiti",
                        column: x => x.role_definition_id,
                        principalTable: "participant_role_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cart_participants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cart_id = table.Column<long>(type: "bigint", nullable: false),
                    role_definition_id = table.Column<long>(type: "bigint", nullable: false),
                    party_id = table.Column<long>(type: "bigint", nullable: false),
                    party_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    party_phone_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    role_label_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_cart_participants_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cart_participants_participant_role_definitions_role_definit",
                        column: x => x.role_definition_id,
                        principalTable: "participant_role_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cart_participants_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    party_id = table.Column<long>(type: "bigint", nullable: false),
                    partner_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_profiles_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_participants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sale_id = table.Column<long>(type: "bigint", nullable: false),
                    role_definition_id = table.Column<long>(type: "bigint", nullable: false),
                    party_id = table.Column<long>(type: "bigint", nullable: false),
                    party_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    party_phone_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    role_label_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_participants_participant_role_definitions_role_definit",
                        column: x => x.role_definition_id,
                        principalTable: "participant_role_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_participants_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_participants_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trade_case_participants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trade_case_id = table.Column<long>(type: "bigint", nullable: false),
                    role_definition_id = table.Column<long>(type: "bigint", nullable: false),
                    party_id = table.Column<long>(type: "bigint", nullable: false),
                    party_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    party_phone_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    role_label_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trade_case_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_trade_case_participants_participant_role_definitions_role_d",
                        column: x => x.role_definition_id,
                        principalTable: "participant_role_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_case_participants_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_case_participants_trade_cases_trade_case_id",
                        column: x => x.trade_case_id,
                        principalTable: "trade_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "partner_reward_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partner_program_id = table.Column<long>(type: "bigint", nullable: false),
                    scope = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    target_id = table.Column<long>(type: "bigint", nullable: false),
                    is_excluded = table.Column<bool>(type: "boolean", nullable: false),
                    value_override = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_reward_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_reward_rules_partner_programs_partner_program_id",
                        column: x => x.partner_program_id,
                        principalTable: "partner_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "partner_redemption_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    partner_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    mode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    product_variant_id = table.Column<long>(type: "bigint", nullable: true),
                    product_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_redemption_documents", x => x.id);
                    table.CheckConstraint("ck_partner_redemptions_amount", "\"amount\" > 0");
                    table.ForeignKey(
                        name: "fk_partner_redemption_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_redemption_documents_partner_profiles_partner_profi",
                        column: x => x.partner_profile_id,
                        principalTable: "partner_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_redemption_documents_product_variants_product_varia",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_redemption_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_reward_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<long>(type: "bigint", nullable: false),
                    partner_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    partner_program_id = table.Column<long>(type: "bigint", nullable: false),
                    sale_id = table.Column<long>(type: "bigint", nullable: true),
                    sale_item_id = table.Column<long>(type: "bigint", nullable: true),
                    customer_return_document_id = table.Column<long>(type: "bigint", nullable: true),
                    original_entry_id = table.Column<long>(type: "bigint", nullable: true),
                    partner_redemption_document_id = table.Column<long>(type: "bigint", nullable: true),
                    mode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    state = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    quantity_basis = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    financial_basis = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    details_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_reward_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_customer_return_documents_customer_r",
                        column: x => x.customer_return_document_id,
                        principalTable: "customer_return_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_partner_profiles_partner_profile_id",
                        column: x => x.partner_profile_id,
                        principalTable: "partner_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_partner_programs_partner_program_id",
                        column: x => x.partner_program_id,
                        principalTable: "partner_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_partner_redemption_documents_partner",
                        column: x => x.partner_redemption_document_id,
                        principalTable: "partner_redemption_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_partner_reward_entries_original_entr",
                        column: x => x.original_entry_id,
                        principalTable: "partner_reward_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_sale_items_sale_item_id",
                        column: x => x.sale_item_id,
                        principalTable: "sale_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_reward_entries_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_partner_redemption_document_id",
                table: "transactions",
                column: "partner_redemption_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_party_id",
                table: "customers",
                column: "party_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_participants_cart_id_role_definition_id_party_id",
                table: "cart_participants",
                columns: new[] { "cart_id", "role_definition_id", "party_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_participants_party_id",
                table: "cart_participants",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_participants_role_definition_id",
                table: "cart_participants",
                column: "role_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_participant_role_definitions_business_id_is_enabled_sort_or",
                table: "participant_role_definitions",
                columns: new[] { "business_id", "is_enabled", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_participant_role_definitions_business_id_key",
                table: "participant_role_definitions",
                columns: new[] { "business_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parties_business_id_full_name",
                table: "parties",
                columns: new[] { "business_id", "full_name" });

            migrationBuilder.CreateIndex(
                name: "ix_parties_business_id_phone",
                table: "parties",
                columns: new[] { "business_id", "phone" },
                unique: true,
                filter: "\"phone\" IS NOT NULL AND NOT \"is_deleted\"");

            migrationBuilder.CreateIndex(
                name: "ix_partner_profiles_partner_code",
                table: "partner_profiles",
                column: "partner_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_profiles_party_id",
                table: "partner_profiles",
                column: "party_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_programs_branch_id",
                table: "partner_programs",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_programs_business_id_role_definition_id_branch_id_i",
                table: "partner_programs",
                columns: new[] { "business_id", "role_definition_id", "branch_id", "is_enabled" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_programs_role_definition_id",
                table: "partner_programs",
                column: "role_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_redemption_documents_branch_id_idempotency_key",
                table: "partner_redemption_documents",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_partner_redemption_documents_document_number",
                table: "partner_redemption_documents",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_redemption_documents_partner_profile_id",
                table: "partner_redemption_documents",
                column: "partner_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_redemption_documents_product_variant_id",
                table: "partner_redemption_documents",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_redemption_documents_user_id",
                table: "partner_redemption_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_branch_id",
                table: "partner_reward_entries",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_customer_return_document_id",
                table: "partner_reward_entries",
                column: "customer_return_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_event_id",
                table: "partner_reward_entries",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_original_entry_id",
                table: "partner_reward_entries",
                column: "original_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_partner_profile_id_mode_state_availa",
                table: "partner_reward_entries",
                columns: new[] { "partner_profile_id", "mode", "state", "available_at" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_partner_program_id",
                table: "partner_reward_entries",
                column: "partner_program_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_partner_redemption_document_id",
                table: "partner_reward_entries",
                column: "partner_redemption_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_sale_id",
                table: "partner_reward_entries",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_sale_item_id_partner_program_id",
                table: "partner_reward_entries",
                columns: new[] { "sale_item_id", "partner_program_id" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_rules_partner_program_id_scope_target_id",
                table: "partner_reward_rules",
                columns: new[] { "partner_program_id", "scope", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_participants_party_id",
                table: "sale_participants",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_participants_role_definition_id",
                table: "sale_participants",
                column: "role_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_participants_sale_id_role_definition_id_party_id",
                table: "sale_participants",
                columns: new[] { "sale_id", "role_definition_id", "party_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_participants_party_id",
                table: "trade_case_participants",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_participants_role_definition_id",
                table: "trade_case_participants",
                column: "role_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_case_participants_trade_case_id_role_definition_id_pa",
                table: "trade_case_participants",
                columns: new[] { "trade_case_id", "role_definition_id", "party_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_customers_parties_party_id",
                table: "customers",
                column: "party_id",
                principalTable: "parties",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_partner_redemption_documents_partner_redemptio",
                table: "transactions",
                column: "partner_redemption_document_id",
                principalTable: "partner_redemption_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customers_parties_party_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_partner_redemption_documents_partner_redemptio",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "cart_participants");

            migrationBuilder.DropTable(
                name: "partner_reward_entries");

            migrationBuilder.DropTable(
                name: "partner_reward_rules");

            migrationBuilder.DropTable(
                name: "sale_participants");

            migrationBuilder.DropTable(
                name: "trade_case_participants");

            migrationBuilder.DropTable(
                name: "partner_redemption_documents");

            migrationBuilder.DropTable(
                name: "partner_programs");

            migrationBuilder.DropTable(
                name: "partner_profiles");

            migrationBuilder.DropTable(
                name: "participant_role_definitions");

            migrationBuilder.DropTable(
                name: "parties");

            migrationBuilder.DropIndex(
                name: "ix_transactions_partner_redemption_document_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_customers_party_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "partner_redemption_document_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "party_id",
                table: "customers");
        }
    }
}
