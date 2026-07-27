using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// A stable named, typed gameplay tag applied to floor tiles. Tags are
/// declarative inputs to typed rules and never contain executable behavior.
/// </summary>
public sealed record ActorMapTileTagDefinition(
    string TagId,
    ActorMapTileTagDefinition.TileTagKind Kind,
    ImmutableArray<Position> Tiles)
{
    public enum TileTagKind
    {
        TransitionPlacementForbidden = 0,
        SpawnProtected = 1,
    }
}
