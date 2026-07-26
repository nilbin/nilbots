using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>One ordered objective footprint.</summary>
public sealed record FrontlineRegion(
    int PositionIndex,
    ImmutableArray<Position> Tiles);
