using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Accounts;

/// <summary>
/// Display names identify people, so they are unique — case-insensitively.
/// <para>
/// The ladder, every match row and every bot card name their owner this way, and two
/// accounts a case-fold apart are indistinguishable at a glance. That is impersonation
/// rather than a collision, which is why the uniqueness is on <c>lower(DisplayName)</c>
/// rather than the raw string.
/// </para>
/// <para>
/// How a conflict is resolved depends on whether a person is there to ask. A name typed
/// into the registration form is rejected — quietly storing "Pincer2" puts someone on the
/// ladder under a name they did not choose. A name arriving from Google has no form behind
/// it and no one to prompt, so it is suffixed and the sign-in completes (DECISIONS #121).
/// </para>
/// </summary>
public static class DisplayNames
{
    public const int MinLength = 2;
    public const int MaxLength = 40;

    public static Task<bool> IsTakenAsync(
        AppDbContext db,
        string displayName,
        CancellationToken cancellationToken) =>
        db.Users.AnyAsync(
            user => user.DisplayName.ToLower() == displayName.ToLower(),
            cancellationToken);

    /// <summary>
    /// The requested name, or the first free variant of it.
    /// <para>
    /// Suffixes are appended within <see cref="MaxLength"/> by trimming the stem, so a
    /// 40-character Google name does not produce a 41-character one the column rejects.
    /// </para>
    /// <para>
    /// This is advisory, not a reservation: two simultaneous sign-ups can both be told the
    /// same variant is free. The unique index is what actually decides, and the caller
    /// retries — see <see cref="ExternalSignInService"/>.
    /// </para>
    /// </summary>
    public static async Task<string> FindFreeAsync(
        AppDbContext db,
        string requested,
        CancellationToken cancellationToken)
    {
        string stem = Clamp(requested);
        if (!await IsTakenAsync(db, stem, cancellationToken))
            return stem;

        for (int suffix = 2; suffix < 1000; suffix++)
        {
            string candidate = WithSuffix(stem, suffix);
            if (!await IsTakenAsync(db, candidate, cancellationToken))
                return candidate;
        }

        // A thousand people called the same thing is not a case worth designing for, but
        // returning a duplicate would fail the insert with nothing to explain it.
        return WithSuffix(stem, Random.Shared.Next(1_000, 1_000_000));
    }

    /// <summary>Fit a name to the length rule registration enforces.</summary>
    public static string Clamp(string candidate)
    {
        string trimmed = candidate.Trim();
        if (trimmed.Length == 0) trimmed = "player";
        while (trimmed.Length < MinLength) trimmed += "_";
        return trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }

    private static string WithSuffix(string stem, int suffix)
    {
        string tail = suffix.ToString();
        int room = MaxLength - tail.Length;
        return (stem.Length > room ? stem[..room] : stem) + tail;
    }
}
