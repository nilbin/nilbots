using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class LegacyMatchExecutionContractProfileTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task DuelExecutorFailsClosedForGenericOnlyPinnedArtifact()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid matchId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            LegacyCompetitionIdentity identity =
                await new LegacyCompetitionIdentityResolver(db)
                    .ResolveOrCreateAsync(
                        GameRules.Current.RulesVersion,
                        GameRules.Current.RulesVersion);
            var owner = new User
            {
                DisplayName = "Duel Profile Guard",
                Email = "duel-profile-guard@example.test",
                PasswordHash = "not-used",
            };
            var botA = new Bot
            {
                OwnerUserId = owner.Id,
                Name = "Generic Only",
                Slug = "duel-profile-generic-only",
            };
            var botB = new Bot
            {
                OwnerUserId = owner.Id,
                Name = "Legacy Compatible",
                Slug = "duel-profile-legacy",
            };
            BotVersion versionA = Version(
                botA.Id,
                BotArenaVersions.GenericActorContractProfileId);
            BotVersion versionB = Version(
                botB.Id,
                BotContractProfiles.LegacyDuel);
            var match = new Match
            {
                MapId = "basic-01",
                GameRulesVersion = GameRules.Current.RulesVersion,
                RuntimeConfigurationVersion =
                    BotArenaVersions.RuntimeConfigurationVersion,
                PlaylistVersionId = identity.PlaylistVersionId,
                Seed = 1,
            };
            match.Participants.Add(
                Participant(
                    match.Id,
                    slot: 0,
                    botA,
                    versionA));
            match.Participants.Add(
                Participant(
                    match.Id,
                    slot: 1,
                    botB,
                    versionB));
            db.AddRange(
                owner,
                botA,
                botB,
                versionA,
                versionB,
                match);
            await db.SaveChangesAsync();
            matchId = match.Id;
        }

        using var factory =
            new BotArenaApplicationFactory(database.ConnectionString);
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<MatchExecutionJobHandler>();

        JobExecutionResult result = await handler.HandleAsync(
            matchId,
            CancellationToken.None);

        Assert.Equal("match_failed", result.Outcome);
        await using AppDbContext verify = database.CreateContext();
        Match failed = await verify.Matches.SingleAsync(
            match => match.Id == matchId);
        Assert.Equal(MatchStatus.Failed, failed.Status);
        Assert.Contains(
            BotContractProfiles.LegacyDuel,
            failed.Error,
            StringComparison.Ordinal);
        Assert.Null(failed.ReplayKey);
        Assert.Null(failed.WinnerSlot);
    }

    private static BotVersion Version(
        Guid botId,
        string contractProfile) =>
        new()
        {
            BotId = botId,
            VersionNumber = 1,
            EntryType = "Bot",
            SourcesJson = "[]",
            SourceHash = $"source-{contractProfile}",
            Status = BuildStatus.Built,
            ArtifactKey = $"artifacts/{Guid.NewGuid():N}.wasm",
            ArtifactHash = new string('a', 64),
            SupportedContractProfiles = [contractProfile],
            IsActive = true,
        };

    private static MatchParticipant Participant(
        Guid matchId,
        int slot,
        Bot bot,
        BotVersion version) =>
        new()
        {
            MatchId = matchId,
            Slot = slot,
            BotId = bot.Id,
            BotVersionId = version.Id,
            NameSnapshot = bot.Name,
            AccentSnapshot = bot.Accent,
            ArtifactHashSnapshot =
                version.ArtifactHash
                ?? throw new InvalidOperationException(
                    "Test version needs an artifact hash."),
        };
}
