using BotArena.App.Accounts;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

/// <summary>
/// Who a Google sign-in turns out to be.
/// <para>
/// Every case here is an account-takeover shape if it goes the other way, which is why
/// they are pinned rather than left to the reading of a service that looks obvious.
/// </para>
/// </summary>
public class ExternalSignInServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 27, 9, 0, 0, DateTimeKind.Utc);

    private static ExternalIdentity Google(
        string subject,
        string email,
        bool verified = true,
        string? name = "Player One") =>
        new(ExternalLoginProviders.Google, subject, email, name, verified);

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AFirstSignInCreatesAnAccountWithNoPassword()
    {
        await using var harness = await Harness.CreateAsync();

        var result = await harness.Service.SignInAsync(
            Google("google-1", "New.Player@Example.test"), default);

        Assert.True(result.Created);
        // Lower-cased on the way in, matching registration, or the same person signing in
        // with a differently-cased address would get a second account.
        Assert.Equal("new.player@example.test", result.User!.Email);
        // Not an empty hash: the login endpoint refuses null outright, so there is nothing
        // for a password guess to compare against.
        Assert.Null(result.User!.PasswordHash);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AVerifiedEmailLinksToAnExistingLocalAccount()
    {
        await using var harness = await Harness.CreateAsync();
        Guid existing = await harness.SeedLocalUserAsync("player@example.test");

        var result = await harness.Service.SignInAsync(
            Google("google-1", "player@example.test"), default);

        // Someone who registered with a password and later clicks "Continue with Google"
        // must land in their own garage, not a duplicate account with none of their bots.
        Assert.False(result.Created);
        Assert.Equal(existing, result.User!.Id);
        Assert.NotNull(result.User!.PasswordHash);
        Assert.Single(await harness.Db.Users.ToListAsync());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AnUnverifiedEmailNeverClaimsAnExistingAccount()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedLocalUserAsync("player@example.test");

        var result = await harness.Service.SignInAsync(
            Google("attacker", "player@example.test", verified: false), default);

        // The attack this gates: a provider that does not verify addresses vouches only
        // that the user controls *that provider account*, not that inbox. Linking on it
        // would hand over the victim's bots, ladder standing and entitlements.
        //
        // Refused rather than given a second account: emails are unique, so "create one
        // anyway" is not available — an earlier draft tried it and died on the constraint,
        // which is how this case was found.
        Assert.Null(result.User);
        Assert.Equal("email-taken", result.Error);
        Assert.Single(await harness.Db.Users.ToListAsync());
        Assert.Empty(await harness.Db.ExternalLogins.ToListAsync());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AnUnverifiedEmailWithNoLocalAccountStillGetsOne()
    {
        await using var harness = await Harness.CreateAsync();

        // Nothing to take over, so nothing to refuse. The gate is about collisions, not
        // about distrusting the provider's users.
        var result = await harness.Service.SignInAsync(
            Google("google-1", "nobody@example.test", verified: false), default);

        Assert.True(result.Created);
        Assert.NotNull(result.User);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AKnownSubjectIsFollowedEvenWhenTheEmailChanged()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Service.SignInAsync(
            Google("google-1", "old.address@example.test"), default);
        harness.Db.ChangeTracker.Clear();

        // Google accounts get renamed, and a Workspace address can be reassigned to a
        // different person entirely. The subject is the identity; the email is not.
        var second = await harness.Service.SignInAsync(
            Google("google-1", "new.address@example.test"), default);

        Assert.False(second.Created);
        Assert.Equal(first.User!.Id, second.User!.Id);
        Assert.Single(await harness.Db.ExternalLogins.ToListAsync());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task TwoGoogleAccountsOnOneAddressDoNotShareAnAccount()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Service.SignInAsync(
            Google("google-1", "shared@example.test"), default);
        harness.Db.ChangeTracker.Clear();

        // The second one links to the account the first created, by verified email — which
        // is correct, and is exactly why (provider, subject) is unique: it must attach as a
        // second identity rather than displacing the first.
        var second = await harness.Service.SignInAsync(
            Google("google-2", "shared@example.test"), default);

        Assert.Equal(first.User!.Id, second.User!.Id);
        Assert.Equal(2, await harness.Db.ExternalLogins.CountAsync());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AProviderThatSendsNoNameStillProducesAUsableDisplayName()
    {
        await using var harness = await Harness.CreateAsync();

        var result = await harness.Service.SignInAsync(
            Google("google-1", "quiet@example.test", name: null), default);

        // An empty display name renders as a blank owner on every bot card and every
        // match row, which reads as data loss rather than a missing optional field.
        Assert.Equal("quiet", result.User!.DisplayName);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private PostgreSqlDatabaseFixture Database { get; init; } = null!;
        public AppDbContext Db { get; private init; } = null!;
        public ExternalSignInService Service { get; private init; } = null!;

        public static async Task<Harness> CreateAsync()
        {
            var database = await PostgreSqlDatabaseFixture.CreateAsync();
            await using (var migrate = await database.CreateMigratedContextAsync()) { }
            var db = database.CreateContext();
            return new Harness
            {
                Database = database,
                Db = db,
                Service = new ExternalSignInService(db, new FixedTime(Now)),
            };
        }

        public async Task<Guid> SeedLocalUserAsync(string email)
        {
            var user = new User
            {
                Email = email,
                DisplayName = "Local Player",
                PasswordHash = "a-real-hash",
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return user.Id;
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
