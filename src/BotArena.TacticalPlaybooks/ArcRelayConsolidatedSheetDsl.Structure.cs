using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static JsonArray BuildRuntimeGroups(
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlyDictionary<string, string> roleGroups)
    {
        var result = new JsonArray();
        foreach (ConsolidatedRole role in roles)
        {
            int preferred = role.Runtime["preferred"]!.GetValue<int>();
            int maximum = role.Runtime["maximum"]!.GetValue<int>();
            string casualty = role.Runtime["deathPolicy"]!.GetValue<string>()
                switch
                {
                    "hold-vacancy" => "hold-vacancy",
                    "promote-best" => "promote-role",
                    "rebalance" => "rebalance",
                    string unknown => throw new InvalidDataException(
                        $"Role '{role.Id}' has unknown deathPolicy "
                        + $"'{unknown}'."),
                };
            result.Add(new JsonObject
            {
                ["groupId"] = roleGroups[role.Id],
                ["roleIds"] = new JsonArray(role.Id),
                ["minimum"] = preferred > 0 ? 1 : 0,
                ["preferred"] = preferred,
                ["maximum"] = maximum,
                ["membership"] = new JsonObject
                {
                    ["persistence"] = "stable-slot",
                    ["casualty"] = casualty,
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

    private static JsonObject BuildConditions(
        JsonObject source,
        IReadOnlyDictionary<string, string> roleGroups,
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
            if (fact == "max-level")
            {
                if (condition["subject"] is JsonValue subjectValue)
                {
                    string role = subjectValue.GetValue<string>();
                    if (!roleGroups.TryGetValue(role, out string? group))
                    {
                        throw Error($"{path}.conditions.{id}",
                            $"max-level references unknown role '{role}'.");
                    }
                    condition["fact"] = "group-max-level";
                    condition["subject"] = group;
                }
                else
                {
                    condition["fact"] = "team-max-level";
                }
            }
            else if (fact == "tempo-active")
                throw Error($"{path}.conditions.{id}",
                    "tempo-active is retired; express a beat as a drawn "
                    + "patrol route so progress resumes after interruption.");
            else if (fact is "live-count" or "joining-count"
                     or "in-zone-count" or "cohesion" or "stuck-ticks")
            {
                string role = condition["subject"]?.GetValue<string>()
                    ?? throw Error($"{path}.conditions.{id}",
                        $"fact '{fact}' needs a role subject.");
                if (!roleGroups.TryGetValue(role, out string? group))
                {
                    throw Error($"{path}.conditions.{id}",
                        $"fact '{fact}' references unknown role '{role}'.");
                }
                condition["fact"] = fact switch
                {
                    "live-count" => "group-live-count",
                    "joining-count" => "group-joining-count",
                    "in-zone-count" => "group-in-zone-count",
                    "cohesion" => "group-cohesion",
                    _ => "group-stuck-ticks",
                };
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
        JsonObject doctrines,
        IReadOnlyDictionary<string, string> roleGroups,
        string path)
    {
        var result = new JsonArray();
        foreach (JsonNode? node in source)
        {
            JsonObject policy = node?.DeepClone().AsObject()
                ?? throw Error($"{path}.custodyPolicies",
                    "custody policy must be an object.");
            if (policy.ContainsKey("escortGroups") || policy.ContainsKey("escorts"))
            {
                throw Error($"{path}.custodyPolicies",
                    "custody escort lists are retired; fetch rungs already "
                    + "state which roles volunteer for Core work.");
            }
            string custodyId = policy["custodyId"]?.GetValue<string>()
                ?? throw Error($"{path}.custodyPolicies",
                    "custody policy needs custodyId.");
            string[] courierGroups = doctrines
                .Where(pair => pair.Value is JsonObject doctrine
                    && doctrine["modes"] is JsonArray modes
                    && modes.Any(mode => string.Equals(
                        mode?["fetch"]?.GetValue<string>(),
                        custodyId,
                        StringComparison.Ordinal)))
                .Select(pair => roleGroups[pair.Key])
                .ToArray();
            policy["escortGroups"] = new JsonArray(
                courierGroups.Select(group => (JsonNode)group).ToArray());
            result.Add(policy);
        }
        return result;
    }

    private static JsonObject BuildProfiles(JsonArray groups)
    {
        var result = new JsonObject();
        foreach (JsonObject group in groups.Select(node => node!.AsObject()))
        {
            string groupId = group["groupId"]!.GetValue<string>();
            result[$"{groupId}-profile"] = new JsonObject
            {
                ["groupId"] = groupId,
                ["localState"] = "active",
                ["priority"] = 20,
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
