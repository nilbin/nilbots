using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class PracticeAuthoringSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PracticeAuthoringSnapshotVersion",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeWorkshopJsonSnapshot",
                table: "Matches",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PracticeAuthoringSnapshotVersion",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PracticeWorkshopJsonSnapshot",
                table: "Matches");
        }
    }
}
