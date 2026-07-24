using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class BotLookPresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LookIdSnapshot",
                table: "MatchParticipants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "vanguard");

            migrationBuilder.AddColumn<string>(
                name: "LookId",
                table: "Bots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "vanguard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LookIdSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "LookId",
                table: "Bots");
        }
    }
}
