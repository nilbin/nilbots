using BotArena.Sdk;

/// <summary>
/// Deterministic viewing-position search for the ordinary Scan Instruction.
/// It models the published vision profile directly and returns proposals to
/// the existing target-capacity and movement machinery; it allocates nobody.
/// </summary>
internal static class TacticalScanPlanner
{
    internal sealed record ViewTarget(
        Position Position,
        Direction Facing,
        int UsefulTileCount,
        int WalkingDistance)
    {
        public string Key =>
            $"view:{Position.X},{Position.Y}:{Facing.ToString().ToLowerInvariant()}";
    }

    internal static ViewTarget[] Targets(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        IReadOnlyCollection<Position> staleTiles,
        Func<Position, int?> walkingDistance,
        string? preferredTargetKey = null)
    {
        if (staleTiles.Count == 0)
            return [];
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(value => value.Id == body.FormId);
        GenericActorRulesContract.VisionProfile? vision = form is null
            ? null
            : contract.Rules.VisionProfiles.FirstOrDefault(value =>
                value.Id == form.VisionProfileId);
        if (vision is null)
            return [];

        return Targets(
            contract.Map.Width,
            contract.Map.Height,
            position => ArenaBasics.CanEnter(contract.Map, position),
            position => contract.Map.TileRows[position.Y][position.X] == '#',
            vision,
            body.Position,
            staleTiles,
            walkingDistance,
            preferredTargetKey);
    }

    internal static ViewTarget[] Targets(
        int width,
        int height,
        Func<Position, bool> canEnter,
        Func<Position, bool> blocksVision,
        GenericActorRulesContract.VisionProfile vision,
        Position bodyPosition,
        IReadOnlyCollection<Position> staleTiles,
        Func<Position, int?> walkingDistance,
        string? preferredTargetKey = null)
    {
        if (staleTiles.Count == 0)
            return [];

        Direction[] facings =
            [Direction.North, Direction.East, Direction.South, Direction.West];
        HashSet<Position> stale = staleTiles.ToHashSet();
        var values = new List<ViewTarget>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var position = new Position(x, y);
            if (!canEnter(position))
                continue;
            int? distance = position == bodyPosition
                ? 0
                : walkingDistance(position);
            if (distance is null)
                continue;
            foreach (Direction facing in facings)
            {
                int useful = stale.Count(tile => VisibleFrom(
                    width,
                    height,
                    blocksVision,
                    vision,
                    position,
                    facing,
                    tile));
                if (useful == 0)
                    continue;
                values.Add(new ViewTarget(
                    position, facing, useful, distance.Value));
            }
        }

        // Turning in place is the cheapest truthful scan. When movement is
        // needed, useful coverage wins before distance so a scanner does not
        // orbit through a chain of one-tile peeks. A still-useful held target
        // sorts before the candidate cut: target stickiness must not depend on
        // whether 24 stronger-but-new targets happened to exist this tick.
        return Order(values, bodyPosition, preferredTargetKey)
            .Take(24)
            .ToArray();
    }

    internal static ViewTarget[] Order(
        IEnumerable<ViewTarget> values,
        Position bodyPosition,
        string? preferredTargetKey) => values
            .OrderBy(value => value.Key == preferredTargetKey ? 0 : 1)
            .ThenBy(value => value.Position == bodyPosition ? 0 : 1)
            .ThenByDescending(value => value.UsefulTileCount)
            .ThenBy(value => value.WalkingDistance)
            .ThenBy(value => value.Position.Y)
            .ThenBy(value => value.Position.X)
            .ThenBy(value => value.Facing)
            .ToArray();

    internal static bool VisibleFrom(
        int width,
        int height,
        Func<Position, bool> blocksVision,
        GenericActorRulesContract.VisionProfile vision,
        Position origin,
        Direction facing,
        Position target)
    {
        if (origin.X < 0 || origin.X >= width
            || origin.Y < 0 || origin.Y >= height
            || target.X < 0 || target.X >= width
            || target.Y < 0 || target.Y >= height)
        {
            return false;
        }
        int dx = target.X - origin.X;
        int dy = target.Y - origin.Y;
        int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance > vision.Range)
            return false;
        if (vision.Shape == "facing-quadrant"
            && distance > vision.OmnidirectionalProximityRange
            && !InQuadrant(dx, dy, facing))
        {
            return false;
        }
        foreach (Position tile in SupercoverLine(origin, target))
        {
            if (tile != origin && tile != target
                && blocksVision(tile))
            {
                return false;
            }
        }
        return true;
    }

    private static bool InQuadrant(int dx, int dy, Direction facing)
    {
        (int fx, int fy) = facing switch
        {
            Direction.North => (0, -1),
            Direction.East => (1, 0),
            Direction.South => (0, 1),
            Direction.West => (-1, 0),
            _ => (0, -1),
        };
        int forward = dx * fx + dy * fy;
        int lateral = Math.Abs(dx * fy) + Math.Abs(dy * fx);
        return forward >= 0 && lateral <= forward;
    }

    private static IEnumerable<Position> SupercoverLine(Position a, Position b)
    {
        int dx = b.X - a.X;
        int dy = b.Y - a.Y;
        int nx = Math.Abs(dx);
        int ny = Math.Abs(dy);
        int sx = Math.Sign(dx);
        int sy = Math.Sign(dy);
        int x = a.X;
        int y = a.Y;
        yield return a;
        int ix = 0;
        int iy = 0;
        while (ix < nx || iy < ny)
        {
            long crossX = (long)(1 + 2 * ix) * ny;
            long crossY = (long)(1 + 2 * iy) * nx;
            if (crossX == crossY)
            {
                yield return new Position(x + sx, y);
                yield return new Position(x, y + sy);
                x += sx;
                y += sy;
                ix++;
                iy++;
            }
            else if (crossX < crossY)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }
            yield return new Position(x, y);
        }
    }
}
