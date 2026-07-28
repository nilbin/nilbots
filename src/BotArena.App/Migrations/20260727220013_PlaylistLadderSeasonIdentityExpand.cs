using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class PlaylistLadderSeasonIdentityExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RulesVersion",
                table: "BotRatings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AddColumn<Guid>(
                name: "PlaylistVersionId",
                table: "Matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LadderId",
                table: "MatchSets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlaylistVersionId",
                table: "MatchSets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LadderId",
                table: "BotRatings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonOpeningRank",
                table: "BotRatings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BotRatings_SeasonOpeningRank_Positive",
                table: "BotRatings",
                sql: "\"SeasonOpeningRank\" IS NULL OR \"SeasonOpeningRank\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BotRatings_SeasonOpeningRank_RequiresLadder",
                table: "BotRatings",
                sql: "\"SeasonOpeningRank\" IS NULL OR \"LadderId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchSets_CompetitionIdentity_Paired",
                table: "MatchSets",
                sql: "(\"PlaylistVersionId\" IS NULL AND \"LadderId\" IS NULL) OR (\"PlaylistVersionId\" IS NOT NULL AND \"LadderId\" IS NOT NULL)");

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.CheckConstraint("CK_Seasons_TimeWindow", "\"StartsAt\" IS NULL OR \"EndsAt\" IS NULL OR \"EndsAt\" > \"StartsAt\"");
                });

            migrationBuilder.CreateTable(
                name: "PlaylistVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    GameModeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RulesetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MatchFormatId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MapPoolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SeriesPolicyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MatchmakingPolicyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdmissionPolicyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CanonicalDefinition = table.Column<string>(type: "jsonb", nullable: false),
                    DefinitionFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Provenance = table.Column<string>(type: "jsonb", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistVersions", x => x.Id);
                    table.CheckConstraint("CK_PlaylistVersions_DefinitionFingerprint", "\"DefinitionFingerprint\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_PlaylistVersions_Version_Positive", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_PlaylistVersions_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ladders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RatingPolicyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LegacyRulesVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsListed = table.Column<bool>(type: "boolean", nullable: false),
                    AwardsAchievements = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ladders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ladders_PlaylistVersions_PlaylistVersionId",
                        column: x => x.PlaylistVersionId,
                        principalTable: "PlaylistVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ladders_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlaylistVersionId",
                table: "Matches",
                column: "PlaylistVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSets_LadderId_CreatedAt",
                table: "MatchSets",
                columns: new[] { "LadderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchSets_PlaylistVersionId",
                table: "MatchSets",
                column: "PlaylistVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BotRatings_BotId_LadderId",
                table: "BotRatings",
                columns: new[] { "BotId", "LadderId" },
                unique: true,
                filter: "\"LadderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BotRatings_LadderId_Rating_BotId",
                table: "BotRatings",
                columns: new[] { "LadderId", "Rating", "BotId" },
                descending: new[] { false, true, false },
                filter: "\"LadderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ladders_LegacyRulesVersion",
                table: "Ladders",
                column: "LegacyRulesVersion",
                unique: true,
                filter: "\"LegacyRulesVersion\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ladders_OneOpenPerPlaylistVersion",
                table: "Ladders",
                column: "PlaylistVersionId",
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_Ladders_PlaylistVersionId_SeasonId",
                table: "Ladders",
                columns: new[] { "PlaylistVersionId", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ladders_SeasonId",
                table: "Ladders",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistVersions_PlaylistId_Version",
                table: "PlaylistVersions",
                columns: new[] { "PlaylistId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_Key",
                table: "Playlists",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_Key",
                table: "Seasons",
                column: "Key",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION botarena_reject_playlist_version_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'PlaylistVersion rows are immutable; create a new version instead.'
                        USING ERRCODE = '55000';
                END;
                $$;

                CREATE TRIGGER "TR_PlaylistVersions_Immutable"
                BEFORE UPDATE OR DELETE ON "PlaylistVersions"
                FOR EACH ROW
                EXECUTE FUNCTION botarena_reject_playlist_version_mutation();
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_BotRatings_Ladders_LadderId",
                table: "BotRatings",
                column: "LadderId",
                principalTable: "Ladders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchSets_Ladders_LadderId",
                table: "MatchSets",
                column: "LadderId",
                principalTable: "Ladders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchSets_PlaylistVersions_PlaylistVersionId",
                table: "MatchSets",
                column: "PlaylistVersionId",
                principalTable: "PlaylistVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_PlaylistVersions_PlaylistVersionId",
                table: "Matches",
                column: "PlaylistVersionId",
                principalTable: "PlaylistVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_PlaylistVersions_Immutable"
                    ON "PlaylistVersions";
                DROP FUNCTION IF EXISTS
                    botarena_reject_playlist_version_mutation();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_BotRatings_Ladders_LadderId",
                table: "BotRatings");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchSets_Ladders_LadderId",
                table: "MatchSets");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchSets_PlaylistVersions_PlaylistVersionId",
                table: "MatchSets");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_PlaylistVersions_PlaylistVersionId",
                table: "Matches");

            migrationBuilder.DropTable(
                name: "Ladders");

            migrationBuilder.DropTable(
                name: "PlaylistVersions");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Matches_PlaylistVersionId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_MatchSets_LadderId_CreatedAt",
                table: "MatchSets");

            migrationBuilder.DropIndex(
                name: "IX_MatchSets_PlaylistVersionId",
                table: "MatchSets");

            migrationBuilder.DropIndex(
                name: "IX_BotRatings_BotId_LadderId",
                table: "BotRatings");

            migrationBuilder.DropIndex(
                name: "IX_BotRatings_LadderId_Rating_BotId",
                table: "BotRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BotRatings_SeasonOpeningRank_Positive",
                table: "BotRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BotRatings_SeasonOpeningRank_RequiresLadder",
                table: "BotRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchSets_CompetitionIdentity_Paired",
                table: "MatchSets");

            migrationBuilder.DropColumn(
                name: "PlaylistVersionId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LadderId",
                table: "MatchSets");

            migrationBuilder.DropColumn(
                name: "PlaylistVersionId",
                table: "MatchSets");

            migrationBuilder.DropColumn(
                name: "LadderId",
                table: "BotRatings");

            migrationBuilder.DropColumn(
                name: "SeasonOpeningRank",
                table: "BotRatings");

            migrationBuilder.AlterColumn<string>(
                name: "RulesVersion",
                table: "BotRatings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
