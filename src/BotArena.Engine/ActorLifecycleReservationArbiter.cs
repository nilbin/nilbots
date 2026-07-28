using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Family-neutral, all-block arbitration for one joint lifecycle batch.
/// Every bundle in a connected component containing an intersecting slot,
/// tile, or operation claim is blocked.
/// </summary>
internal static class ActorLifecycleReservationArbiter
{
    public static ImmutableHashSet<string> BlockedOperationIds(
        IEnumerable<ActorLifecycleReservationClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ActorLifecycleReservationClaim[] snapshot = [.. claims];
        if (snapshot.Any(claim => claim is null))
        {
            throw new ArgumentException(
                "Lifecycle arbitration claims cannot contain null entries.",
                nameof(claims));
        }
        if (snapshot.Length < 2)
            return ImmutableHashSet<string>.Empty;

        int[] parents = Enumerable.Range(0, snapshot.Length).ToArray();
        int[] componentSizes = Enumerable.Repeat(1, snapshot.Length).ToArray();
        for (int left = 0; left < snapshot.Length; left++)
        {
            for (int right = left + 1; right < snapshot.Length; right++)
            {
                if (Intersects(snapshot[left], snapshot[right]))
                    Union(left, right, parents, componentSizes);
            }
        }

        var blocked = ImmutableHashSet.CreateBuilder<string>(
            StringComparer.Ordinal);
        for (int index = 0; index < snapshot.Length; index++)
        {
            int root = Find(index, parents);
            if (componentSizes[root] > 1)
                blocked.Add(snapshot[index].OperationId);
        }
        return blocked.ToImmutable();
    }

    private static bool Intersects(
        ActorLifecycleReservationClaim left,
        ActorLifecycleReservationClaim right) =>
        string.Equals(
            left.OperationId,
            right.OperationId,
            StringComparison.Ordinal)
        || left.Slots.Intersect(right.Slots).Any()
        || left.Tiles.Intersect(right.Tiles).Any();

    private static int Find(int value, int[] parents)
    {
        while (parents[value] != value)
        {
            parents[value] = parents[parents[value]];
            value = parents[value];
        }
        return value;
    }

    private static void Union(
        int left,
        int right,
        int[] parents,
        int[] componentSizes)
    {
        int leftRoot = Find(left, parents);
        int rightRoot = Find(right, parents);
        if (leftRoot == rightRoot)
            return;
        if (componentSizes[leftRoot] < componentSizes[rightRoot])
            (leftRoot, rightRoot) = (rightRoot, leftRoot);
        parents[rightRoot] = leftRoot;
        componentSizes[leftRoot] = checked(
            componentSizes[leftRoot] + componentSizes[rightRoot]);
    }
}
