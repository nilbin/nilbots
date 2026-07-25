using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class CosmeticEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InitiatedByUserId",
                table: "Matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EntitlementGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntitlementKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntitlementGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntitlementGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_InitiatedByUserId",
                table: "Matches",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntitlementGrants_UserId_EntitlementKey_RevokedAt",
                table: "EntitlementGrants",
                columns: new[] { "UserId", "EntitlementKey", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EntitlementGrants_UserId_EntitlementKey_SourceKind_SourceId",
                table: "EntitlementGrants",
                columns: new[] { "UserId", "EntitlementKey", "SourceKind", "SourceId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Users_InitiatedByUserId",
                table: "Matches",
                column: "InitiatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Before InitiatedByUserId existed, every setless persisted match came
            // from the authenticated unranked challenge endpoint and slot 0 was the
            // caller's bot. Preserve those durable product events.
            migrationBuilder.Sql("""
                UPDATE "Matches" AS match
                SET "InitiatedByUserId" = bot."OwnerUserId"
                FROM "MatchParticipants" AS participant
                JOIN "Bots" AS bot ON bot."Id" = participant."BotId"
                WHERE match."MatchSetId" IS NULL
                  AND match."InitiatedByUserId" IS NULL
                  AND participant."MatchId" = match."Id"
                  AND participant."Slot" = 0;
                """);

            // Backfill accomplishments that predate the grant ledger. These use the
            // same stable event identity as live awards, so retries remain idempotent.
            migrationBuilder.Sql("""
                INSERT INTO "EntitlementGrants"
                    ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                     "GrantedAt", "RevokedAt", "MetadataJson")
                SELECT gen_random_uuid(), bot."OwnerUserId", 'bot-look:lancer',
                       'achievement', 'first-successful-build', now(), NULL, NULL
                FROM "Bots" AS bot
                WHERE EXISTS (
                    SELECT 1
                    FROM "BotVersions" AS version
                    WHERE version."BotId" = bot."Id"
                      AND version."Status" = 'Built')
                GROUP BY bot."OwnerUserId"
                ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
                DO NOTHING;

                INSERT INTO "EntitlementGrants"
                    ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                     "GrantedAt", "RevokedAt", "MetadataJson")
                SELECT gen_random_uuid(), match."InitiatedByUserId",
                       'projectile-look:arc-spark',
                       'challenge', 'first-unranked-match', now(), NULL, NULL
                FROM "Matches" AS match
                WHERE match."MatchSetId" IS NULL
                  AND match."Status" = 'Completed'
                  AND match."InitiatedByUserId" IS NOT NULL
                GROUP BY match."InitiatedByUserId"
                ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
                DO NOTHING;
                """);

            // Items were freely selectable before this migration. Keep any equipped
            // choice valid even when its owner has not yet completed the new unlock.
            migrationBuilder.Sql("""
                INSERT INTO "EntitlementGrants"
                    ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                     "GrantedAt", "RevokedAt", "MetadataJson")
                SELECT gen_random_uuid(), bot."OwnerUserId", 'bot-look:lancer',
                       'legacy', 'equipped-before-entitlements', now(), NULL, NULL
                FROM "Bots" AS bot
                WHERE bot."LookId" = 'lancer'
                GROUP BY bot."OwnerUserId"
                ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
                DO NOTHING;

                INSERT INTO "EntitlementGrants"
                    ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                     "GrantedAt", "RevokedAt", "MetadataJson")
                SELECT gen_random_uuid(), bot."OwnerUserId",
                       'projectile-look:arc-spark',
                       'legacy', 'equipped-before-entitlements', now(), NULL, NULL
                FROM "Bots" AS bot
                WHERE bot."ProjectileLookId" = 'arc-spark'
                GROUP BY bot."OwnerUserId"
                ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
                DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Users_InitiatedByUserId",
                table: "Matches");

            migrationBuilder.DropTable(
                name: "EntitlementGrants");

            migrationBuilder.DropIndex(
                name: "IX_Matches_InitiatedByUserId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "InitiatedByUserId",
                table: "Matches");
        }
    }
}
