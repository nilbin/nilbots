using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class TacticalSheetAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Commander documents have no lossless conversion to tactical
            // playbooks. Keep their entrant identities for historical ranked
            // match foreign keys, but retire them from all future pairing
            // before dropping the obsolete documents.
            migrationBuilder.Sql(
                """
                UPDATE "ArcRelayEntrants"
                SET "LadderOptedIn" = FALSE,
                    "LadderOptedInAt" = NULL
                WHERE "Kind" = 'Sheet';
                """);

            migrationBuilder.DropTable(
                name: "ArcRelaySheets");

            migrationBuilder.AddColumn<string>(
                name: "SheetLayoutJsonSnapshot",
                table: "MatchParticipants",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TacticalSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PlaybookJson = table.Column<string>(type: "json", nullable: false),
                    LayoutJson = table.Column<string>(type: "json", nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TacticalSheets", x => x.Id);
                    table.CheckConstraint("CK_TacticalSheets_Revision_Positive", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_TacticalSheets_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TacticalSheets_OwnerUserId_Name",
                table: "TacticalSheets",
                columns: new[] { "OwnerUserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TacticalSheets_OwnerUserId_UpdatedAt",
                table: "TacticalSheets",
                columns: new[] { "OwnerUserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TacticalSheets");

            migrationBuilder.DropColumn(
                name: "SheetLayoutJsonSnapshot",
                table: "MatchParticipants");

            migrationBuilder.CreateTable(
                name: "ArcRelaySheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
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
    }
}
