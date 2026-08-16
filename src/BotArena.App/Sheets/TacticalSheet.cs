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
    /// <summary>
    /// Optional editor-only Program linkage. It is never compiled or included
    /// in <see cref="ContentHash"/>. Practice alone takes an owner-only copy
    /// for revision-correct post-run explanation.
    /// </summary>
    public string? WorkshopJson { get; set; }
    public required string ContentHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
