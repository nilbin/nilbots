namespace BotArena.App.Cosmetics;

/// <summary>
/// Append-oriented evidence that an account may equip a catalog item. Multiple
/// independent grants can authorize one item; revocation affects only its source.
/// </summary>
public sealed class EntitlementGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string EntitlementKey { get; set; }
    public required string SourceKind { get; set; }
    public required string SourceId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? MetadataJson { get; set; }
}
