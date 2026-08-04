using BotArena.Sdk;

internal static class TacticalFormationPrimitives
{
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
        if (orientation is not "fixed" && focusTarget is Position focus)
            return focus;
        return orientation switch
        {
            "route" => movementTarget == bodyPosition
                ? null
                : movementTarget,
            "enemy-reactor" => enemyReactor,
            "own-reactor" => ownReactor,
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
        string vacancyPolicy)
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
        return ordinal;
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
        Position[] legal = candidates
            .Where(position => IsEnterable(
                width, height, tileRows, position))
            .Distinct()
            .ToArray();
        return legal.Length == 0 ? [target] : legal;
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
