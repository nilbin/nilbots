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
        string GroupId,
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

    internal sealed record GroupRule(
        string GroupId,
        int Minimum,
        int Preferred,
        int Maximum);

    internal static Dictionary<int, string> Allocate(
        IReadOnlyCollection<Candidate> candidates,
        IReadOnlyList<RoleRule> rules,
        IReadOnlyList<GroupRule> groups,
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
        Dictionary<string, GroupRule> groupsById = groups.ToDictionary(
            value => value.GroupId, StringComparer.Ordinal);
        if (rules.Any(rule => !groupsById.ContainsKey(rule.GroupId)))
            throw new InvalidDataException(
                "Every tactical role must reference a declared group.");
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
                    < rule.Maximum
                && CanAssignToGroup(
                    candidate.UnitId,
                    rule,
                    result,
                    byId,
                    groupsById[rule.GroupId].Maximum))
                result[candidate.UnitId] = rule.RoleId;
        }

        // First satisfy role minima within the hard group maximum. Then grow
        // toward role preferences, but never let one role consume more than
        // its group's preferred editor-authored membership target.
        foreach (RoleRule rule in rules)
        {
            FillRole(
                rule,
                rule.Minimum,
                groupsById[rule.GroupId].Maximum,
                orderedCandidates,
                rules,
                prior,
                result,
                byId,
                priority,
                phaseBoundary);
        }
        foreach (RoleRule rule in rules)
        {
            FillRole(
                rule,
                rule.Preferred,
                groupsById[rule.GroupId].Preferred,
                orderedCandidates,
                rules,
                prior,
                result,
                byId,
                priority,
                phaseBoundary);
        }

        // A group preference may exceed the sum of its role preferences. Fill
        // that deliberate flexible band from unassigned eligible bodies before
        // general overflow, balancing the live role counts deterministically.
        foreach (GroupRule group in groups)
        {
            while (CountGroup(result, byId, group.GroupId) < group.Preferred)
            {
                (Candidate Candidate, RoleRule Rule)? selected =
                    orderedCandidates
                        .Where(candidate => !result.ContainsKey(
                            candidate.UnitId))
                        .SelectMany(candidate => rules
                            .Where(rule => rule.GroupId == group.GroupId
                                && Eligible(candidate, rule)
                                && result.Count(value => value.Value
                                        == rule.RoleId)
                                    < rule.Maximum)
                            .Select(rule => (Candidate: candidate, Rule: rule)))
                        .OrderBy(value => result.Count(assignment =>
                            assignment.Value == value.Rule.RoleId))
                        .ThenBy(value => Array.IndexOf(
                            value.Rule.CandidateClasses,
                            value.Candidate.ClassId))
                        .ThenByDescending(value => value.Candidate.Health)
                        .ThenBy(value => value.Candidate.UnitId)
                        .ThenBy(value => priority[value.Rule.RoleId])
                        .Cast<(Candidate Candidate, RoleRule Rule)?>()
                        .FirstOrDefault();
                if (selected is null)
                    break;
                result[selected.Value.Candidate.UnitId] =
                    selected.Value.Rule.RoleId;
            }
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
                && CanAssignToGroup(
                    candidate.UnitId,
                    declared,
                    result,
                    byId,
                    groupsById[declared.GroupId].Maximum)
                    ? declared
                    : LowestCountEligible(
                        candidate, rules, groupsById, result,
                        requireLowestCountPolicy: true)
                    ?? LowestCountEligible(
                        candidate, rules, groupsById, result,
                        requireLowestCountPolicy: false);
            if (selected is null)
            {
                throw new InvalidDataException(
                    $"No tactical role can own live unit {candidate.UnitId}.");
            }
            result[candidate.UnitId] = selected.RoleId;
        }
        foreach (GroupRule group in groups)
        {
            int count = CountGroup(result, byId, group.GroupId);
            if (count > group.Maximum)
            {
                throw new InvalidDataException(
                    $"Tactical group '{group.GroupId}' cardinality {count} "
                    + $"exceeds its maximum {group.Maximum}.");
            }
        }
        return result;
    }

    private static void FillRole(
        RoleRule rule,
        int target,
        int groupLimit,
        IReadOnlyList<Candidate> candidates,
        IReadOnlyList<RoleRule> rules,
        IReadOnlyDictionary<int, string> prior,
        Dictionary<int, string> result,
        IReadOnlyDictionary<string, RoleRule> byId,
        IReadOnlyDictionary<string, int> priority,
        bool phaseBoundary)
    {
        int absentStableSlots = prior
            .Where(value => value.Value == rule.RoleId)
            .Count(value => candidates.All(candidate =>
                candidate.UnitId != value.Key));
        int vacancyCredit = string.Equals(
                rule.DeathPolicy, "hold-vacancy", StringComparison.Ordinal)
            ? absentStableSlots
            : 0;
        while (result.Count(value => value.Value == rule.RoleId)
                   + vacancyCredit
               < Math.Min(target, rule.Maximum))
        {
            Candidate? selected = candidates
                .Where(candidate => Eligible(candidate, rule))
                .Where(candidate => !result.ContainsKey(candidate.UnitId)
                    || MayPreempt(
                        rule,
                        result[candidate.UnitId],
                        priority,
                        phaseBoundary))
                .Where(candidate => CanAssignToGroup(
                    candidate.UnitId,
                    rule,
                    result,
                    byId,
                    groupLimit))
                .OrderBy(candidate => Array.IndexOf(
                    rule.CandidateClasses, candidate.ClassId))
                .ThenByDescending(candidate => candidate.Health)
                .ThenBy(candidate => candidate.UnitId)
                .FirstOrDefault();
            if (selected is null)
                break;
            result[selected.UnitId] = rule.RoleId;
        }
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
        IReadOnlyDictionary<string, GroupRule> groups,
        IReadOnlyDictionary<int, string> result,
        bool requireLowestCountPolicy) => rules
        .Where(rule => !requireLowestCountPolicy || string.Equals(
            rule.GroupOverflow, "lowest-count", StringComparison.Ordinal))
        .Where(rule => Eligible(candidate, rule))
        .Where(rule => result.Count(value => value.Value == rule.RoleId)
            < rule.Maximum)
        .Where(rule => CanAssignToGroup(
            candidate.UnitId,
            rule,
            result,
            rules.ToDictionary(value => value.RoleId, StringComparer.Ordinal),
            groups[rule.GroupId].Maximum))
        .OrderBy(rule => result.Count(value => value.Value == rule.RoleId))
        .ThenBy(rule => Array.IndexOf(
            rule.CandidateClasses, candidate.ClassId))
        .ThenBy(rule => RoleIndex(rules, rule.RoleId))
        .FirstOrDefault();

    private static int CountGroup(
        IReadOnlyDictionary<int, string> assignments,
        IReadOnlyDictionary<string, RoleRule> roles,
        string groupId) => assignments.Count(value => string.Equals(
            roles[value.Value].GroupId, groupId, StringComparison.Ordinal));

    private static bool CanAssignToGroup(
        int unitId,
        RoleRule incoming,
        IReadOnlyDictionary<int, string> assignments,
        IReadOnlyDictionary<string, RoleRule> roles,
        int groupLimit)
    {
        int count = CountGroup(assignments, roles, incoming.GroupId);
        if (assignments.TryGetValue(unitId, out string? currentRole)
            && string.Equals(
                roles[currentRole].GroupId,
                incoming.GroupId,
                StringComparison.Ordinal))
        {
            return count <= groupLimit;
        }
        return count < groupLimit;
    }

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
