using BotArena.App.Jobs;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

/// <summary>
/// What decides whether a phone buzzes.
/// <para>
/// Every case here is a way to send something the player should not receive: a duplicate of
/// what they just read on screen, a kind they turned off, or the same result twice because
/// the job retried. None of them is visible in a type signature.
/// </para>
/// </summary>
public class DeliverPushJobHandlerTests
{
    private sealed class RecordingTransport : IPushTransport
    {
        public List<PushMessage> Sent { get; } = [];
        public bool ReportDead { get; set; }

        public Task<IReadOnlyList<PushResult>> SendAsync(
            IReadOnlyList<PushMessage> messages,
            CancellationToken cancellationToken)
        {
            Sent.AddRange(messages);
            return Task.FromResult<IReadOnlyList<PushResult>>(messages
                .Select(message => new PushResult(
                    message.PushToken, !ReportDead, ReportDead ? "DeviceNotRegistered" : null,
                    ReportDead))
                .ToList());
        }
    }

    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    private static MatchSettledPayload Result() => new(
        Guid.NewGuid(), "arena-01", Guid.NewGuid(), "Pincer", "vanguard", "#22d3ee",
        "Win", "hunter");

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ANotificationAlreadyReadInAppIsNotPushed()
    {
        await using var harness = await Harness.CreateAsync();
        Guid notificationId = await harness.SeedNotificationAsync(readAt: Now);
        await harness.SeedDeviceAsync("ExponentPushToken[a]");

        var outcome = await harness.Handler.HandleAsync(notificationId, default);

        // The whole reason the push job is scheduled late rather than sent inline: someone
        // watching the app has already been told.
        Assert.Equal("suppressed_read_in_app", outcome.Outcome);
        Assert.Empty(harness.Transport.Sent);
        Assert.Equal(
            NotificationDeliveryStates.Suppressed,
            (await harness.Db.NotificationDeliveries.SingleAsync()).State);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AKindThePlayerTurnedOffIsNotPushed()
    {
        await using var harness = await Harness.CreateAsync();
        Guid notificationId = await harness.SeedNotificationAsync();
        await harness.SeedDeviceAsync("ExponentPushToken[a]");
        harness.Db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = harness.UserId,
            Kind = UserNotificationKinds.MatchSettled,
            PushEnabled = false,
        });
        await harness.Db.SaveChangesAsync();

        var outcome = await harness.Handler.HandleAsync(notificationId, default);

        Assert.Equal("suppressed_opted_out", outcome.Outcome);
        Assert.Empty(harness.Transport.Sent);
        // The durable record still exists and still reached the inbox — a preference
        // silences a channel, it does not erase history.
        Assert.NotNull(await harness.Db.UserNotifications.FindAsync(notificationId));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ARetryDoesNotPushTwiceToTheSameDevice()
    {
        await using var harness = await Harness.CreateAsync();
        Guid notificationId = await harness.SeedNotificationAsync();
        await harness.SeedDeviceAsync("ExponentPushToken[a]");

        Assert.Equal("pushed", (await harness.Handler.HandleAsync(notificationId, default)).Outcome);
        Assert.Single(harness.Transport.Sent);

        // A job that failed after sending — or an operator replaying one. Without the
        // delivery record this notifies the same phone again.
        harness.Db.ChangeTracker.Clear();
        Assert.Equal(
            "already_delivered",
            (await harness.Handler.HandleAsync(notificationId, default)).Outcome);
        Assert.Single(harness.Transport.Sent);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AnUnregisteredTokenIsDroppedRatherThanRetriedForever()
    {
        await using var harness = await Harness.CreateAsync();
        Guid notificationId = await harness.SeedNotificationAsync();
        await harness.SeedDeviceAsync("ExponentPushToken[gone]");
        harness.Transport.ReportDead = true;

        await harness.Handler.HandleAsync(notificationId, default);

        harness.Db.ChangeTracker.Clear();
        // Kept, and every future notification for this account would fan out to a phone
        // that deleted the app.
        Assert.Empty(await harness.Db.DeviceRegistrations.ToListAsync());
        Assert.Equal(
            NotificationDeliveryStates.Failed,
            (await harness.Db.NotificationDeliveries.SingleAsync()).State);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AnAccountWithNoDevicesIsRecordedRatherThanRetried()
    {
        await using var harness = await Harness.CreateAsync();
        Guid notificationId = await harness.SeedNotificationAsync();

        var outcome = await harness.Handler.HandleAsync(notificationId, default);

        Assert.Equal("suppressed_no_devices", outcome.Outcome);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private PostgreSqlDatabaseFixture Database { get; init; } = null!;
        public AppDbContext Db { get; private init; } = null!;
        public RecordingTransport Transport { get; } = new();
        public Guid UserId { get; private set; }
        public DeliverPushJobHandler Handler { get; private set; } = null!;

        public static async Task<Harness> CreateAsync()
        {
            var database = await PostgreSqlDatabaseFixture.CreateAsync();
            await using (var migrate = await database.CreateMigratedContextAsync()) { }
            var harness = new Harness { Database = database, Db = database.CreateContext() };

            var user = new Accounts.User
            {
                Email = $"push-{Guid.NewGuid():N}@example.test",
                DisplayName = "Push",
                PasswordHash = "x",
            };
            harness.Db.Users.Add(user);
            await harness.Db.SaveChangesAsync();
            harness.UserId = user.Id;
            harness.Handler = new DeliverPushJobHandler(
                harness.Db,
                harness.Transport,
                new FakeTimeProvider(Now),
                NullLogger<DeliverPushJobHandler>.Instance);
            return harness;
        }

        public async Task<Guid> SeedNotificationAsync(DateTime? readAt = null)
        {
            var notification = new UserNotification
            {
                UserId = UserId,
                Kind = UserNotificationKinds.MatchSettled,
                DedupeKey = $"match:{Guid.NewGuid()}",
                PayloadJson = UserNotificationContracts.Serialize(Result()),
                CreatedAt = Now,
                ReadAt = readAt,
            };
            Db.UserNotifications.Add(notification);
            await Db.SaveChangesAsync();
            return notification.Id;
        }

        public async Task SeedDeviceAsync(string token)
        {
            Db.DeviceRegistrations.Add(new DeviceRegistration
            {
                UserId = UserId,
                PushToken = token,
                DeviceId = Guid.NewGuid().ToString(),
                Platform = "ios",
            });
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private sealed class FakeTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
