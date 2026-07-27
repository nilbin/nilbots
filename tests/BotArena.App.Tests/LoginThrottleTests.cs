using System.Net;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Shared;

namespace BotArena.App.Tests;

/// <summary>
/// Brute-force protection that survives a restart and more than one web server.
/// <para>
/// The only limit here whose loose end helps an attacker rather than a customer: the
/// in-memory limiter's ten a minute is ten *per web process*, and resets on every deploy.
/// </para>
/// </summary>
public class LoginThrottleTests
{
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IPAddress Origin = IPAddress.Parse("198.51.100.7");
    private static readonly IPAddress OtherOrigin = IPAddress.Parse("203.0.113.9");

    private static readonly LoginThrottleLimits Limits =
        new(IdentifierLimit: 3, NetworkLimit: 5, Window: TimeSpan.FromMinutes(15));

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task FailuresAgainstOneAddressEventuallyStopIt()
    {
        await using var harness = await Harness.CreateAsync();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.True(await harness.Throttle.IsAllowedAsync("victim@example.test", Origin, default));
            await harness.Throttle.RecordFailureAsync("victim@example.test", Origin, default);
        }

        Assert.False(await harness.Throttle.IsAllowedAsync("victim@example.test", Origin, default));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task OneAddressBeingBlockedDoesNotBlockAnother()
    {
        await using var harness = await Harness.CreateAsync();

        for (int attempt = 0; attempt < 3; attempt++)
            await harness.Throttle.RecordFailureAsync("victim@example.test", Origin, default);

        // From a different origin, so the network limit is not what is being tested.
        Assert.True(await harness.Throttle.IsAllowedAsync(
            "someone.else@example.test", OtherOrigin, default));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task OneOriginGuessingManyAddressesIsStopped()
    {
        await using var harness = await Harness.CreateAsync();

        // Credential stuffing: many accounts, one guess each, which never trips a
        // per-account limit. This is the case the network limit exists for.
        for (int account = 0; account < 5; account++)
            await harness.Throttle.RecordFailureAsync($"user{account}@example.test", Origin, default);

        Assert.False(await harness.Throttle.IsAllowedAsync("fresh@example.test", Origin, default));
        Assert.True(await harness.Throttle.IsAllowedAsync("fresh@example.test", OtherOrigin, default));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task SigningInSuccessfullyClearsTheAddress()
    {
        await using var harness = await Harness.CreateAsync();
        for (int attempt = 0; attempt < 3; attempt++)
            await harness.Throttle.RecordFailureAsync("owner@example.test", Origin, default);
        Assert.False(await harness.Throttle.IsAllowedAsync("owner@example.test", Origin, default));

        await harness.Throttle.ClearAsync("owner@example.test", default);

        // Otherwise a failed guessing run against someone's address keeps its owner locked
        // out even after they prove they are the owner.
        Assert.True(await harness.Throttle.IsAllowedAsync("owner@example.test", Origin, default));
    }

    private sealed class Harness : IAsyncDisposable
    {
        private PostgreSqlDatabaseFixture Database { get; init; } = null!;
        private AppDbContext Db { get; init; } = null!;
        public LoginThrottle Throttle { get; private init; } = null!;

        public static async Task<Harness> CreateAsync()
        {
            var database = await PostgreSqlDatabaseFixture.CreateAsync();
            await using (var migrate = await database.CreateMigratedContextAsync()) { }
            var db = database.CreateContext();
            return new Harness
            {
                Database = database,
                Db = db,
                Throttle = new LoginThrottle(
                    db,
                    Limits,
                    new SubmissionNetwork("login-throttle-tests-network-hash-key-0123456789"),
                    new FixedTime(Now)),
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
