using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

/// <summary>
/// A challenge and its result are one row that changes, not two rows (DECISIONS #118).
/// <para>
/// PostgreSQL-backed rather than mocked, because the behaviour under test *is* the SQL:
/// <c>ON CONFLICT DO UPDATE</c> with a guard, and <c>RETURNING</c> feeding the realtime
/// channel. Nothing about that is exercised by a fake, and it is the half most likely to
/// be subtly wrong.
/// </para>
/// </summary>
public class UserNotificationSupersessionTests
{
    private static readonly Guid MatchId = Guid.NewGuid();
    private static readonly Guid BotId = Guid.NewGuid();

    private static MatchChallengedPayload Challenge() =>
        new(MatchId, "arena-01", BotId, "Pincer", "vanguard", "#22d3ee", "hunter");

    private static MatchSettledPayload Result(string outcome = "Win") =>
        new(MatchId, "arena-01", BotId, "Pincer", "vanguard", "#22d3ee", outcome, "hunter");

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AResultReplacesItsChallengeInPlace()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migrate = await database.CreateMigratedContextAsync()) { }
        await using var db = database.CreateContext();
        var writer = new UserNotificationWriter(db);
        Guid userId = await SeedUserAsync(db);
        string key = UserNotificationKeys.MatchSubject(MatchId, BotId);
        DateTime now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(await writer.WriteAsync(
            userId, UserNotificationKinds.MatchChallenged, key, Challenge(), now, default));

        // The player reads "watch this", and then does not look.
        UserNotification stored = await db.UserNotifications.SingleAsync();
        stored.ReadAt = now;
        await db.SaveChangesAsync();

        Assert.True(await writer.SupersedeAsync(
            userId, UserNotificationKinds.MatchSettled, key, Result(),
            now.AddMinutes(1), default));

        db.ChangeTracker.Clear();
        UserNotification settled = await db.UserNotifications.SingleAsync();

        // One row, not two: the inbox never carries a dead "watch this" beside its result.
        Assert.Equal(UserNotificationKinds.MatchSettled, settled.Kind);
        Assert.Equal(settled.Id, stored.Id);
        // Cleared, because an outcome is new information — reading the challenge is not
        // reading the result.
        Assert.Null(settled.ReadAt);
        var payload = Assert.IsType<MatchSettledPayload>(
            UserNotificationContracts.ToResponse(settled).Payload);
        Assert.Equal("Win", payload.Outcome);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AnIdenticalRetryChangesNothingAndAnnouncesNothing()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migrate = await database.CreateMigratedContextAsync()) { }
        await using var db = database.CreateContext();
        var writer = new UserNotificationWriter(db);
        Guid userId = await SeedUserAsync(db);
        string key = UserNotificationKeys.MatchSubject(MatchId, BotId);
        DateTime now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(await writer.SupersedeAsync(
            userId, UserNotificationKinds.MatchSettled, key, Result(), now, default));

        UserNotification first = await db.UserNotifications.SingleAsync();
        first.ReadAt = now;
        await db.SaveChangesAsync();

        // A replayed job. Without the WHERE guard this would clear ReadAt and re-announce,
        // so a result the player had already dismissed would come back.
        Assert.False(await writer.SupersedeAsync(
            userId, UserNotificationKinds.MatchSettled, key, Result(),
            now.AddMinutes(5), default));

        db.ChangeTracker.Clear();
        UserNotification unchanged = await db.UserNotifications.SingleAsync();
        Assert.NotNull(unchanged.ReadAt);
        Assert.Equal(now, unchanged.CreatedAt);
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext db)
    {
        var user = new Accounts.User
        {
            Email = $"supersede-{Guid.NewGuid():N}@example.test",
            DisplayName = "Supersede",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
