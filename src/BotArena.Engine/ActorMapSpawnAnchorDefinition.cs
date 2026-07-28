using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One stable named initial spawn and the movement layers that may start
/// there. The immutable map advertises compatibility; it does not grant a
/// runtime support for a movement layer.
/// </summary>
public sealed record ActorMapSpawnAnchorDefinition(
    InitialSpawnDefinition Spawn,
    ImmutableArray<ActorMovementLayer> CompatibleMovementLayers);
