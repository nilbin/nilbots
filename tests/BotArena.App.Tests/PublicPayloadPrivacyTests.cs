using System.Reflection;
using BotArena.App.Accounts;

namespace BotArena.App.Tests;

/// <summary>
/// Email addresses belong to the account that owns them and to nobody else. Public
/// listings identify people by display name and bots by name or slug (DECISIONS #96).
/// A leak here would be silent — a new field on a projection, reviewed by nobody — so
/// the response types that may legitimately carry an email are enumerated instead.
/// </summary>
public class PublicPayloadPrivacyTests
{
    /// <summary>Register, login and /me each answer the caller with the caller's own
    /// record. Every other response type in the app must not name an email at all.</summary>
    private static readonly HashSet<string> SelfScopedResponses =
    [
        nameof(UserResponse),
        nameof(RegisterRequest),
        nameof(LoginRequest),
    ];

    /// <summary>
    /// Types that hold an email server-side and are never serialized to a client.
    /// <para>
    /// Kept separate from <see cref="SelfScopedResponses"/> on purpose. That list answers
    /// "which responses may show someone their own address"; this one answers "which
    /// internal types are allowed to know one at all". Collapsing them would let a new
    /// *response* type be waved through by adding a name to a list about storage.
    /// </para>
    /// <para>
    /// A type only belongs here if no endpoint can return it, directly or as a member of
    /// something returned. Check that before adding to it.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> InternalEmailHolders =
    [
        nameof(User),
        // The provider link: rows recording which Google account maps to which local one.
        nameof(ExternalLogin),
        // What a provider told us during sign-in, consumed only by ExternalSignInService.
        nameof(ExternalIdentity),
    ];

    [Fact]
    public void OnlySelfScopedResponsesCarryAnEmail()
    {
        var candidates = typeof(UserResponse).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("BotArena.App") == true)
            .Where(t => !SelfScopedResponses.Contains(t.Name))
            .Where(t => !InternalEmailHolders.Contains(t.Name))
            .Where(t => !t.Name.Contains("DbContext") && !t.Name.Contains("Seeder"))
            .ToList();

        // Without this the test would pass just as happily on an empty scan.
        Assert.True(candidates.Count > 20, $"only {candidates.Count} types scanned — the filter is wrong");

        var offenders = candidates
            .Where(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Name)
            .ToList();
        Assert.True(offenders.Count == 0,
            "these types expose an email outside the account that owns it: " + string.Join(", ", offenders));
    }
}
