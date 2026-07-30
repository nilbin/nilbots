using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Optional replay-owned presentation for a generic actor match.
///
/// It is deliberately separate from <see cref="ActorMapDefinition"/> and the
/// resolved match contract: themes, wall families, and form artwork affect
/// playback and the replay hash, never gameplay fingerprints.
/// </summary>
public sealed record GenericActorReplayPresentation(
    string? ThemeId,
    GenericActorReplayPresentation.MapPresentation? Map,
    ImmutableArray<GenericActorReplayPresentation.FormPresentation> Forms)
{
    /// <summary>Non-gameplay wall-family presentation for the replay map.</summary>
    public sealed record MapPresentation(
        string BoundaryWall,
        string InteriorWall,
        ImmutableArray<WallGroup> WallGroups);

    /// <summary>Presentation override for one set of blocked map tiles.</summary>
    public sealed record WallGroup(
        string Family,
        ImmutableArray<Position> Tiles);

    /// <summary>Renderer-owned chassis and projectile identity for one form.</summary>
    public sealed record FormPresentation(
        string FormId,
        string? LookId,
        string? ProjectileLookId);
}
