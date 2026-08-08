namespace BotArena.App.Sheets;

/// <summary>
/// One player-authored Arc Relay tactical playbook and its byte-pinned map
/// layout. PostgreSQL <c>json</c> columns preserve the source text because
/// the compiler hashes the layout bytes; <c>jsonb</c> would rewrite them.
/// </summary>
public sealed class TacticalSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public int Revision { get; set; } = 1;
    public required string PlaybookJson { get; set; }
    public required string LayoutJson { get; set; }
    public required string ContentHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
