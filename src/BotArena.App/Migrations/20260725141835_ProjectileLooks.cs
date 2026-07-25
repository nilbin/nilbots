using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class ProjectileLooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectileLookIdSnapshot",
                table: "MatchParticipants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "pulse-bolt");

            migrationBuilder.AddColumn<string>(
                name: "ProjectileLookId",
                table: "Bots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "pulse-bolt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectileLookIdSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "ProjectileLookId",
                table: "Bots");
        }
    }
}
