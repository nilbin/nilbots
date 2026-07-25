using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class MatchParticipantOwnerSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerDisplayNameSnapshot",
                table: "MatchParticipants",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "MatchParticipants" AS participant
                SET "OwnerDisplayNameSnapshot" = account."DisplayName"
                FROM "Bots" AS bot
                INNER JOIN "Users" AS account
                    ON account."Id" = bot."OwnerUserId"
                WHERE participant."BotId" = bot."Id"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerDisplayNameSnapshot",
                table: "MatchParticipants");
        }
    }
}
