using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class GenericActorMatchRetryTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task UnknownFailureEscapesWithoutPrematurelyFailingMatch()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid matchId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            PlaylistVersion playlistVersion =
                await new FrontlineLabsPlaylistSeeder(db).SeedAsync();
            FrontlineLabsPlaylistDefinition definition =
                FrontlineLabsPlaylistDefinition.Create();
            var match = new Match
            {
                MapId = definition.Match.Map.Id,
                MapVersion = definition.Match.Map.Version,
                GameRulesVersion = definition.Match.Rules.RulesetId,
                RuntimeConfigurationVersion =
                    definition.Match.CapabilityVersions
                        .RuntimeConfigurationVersion,
                PlaylistVersionId = playlistVersion.Id,
                Seed = 9,
                // Deliberately no participants: execution reaches a domain
                // contradiction without touching the object store or WASM.
            };
            db.Matches.Add(match);
            await db.SaveChangesAsync();
            matchId = match.Id;
        }

        using var factory =
            new BotArenaApplicationFactory(database.ConnectionString);
        InvalidOperationException first =
            await ExecuteAttemptAsync(
                factory,
                matchId);
        Assert.Contains(
            "participants",
            first.Message,
            StringComparison.OrdinalIgnoreCase);

        await using (AppDbContext verifyRetryable =
                     database.CreateContext())
        {
            Match match = await verifyRetryable.Matches.SingleAsync(
                candidate => candidate.Id == matchId);
            Assert.Equal(MatchStatus.Running, match.Status);
            Assert.Null(match.Error);
            Assert.Null(match.CompletedAt);
        }

        InvalidOperationException retry =
            await ExecuteAttemptAsync(
                factory,
                matchId);
        Assert.Equal(first.Message, retry.Message);

        await using AppDbContext verifyStillRetryable =
            database.CreateContext();
        Match retryable = await verifyStillRetryable.Matches.SingleAsync(
            candidate => candidate.Id == matchId);
        Assert.Equal(MatchStatus.Running, retryable.Status);
        Assert.Null(retryable.Error);
        Assert.Null(retryable.CompletedAt);
    }

    private static async Task<InvalidOperationException>
        ExecuteAttemptAsync(
            BotArenaApplicationFactory factory,
            Guid matchId)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();
        BackgroundJob job =
            BackgroundJob.ExecuteGenericActorMatch(
                matchId,
                FrontlineLabsPlaylistDefinition.PlaylistKey,
                FrontlineLabsPlaylistDefinition.Version);
        var dispatcher = scope.ServiceProvider.GetRequiredService<
            BackgroundJobDispatcher>();
        return await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(
                job,
                CancellationToken.None));
    }
}
