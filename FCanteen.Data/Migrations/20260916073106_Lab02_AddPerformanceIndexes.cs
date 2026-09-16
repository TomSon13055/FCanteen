using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCanteen.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lab02_AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BranchCode",
                table: "OrderTickets",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTickets_BranchCode_CreatedAt",
                table: "OrderTickets",
                columns: new[] { "BranchCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderTickets_CreatedAt",
                table: "OrderTickets",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderTickets_BranchCode_CreatedAt",
                table: "OrderTickets");

            migrationBuilder.DropIndex(
                name: "IX_OrderTickets_CreatedAt",
                table: "OrderTickets");

            migrationBuilder.AlterColumn<string>(
                name: "BranchCode",
                table: "OrderTickets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
