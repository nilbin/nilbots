namespace BotArena.App.Accounts;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The system account that owns the built-in opponents.</summary>
    public bool IsSystem { get; set; }
}
