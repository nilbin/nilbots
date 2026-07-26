namespace BotArena.App.Accounts;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DisplayName { get; set; }
    public required string Email { get; set; }

    /// <summary>
    /// The password hash, or null for an account that has only ever signed in through an
    /// external provider.
    /// <para>
    /// Nullable rather than an empty string, because the two mean different things and the
    /// difference is a security boundary: "" is a hash that no password produces, which
    /// happens to be safe, but it makes "has no password" indistinguishable from "has a
    /// password that failed to save". Null says it outright, and the login endpoint refuses
    /// it before ever reaching the verifier.
    /// </para>
    /// </summary>
    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The system account that owns the built-in opponents.</summary>
    public bool IsSystem { get; set; }
}
