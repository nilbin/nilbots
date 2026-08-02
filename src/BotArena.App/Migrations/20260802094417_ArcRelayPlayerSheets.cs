using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class ArcRelayPlayerSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "PresentationTicksPerSecond",
                table: "Matches",
                type: "double precision",
                precision: 6,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<byte[]>(
                name: "MindDataSnapshot",
                table: "MatchParticipants",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SheetCanonicalJsonSnapshot",
                table: "MatchParticipants",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SheetHashSnapshot",
                table: "MatchParticipants",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SheetIdSnapshot",
                table: "MatchParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SheetNameSnapshot",
                table: "MatchParticipants",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SheetRevisionSnapshot",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArcRelaySheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CanonicalJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcRelaySheets", x => x.Id);
                    table.CheckConstraint("CK_ArcRelaySheets_Revision_Positive", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_ArcRelaySheets_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelaySheets_OwnerUserId_Name",
                table: "ArcRelaySheets",
                columns: new[] { "OwnerUserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ArcRelaySheets_OwnerUserId_UpdatedAt",
                table: "ArcRelaySheets",
                columns: new[] { "OwnerUserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArcRelaySheets");

            migrationBuilder.DropColumn(
                name: "MindDataSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SheetCanonicalJsonSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SheetHashSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SheetIdSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SheetNameSnapshot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SheetRevisionSnapshot",
                table: "MatchParticipants");

            migrationBuilder.AlterColumn<int>(
                name: "PresentationTicksPerSecond",
                table: "Matches",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 6,
                oldScale: 3);
        }
    }
}
