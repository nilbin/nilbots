namespace BotArena.App.Sheets;

/// <summary>
/// One player-authored Arc Relay sheet. The whole-team Tactics document is the
/// only player-owned source; the server deterministically materializes it into
/// the executable Orders v2 pair stored beside it. PostgreSQL <c>json</c>
/// columns preserve the materialized source text because the compiler hashes
/// the layout bytes; <c>jsonb</c> would rewrite them.
/// </summary>
public sealed class TacticalSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public int Revision { get; set; } = 1;
    /// <summary>
    /// The player-owned arc-relay-tactics-v1 document. Null only on inert
    /// pre-Tactics rows, which no current surface lists or edits.
    /// </summary>
    public string? TacticsJson { get; set; }
    public required string PlaybookJson { get; set; }
    public required string LayoutJson { get; set; }
    public required string ContentHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
