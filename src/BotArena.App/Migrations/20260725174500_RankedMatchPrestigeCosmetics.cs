using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260725174500_RankedMatchPrestigeCosmetics")]
public partial class RankedMatchPrestigeCosmetics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The catalog ships the new items, but accounts that already crossed
        // the milestone need the same grants a live match completion emits.
        migrationBuilder.Sql("""
            WITH eligible AS (
                SELECT bot."OwnerUserId" AS "UserId",
                       COUNT(DISTINCT ranked."Id") AS "RankedMatches"
                FROM "MatchSets" AS ranked
                JOIN "Bots" AS bot
                  ON bot."Id" = ranked."BotAId"
                  OR bot."Id" = ranked."BotBId"
                WHERE ranked."Status" = 'Completed'
                GROUP BY bot."OwnerUserId"
                HAVING COUNT(DISTINCT ranked."Id") >= 100
            )
            INSERT INTO "EntitlementGrants"
                ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                 "GrantedAt", "RevokedAt", "MetadataJson")
            SELECT gen_random_uuid(), eligible."UserId",
                   'bot-look:aureate-warden',
                   'achievement', 'ranked-matches-100', now(), NULL,
                   jsonb_build_object(
                       'rankedMatches', eligible."RankedMatches",
                       'backfill', true)
            FROM eligible
            ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
            DO NOTHING;

            WITH eligible AS (
                SELECT bot."OwnerUserId" AS "UserId",
                       COUNT(DISTINCT ranked."Id") AS "RankedMatches"
                FROM "MatchSets" AS ranked
                JOIN "Bots" AS bot
                  ON bot."Id" = ranked."BotAId"
                  OR bot."Id" = ranked."BotBId"
                WHERE ranked."Status" = 'Completed'
                GROUP BY bot."OwnerUserId"
                HAVING COUNT(DISTINCT ranked."Id") >= 100
            )
            INSERT INTO "EntitlementGrants"
                ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                 "GrantedAt", "RevokedAt", "MetadataJson")
            SELECT gen_random_uuid(), eligible."UserId",
                   'projectile-look:regent-lance',
                   'achievement', 'ranked-matches-100', now(), NULL,
                   jsonb_build_object(
                       'rankedMatches', eligible."RankedMatches",
                       'backfill', true)
            FROM eligible
            ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
            DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "EntitlementGrants"
            WHERE "SourceKind" = 'achievement'
              AND "SourceId" = 'ranked-matches-100'
              AND "EntitlementKey" IN (
                  'bot-look:aureate-warden',
                  'projectile-look:regent-lance');
            """);
    }
}
