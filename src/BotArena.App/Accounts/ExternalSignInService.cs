using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Accounts;

/// <summary>What a provider told us about the person signing in.</summary>
/// <param name="Subject">The provider's stable identifier. Never the email.</param>
/// <param name="EmailVerified">
/// Whether the provider vouches for the address. Google sets this false for some Workspace
/// configurations, and it is the difference between linking an account and giving it away.
/// </param>
public sealed record ExternalIdentity(
    string Provider,
    string Subject,
    string Email,
    string? DisplayName,
    bool EmailVerified);

/// <summary>
/// Who signed in, or why nobody did.
/// <para>
/// A refusal is a real outcome here rather than an exception: the only way to reach one is
/// a specific, explainable collision, and the person on the other end needs to be told what
/// to do about it — not shown a 500.
/// </para>
/// </summary>
public sealed record ExternalSignInOutcome(User? User, bool Created, string? Error)
{
    public static ExternalSignInOutcome Signed(User user, bool created) =>
        new(user, created, null);

    /// <summary>
    /// The provider's address already belongs to a local account, and the provider would
    /// not vouch for it.
    /// </summary>
    public static ExternalSignInOutcome EmailTaken() => new(null, false, "email-taken");
}

/// <summary>
/// Turns an external identity into a local account, creating or linking as needed.
/// <para>
/// Three cases, in order, and the order is the security property:
/// </para>
/// <list type="number">
/// <item>
/// <b>Known link.</b> A row for (provider, subject) already exists — sign that user in.
/// The email is not consulted, so a renamed Google account still reaches its own bots.
/// </item>
/// <item>
/// <b>Verified email matching a local account.</b> Link them. Someone who registered with
/// a password and later clicks "Continue with Google" expects to land in their own garage,
/// not a duplicate — and requiring them to remember which method they used is the kind of
/// thing that makes people give up on an account.
/// </item>
/// <item>
/// <b>Anything else.</b> A new account.
/// </item>
/// </list>
/// <para>
/// **Linking on an unverified email is account takeover**, which is why case 2 is gated on
/// it. Anyone able to create an identity at a provider that does not verify addresses could
/// otherwise claim someone else's, and the provider would be telling the truth about the
/// only thing it checked — that the user controls *that provider account*, not that inbox.
/// An unverified email therefore falls through to case 3 and gets its own account.
/// </para>
/// </summary>
public sealed class ExternalSignInService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<ExternalSignInOutcome> SignInAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        string email = identity.Email.Trim().ToLowerInvariant();

        ExternalLogin? link = await db.ExternalLogins.SingleOrDefaultAsync(
            row => row.Provider == identity.Provider && row.Subject == identity.Subject,
            cancellationToken);

        if (link is not null)
        {
            User? linked = await db.Users.FindAsync([link.UserId], cancellationToken);
            if (linked is not null)
            {
                link.LastSignedInAt = now;
                await db.SaveChangesAsync(cancellationToken);
                return ExternalSignInOutcome.Signed(linked, created: false);
            }
            // The account was deleted and the link outlived it. Drop the orphan rather than
            // failing the sign-in; the user gets a fresh account below.
            db.ExternalLogins.Remove(link);
        }

        User? sameEmail = await db.Users.SingleOrDefaultAsync(
            user => user.Email == email, cancellationToken);

        // Unverified, and that address is already somebody's account. Linking is the
        // takeover this whole method is arranged to prevent, and creating is impossible —
        // emails are unique, so it would fail on the insert. Refuse and say why: the owner
        // can sign in with their password, and linking Google afterwards is a known
        // subject from then on.
        if (sameEmail is not null && !identity.EmailVerified)
            return ExternalSignInOutcome.EmailTaken();

        User? existing = identity.EmailVerified ? sameEmail : null;
        bool created = existing is null;
        User user = existing ?? new User
        {
            DisplayName = DisplayNameFor(identity, email),
            Email = email,
            // No password at all, rather than an unusable one. The login endpoint refuses
            // a passwordless account outright, so this cannot be brute-forced into.
            PasswordHash = null,
        };
        if (created) db.Users.Add(user);

        db.ExternalLogins.Add(new ExternalLogin
        {
            UserId = user.Id,
            Provider = identity.Provider,
            Subject = identity.Subject,
            Email = email,
            CreatedAt = now,
            LastSignedInAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        return ExternalSignInOutcome.Signed(user, created);
    }

    /// <summary>
    /// A display name that fits the same 2–40 rule registration enforces.
    /// <para>
    /// Falls back to the local part of the email, because a provider may return no name at
    /// all and an empty display name would render as a blank owner on every bot card.
    /// </para>
    /// </summary>
    private static string DisplayNameFor(ExternalIdentity identity, string email)
    {
        string candidate = identity.DisplayName?.Trim() is { Length: > 0 } name
            ? name
            : email.Split('@')[0];
        if (candidate.Length < 2) candidate = $"{candidate}_";
        return candidate.Length > 40 ? candidate[..40] : candidate;
    }
}
