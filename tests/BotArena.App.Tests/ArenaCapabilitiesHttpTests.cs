using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Store;
using BotArena.Engine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class ArenaCapabilitiesHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Get_ProjectsFormatEffectiveAllowancesAndAuthoritativePlayability()
    {
        await using PostgreSqlDatabaseFixture database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
                     await database.CreateMigratedContextAsync())
        {
            Assert.Empty(migration.ChangeTracker.Entries());
        }

        // Anchored to real time, not a literal date: the dev signing cert's
        // NotBefore is stamped at first app start, so a frozen calendar date
        // eventually falls behind it on fresh checkouts. One hour ahead keeps
        // the fixed clock inside the cert's validity even when the cert is
        // minted during this very test's startup.
        DateTimeOffset now = DateTimeOffset.UtcNow.AddHours(1);
        using var baseFactory =
            new BotArenaApplicationFactory(database.ConnectionString);
        using WebApplicationFactory<Program> factory =
            baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(
                        new FixedTimeProvider(now));
                    services.RemoveAll<UnrankedMatchLimits>();
                    services.AddSingleton(
                        new UnrankedMatchLimits(AccountDailyLimit: 2));
                    services.RemoveAll<RankedSetLimits>();
                    services.AddSingleton(
                        new RankedSetLimits(
                            AccountDailyLimit: 2,
                            AccountConcurrentLimit: 1));
                });
            });

        using HttpClient anonymous = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                HandleCookies = false,
            });
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/arena")).StatusCode);

        using HttpClient client = factory.CreateClient();
        Guid accountId = await RegisterAsync(client);

        Guid playableOwnedId;
        Guid missingVersionId;
        Guid genericOnlyId;
        Guid lockedAppearanceId;
        await using (AppDbContext db = database.CreateContext())
        {
            var opponentOwner = new User
            {
                DisplayName = "Arena Opponent",
                Email = "arena-opponent@example.test",
                PasswordHash = "not-used",
            };
            var lockedOwner = new User
            {
                DisplayName = "Locked Look Owner",
                Email = "locked-look-owner@example.test",
                PasswordHash = "not-used",
            };
            var seededPlayableOwned = Bot(
                accountId,
                "Arena Ready",
                "arena-ready");
            var seededMissingVersion = Bot(
                accountId,
                "Arena Unbuilt",
                "arena-unbuilt");
            var playableOpponent = Bot(
                opponentOwner.Id,
                "Arena Opponent",
                "arena-opponent");
            var seededGenericOnly = Bot(
                opponentOwner.Id,
                "Generic Only",
                "generic-only");
            var seededLockedAppearance = Bot(
                lockedOwner.Id,
                "Locked Appearance",
                "locked-appearance");
            seededLockedAppearance.LookId = "helio-kite";

            BotVersion playableOwnedVersion = BuiltVersion(
                seededPlayableOwned.Id,
                "owned-artifact",
                BotContractProfiles.LegacyDuel);
            BotVersion playableOpponentVersion = BuiltVersion(
                playableOpponent.Id,
                "opponent-artifact",
                BotContractProfiles.LegacyDuel);
            BotVersion genericOnlyVersion = BuiltVersion(
                seededGenericOnly.Id,
                "generic-artifact",
                BotArenaVersions.GenericActorContractProfileId);
            BotVersion lockedVersion = BuiltVersion(
                seededLockedAppearance.Id,
                "locked-artifact",
                BotContractProfiles.LegacyDuel);

            db.AddRange(
                opponentOwner,
                lockedOwner,
                seededPlayableOwned,
                seededMissingVersion,
                playableOpponent,
                seededGenericOnly,
                seededLockedAppearance);
            db.BotVersions.AddRange(
                playableOwnedVersion,
                playableOpponentVersion,
                genericOnlyVersion,
                lockedVersion);
            db.EntitlementGrants.Add(new EntitlementGrant
            {
                UserId = accountId,
                EntitlementKey =
                    AccountCapacity.ExtraDailyRankedSetsKey,
                SourceKind = CosmeticCatalog.PurchaseSource,
                SourceId = "arena-capability-test",
                GrantedAt = now.UtcDateTime.AddDays(-1),
            });
            db.Matches.AddRange(
                UnrankedMatch(
                    accountId,
                    now.UtcDateTime.AddHours(-23)),
                UnrankedMatch(
                    accountId,
                    now.UtcDateTime.AddHours(-2)));
            for (int index = 0;
                 index < 2 + AccountCapacity.ExtraDailyRankedSets;
                 index++)
            {
                db.MatchSets.Add(new MatchSet
                {
                    BotAId = seededPlayableOwned.Id,
                    BotBId = playableOpponent.Id,
                    BotAVersionId = playableOwnedVersion.Id,
                    BotBVersionId = playableOpponentVersion.Id,
                    Status = index == 0
                        ? MatchSetStatus.Running
                        : MatchSetStatus.Completed,
                    CreatedAt = now.UtcDateTime.AddHours(-22 + index * 2),
                });
            }

            await db.SaveChangesAsync();
            playableOwnedId = seededPlayableOwned.Id;
            missingVersionId = seededMissingVersion.Id;
            genericOnlyId = seededGenericOnly.Id;
            lockedAppearanceId = seededLockedAppearance.Id;
        }

        ArenaCapabilitiesResponse capabilities =
            await GetCapabilitiesAsync(client);

        Assert.Equal(
            GameRules.Current.RulesVersion,
            capabilities.Format.RulesVersion);
        Assert.Equal(
            BotContractProfiles.LegacyDuel,
            capabilities.Format.RequiredContractProfileId);
        Assert.Equal(1, capabilities.Format.Unranked.GamesPerMatch);
        Assert.Equal(
            DuelArenaDefinition.Official.DefaultUnrankedMapId,
            capabilities.Format.Unranked.DefaultMapId);
        Assert.Equal(
            DuelMirrored6V1.GameCount,
            capabilities.Format.Ranked.GamesPerSet);
        Assert.Equal(
            DuelMirrored6V1.MapPairCount,
            capabilities.Format.Ranked.MapSeedPairs);
        Assert.True(capabilities.Format.Ranked.MirroredSlots);
        Assert.Equal(
            DuelArenaDefinition.Official.RankedMapPool,
            capabilities.Format.Ranked.MapPool);
        Assert.Equal(
            RankedMatchmaking.PoolSize,
            capabilities.Format.Ranked.MatchmakingPoolSize);

        Assert.Equal(2, capabilities.UnrankedAllowance.Used);
        Assert.Equal(2, capabilities.UnrankedAllowance.Limit);
        Assert.Equal(0, capabilities.UnrankedAllowance.Remaining);
        Assert.Equal(
            ArenaAllowanceService.RollingWindowHours,
            capabilities.UnrankedAllowance.RollingWindowHours);
        Assert.Equal(
            now.UtcDateTime.AddHours(1),
            capabilities.UnrankedAllowance.NextDailySlotAt);
        Assert.False(capabilities.UnrankedAllowance.CanStart);
        Assert.Equal(
            ApplicationErrorCodes.MatchUnrankedDailyLimit,
            capabilities.UnrankedAllowance.RefusalCode);
        Assert.Equal(
            capabilities.UnrankedAllowance.NextDailySlotAt,
            capabilities.UnrankedAllowance.RetryAt);

        Assert.Equal(7, capabilities.RankedAllowance.Used);
        Assert.Equal(7, capabilities.RankedAllowance.Limit);
        Assert.Equal(0, capabilities.RankedAllowance.Remaining);
        Assert.Equal(1, capabilities.RankedAllowance.InProgress);
        Assert.Equal(1, capabilities.RankedAllowance.ConcurrencyLimit);
        Assert.Equal(
            now.UtcDateTime.AddHours(2),
            capabilities.RankedAllowance.NextDailySlotAt);
        Assert.False(capabilities.RankedAllowance.CanStart);
        Assert.Equal(
            ApplicationErrorCodes.MatchRankedConcurrentLimit,
            capabilities.RankedAllowance.RefusalCode);
        Assert.Null(capabilities.RankedAllowance.RetryAt);

        MatchPlayabilityResponse playableOwned =
            FindBot(capabilities, playableOwnedId);
        Assert.True(playableOwned.IsOwned);
        Assert.True(playableOwned.Playable);
        Assert.Null(playableOwned.RefusalCode);
        Assert.Null(playableOwned.RefusalDetail);

        MatchPlayabilityResponse missingVersion =
            FindBot(capabilities, missingVersionId);
        Assert.True(missingVersion.IsOwned);
        Assert.False(missingVersion.Playable);
        Assert.Equal(
            ApplicationErrorCodes.MatchActiveVersionRequired,
            missingVersion.RefusalCode);
        Assert.False(string.IsNullOrEmpty(missingVersion.RefusalDetail));

        MatchPlayabilityResponse genericOnly =
            FindBot(capabilities, genericOnlyId);
        Assert.False(genericOnly.IsOwned);
        Assert.False(genericOnly.Playable);
        Assert.Equal(
            ApplicationErrorCodes.MatchContractProfileRequired,
            genericOnly.RefusalCode);

        MatchPlayabilityResponse lockedAppearance =
            FindBot(capabilities, lockedAppearanceId);
        Assert.False(lockedAppearance.Playable);
        Assert.Equal(
            ApplicationErrorCodes.BotLookLocked,
            lockedAppearance.RefusalCode);

        await using (AppDbContext db = database.CreateContext())
        {
            MatchSet running = await db.MatchSets.SingleAsync(
                set => set.Status == MatchSetStatus.Running);
            running.Status = MatchSetStatus.Completed;
            await db.SaveChangesAsync();
        }

        ArenaCapabilitiesResponse dailyBlocked =
            await GetCapabilitiesAsync(client);
        Assert.Equal(0, dailyBlocked.RankedAllowance.InProgress);
        Assert.False(dailyBlocked.RankedAllowance.CanStart);
        Assert.Equal(
            ApplicationErrorCodes.MatchRankedDailyLimit,
            dailyBlocked.RankedAllowance.RefusalCode);
        Assert.Equal(
            dailyBlocked.RankedAllowance.NextDailySlotAt,
            dailyBlocked.RankedAllowance.RetryAt);
    }

    private static MatchPlayabilityResponse FindBot(
        ArenaCapabilitiesResponse response,
        Guid botId) =>
        response.Bots.Single(bot => bot.BotId == botId);

    private static Bot Bot(
        Guid ownerUserId,
        string name,
        string slug) =>
        new()
        {
            OwnerUserId = ownerUserId,
            Name = name,
            Slug = slug,
        };

    private static BotVersion BuiltVersion(
        Guid botId,
        string artifactHash,
        string contractProfile) =>
        new()
        {
            BotId = botId,
            VersionNumber = 1,
            EntryType = "Bot",
            SourcesJson = "[]",
            SourceHash = $"source-{artifactHash}",
            Status = BuildStatus.Built,
            ArtifactHash = artifactHash,
            SupportedContractProfiles = [contractProfile],
            IsActive = true,
        };

    private static Match UnrankedMatch(
        Guid accountId,
        DateTime createdAt) =>
        new()
        {
            MapId = "arena-01",
            InitiatedByUserId = accountId,
            CreatedAt = createdAt,
        };

    private static async Task<Guid> RegisterAsync(
        HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                displayName = "Arena Capability Owner",
                email = "arena-capability-owner@example.test",
                password = "correct-horse-battery-staple",
            });
        response.EnsureSuccessStatusCode();
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<ArenaCapabilitiesResponse>
        GetCapabilitiesAsync(HttpClient client)
    {
        ArenaCapabilitiesResponse? response =
            await client.GetFromJsonAsync<ArenaCapabilitiesResponse>(
                "/api/arena");
        return response
            ?? throw new InvalidOperationException(
                "Arena capabilities response was empty.");
    }
}
