using System.Net.Http.Json;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class RankedContractProfileMatchmakingHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task RankedMatchmakingSkipsGenericOnlyActiveBots()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
                     await database.CreateMigratedContextAsync())
        {
            Assert.Empty(migration.ChangeTracker.Entries());
        }

        using var factory =
            new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        Guid challengerOwnerId = await RegisterAsync(client);

        Guid challengerId;
        Guid legacyOpponentId;
        await using (AppDbContext db = database.CreateContext())
        {
            var opponentOwner = new User
            {
                DisplayName = "Ranked Profile Opponent",
                Email = "ranked-profile-opponent@example.test",
                PasswordHash = "not-used",
            };
            var challenger = Bot(
                challengerOwnerId,
                "Ranked Profile Challenger",
                "ranked-profile-challenger");
            var legacyOpponent = Bot(
                opponentOwner.Id,
                "Ranked Legacy Opponent",
                "ranked-legacy-opponent");
            db.AddRange(opponentOwner, challenger, legacyOpponent);
            db.BotVersions.AddRange(
                Version(
                    challenger.Id,
                    "ranked-challenger-artifact",
                    BotContractProfiles.LegacyDuel),
                Version(
                    legacyOpponent.Id,
                    "ranked-legacy-artifact",
                    BotContractProfiles.LegacyDuel));
            db.BotRatings.Add(new BotRating
            {
                BotId = legacyOpponent.Id,
                RulesVersion = GameRules.Current.RulesVersion,
                Rating = 3000,
            });

            for (int index = 0;
                 index < RankedMatchmaking.PoolSize;
                 index++)
            {
                var owner = new User
                {
                    DisplayName = $"Generic Ranked Owner {index}",
                    Email = $"generic-ranked-{index}@example.test",
                    PasswordHash = "not-used",
                };
                Bot genericOnly = Bot(
                    owner.Id,
                    $"Generic Ranked Decoy {index}",
                    $"generic-ranked-decoy-{index}");
                db.AddRange(owner, genericOnly);
                db.BotVersions.Add(
                    Version(
                        genericOnly.Id,
                        $"generic-ranked-artifact-{index}",
                        BotArenaVersions
                            .GenericActorContractProfileId));
                db.BotRatings.Add(new BotRating
                {
                    BotId = genericOnly.Id,
                    RulesVersion = GameRules.Current.RulesVersion,
                    Rating = BotRating.DefaultRating + index,
                });
            }

            await db.SaveChangesAsync();
            challengerId = challenger.Id;
            legacyOpponentId = legacyOpponent.Id;
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/matches/ranked",
            new { botId = challengerId });
        response.EnsureSuccessStatusCode();
        Guid setId = await ReadIdAsync(response);

        await using AppDbContext verify = database.CreateContext();
        MatchSet set = await verify.MatchSets.SingleAsync(
            candidate => candidate.Id == setId);
        Assert.Equal(legacyOpponentId, set.BotBId);
    }

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

    private static BotVersion Version(
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

    private static async Task<Guid> RegisterAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                displayName = "Ranked Profile Challenger",
                email = "ranked-profile-challenger@example.test",
                password = "correct-horse-battery-staple",
            });
        response.EnsureSuccessStatusCode();
        return await ReadIdAsync(response);
    }

    private static async Task<Guid> ReadIdAsync(
        HttpResponseMessage response)
    {
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
