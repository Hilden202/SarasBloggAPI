using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SarasBloggAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderToBloggImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "BloggImages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "BloggImages");
        }
    }
}
