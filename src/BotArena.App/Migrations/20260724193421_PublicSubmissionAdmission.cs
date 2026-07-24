using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class PublicSubmissionAdmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmissionNetworkHash",
                table: "BotVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotVersions_SubmissionNetworkHash_CreatedAt",
                table: "BotVersions",
                columns: new[] { "SubmissionNetworkHash", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BotVersions_SubmissionNetworkHash_CreatedAt",
                table: "BotVersions");

            migrationBuilder.DropColumn(
                name: "SubmissionNetworkHash",
                table: "BotVersions");
        }
    }
}
