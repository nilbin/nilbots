using BotArena.Sdk;

internal static class TacticalFormationPrimitives
{
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
