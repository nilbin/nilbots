using System.Data.Common;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BotArena.App.Tests;

public sealed class RankedMatchSetFinalizerIntegrationTests
{
    private static readonly DateTimeOffset FinalizedAt =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task TwoWorkers_FinalizeRatingsAndProgressionExactlyOnce()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        Guid setId;
        await using (AppDbContext seed = await database.CreateMigratedContextAsync())
            setId = await SeedReadySetAsync(seed, previousCompletedSets: 99);

        await using AppDbContext firstDb = database.CreateContext();
        await using AppDbContext secondDb = database.CreateContext();
        RankedMatchSetFinalizer first = CreateFinalizer(firstDb);
        RankedMatchSetFinalizer second = CreateFinalizer(secondDb);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<RankedSetFinalizationOutcome> firstTask = Task.Run(async () =>
        {
            await start.Task;
            return await first.TryFinalizeAsync(setId);
        });
        Task<RankedSetFinalizationOutcome> secondTask = Task.Run(async () =>
        {
            await start.Task;
            return await second.TryFinalizeAsync(setId);
        });
        start.SetResult();
        RankedSetFinalizationOutcome[] outcomes =
            await Task.WhenAll(firstTask, secondTask);

        Assert.Contains(RankedSetFinalizationOutcome.Finalized, outcomes);
        Assert.Contains(RankedSetFinalizationOutcome.AlreadyTerminal, outcomes);

        await using AppDbContext verify = database.CreateContext();
        MatchSet set = await verify.MatchSets.SingleAsync(candidate => candidate.Id == setId);
        Assert.Equal(MatchSetStatus.Completed, set.Status);
        Assert.Equal(FinalizedAt.UtcDateTime, set.CompletedAt);
        Assert.Equal(6, set.ScoreA);
        Assert.Equal(0, set.ScoreB);
        Assert.Equal(16, set.RatingChangeA, precision: 8);

        BotRating[] ratings = await verify.BotRatings
            .OrderBy(rating => rating.Rating)
            .ToArrayAsync();
        Assert.Equal(2, ratings.Length);
        Assert.Equal(1184, ratings[0].Rating, precision: 8);
        Assert.Equal(1216, ratings[1].Rating, precision: 8);
        Assert.All(ratings, rating => Assert.Equal(1, rating.RankedSets));

        Assert.Equal(2, await verify.EntitlementGrants.CountAsync());
        UserNotification notification =
            Assert.Single(await verify.UserNotifications.ToArrayAsync());
        UserNotificationResponse response =
            UserNotificationContracts.ToResponse(notification);
        var payload = Assert.IsType<EntitlementEarnedPayload>(response.Payload);
        Assert.Equal(2, payload.Items.Count);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ConcurrentSetsSharingBots_SerializeRatingUpdates()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        Guid firstSetId;
        Guid secondSetId;
        await using (AppDbContext seed = await database.CreateMigratedContextAsync())
        {
            firstSetId = await SeedReadySetAsync(seed, previousCompletedSets: 0);
            Bot[] bots = await seed.Bots.OrderBy(bot => bot.Id).ToArrayAsync();
            MatchSet secondSet = AddReadySet(seed, bots[0], bots[1]);
            secondSetId = secondSet.Id;
            await seed.SaveChangesAsync();
        }

        await using AppDbContext firstDb = database.CreateContext();
        await using AppDbContext secondDb = database.CreateContext();
        RankedSetFinalizationOutcome[] outcomes = await Task.WhenAll(
            CreateFinalizer(firstDb).TryFinalizeAsync(firstSetId),
            CreateFinalizer(secondDb).TryFinalizeAsync(secondSetId));

        Assert.All(
            outcomes,
            outcome => Assert.Equal(RankedSetFinalizationOutcome.Finalized, outcome));
        await using AppDbContext verify = database.CreateContext();
        BotRating[] ratings = await verify.BotRatings.ToArrayAsync();
        Assert.Equal(2, ratings.Length);
        Assert.All(ratings, rating => Assert.Equal(2, rating.RankedSets));
        Assert.Equal(2400, ratings.Sum(rating => rating.Rating), precision: 8);
        Assert.Equal(
            2,
            await verify.MatchSets.CountAsync(
                set => set.Status == MatchSetStatus.Completed));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task FailureAfterRatingFlush_RollsBackSetAndRatings()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        Guid setId;
        await using (AppDbContext seed = await database.CreateMigratedContextAsync())
            setId = await SeedReadySetAsync(seed, previousCompletedSets: 99);

        await using AppDbContext failingDb =
            database.CreateContext(new FailAfterSaveInterceptor());
        await Assert.ThrowsAsync<InjectedFinalizationFailure>(
            () => CreateFinalizer(failingDb).TryFinalizeAsync(setId));

        await AssertStillRunningWithoutEffectsAsync(database, setId);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task FailureBeforeCommit_RollsBackProgressionAndNotification()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        Guid setId;
        await using (AppDbContext seed = await database.CreateMigratedContextAsync())
            setId = await SeedReadySetAsync(seed, previousCompletedSets: 99);

        await using AppDbContext failingDb =
            database.CreateContext(new FailBeforeCommitInterceptor());
        await Assert.ThrowsAsync<InjectedFinalizationFailure>(
            () => CreateFinalizer(failingDb).TryFinalizeAsync(setId));

        await AssertStillRunningWithoutEffectsAsync(database, setId);
    }

    private static RankedMatchSetFinalizer CreateFinalizer(AppDbContext db)
    {
        CosmeticCatalog catalog = CosmeticCatalog.LoadDefault();
        var entitlements = new CosmeticEntitlementService(
            db,
            catalog,
            new FixedTimeProvider(FinalizedAt));
        var achievements = new CosmeticAchievementService(db, entitlements);
        return new RankedMatchSetFinalizer(
            db,
            achievements,
            new FixedTimeProvider(FinalizedAt));
    }

    private static async Task<Guid> SeedReadySetAsync(
        AppDbContext db,
        int previousCompletedSets)
    {
        string suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            DisplayName = "finalizer-test",
            Email = $"finalizer-{suffix}@example.test",
            PasswordHash = "not-used",
        };
        var botA = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Finalizer A",
            Slug = $"finalizer-a-{suffix}",
        };
        var botB = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Finalizer B",
            Slug = $"finalizer-b-{suffix}",
        };
        db.Users.Add(user);
        db.Bots.AddRange(botA, botB);
        for (int index = 0; index < previousCompletedSets; index++)
        {
            db.MatchSets.Add(new MatchSet
            {
                BotAId = botA.Id,
                BotBId = botB.Id,
                BotAVersionId = Guid.NewGuid(),
                BotBVersionId = Guid.NewGuid(),
                Status = MatchSetStatus.Completed,
                CompletedAt = FinalizedAt.UtcDateTime.AddDays(-1),
            });
        }

        MatchSet set = AddReadySet(db, botA, botB);
        await db.SaveChangesAsync();
        return set.Id;
    }

    private static MatchSet AddReadySet(
        AppDbContext db,
        Bot botA,
        Bot botB)
    {
        var set = new MatchSet
        {
            BotAId = botA.Id,
            BotBId = botB.Id,
            BotAVersionId = Guid.NewGuid(),
            BotBVersionId = Guid.NewGuid(),
            GameRulesVersion = "0.5",
        };
        db.MatchSets.Add(set);
        for (int game = 1; game <= MatchSet.Games; game++)
        {
            var match = new Match
            {
                MapId = "basic-01",
                Seed = game,
                Status = MatchStatus.Completed,
                WinnerSlot = 0,
                EndReason = "Elimination",
                EndTick = 10,
                GameRulesVersion = "0.5",
                MatchSetId = set.Id,
                SetGame = game,
                CompletedAt = FinalizedAt.UtcDateTime.AddMinutes(-1),
            };
            match.Participants.Add(CreateParticipant(match.Id, 0, botA.Id));
            match.Participants.Add(CreateParticipant(match.Id, 1, botB.Id));
            db.Matches.Add(match);
        }
        return set;
    }

    private static MatchParticipant CreateParticipant(
        Guid matchId,
        int slot,
        Guid botId) =>
        new()
        {
            MatchId = matchId,
            Slot = slot,
            BotId = botId,
            BotVersionId = Guid.NewGuid(),
            NameSnapshot = $"bot-{slot}",
            AccentSnapshot = "#22d3ee",
        };

    private static async Task AssertStillRunningWithoutEffectsAsync(
        PostgreSqlDatabaseFixture database,
        Guid setId)
    {
        await using AppDbContext verify = database.CreateContext();
        MatchSet set = await verify.MatchSets.SingleAsync(candidate => candidate.Id == setId);
        Assert.Equal(MatchSetStatus.Running, set.Status);
        Assert.Null(set.CompletedAt);
        Assert.Empty(await verify.BotRatings.ToArrayAsync());
        Assert.Empty(await verify.EntitlementGrants.ToArrayAsync());
        Assert.Empty(await verify.UserNotifications.ToArrayAsync());
    }

    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new InjectedFinalizationFailure());
    }

    private sealed class FailBeforeCommitInterceptor : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult>(
                new InjectedFinalizationFailure());
    }

    private sealed class InjectedFinalizationFailure : Exception;
}
