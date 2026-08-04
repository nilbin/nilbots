/// <summary>
/// Pure deterministic role allocation for the tactical-playbook runtime.
/// Runtime observations are reduced to these records so death, respawn,
/// stability, pre-emption, and overflow can be tested without an engine.
/// </summary>
internal static class TacticalMembershipPrimitives
{
    internal sealed record Candidate(
        int UnitId,
        string ClassId,
        int Health,
        bool Respawned);

    internal sealed record RoleRule(
        string RoleId,
        string[] CandidateClasses,
        int Minimum,
        int Preferred,
        int Maximum,
        string DeathPolicy,
        string RespawnPolicy,
        string OverflowRoleId,
        string Persistence,
        string Preemption,
        string GroupOverflow);

    internal static Dictionary<int, string> Allocate(
        IReadOnlyCollection<Candidate> candidates,
        IReadOnlyList<RoleRule> rules,
        IReadOnlyDictionary<int, string> prior,
        bool phaseBoundary)
    {
        var result = new Dictionary<int, string>();
        Dictionary<string, RoleRule> byId = rules.ToDictionary(
            value => value.RoleId, StringComparer.Ordinal);
        Dictionary<string, int> priority = rules
            .Select((value, index) => (value.RoleId, index))
            .ToDictionary(value => value.RoleId, value => value.index,
                StringComparer.Ordinal);
        Candidate[] orderedCandidates = candidates
            .OrderBy(value => value.UnitId).ToArray();

        // Stable-slot roles retain eligible lives. A replacement may resume
        // or rejoin its old slot; replace deliberately lets an already
        // promoted live body keep the job until normal allocation chooses.
        foreach (Candidate candidate in orderedCandidates)
        {
            if (!prior.TryGetValue(candidate.UnitId, out string? priorRole)
                || !byId.TryGetValue(priorRole, out RoleRule? rule)
                || !Eligible(candidate, rule)
                || !string.Equals(
                    rule.Persistence, "stable-slot", StringComparison.Ordinal)
                || string.Equals(
                    rule.DeathPolicy, "rebalance", StringComparison.Ordinal)
                || candidate.Respawned && string.Equals(
                    rule.RespawnPolicy, "replace", StringComparison.Ordinal))
            {
                continue;
            }
            if (result.Count(value => value.Value == rule.RoleId)
                < rule.Maximum)
                result[candidate.UnitId] = rule.RoleId;
        }

        foreach (RoleRule rule in rules)
        {
            int live = result.Count(value => value.Value == rule.RoleId);
            int absentStableSlots = prior
                .Where(value => value.Value == rule.RoleId)
                .Count(value => orderedCandidates.All(candidate =>
                    candidate.UnitId != value.Key));
            int vacancyCredit = string.Equals(
                    rule.DeathPolicy, "hold-vacancy",
                    StringComparison.Ordinal)
                ? absentStableSlots
                : 0;
            int needed = Math.Max(
                0, Math.Min(rule.Preferred, rule.Maximum)
                    - live - vacancyCredit);
            if (needed == 0)
                continue;

            Candidate[] pool = orderedCandidates
                .Where(candidate => Eligible(candidate, rule))
                .Where(candidate => !result.ContainsKey(candidate.UnitId)
                    || MayPreempt(
                        rule,
                        result[candidate.UnitId],
                        priority,
                        phaseBoundary))
                .OrderBy(candidate => Array.IndexOf(
                    rule.CandidateClasses, candidate.ClassId))
                .ThenByDescending(candidate => candidate.Health)
                .ThenBy(candidate => candidate.UnitId)
                .Take(needed)
                .ToArray();
            foreach (Candidate candidate in pool)
                result[candidate.UnitId] = rule.RoleId;
        }

        foreach (Candidate candidate in orderedCandidates
                     .Where(value => !result.ContainsKey(value.UnitId)))
        {
            string priorRole = prior.GetValueOrDefault(candidate.UnitId, "");
            string overflow = byId.GetValueOrDefault(priorRole)
                ?.OverflowRoleId ?? "";
            RoleRule? selected = overflow.Length > 0
                && byId.TryGetValue(overflow, out RoleRule? declared)
                && Eligible(candidate, declared)
                && result.Count(value => value.Value == declared.RoleId)
                    < declared.Maximum
                    ? declared
                    : LowestCountEligible(
                        candidate, rules, result, requireLowestCountPolicy: true)
                    ?? LowestCountEligible(
                        candidate, rules, result,
                        requireLowestCountPolicy: false);
            if (selected is null)
            {
                throw new InvalidDataException(
                    $"No tactical role can own live unit {candidate.UnitId}.");
            }
            result[candidate.UnitId] = selected.RoleId;
        }
        return result;
    }

    internal static bool JoinsCohort(string respawnPolicy) =>
        respawnPolicy switch
        {
            "resume" => false,
            "rejoin" or "rally" or "replace" => true,
            _ => throw new InvalidDataException(
                $"Unknown respawn policy '{respawnPolicy}'."),
        };

    private static RoleRule? LowestCountEligible(
        Candidate candidate,
        IReadOnlyList<RoleRule> rules,
        IReadOnlyDictionary<int, string> result,
        bool requireLowestCountPolicy) => rules
        .Where(rule => !requireLowestCountPolicy || string.Equals(
            rule.GroupOverflow, "lowest-count", StringComparison.Ordinal))
        .Where(rule => Eligible(candidate, rule))
        .Where(rule => result.Count(value => value.Value == rule.RoleId)
            < rule.Maximum)
        .OrderBy(rule => result.Count(value => value.Value == rule.RoleId))
        .ThenBy(rule => Array.IndexOf(
            rule.CandidateClasses, candidate.ClassId))
        .ThenBy(rule => RoleIndex(rules, rule.RoleId))
        .FirstOrDefault();

    private static int RoleIndex(
        IReadOnlyList<RoleRule> rules,
        string roleId)
    {
        for (int index = 0; index < rules.Count; index++)
        {
            if (string.Equals(
                    rules[index].RoleId, roleId, StringComparison.Ordinal))
                return index;
        }
        return int.MaxValue;
    }

    private static bool MayPreempt(
        RoleRule incoming,
        string assignedRole,
        IReadOnlyDictionary<string, int> priority,
        bool phaseBoundary) => priority[incoming.RoleId]
            < priority.GetValueOrDefault(assignedRole, int.MaxValue)
        && incoming.Preemption switch
        {
            "never" => false,
            "higher-priority" => true,
            "phase-boundary" => phaseBoundary,
            _ => throw new InvalidDataException(
                $"Unknown membership preemption '{incoming.Preemption}'."),
        };

    private static bool Eligible(Candidate candidate, RoleRule rule) =>
        rule.CandidateClasses.Contains(
            candidate.ClassId, StringComparer.Ordinal);
}
