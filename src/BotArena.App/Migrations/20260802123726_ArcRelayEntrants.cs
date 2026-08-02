using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class ArcRelayEntrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArcRelayLane",
                table: "Matches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompositionHashSnapshot",
                table: "MatchParticipants",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompositionSnapshot",
                table: "MatchParticipants",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CrestSnapshot",
                table: "MatchParticipants",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntrantIdSnapshot",
                table: "MatchParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntrantKindSnapshot",
                table: "MatchParticipants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EntrantRevisionSnapshot",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArcRelayEntrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CrestVariant = table.Column<int>(type: "integer", nullable: false),
                    MindBotId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompositionJson = table.Column<string>(type: "jsonb", nullable: true),
                    CompositionHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    PreflightStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreflightMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreflightRevision = table.Column<int>(type: "integer", nullable: true),
                    PreflightFailure = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LadderOptedIn = table.Column<bool>(type: "boolean", nullable: false),
                    LadderOptedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuspensionMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcRelayEntrants", x => x.Id);
                    table.CheckConstraint("CK_ArcRelayEntrants_CrestVariant", "\"CrestVariant\" BETWEEN 0 AND 4095");
                    table.CheckConstraint("CK_ArcRelayEntrants_CustomMindData", "(\"Kind\" = 'Sheet' AND \"MindBotId\" IS NULL AND \"CompositionJson\" IS NULL AND \"CompositionHash\" IS NULL) OR (\"Kind\" = 'CustomMind' AND \"MindBotId\" IS NOT NULL AND \"CompositionJson\" IS NOT NULL AND \"CompositionHash\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ArcRelayEntrants_Bots_MindBotId",
                        column: x => x.MindBotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArcRelayEntrants_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArcRelayEntrantRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LadderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1200.0),
                    RankedMatches = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcRelayEntrantRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArcRelayEntrantRatings_ArcRelayEntrants_EntrantId",
                        column: x => x.EntrantId,
                        principalTable: "ArcRelayEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArcRelayEntrantRatings_Ladders_LadderId",
                        column: x => x.LadderId,
                        principalTable: "Ladders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "ArcRelayEntrants" (
                    "Id", "OwnerUserId", "Kind", "Name", "CrestVariant",
                    "PreflightStatus", "LadderOptedIn", "CreatedAt", "UpdatedAt")
                SELECT "Id", "OwnerUserId", 'Sheet', "Name", 0,
                    'NotRequired', FALSE, "CreatedAt", "UpdatedAt"
                FROM "ArcRelaySheets";
                """);

            migrationBuilder.CreateTable(
                name: "ArcRelayRankedMatches",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LadderId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrantAId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrantBId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatingABefore = table.Column<double>(type: "double precision", nullable: false),
                    RatingBBefore = table.Column<double>(type: "double precision", nullable: false),
                    RatingChangeA = table.Column<double>(type: "double precision", nullable: true),
                    RatingChangeB = table.Column<double>(type: "double precision", nullable: true),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcRelayRankedMatches", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_ArcRelayRankedMatches_ArcRelayEntrants_EntrantAId",
                        column: x => x.EntrantAId,
                        principalTable: "ArcRelayEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArcRelayRankedMatches_ArcRelayEntrants_EntrantBId",
                        column: x => x.EntrantBId,
                        principalTable: "ArcRelayEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArcRelayRankedMatches_Ladders_LadderId",
                        column: x => x.LadderId,
                        principalTable: "Ladders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArcRelayRankedMatches_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayEntrantRatings_EntrantId_LadderId",
                table: "ArcRelayEntrantRatings",
                columns: new[] { "EntrantId", "LadderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayEntrantRatings_LadderId_Rating_EntrantId",
                table: "ArcRelayEntrantRatings",
                columns: new[] { "LadderId", "Rating", "EntrantId" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayEntrants_MindBotId",
                table: "ArcRelayEntrants",
                column: "MindBotId",
                unique: true,
                filter: "\"MindBotId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayEntrants_OwnerUserId_LadderOptedIn",
                table: "ArcRelayEntrants",
                columns: new[] { "OwnerUserId", "LadderOptedIn" });

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayEntrants_OwnerUserId_UpdatedAt",
                table: "ArcRelayEntrants",
                columns: new[] { "OwnerUserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayRankedMatches_EntrantAId_EntrantBId_SettledAt",
                table: "ArcRelayRankedMatches",
                columns: new[] { "EntrantAId", "EntrantBId", "SettledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayRankedMatches_EntrantBId",
                table: "ArcRelayRankedMatches",
                column: "EntrantBId");

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelayRankedMatches_LadderId",
                table: "ArcRelayRankedMatches",
                column: "LadderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArcRelayEntrantRatings");

            migrationBuilder.DropTable(
                name: "ArcRelayRankedMatches");

            migrationBuilder.DropTable(
                name: "ArcRelayEntrants");

            migrationBuilder.DropColumn(
                name: "ArcRelayLane",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CompositionHashSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "CompositionSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "CrestSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "EntrantIdSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "EntrantKindSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "EntrantRevisionSnapshot",
                table: "MatchParticipants");
        }
    }
}
