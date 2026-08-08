using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BotArena.App.Tests;

public class PostgreSqlSchemaIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task LatestMigrations_FromEmptyDatabase_CreatePinnedIndexesAndConstraints()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = await database.CreateMigratedContextAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var indexes = (await db.Database
                .SqlQueryRaw<string>("""
                    SELECT indexname AS "Value"
                    FROM pg_indexes
                    WHERE schemaname = 'public'
                    """)
                .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("IX_Bots_Slug", indexes);
        Assert.Contains(
            "IX_EntitlementGrants_UserId_EntitlementKey_SourceKind_SourceId",
            indexes);
        Assert.Contains("IX_Matches_MapId_CreatedAt", indexes);
        Assert.Contains("IX_MatchParticipants_BotId_MatchId", indexes);
        Assert.Contains("IX_BotRatings_RulesVersion_Rating", indexes);

        var user = new User
        {
            DisplayName = "schema-test",
            Email = "schema@example.test",
            PasswordHash = "not-used",
        };
        db.Users.Add(user);
        db.Bots.AddRange(
            new Bot
            {
                OwnerUserId = user.Id,
                Name = "Schema One",
                Slug = "schema-unique",
            },
            new Bot
            {
                OwnerUserId = user.Id,
                Name = "Schema Two",
                Slug = "schema-unique",
            });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task CosmeticMigration_BackfillsExistingBuiltAndEquippedBot()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = database.CreateContext();
        IMigrator migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260725141835_ProjectileLooks");

        // Seed through the historical schema, not the current EF model. New nullable
        // Bot columns do not exist at this migration point, and EF includes them in an
        // INSERT even when their values are null.
        Guid userId = Guid.NewGuid();
        Guid botId = Guid.NewGuid();
        Guid versionId = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;
        DateTime builtAt = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users"
                ("Id", "DisplayName", "Email", "PasswordHash", "CreatedAt", "IsSystem")
            VALUES
                ({userId}, {"backfill-test"}, {"backfill@example.test"},
                 {"not-used"}, {createdAt}, {false})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Bots"
                ("Id", "OwnerUserId", "Name", "Slug", "Accent", "LookId",
                 "ProjectileLookId", "CreatedAt")
            VALUES
                ({botId}, {userId}, {"Legacy Lancer"}, {"legacy-lancer"},
                 {"#22d3ee"}, {"lancer"}, {"pulse-bolt"}, {createdAt})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "BotVersions"
                ("Id", "BotId", "VersionNumber", "EntryType", "SourcesJson",
                 "SourceHash", "Status", "GameRulesVersion",
                 "RuntimeProtocolVersion", "RuntimeConfigurationVersion",
                 "CreatedAt", "BuiltAt", "IsActive")
            VALUES
                ({versionId}, {botId}, {1}, {"Bot"}, CAST({"[]"} AS jsonb),
                 {"legacy"}, {"Built"},
                 {Engine.BotArenaVersions.GameRulesVersion},
                 {Engine.BotArenaVersions.RuntimeProtocolVersion},
                 {Engine.BotArenaVersions.RuntimeConfigurationVersion},
                 {builtAt}, {builtAt}, {true})
            """);

        await migrator.MigrateAsync();

        var grants = await db.EntitlementGrants
            .Where(grant => grant.UserId == userId)
            .Select(grant => new { grant.EntitlementKey, grant.SourceKind, grant.SourceId })
            .ToListAsync();
        Assert.Contains(
            grants,
            grant =>
                grant.EntitlementKey == "bot-look:lancer" &&
                grant.SourceKind == "achievement" &&
                grant.SourceId == "first-successful-build");
        Assert.Contains(
            grants,
            grant =>
                grant.EntitlementKey == "bot-look:lancer" &&
                grant.SourceKind == "legacy" &&
                grant.SourceId == "equipped-before-entitlements");
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ParticipantOwnerSnapshotMigration_BackfillsHistoricalOwnerName()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = database.CreateContext();
        IMigrator migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260725200541_UserNotifications");

        Guid userId = Guid.NewGuid();
        Guid botId = Guid.NewGuid();
        Guid matchId = Guid.NewGuid();
        Guid participantId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users"
                ("Id", "DisplayName", "Email", "PasswordHash", "CreatedAt", "IsSystem")
            VALUES
                ({userId}, {"Historical Owner"}, {"historical-owner@example.test"},
                 {"not-used"}, {now}, {false})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Bots"
                ("Id", "OwnerUserId", "Name", "Slug", "Accent", "LookId",
                 "ProjectileLookId", "CreatedAt")
            VALUES
                ({botId}, {userId}, {"Historical Bot"}, {"historical-bot"},
                 {"#22d3ee"}, {"vanguard"}, {"pulse-bolt"}, {now})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Matches"
                ("Id", "MapId", "MapVersion", "GameRulesVersion",
                 "RuntimeConfigurationVersion", "Seed", "Status", "CreatedAt",
                 "PresentationTicksPerSecond")
            VALUES
                ({matchId}, {"arena-01"}, {1},
                 {Engine.BotArenaVersions.GameRulesVersion},
                 {Engine.BotArenaVersions.RuntimeConfigurationVersion},
                 {1L}, {"Pending"}, {now}, {5})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MatchParticipants"
                ("Id", "MatchId", "Slot", "BotId", "BotVersionId",
                 "NameSnapshot", "AccentSnapshot", "LookIdSnapshot",
                 "ProjectileLookIdSnapshot", "ArtifactHashSnapshot")
            VALUES
                ({participantId}, {matchId}, {0}, {botId}, {Guid.NewGuid()},
                 {"Historical Bot"}, {"#22d3ee"}, {"vanguard"},
                 {"pulse-bolt"}, {"historical-artifact"})
            """);

        await migrator.MigrateAsync();

        MatchParticipant participant =
            await db.MatchParticipants.SingleAsync(
                candidate => candidate.Id == participantId);
        Assert.Equal(
            "Historical Owner",
            participant.OwnerDisplayNameSnapshot);
    }
}
