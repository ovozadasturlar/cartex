using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerPaymentRewardLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "customer_payment_document_id",
                table: "partner_reward_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_reward_entries_customer_payment_document_id",
                table: "partner_reward_entries",
                column: "customer_payment_document_id");

            migrationBuilder.AddForeignKey(
                name: "fk_partner_reward_entries_customer_payment_documents_customer_",
                table: "partner_reward_entries",
                column: "customer_payment_document_id",
                principalTable: "customer_payment_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_partner_reward_entries_customer_payment_documents_customer_",
                table: "partner_reward_entries");

            migrationBuilder.DropIndex(
                name: "ix_partner_reward_entries_customer_payment_document_id",
                table: "partner_reward_entries");

            migrationBuilder.DropColumn(
                name: "customer_payment_document_id",
                table: "partner_reward_entries");
        }
    }
}
