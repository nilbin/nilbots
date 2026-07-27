using BotArena.App.Bots;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Accounts;

/// <summary>
/// How many wrong passwords are allowed before the door stops answering.
/// </summary>
/// <param name="IdentifierLimit">
/// Failures against one email address. Generous on purpose — a low number here is a denial
/// of service, because anyone can lock a victim out by guessing at *their* address. The
/// network limit is the one meant to bite.
/// </param>
/// <param name="NetworkLimit">
/// Failures from one origin, across all addresses. This is the real brake: credential
/// stuffing works by trying many accounts once each, which never trips a per-account limit.
/// </param>
public sealed record LoginThrottleLimits(int IdentifierLimit, int NetworkLimit, TimeSpan Window)
{
    public static LoginThrottleLimits FromConfiguration(IConfiguration configuration) => new(
        AdmissionSupport.ReadLimit(configuration, "BOTARENA_LOGIN_IDENTIFIER_LIMIT", 10, 3, 1000),
        AdmissionSupport.ReadLimit(configuration, "BOTARENA_LOGIN_NETWORK_LIMIT", 30, 5, 5000),
        TimeSpan.FromMinutes(
            AdmissionSupport.ReadLimit(configuration, "BOTARENA_LOGIN_WINDOW_MINUTES", 15, 1, 1440)));
}

/// <summary>
/// Durable brute-force protection for password sign-in.
/// <para>
/// The one limit in the application with no domain rows to count: a failed login
/// deliberately leaves no trace anywhere else, which is exactly why it needs a table of its
/// own where compilation and ranked sets can simply count what they created.
/// </para>
/// </summary>
public sealed class LoginThrottle(
    AppDbContext db,
    LoginThrottleLimits limits,
    SubmissionNetwork network,
    TimeProvider timeProvider)
{
    /// <summary>Whether this attempt may proceed to checking the password at all.</summary>
    public async Task<bool> IsAllowedAsync(
        string identifier,
        System.Net.IPAddress? remoteAddress,
        CancellationToken cancellationToken)
    {
        DateTime since = timeProvider.GetUtcNow().UtcDateTime - limits.Window;
        string networkHash = network.Hash(remoteAddress);

        int byIdentifier = await db.LoginAttempts.CountAsync(
            attempt => attempt.Identifier == identifier && attempt.OccurredAt >= since,
            cancellationToken);
        if (byIdentifier >= limits.IdentifierLimit) return false;

        int byNetwork = await db.LoginAttempts.CountAsync(
            attempt => attempt.NetworkHash == networkHash && attempt.OccurredAt >= since,
            cancellationToken);
        return byNetwork < limits.NetworkLimit;
    }

    public async Task RecordFailureAsync(
        string identifier,
        System.Net.IPAddress? remoteAddress,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        db.LoginAttempts.Add(new LoginAttempt
        {
            Identifier = identifier,
            NetworkHash = network.Hash(remoteAddress),
            OccurredAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        // Housekeeping on the write path rather than a scheduled job: the table only grows
        // when someone is failing to log in, and a sweeper for it would be a moving part
        // that exists for a table that is usually empty.
        DateTime expiry = now - limits.Window - TimeSpan.FromHours(1);
        await db.LoginAttempts
            .Where(attempt => attempt.OccurredAt < expiry)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Someone got in: forget this address's failures.
    /// <para>
    /// So a person who mistyped their password four times is not still carrying those four
    /// an hour later, and — more importantly — so a failed guessing run against an address
    /// cannot keep its owner locked out after they successfully sign in.
    /// </para>
    /// </summary>
    public Task ClearAsync(string identifier, CancellationToken cancellationToken) =>
        db.LoginAttempts
            .Where(attempt => attempt.Identifier == identifier)
            .ExecuteDeleteAsync(cancellationToken);
}
