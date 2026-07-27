namespace BotArena.App.Accounts;

/// <summary>
/// One failed sign-in.
/// <para>
/// Only failures are recorded. A successful login clears the identifier's rows, so this
/// table holds "guesses since someone last got in" rather than a history of everyone's
/// logins — which keeps it small, keeps the count meaningful, and means it is not a record
/// of when a person was at their computer.
/// </para>
/// <para>
/// It exists because the HTTP limiter cannot do this job. That limiter lives in one web
/// process's memory, so its ten-per-minute becomes ten per minute *per replica* and resets
/// on deploy — and unlike a capacity limit, the party who benefits from it being loose is
/// the one guessing passwords.
/// </para>
/// </summary>
public class LoginAttempt
{
    public long Id { get; set; }

    /// <summary>The email that was tried, lower-cased. Not necessarily a real account.</summary>
    public required string Identifier { get; set; }

    /// <summary>Hashed origin, so the table cannot be read as a log of who was where.</summary>
    public required string NetworkHash { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
