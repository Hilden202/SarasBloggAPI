using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SarasBloggAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveImageFromBlogg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Blogg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Blogg",
                type: "text",
                nullable: true);
        }
    }
}
