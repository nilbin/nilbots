namespace BotArena.App.Accounts;

public static class ExternalLoginProviders
{
    public const string Google = "google";
}

/// <summary>
/// An identity at an external provider, bound to a local account.
/// <para>
/// The provider's subject is the identity, not the email. Emails change — a Google account
/// can be renamed, and a Workspace address can be reassigned to a different person
/// entirely — while <c>sub</c> is stable for the life of the account. Matching on email at
/// every sign-in would hand someone else's arena account to whoever inherits their old
/// work address.
/// </para>
/// <para>
/// Email is still how a *first* Google sign-in finds an existing local account to link to,
/// but only once, and only when the provider says it is verified. After that this row is
/// the link.
/// </para>
/// </summary>
public class ExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }

    /// <summary>A value from <see cref="ExternalLoginProviders"/>.</summary>
    public required string Provider { get; set; }

    /// <summary>The provider's stable subject identifier for this user.</summary>
    public required string Subject { get; set; }

    /// <summary>What the provider said the email was when the link was made. Diagnostic only.</summary>
    public required string Email { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSignedInAt { get; set; } = DateTime.UtcNow;
}
