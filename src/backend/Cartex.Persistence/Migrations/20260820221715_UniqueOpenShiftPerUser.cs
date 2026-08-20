using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueOpenShiftPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_shifts_user_id_branch_id",
                table: "shifts",
                columns: new[] { "user_id", "branch_id" },
                unique: true,
                filter: "\"status\" = 'Open' AND \"is_deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shifts_user_id_branch_id",
                table: "shifts");
        }
    }
}
