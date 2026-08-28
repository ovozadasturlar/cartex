using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartyAndShiftCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customers_party_id",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ix_customers_phone",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ix_customers_search_fold_trgm",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "counted_cash",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "opening_float",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "address",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "email",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "search_fold",
                table: "customers");

            migrationBuilder.AddColumn<string>(
                name: "search_fold",
                table: "parties",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_parties_search_fold_trgm",
                table: "parties",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_customers_party_id",
                table: "customers",
                column: "party_id",
                unique: true,
                filter: "NOT \"is_deleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_parties_search_fold_trgm",
                table: "parties");

            migrationBuilder.DropIndex(
                name: "ix_customers_party_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "search_fold",
                table: "parties");

            migrationBuilder.AddColumn<decimal>(
                name: "counted_cash",
                table: "shifts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "opening_float",
                table: "shifts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "customers",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "customers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "customers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_fold",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_party_id",
                table: "customers",
                column: "party_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_phone",
                table: "customers",
                column: "phone",
                unique: true,
                filter: "\"phone\" IS NOT NULL AND NOT \"is_deleted\"");

            migrationBuilder.CreateIndex(
                name: "ix_customers_search_fold_trgm",
                table: "customers",
                column: "search_fold")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }
    }
}
