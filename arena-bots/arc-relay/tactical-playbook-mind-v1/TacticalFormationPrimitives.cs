using BotArena.Sdk;

internal static class TacticalFormationPrimitives
{
    internal readonly record struct AssignedTarget(
        string RoleId,
        Position Position);

    internal readonly record struct Lifecycle(
        bool Armed,
        bool Broken,
        int BreakStreak,
        int ReformStreak);

    internal static Position? FacingTarget(
        string orientation,
        Position bodyPosition,
        Position movementTarget,
        Position ownReactor,
        Position enemyReactor,
        Position? focusTarget)
    {
        if (orientation is not "fixed" and not "watch-own-reactor"
            and not "watch-enemy-reactor"
            && focusTarget is Position focus)
            return focus;
        return orientation switch
        {
            "route" => movementTarget == bodyPosition
                ? null
                : movementTarget,
            "enemy-reactor" => enemyReactor,
            "own-reactor" => ownReactor,
            "watch-own-reactor" => ownReactor,
            "watch-enemy-reactor" => enemyReactor,
            "focus-target" or "fixed" => null,
            _ => throw new InvalidDataException(
                $"Unknown formation orientation '{orientation}'."),
        };
    }

    internal static int FormationOrdinal(
        int unitId,
        string roleId,
        IReadOnlyDictionary<int, string> liveRoles,
        IReadOnlyDictionary<int, string> stableRoles,
        string vacancyPolicy,
        int placementCount)
    {
        IReadOnlyDictionary<int, string> source =
            string.Equals(vacancyPolicy, "preserve", StringComparison.Ordinal)
                ? stableRoles
                : liveRoles;
        int[] members = source
            .Where(value => string.Equals(
                value.Value, roleId, StringComparison.Ordinal))
            .Select(value => value.Key)
            .Order()
            .ToArray();
        int ordinal = Array.IndexOf(members, unitId);
        if (ordinal < 0)
        {
            throw new InvalidDataException(
                $"Unit {unitId} has no '{roleId}' formation ordinal.");
        }
        return vacancyPolicy switch
        {
            "preserve" or "compress" => ordinal,
            "rebalance-role" when members.Length <= 1 => 0,
            "rebalance-role" => ordinal * (placementCount - 1)
                / (members.Length - 1),
            _ => throw new InvalidDataException(
                $"Unknown formation vacancy policy '{vacancyPolicy}'."),
        };
    }

    internal static Lifecycle AdvanceLifecycle(
        Lifecycle prior,
        int cohesionPercent,
        int breakRatioPercent,
        int breakTicks,
        int reformRatioPercent,
        int reformTicks)
    {
        if (!prior.Armed)
        {
            int establish = cohesionPercent >= reformRatioPercent
                ? prior.ReformStreak + 1
                : 0;
            return establish >= reformTicks
                ? new Lifecycle(true, false, 0, 0)
                : new Lifecycle(false, false, 0, establish);
        }
        if (!prior.Broken)
        {
            int streak = cohesionPercent <= breakRatioPercent
                ? prior.BreakStreak + 1
                : 0;
            return streak >= breakTicks
                ? new Lifecycle(true, true, 0, 0)
                : new Lifecycle(true, false, streak, 0);
        }

        int reform = cohesionPercent >= reformRatioPercent
            ? prior.ReformStreak + 1
            : 0;
        return reform >= reformTicks
            ? new Lifecycle(true, false, 0, 0)
            : new Lifecycle(true, true, 0, reform);
    }

    internal static bool OrderComplete(
        string completion,
        int unitId,
        int leaderUnitId,
        bool unitArrived,
        int arrivedCount,
        int memberCount,
        int arrivalRatioPercent) => completion switch
    {
        "continuous" => false,
        "leader-arrived" => unitId == leaderUnitId && unitArrived,
        "all-arrived" => memberCount > 0 && arrivedCount == memberCount,
        "cohesion-arrived" => memberCount > 0
            && arrivedCount * 100 / memberCount >= arrivalRatioPercent,
        _ => throw new InvalidDataException(
            $"Unknown movement completion '{completion}'."),
    };

    internal static bool CanAdvanceAtPace(
        string pace,
        int unitId,
        int leaderUnitId,
        int distanceToGoal,
        int leaderDistanceToGoal,
        int furthestDistanceToGoal) => pace switch
    {
        "free" => true,
        // A slowest-paced body may never get more than one step ahead of the
        // current tail. This keeps a column together without serializing all
        // eight bodies onto one mover.
        "slowest" => distanceToGoal >= furthestDistanceToGoal - 1,
        // Followers may catch the leader but not advance past it. The leader
        // remains free to establish the pace.
        "leader" => unitId == leaderUnitId
            || distanceToGoal >= leaderDistanceToGoal,
        _ => throw new InvalidDataException(
            $"Unknown movement pace '{pace}'."),
    };

    internal static Position[] ReflowGoals(
        int width,
        int height,
        IReadOnlyList<string> tileRows,
        Position target,
        int radius,
        string blockedSlotPolicy)
    {
        if (string.Equals(
                blockedSlotPolicy, "hold", StringComparison.Ordinal))
        {
            return [target];
        }

        var candidates = new List<Position> { target };
        for (int distance = 1; distance <= radius; distance++)
        {
            for (int dy = -distance; dy <= distance; dy++)
            for (int dx = -distance; dx <= distance; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != distance)
                    continue;
                candidates.Add(target.Offset(dx, dy));
            }
        }
        IEnumerable<Position> ordered = string.Equals(
                blockedSlotPolicy, "rotate-shape", StringComparison.Ordinal)
            ? candidates
                .OrderBy(position => target.ChebyshevDistance(position))
                .ThenBy(position => ClockwiseRank(target, position))
                .ThenBy(position => position.Y)
                .ThenBy(position => position.X)
            : candidates
                .OrderBy(position => target.ChebyshevDistance(position))
                .ThenBy(position => position.Y)
                .ThenBy(position => position.X);
        Position[] legal = ordered
            .Where(position => IsEnterable(
                width, height, tileRows, position))
            .Distinct()
            .ToArray();
        return legal.Length == 0 ? [target] : legal;
    }

    internal static Position SelectFormationTarget(
        int width,
        int height,
        IReadOnlyList<string> tileRows,
        Position authored,
        string roleId,
        int minimumSpacing,
        int preferredSpacing,
        int maximumSpacing,
        int searchRadius,
        string blockedSlotPolicy,
        int medicSeparation,
        IReadOnlyCollection<AssignedTarget> assigned)
    {
        Position[] candidates = ReflowGoals(
                width,
                height,
                tileRows,
                authored,
                searchRadius,
                blockedSlotPolicy)
            .Where(candidate => IsEnterable(
                width, height, tileRows, candidate))
            .Where(candidate => assigned.All(other =>
                candidate.ChebyshevDistance(other.Position)
                    >= minimumSpacing))
            .Where(candidate => !string.Equals(
                    roleId, "medic", StringComparison.Ordinal)
                || assigned.Where(other => string.Equals(
                        other.RoleId, "medic", StringComparison.Ordinal))
                    .All(other => candidate.ChebyshevDistance(other.Position)
                        >= medicSeparation))
            .Where(candidate => assigned.Count == 0
                || assigned.Any(other => candidate.ChebyshevDistance(
                        other.Position)
                    <= maximumSpacing))
            .OrderBy(candidate => assigned.Count == 0
                ? 0
                : assigned.Min(other => Math.Abs(
                    candidate.ChebyshevDistance(other.Position)
                    - preferredSpacing)))
            .ThenBy(candidate => candidate.ChebyshevDistance(authored))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .ToArray();
        return candidates.FirstOrDefault(authored);
    }

    private static int ClockwiseRank(Position target, Position position)
    {
        int dx = position.X - target.X;
        int dy = position.Y - target.Y;
        if (dx == 0 && dy == 0)
            return 0;
        // Clockwise from north, expressed without floating point.
        if (dy < 0 && dx >= 0)
            return dx == 0 ? 1 : 2;
        if (dx > 0 && dy >= 0)
            return dy == 0 ? 3 : 4;
        if (dy > 0 && dx <= 0)
            return dx == 0 ? 5 : 6;
        return dy == 0 ? 7 : 8;
    }

    internal static bool IsEnterable(
        int width,
        int height,
        IReadOnlyList<string> tileRows,
        Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < width
        && position.Y < height
        && tileRows[position.Y][position.X] != '#';
}
