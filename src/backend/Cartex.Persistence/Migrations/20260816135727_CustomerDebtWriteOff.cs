using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerDebtWriteOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "write_off_base_amount",
                table: "customer_payment_documents",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "write_off_reason",
                table: "customer_payment_documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "customer_payment_allocations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "write_off_base_amount",
                table: "customer_payment_documents");

            migrationBuilder.DropColumn(
                name: "write_off_reason",
                table: "customer_payment_documents");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "customer_payment_allocations");
        }
    }
}
