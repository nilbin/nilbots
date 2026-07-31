using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// The contract's forward-fabrication route, reduced to the three things the
/// banker actually decides with: where it must stand, where the child may
/// legally appear, and the declared order in which the host picks that tile.
/// Both known arms are covered - a pad-bound route whose source and output are
/// the protected home region, and the class route whose source is the whole map
/// and whose output is everywhere except a protected pad.
/// </summary>
internal sealed class FabricationRoute
{
    public FabricationRoute(
        string actionId,
        ImmutableArray<GenericActorRulesContract.RelativePositionOffset> offsets,
        HashSet<Position> sourceTiles,
        HashSet<Position> outputTiles)
    {
        ActionId = actionId;
        Offsets = offsets;
        SourceTiles = sourceTiles;
        OutputTiles = outputTiles;
    }

    /// <summary>Stable action ID that requests this fabrication.</summary>
    public string ActionId { get; }
    /// <summary>Facing-relative output candidates in their declared order.</summary>
    public ImmutableArray<GenericActorRulesContract.RelativePositionOffset>
        Offsets { get; }
    /// <summary>Tiles the source body must occupy to queue.</summary>
    public HashSet<Position> SourceTiles { get; }
    /// <summary>Tiles a child may legally be placed on.</summary>
    public HashSet<Position> OutputTiles { get; }

    /// <summary>
    /// Replays the host's declared "first eligible offset" placement rule from a
    /// hypothetical source pose, so the banker can choose a pose that drops the
    /// replacement where the last exchange happened.
    /// </summary>
    public Position? Predict(
        MatchLens lens,
        Position source,
        Direction facing,
        IReadOnlySet<Position> occupied)
    {
        (int fx, int fy) = facing.Vector();
        (int rx, int ry) = facing.TurnedRight().Vector();
        foreach (GenericActorRulesContract.RelativePositionOffset offset
                 in Offsets)
        {
            Position candidate = source.Offset(
                (offset.Forward * fx) + (offset.Right * rx),
                (offset.Forward * fy) + (offset.Right * ry));
            if (!lens.IsOpen(candidate)
                || occupied.Contains(candidate)
                || (OutputTiles.Count > 0 && !OutputTiles.Contains(candidate)))
            {
                continue;
            }
            return candidate;
        }
        return null;
    }

    /// <summary>
    /// Facing whose predicted placement lands nearest <paramref name="anchor"/>,
    /// together with that distance. Ties keep the current facing so the banker
    /// never spends a tick rotating for nothing.
    /// </summary>
    public (Direction Facing, int Distance)? BestFacing(
        MatchLens lens,
        Position source,
        Direction current,
        IReadOnlySet<Position> occupied,
        Position anchor,
        Direction[] order)
    {
        (Direction Facing, int Distance)? best = null;
        foreach (Direction facing in order)
        {
            if (Predict(lens, source, facing, occupied) is not Position tile)
                continue;
            int distance = tile.ChebyshevDistance(anchor);
            if (best is null
                || distance < best.Value.Distance
                || (distance == best.Value.Distance && facing == current))
            {
                best = (facing, distance);
            }
        }
        return best;
    }
}
