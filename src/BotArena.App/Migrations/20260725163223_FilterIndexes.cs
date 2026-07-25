using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class FilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Matches_MapId_CreatedAt",
                table: "Matches",
                columns: new[] { "MapId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_BotId_MatchId",
                table: "MatchParticipants",
                columns: new[] { "BotId", "MatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_BotRatings_RulesVersion_Rating",
                table: "BotRatings",
                columns: new[] { "RulesVersion", "Rating" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_MapId_CreatedAt",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_MatchParticipants_BotId_MatchId",
                table: "MatchParticipants");

            migrationBuilder.DropIndex(
                name: "IX_BotRatings_RulesVersion_Rating",
                table: "BotRatings");
        }
    }
}
