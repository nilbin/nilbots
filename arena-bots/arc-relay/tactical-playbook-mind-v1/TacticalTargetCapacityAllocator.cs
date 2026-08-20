/// <summary>
/// Chooses the first available authored target for every body while enforcing
/// target capacity across the entire fallback walk. Claims already won in an
/// earlier pass remain live when another body falls through to the same target.
/// </summary>
internal static class TacticalTargetCapacityAllocator
{
    internal sealed record Candidate<T>(
        T Value,
        string? Scope,
        string Target,
        int Limit,
        int Distance,
        string? Selection = null,
        int VeteranLevel = 1,
        int? ScopeLimit = null);

    internal static Dictionary<int, T> Allocate<T>(
        IReadOnlyDictionary<int, IReadOnlyList<Candidate<T>>> candidatesByBody)
    {
        var selected = new Dictionary<int, T>();
        var indexes = candidatesByBody.Keys.ToDictionary(bodyId => bodyId, _ => 0);
        var claimed = new Dictionary<(string Scope, string Target), int>();
        var claimedScopes = new Dictionary<string, int>(StringComparer.Ordinal);
        var scopeLimits = new Dictionary<string, int>(StringComparer.Ordinal);

        while (indexes.Count > 0)
        {
            foreach (int exhausted in indexes
                         .Where(pair => pair.Value
                             >= candidatesByBody[pair.Key].Count)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                indexes.Remove(exhausted);
            }
            if (indexes.Count == 0)
                break;

            (int BodyId, Candidate<T> Candidate)[] current = indexes
                .Select(pair => (
                    pair.Key,
                    candidatesByBody[pair.Key][pair.Value]))
                .ToArray();

            foreach ((int bodyId, Candidate<T> candidate) in current
                         .Where(value => value.Candidate.Scope is null))
            {
                selected[bodyId] = candidate.Value;
                indexes.Remove(bodyId);
            }

            foreach (IGrouping<(string Scope, string Target),
                         (int BodyId, Candidate<T> Candidate)> group in current
                         .Where(value => value.Candidate.Scope is not null)
                         .GroupBy(value => (
                             value.Candidate.Scope!,
                             value.Candidate.Target)))
            {
                int[] limits = group.Select(value => value.Candidate.Limit)
                    .Distinct()
                    .ToArray();
                if (limits.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Capacity {group.Key.Scope} has inconsistent limits "
                        + $"for target {group.Key.Target}.");
                }

                string?[] selections = group
                    .Select(value => value.Candidate.Selection)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (selections.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Capacity {group.Key.Scope} has inconsistent selection policies "
                        + $"for target {group.Key.Target}.");
                }

                int remaining = Math.Max(
                    0,
                    limits[0] - claimed.GetValueOrDefault(group.Key));
                int? scopeLimit = group.Select(value =>
                        value.Candidate.ScopeLimit)
                    .Distinct()
                    .SingleOrDefault();
                if (scopeLimit is int bounded)
                {
                    if (scopeLimits.TryGetValue(
                            group.Key.Scope, out int prior)
                        && prior != bounded)
                    {
                        throw new InvalidOperationException(
                            $"Capacity {group.Key.Scope} has inconsistent scope limits.");
                    }
                    scopeLimits[group.Key.Scope] = bounded;
                    remaining = Math.Min(
                        remaining,
                        Math.Max(0, bounded
                            - claimedScopes.GetValueOrDefault(group.Key.Scope)));
                }
                IEnumerable<(int BodyId, Candidate<T> Candidate)> ranked = group;
                ranked = string.Equals(selections[0], "highest-veteran",
                        StringComparison.Ordinal)
                    ? ranked.OrderByDescending(value => value.Candidate.VeteranLevel)
                        .ThenBy(value => value.Candidate.Distance)
                        .ThenBy(value => value.BodyId)
                    : ranked.OrderBy(value => value.Candidate.Distance)
                        .ThenBy(value => value.BodyId);
                (int BodyId, Candidate<T> Candidate)[] winners = ranked
                    .Take(remaining)
                    .ToArray();
                HashSet<int> winnerIds = winners
                    .Select(value => value.BodyId)
                    .ToHashSet();

                foreach ((int bodyId, Candidate<T> candidate) in winners)
                {
                    selected[bodyId] = candidate.Value;
                    indexes.Remove(bodyId);
                }
                claimed[group.Key] = claimed.GetValueOrDefault(group.Key)
                    + winners.Length;
                claimedScopes[group.Key.Scope] =
                    claimedScopes.GetValueOrDefault(group.Key.Scope)
                    + winners.Length;

                foreach ((int bodyId, _) in group.Where(value =>
                             !winnerIds.Contains(value.BodyId)))
                {
                    indexes[bodyId]++;
                }
            }
        }

        return selected;
    }
}
