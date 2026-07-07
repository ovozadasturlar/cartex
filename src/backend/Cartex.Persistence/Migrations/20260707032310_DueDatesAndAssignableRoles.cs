using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DueDatesAndAssignableRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "debt_due_date",
                table: "sales",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assignable_roles",
                table: "roles",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "debt_due_date",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "assignable_roles",
                table: "roles");
        }
    }
}
