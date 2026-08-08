using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class ArcRelaySeasonOneLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Seasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Seasons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "Seasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RatingSeedPolicyId",
                table: "Seasons",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "legacy-import");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Seasons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateTable(
                name: "ArcRelaySeasonEntrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PresentAtOpening = table.Column<bool>(type: "boolean", nullable: false),
                    OpeningRating = table.Column<double>(type: "double precision", nullable: false),
                    OpeningRank = table.Column<int>(type: "integer", nullable: true),
                    OpeningRevision = table.Column<int>(type: "integer", nullable: false),
                    OpeningPresentationJson = table.Column<string>(type: "jsonb", nullable: false),
                    ClosingRating = table.Column<double>(type: "double precision", nullable: true),
                    ClosingRank = table.Column<int>(type: "integer", nullable: true),
                    ClosingRevision = table.Column<int>(type: "integer", nullable: true),
                    ClosingPresentationJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcRelaySeasonEntrants", x => x.Id);
                    table.CheckConstraint("CK_ArcRelaySeasonEntrants_ClosingRank", "\"ClosingRank\" IS NULL OR \"ClosingRank\" > 0");
                    table.CheckConstraint("CK_ArcRelaySeasonEntrants_OpeningRank", "\"OpeningRank\" IS NULL OR \"OpeningRank\" > 0");
                    table.CheckConstraint("CK_ArcRelaySeasonEntrants_OpeningRevision", "\"OpeningRevision\" >= 0");
                    table.ForeignKey(
                        name: "FK_ArcRelaySeasonEntrants_ArcRelayEntrants_EntrantId",
                        column: x => x.EntrantId,
                        principalTable: "ArcRelayEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArcRelaySeasonEntrants_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seasons_ClosedAt",
                table: "Seasons",
                sql: "\"ClosedAt\" IS NULL OR \"OpenedAt\" IS NULL OR \"ClosedAt\" >= \"OpenedAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seasons_Number_Positive",
                table: "Seasons",
                sql: "\"Number\" IS NULL OR \"Number\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seasons_OpenedAt",
                table: "Seasons",
                sql: "\"OpenedAt\" IS NULL OR \"StartsAt\" IS NULL OR \"OpenedAt\" >= \"StartsAt\"");

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelaySeasonEntrants_EntrantId",
                table: "ArcRelaySeasonEntrants",
                column: "EntrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelaySeasonEntrants_SeasonId_EntrantId",
                table: "ArcRelaySeasonEntrants",
                columns: new[] { "SeasonId", "EntrantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelaySeasonEntrants_SeasonId_OpeningRank",
                table: "ArcRelaySeasonEntrants",
                columns: new[] { "SeasonId", "OpeningRank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArcRelaySeasonEntrants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seasons_ClosedAt",
                table: "Seasons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seasons_Number_Positive",
                table: "Seasons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seasons_OpenedAt",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "RatingSeedPolicyId",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Seasons");
        }
    }
}
