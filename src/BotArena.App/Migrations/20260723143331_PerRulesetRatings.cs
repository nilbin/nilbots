using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class PerRulesetRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RulesName",
                table: "MatchSets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BotRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BotId = table.Column<Guid>(type: "uuid", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Rating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1200.0),
                    RankedSets = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotRatings_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotRatings_BotId_RulesVersion",
                table: "BotRatings",
                columns: new[] { "BotId", "RulesVersion" },
                unique: true);

            // Pre-#54 ratings were earned across mixed eras (0.2/0.3 official play plus
            // experiment tournaments); they migrate to the "0.3" ladder as the legacy
            // record — closest official era, documented in DECISIONS #54. The 0.4-era
            // ladder starts fresh for everyone.
            migrationBuilder.Sql("""
                INSERT INTO "BotRatings" ("Id", "BotId", "RulesVersion", "Rating", "RankedSets")
                SELECT gen_random_uuid(), "Id", '0.3', "Rating", "RankedSets"
                FROM "Bots"
                WHERE "RankedSets" > 0;
                """);

            migrationBuilder.DropColumn(
                name: "RankedSets",
                table: "Bots");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Bots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotRatings");

            migrationBuilder.DropColumn(
                name: "RulesName",
                table: "MatchSets");

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
                defaultValue: 1200.0);
        }
    }
}
