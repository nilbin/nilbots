using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Tactics replace the Programs authoring layer. The editor-only workshop
    /// sidecar is dropped outright rather than renamed: pre-Tactics sheet rows
    /// keep a NULL TacticsJson and become inert, exactly as pre-prod policy
    /// allows. Practice snapshots swap the same way; historical Practice
    /// matches simply report no authoring attribution.
    /// </summary>
    public partial class TacticsAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkshopJson",
                table: "TacticalSheets");

            migrationBuilder.DropColumn(
                name: "PracticeWorkshopJsonSnapshot",
                table: "Matches");

            migrationBuilder.AddColumn<string>(
                name: "TacticsJson",
                table: "TacticalSheets",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeTacticsJsonSnapshot",
                table: "Matches",
                type: "json",
                nullable: true);

            // Pre-Tactics practice snapshots carried version 1 workshop data;
            // their attribution is gone with the dropped column.
            migrationBuilder.Sql(
                "UPDATE \"Matches\" SET \"PracticeAuthoringSnapshotVersion\" = NULL "
                + "WHERE \"PracticeAuthoringSnapshotVersion\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TacticsJson",
                table: "TacticalSheets");

            migrationBuilder.DropColumn(
                name: "PracticeTacticsJsonSnapshot",
                table: "Matches");

            migrationBuilder.AddColumn<string>(
                name: "WorkshopJson",
                table: "TacticalSheets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeWorkshopJsonSnapshot",
                table: "Matches",
                type: "jsonb",
                nullable: true);
        }
    }
}
