using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static IEnumerable<JsonObject> StaffRows(
        JsonArray rows,
        string path)
    {
        if (rows.Count is < 1 or > 8)
            throw Error(path, "expected between 1 and 8 staffing rows.");
        foreach (JsonNode? node in rows)
        {
            JsonObject row = node?.AsObject()
                ?? throw Error(path, "staffing row must be an object.");
            ConsolidatedKeys(row, path,
                ["minimum", "preferred", "maximum", "carrier"],
                ["role", "tag", "near", "select"]);
            if ((row["role"] is null) == (row["tag"] is null))
            {
                throw Error(path,
                    "staffing row must name exactly one role or tag.");
            }
            int minimum = RequiredIntNode(row, "minimum", path, 0, 8);
            int preferred = RequiredIntNode(row, "preferred", path, 0, 8);
            int maximum = RequiredIntNode(row, "maximum", path, 0, 8);
            if (minimum > preferred || preferred > maximum)
            {
                throw Error(path,
                    "staffing cardinality must satisfy minimum <= preferred "
                    + "<= maximum.");
            }
            OneOfNode(row, "carrier", path, "forbid", "allow", "require");
            if (row["near"] is not null && row["select"] is not null)
                throw Error(path, "staffing row cannot name both near and select.");
            if (row["select"] is not null)
                OneOfNode(row, "select", path, "most-experienced");
            yield return row;
        }
    }

    private static string[] ResolveSelectorRoles(
        JsonObject row,
        IReadOnlySet<string> roleIds,
        IReadOnlyDictionary<string, List<string>> tagRoles,
        string path)
    {
        if (row["role"] is JsonValue roleValue)
        {
            string role = roleValue.GetValue<string>();
            if (!roleIds.Contains(role))
                throw Error(path, $"unknown role '{role}'.");
            return [role];
        }
        string tag = row["tag"]?.GetValue<string>()
            ?? throw Error(path, "staffing row has no selector.");
        if (!tagRoles.TryGetValue(tag, out List<string>? roles))
            throw Error(path, $"unknown role tag '{tag}'.");
        return [.. roles];
    }

    private static string[] OrderOperations(
        string[] operationIds,
        IReadOnlyDictionary<string, string[]> ladders,
        string path)
    {
        var edges = operationIds.ToDictionary(
            id => id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var indegree = operationIds.ToDictionary(
            id => id,
            _ => 0,
            StringComparer.Ordinal);
        foreach (string[] ladder in ladders.Values)
            for (int index = 0; index + 1 < ladder.Length; index++)
            {
                if (edges[ladder[index]].Add(ladder[index + 1]))
                    indegree[ladder[index + 1]]++;
            }
        var declarationRank = operationIds
            .Select((id, index) => (id, index))
            .ToDictionary(value => value.id, value => value.index,
                StringComparer.Ordinal);
        var ready = new SortedSet<string>(Comparer<string>.Create((left, right) =>
        {
            int byRank = declarationRank[left].CompareTo(declarationRank[right]);
            return byRank != 0
                ? byRank
                : StringComparer.Ordinal.Compare(left, right);
        }));
        foreach ((string id, int degree) in indegree)
        {
            if (degree == 0)
                ready.Add(id);
        }
        var result = new List<string>();
        while (ready.Count > 0)
        {
            string next = ready.Min!;
            ready.Remove(next);
            result.Add(next);
            foreach (string child in edges[next])
            {
                indegree[child]--;
                if (indegree[child] == 0)
                    ready.Add(child);
            }
        }
        if (result.Count != operationIds.Length)
        {
            throw Error($"{path}.doctrines.roles",
                "operation ladders disagree about priority and form a cycle.");
        }
        return [.. result];
    }

    private static Dictionary<string, string> DeriveGroups(
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlyDictionary<string, string[]> ladders,
        JsonObject operationSource,
        IReadOnlyDictionary<string, string[]> operationCandidates,
        ConsolidatedTempo tempo,
        string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ConsolidatedRole role in roles)
        {
            string[] ladder = ladders[role.Id];
            if (ladder.Length == 0)
            {
                result.Add(role.Id, $"{role.Id}-group");
                continue;
            }
            string floor = ladder[^1];
            JsonObject operation = operationSource[floor]!.AsObject();
            JsonArray variants = operation["variants"]!.AsArray();
            bool hasUnconditioned = variants.Any(node =>
            {
                JsonObject variant = node!.AsObject();
                return variant["while"] is null && variant["during"] is null;
            });
            string[] coveredModes = variants
                .Select(node => node!.AsObject()["during"]?.GetValue<string>())
                .Where(mode => mode is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            bool coversTempo = tempo.ModeIds
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(coveredModes);
            if (!hasUnconditioned && !coversTempo)
            {
                throw Error($"{path}.doctrines.roles.{role.Id}",
                    $"operation ladder ends in '{floor}', which needs either "
                    + "one unconditioned floor variant or exactly one "
                    + "'during' variant for every tempo mode.");
            }
            if (!operationCandidates[floor].Contains(
                    role.Id, StringComparer.Ordinal))
            {
                throw Error($"{path}.doctrines.roles.{role.Id}",
                    $"floor operation '{floor}' cannot staff this role.");
            }
            result.Add(role.Id, $"{floor}-group");
        }
        return result;
    }

    private static JsonArray BuildRuntimeGroups(
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlyDictionary<string, string> roleGroups)
    {
        var result = new JsonArray();
        foreach (IGrouping<string, ConsolidatedRole> group in roles
                     .GroupBy(role => roleGroups[role.Id], StringComparer.Ordinal))
        {
            ConsolidatedRole[] members = group.ToArray();
            int preferred = members.Sum(role =>
                role.Runtime["preferred"]!.GetValue<int>());
            int maximum = Math.Min(8, members.Sum(role =>
                role.Runtime["maximum"]!.GetValue<int>()));
            result.Add(new JsonObject
            {
                ["groupId"] = group.Key,
                ["roleIds"] = new JsonArray(
                    members.Select(role => (JsonNode)role.Id).ToArray()),
                // Hunter's historical non-zero group minima were redundant
                // with a fixed eight-body composition. The executor still
                // needs a viable cohort threshold for respawn joining.
                ["minimum"] = preferred > 0 ? 1 : 0,
                ["preferred"] = preferred,
                ["maximum"] = maximum,
                ["membership"] = new JsonObject
                {
                    ["persistence"] = "stable-slot",
                    ["casualty"] = "hold-vacancy",
                    ["preemption"] = "never",
                    ["overflow"] = "unassigned",
                },
                ["localStateMachine"] = new JsonObject
                {
                    ["initialState"] = "active",
                    ["states"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["stateId"] = "active",
                            ["minimumTicks"] = 0,
                            ["transitions"] = new JsonArray(),
                        },
                    },
                },
            });
        }
        return result;
    }

    private static Dictionary<string, string> SelectorGroups(
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlyDictionary<string, List<string>> tagRoles,
        IReadOnlyDictionary<string, string> roleGroups)
    {
        var result = roles.ToDictionary(
            role => role.Id,
            role => roleGroups[role.Id],
            StringComparer.Ordinal);
        foreach ((string tag, List<string> members) in tagRoles)
        {
            string[] groups = members.Select(role => roleGroups[role])
                .Distinct(StringComparer.Ordinal).ToArray();
            if (groups.Length == 1)
            {
                result.Add(tag, groups[0]);
                continue;
            }
            // Tags may freely span cohorts for staffing selectors. They only
            // need a single runtime group when used by a group-backed fact or
            // custody escort; callers of this map produce the precise error.
        }
        return result;
    }

    private static JsonObject BuildConditions(
        JsonObject source,
        IReadOnlyDictionary<string, string> selectorGroups,
        string path)
    {
        var result = new JsonObject
        {
            ["always"] = new JsonObject
            {
                ["fact"] = "always",
                ["operator"] = "equals",
                ["value"] = 1,
            },
            ["no-live-friendlies"] = new JsonObject
            {
                ["fact"] = "live-friendlies",
                ["operator"] = "equals",
                ["value"] = 0,
            },
        };
        foreach ((string id, JsonNode? node) in source)
        {
            IdentifierValue(id, $"{path}.conditions", "condition");
            if (result.ContainsKey(id))
                throw Error($"{path}.conditions", $"reserved condition '{id}'.");
            JsonObject condition = node?.DeepClone().AsObject()
                ?? throw Error($"{path}.conditions.{id}",
                    "condition must be an object.");
            string fact = condition["fact"]?.GetValue<string>()
                ?? throw Error($"{path}.conditions.{id}",
                    "condition needs a fact.");
            string? translated = fact switch
            {
                "live-count" => "group-live-count",
                "joining-count" => "group-joining-count",
                "in-zone-count" => "group-in-zone-count",
                "cohesion" => "group-cohesion",
                "stuck-ticks" => "group-stuck-ticks",
                "max-level" => "group-max-level",
                _ => null,
            };
            if (translated is not null)
            {
                string subject = condition["subject"]?.GetValue<string>()
                    ?? throw Error($"{path}.conditions.{id}",
                        $"fact '{fact}' needs a role or tag subject.");
                if (!selectorGroups.TryGetValue(subject, out string? group))
                {
                    throw Error($"{path}.conditions.{id}",
                        $"selector '{subject}' spans more than one doctrine "
                        + "cohort and cannot back aggregate fact '{fact}'.");
                }
                condition["fact"] = translated;
                condition["subject"] = group;
            }
            if (fact is "formation-established-ticks" or "formation-broken")
            {
                throw Error($"{path}.conditions.{id}",
                    "formation stability is no longer an authored fact; gate "
                    + "the behavior on the tactical state that matters.");
            }
            result[id] = condition;
        }
        return result;
    }

    private static JsonArray BuildCustodyPolicies(
        JsonArray source,
        IReadOnlyDictionary<string, string> selectorGroups,
        string path)
    {
        var result = new JsonArray();
        foreach (JsonNode? node in source)
        {
            JsonObject policy = node?.DeepClone().AsObject()
                ?? throw Error($"{path}.custodyPolicies",
                    "custody policy must be an object.");
            if (policy["escortGroups"] is not null)
            {
                throw Error($"{path}.custodyPolicies",
                    "'escortGroups' is retired; use role/tag selectors in "
                    + "'escorts'.");
            }
            JsonArray escorts = policy["escorts"] as JsonArray
                ?? throw Error($"{path}.custodyPolicies",
                    "custody policy needs an 'escorts' role/tag list.");
            var groups = new List<string>();
            foreach (JsonNode? escort in escorts)
            {
                string selector = escort?.GetValue<string>()
                    ?? throw Error($"{path}.custodyPolicies.escorts",
                        "escort selector must be a string.");
                if (!selectorGroups.TryGetValue(selector, out string? group))
                {
                    throw Error($"{path}.custodyPolicies.escorts",
                        $"selector '{selector}' spans multiple cohorts.");
                }
                if (!groups.Contains(group, StringComparer.Ordinal))
                    groups.Add(group);
            }
            policy.Remove("escorts");
            policy["escortGroups"] = new JsonArray(
                groups.Select(group => (JsonNode)group).ToArray());
            result.Add(policy);
        }
        return result;
    }

    private static JsonObject BuildProfiles(
        JsonArray groups,
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlyDictionary<string, string> roleGroups,
        IReadOnlyDictionary<string, string[]> ladders)
    {
        var result = new JsonObject();
        foreach (JsonObject group in groups.Select(node => node!.AsObject()))
        {
            string groupId = group["groupId"]!.GetValue<string>();
            bool bodyLocal = roles.Any(role =>
                string.Equals(
                    roleGroups[role.Id], groupId, StringComparison.Ordinal)
                && ladders[role.Id].Length == 0);
            result[$"{groupId}-profile"] = new JsonObject
            {
                ["groupId"] = groupId,
                ["localState"] = "active",
                ["priority"] = bodyLocal ? 20 : 10,
                ["members"] = new JsonObject { ["kind"] = "all" },
                ["stuckRecovery"] = "repath",
                ["supportId"] = "",
            };
        }
        return result;
    }

    private static JsonObject StandardFallbacks() => new()
    {
        ["continue-and-hold-invalid"] = new JsonObject
        {
            ["onNoPath"] = "reflow",
            ["onUnderstrength"] = "continue",
            ["onInvalidTarget"] = "hold",
            ["phaseId"] = "",
        },
        ["continue-with-alternate"] = new JsonObject
        {
            ["onNoPath"] = "reflow",
            ["onUnderstrength"] = "continue",
            ["onInvalidTarget"] = "alternate",
            ["phaseId"] = "",
        },
    };
}
