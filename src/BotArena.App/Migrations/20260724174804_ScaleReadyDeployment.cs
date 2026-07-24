using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class ScaleReadyDeployment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Expand first: keep the legacy path columns for one release so the
            // previous image can still be rolled back safely.
            migrationBuilder.AddColumn<string>(
                name: "ReplayKey",
                table: "Matches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactKey",
                table: "BotVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "BackgroundJobs",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.Sql("""
                UPDATE "BotVersions"
                SET "ArtifactKey" =
                    'artifacts/sha256/' || lower("ArtifactHash") || '.wasm'
                WHERE "ArtifactHash" IS NOT NULL
                """);
            migrationBuilder.Sql("""
                UPDATE "Matches"
                SET "ReplayKey" =
                    'replays/' || replace("Id"::text, '-', '') || '.json'
                WHERE "ReplayPath" IS NOT NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "ReplayKey",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ArtifactKey",
                table: "BotVersions");
        }
    }
}
