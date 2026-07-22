using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class Rankings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MatchSetId",
                table: "Matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetGame",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RankedSets",
                table: "Bots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Bots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "MatchSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BotAId = table.Column<Guid>(type: "uuid", nullable: false),
                    BotBId = table.Column<Guid>(type: "uuid", nullable: false),
                    BotAVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BotBVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameRulesVersion = table.Column<string>(type: "text", nullable: false),
                    RuntimeConfigurationVersion = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScoreA = table.Column<double>(type: "double precision", nullable: false),
                    ScoreB = table.Column<double>(type: "double precision", nullable: false),
                    RatingABefore = table.Column<double>(type: "double precision", nullable: false),
                    RatingBBefore = table.Column<double>(type: "double precision", nullable: false),
                    RatingChangeA = table.Column<double>(type: "double precision", nullable: false),
                    RatingChangeB = table.Column<double>(type: "double precision", nullable: false),
                    WinnerBotId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_MatchSetId",
                table: "Matches",
                column: "MatchSetId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSets_CreatedAt",
                table: "MatchSets",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchSets");

            migrationBuilder.DropIndex(
                name: "IX_Matches_MatchSetId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "MatchSetId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "SetGame",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RankedSets",
                table: "Bots");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Bots");
        }
    }
}
