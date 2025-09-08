using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SarasBloggAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdAndCascades_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Comments.UserId (nullable)
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Comments",
                type: "text",
                nullable: true);

            // 2) Indexer
            migrationBuilder.CreateIndex(
                name: "IX_Comments_BloggId",
                table: "Comments",
                column: "BloggId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BloggLikes_UserId",
                table: "BloggLikes",
                column: "UserId");

            // 3) Tillfälligt: ta bort UNIK index (BloggId,UserId) så UPDATE kan skapa dubbletter
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_BloggLikes_BloggId_UserId"";");

            // 4) BACKFILL i EN update → städa dubbletter → städa orphans (User)
            migrationBuilder.Sql(@"
-- Mappar gamla UserId (email/username) -> riktiga AspNetUsers.Id i EN körning
UPDATE ""BloggLikes"" bl
SET ""UserId"" = u.""Id""
FROM ""AspNetUsers"" u
WHERE bl.""UserId"" = u.""Email""
   OR bl.""UserId"" = u.""UserName"";

-- Ta bort dubbletter som nu krockar med unika (BloggId,UserId)
DELETE FROM ""BloggLikes"" bl
USING (
    SELECT MIN(""Id"") AS keep_id, ""BloggId"", ""UserId""
    FROM ""BloggLikes""
    GROUP BY ""BloggId"", ""UserId""
) k
WHERE bl.""BloggId"" = k.""BloggId""
  AND bl.""UserId"" = k.""UserId""
  AND bl.""Id"" <> k.keep_id;

-- Ta bort rader som fortfarande inte matchar någon användare
DELETE FROM ""BloggLikes"" bl
WHERE NOT EXISTS (SELECT 1 FROM ""AspNetUsers"" u WHERE u.""Id"" = bl.""UserId"");
");

            // 4b) NYTT: rensa likes som pekar på borttagna bloggar (orphans på BloggId)
            migrationBuilder.Sql(@"
DELETE FROM ""BloggLikes"" bl
WHERE NOT EXISTS (SELECT 1 FROM ""Blogg"" b WHERE b.""Id"" = bl.""BloggId"");
");

            // 5) Skapa tillbaka UNIK index (BloggId,UserId) när datan är ren
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX ""IX_BloggLikes_BloggId_UserId""
ON ""BloggLikes"" (""BloggId"", ""UserId"");
");

            // 6) FOREIGN KEYS (EFTER backfill & städning) med CASCADE
            migrationBuilder.AddForeignKey(
                name: "FK_BloggLikes_AspNetUsers_UserId",
                table: "BloggLikes",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BloggLikes_Blogg_BloggId",
                table: "BloggLikes",
                column: "BloggId",
                principalTable: "Blogg",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_AspNetUsers_UserId",
                table: "Comments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Blogg_BloggId",
                table: "Comments",
                column: "BloggId",
                principalTable: "Blogg",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }


        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ta bort FK
            migrationBuilder.DropForeignKey("FK_BloggLikes_AspNetUsers_UserId", "BloggLikes");
            migrationBuilder.DropForeignKey("FK_BloggLikes_Blogg_BloggId", "BloggLikes");
            migrationBuilder.DropForeignKey("FK_Comments_AspNetUsers_UserId", "Comments");
            migrationBuilder.DropForeignKey("FK_Comments_Blogg_BloggId", "Comments");

            // Ta bort indexer som skapades i denna migration
            migrationBuilder.DropIndex(name: "IX_Comments_BloggId", table: "Comments");
            migrationBuilder.DropIndex(name: "IX_Comments_UserId", table: "Comments");
            migrationBuilder.DropIndex(name: "IX_BloggLikes_UserId", table: "BloggLikes");

            // Ta bort kolumnen vi lade till
            migrationBuilder.DropColumn(name: "UserId", table: "Comments");

            // OBS: Vi rör INTE den unika indexen på (BloggId,UserId) i Down.
            // Den fanns redan innan migrationen och ska finnas kvar efter rollback.
        }
    }
}
