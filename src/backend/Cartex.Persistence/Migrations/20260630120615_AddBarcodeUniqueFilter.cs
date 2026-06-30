using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBarcodeUniqueFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_barcodes_code",
                table: "barcodes");

            migrationBuilder.CreateIndex(
                name: "ix_barcodes_code",
                table: "barcodes",
                column: "code",
                unique: true,
                filter: "\"is_deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_barcodes_code",
                table: "barcodes");

            migrationBuilder.CreateIndex(
                name: "ix_barcodes_code",
                table: "barcodes",
                column: "code",
                unique: true);
        }
    }
}
