using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// One corner-strict movement audit for every registered Arc Relay map.
/// Profiles may share geometry; failures retain the profile name so an
/// experimental map cannot hide behind a shipped map's result.
/// </summary>
public sealed class ArcRelayMapMovementSafetyTests
{
    [Fact]
    public void RegisteredMapsMeetCornerAndStationaryBlockerSafety()
    {
        // These immutable historical/candidate geometries predate the shared
        // rule. Keeping the exception list here makes the discrepancy visible
        // without rewriting a frozen map in an executor change. Every other
        // registered profile must satisfy the invariant.
        var knownUnsafe = new HashSet<string>(StringComparer.Ordinal)
        {
            "ambush-warren",
            "ambush-warren-2",
            "ambush-warren-3",
            "ambush-warren-4",
            "ambush-warren-5",
            "ambush-warren-6",
            "ambush-warren-7",
            "ambush-warren-8",
            "ambush-warren-9",
            "ambush-warren-10",
            "ambush-warren-11",
            "crucible-1",
        };
        var failures = new List<string>();
        foreach (ArcRelayLoopProfile profile in ArcRelayLoopProfile.Registered)
        {
            ActorMapDefinition map = ArcRelayH0Definition.Create(
                loopProfile: profile).Map;
            MapAudit audit = Audit(map);
            bool unsafeGeometry = audit.CornerPassages.Count > 0
                || audit.SeveredNooks.Count > 0;
            if (knownUnsafe.Contains(profile.Id) != unsafeGeometry)
            {
                failures.Add(
                    $"{profile.Id}: corners [{string.Join(", ", audit.CornerPassages)}]"
                    + $"; blockers [{string.Join(", ", audit.SeveredNooks)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void CrucibleCandidateKeepsItsKnownSafetyFailuresVisible()
    {
        ActorMapDefinition map = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Crucible1).Map;
        MapAudit audit = Audit(map);

        Assert.Equal(
            [
                "(9,3)-(10,2)",
                "(12,3)-(13,2)",
                "(14,7)-(15,8)",
                "(5,9)-(6,8)",
                "(12,10)-(13,11)",
                "(16,10)-(17,9)",
                "(17,11)-(18,12)",
                "(17,11)-(18,10)",
                "(12,12)-(13,11)",
                "(13,13)-(14,12)",
                "(15,14)-(16,15)",
                "(24,14)-(25,13)",
                "(17,20)-(18,19)",
                "(20,20)-(21,19)",
            ],
            audit.CornerPassages);
        Assert.Empty(audit.SeveredNooks);
    }

    private static MapAudit Audit(ActorMapDefinition map)
    {
        List<Position> floor = Floor(map);
        var corners = new List<string>();
        foreach (Position from in floor)
        {
            foreach ((int dx, int dy) in new[] { (1, 1), (1, -1) })
            {
                var to = new Position(from.X + dx, from.Y + dy);
                if (map.IsWall(to))
                    continue;
                if (map.IsWall(from.X + dx, from.Y)
                    && map.IsWall(from.X, from.Y + dy))
                {
                    corners.Add($"({from.X},{from.Y})-({to.X},{to.Y})");
                }
            }
        }

        var severed = new List<string>();
        foreach (Position blocker in floor)
        {
            int severity = SeveredTileCount(map, floor, blocker);
            if (severity > 2)
                severed.Add($"({blocker.X},{blocker.Y}) severs {severity}");
        }
        return new MapAudit(corners, severed);
    }

    private static int SeveredTileCount(
        ActorMapDefinition map,
        IReadOnlyCollection<Position> floor,
        Position blocker)
    {
        var remaining = floor.Where(tile => tile != blocker).ToHashSet();
        int largest = 0;
        while (remaining.Count > 0)
        {
            Position start = remaining.OrderBy(tile => tile.Y)
                .ThenBy(tile => tile.X).First();
            var seen = new HashSet<Position> { start };
            var queue = new Queue<Position>();
            queue.Enqueue(start);
            remaining.Remove(start);
            while (queue.Count > 0)
            {
                Position from = queue.Dequeue();
                foreach (Position to in Neighbours(map, from))
                {
                    if (to == blocker || !remaining.Remove(to))
                        continue;
                    seen.Add(to);
                    queue.Enqueue(to);
                }
            }
            largest = Math.Max(largest, seen.Count);
        }
        return floor.Count - 1 - largest;
    }

    private static IEnumerable<Position> Neighbours(
        ActorMapDefinition map,
        Position from)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0)
                continue;
            var to = new Position(from.X + dx, from.Y + dy);
            if (GenericActorMatchSession.IsTerrainMovementStepLegal(
                    map, from, to))
            {
                yield return to;
            }
        }
    }

    private static List<Position> Floor(ActorMapDefinition map)
    {
        var floor = new List<Position>();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var tile = new Position(x, y);
            if (!map.IsWall(tile))
                floor.Add(tile);
        }
        return floor;
    }

    private sealed record MapAudit(
        List<string> CornerPassages,
        List<string> SeveredNooks);
}
