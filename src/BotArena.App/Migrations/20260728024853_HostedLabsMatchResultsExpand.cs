using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class HostedLabsMatchResultsExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionEngineVersion",
                table: "PlaylistVersions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "0.1.0");

            migrationBuilder.AddColumn<string>(
                name: "ExecutionPolicyId",
                table: "PlaylistVersions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "legacy-duel-v1");

            migrationBuilder.AddColumn<int>(
                name: "ReplayFormatVersion",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "SupportedContractProfiles",
                table: "BotVersions",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchTeamResults",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Placement = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTeamResults", x => new { x.MatchId, x.TeamId });
                    table.CheckConstraint("CK_MatchTeamResults_Outcome", "\"Outcome\" IN ('Win', 'Loss', 'Draw')");
                    table.CheckConstraint("CK_MatchTeamResults_Placement_Positive", "\"Placement\" > 0");
                    table.CheckConstraint("CK_MatchTeamResults_TeamId_NonNegative", "\"TeamId\" >= 0");
                    table.ForeignKey(
                        name: "FK_MatchTeamResults_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchTeamScores",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    ScoreChannelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTeamScores", x => new { x.MatchId, x.TeamId, x.ScoreChannelId });
                    table.ForeignKey(
                        name: "FK_MatchTeamScores_MatchTeamResults_MatchId_TeamId",
                        columns: x => new { x.MatchId, x.TeamId },
                        principalTable: "MatchTeamResults",
                        principalColumns: new[] { "MatchId", "TeamId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matches_ReplayFormatVersion_Positive",
                table: "Matches",
                sql: "\"ReplayFormatVersion\" IS NULL OR \"ReplayFormatVersion\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchParticipants_TeamId_NonNegative",
                table: "MatchParticipants",
                sql: "\"TeamId\" IS NULL OR \"TeamId\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchTeamScores");

            migrationBuilder.DropTable(
                name: "MatchTeamResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matches_ReplayFormatVersion_Positive",
                table: "Matches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchParticipants_TeamId_NonNegative",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "ExecutionEngineVersion",
                table: "PlaylistVersions");

            migrationBuilder.DropColumn(
                name: "ExecutionPolicyId",
                table: "PlaylistVersions");

            migrationBuilder.DropColumn(
                name: "ReplayFormatVersion",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SupportedContractProfiles",
                table: "BotVersions");
        }
    }
}
