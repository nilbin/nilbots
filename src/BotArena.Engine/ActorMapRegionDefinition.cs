using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>A stable named, typed gameplay region in a generic actor map.</summary>
public sealed record ActorMapRegionDefinition(
    string RegionId,
    ActorMapRegionDefinition.RegionKind Kind,
    ImmutableArray<Position> Tiles)
{
    public enum RegionKind
    {
        Objective = 0,
        TransitionPlacement = 1,
    }
}
