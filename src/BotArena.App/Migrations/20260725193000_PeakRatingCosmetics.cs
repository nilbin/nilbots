using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260725193000_PeakRatingCosmetics")]
public partial class PeakRatingCosmetics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Mantis and Talon become entitlement items, so accounts already standing at
        // or above the line need the grants a live set completion would have emitted.
        //
        // The backfill can only see current ratings. An account that peaked above 1300
        // on some earlier day and has since slipped back is NOT recovered here, because
        // no peak is stored — only the live BotRatings row. Going forward the rating is
        // re-checked after every rated set and the grant is permanent, so the gap is
        // limited to history that predates this migration.
        //
        // Experiment ladders carry '-exp-' in their rules version and are excluded:
        // an experiment arm must not mint cosmetics. Closed official eras still count.
        migrationBuilder.Sql("""
            WITH eligible AS (
                SELECT bot."OwnerUserId" AS "UserId",
                       MAX(rating."Rating") AS "BestRating"
                FROM "BotRatings" AS rating
                JOIN "Bots" AS bot
                  ON bot."Id" = rating."BotId"
                WHERE rating."RulesVersion" NOT LIKE '%-exp-%'
                GROUP BY bot."OwnerUserId"
                HAVING MAX(rating."Rating") >= 1300
            )
            INSERT INTO "EntitlementGrants"
                ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                 "GrantedAt", "RevokedAt", "MetadataJson")
            SELECT gen_random_uuid(), eligible."UserId",
                   item."EntitlementKey",
                   'achievement', 'rating-1300', now(), NULL,
                   jsonb_build_object(
                       'bestRating', eligible."BestRating",
                       'backfill', true)
            FROM eligible
            CROSS JOIN (VALUES
                ('bot-look:mantis'),
                ('projectile-look:talon')) AS item("EntitlementKey")
            ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
            DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "EntitlementGrants"
            WHERE "SourceKind" = 'achievement'
              AND "SourceId" = 'rating-1300'
              AND "EntitlementKey" IN (
                  'bot-look:mantis',
                  'projectile-look:talon');
            """);
    }
}
