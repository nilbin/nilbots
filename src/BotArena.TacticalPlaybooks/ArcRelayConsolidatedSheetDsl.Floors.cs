using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static void BuildDirectFloors(
        IReadOnlyCollection<ConsolidatedRole> roles,
        JsonObject doctrines,
        IReadOnlyDictionary<string, DoctrineTarget> floorTargets,
        IReadOnlyDictionary<string, string> roleGroups,
        JsonArray supportPolicies,
        JsonObject profiles,
        JsonObject standingOrders,
        JsonArray engagements,
        int maximumAttackers,
        string path)
    {
        foreach (ConsolidatedRole role in roles)
        {
            JsonObject doctrine = doctrines[role.Id]!.AsObject();
            JsonArray modes = doctrine["modes"]!.AsArray();
            JsonObject floor = modes[^1]!.AsObject();
            DoctrineTarget destination = floorTargets[role.Id];
            string engagementId = $"{role.Id}-floor";
            string groupId = roleGroups[role.Id];
            string supportId = DirectSupportId(
                role.Id, supportPolicies, path);
            JsonElement doctrineFight = doctrine["fight"] is JsonObject baseFight
                ? JsonDocument.Parse(baseFight.ToJsonString()).RootElement.Clone()
                : default;
            JsonElement floorFight = floor["fight"] is JsonObject overrideFight
                ? JsonDocument.Parse(overrideFight.ToJsonString()).RootElement.Clone()
                : default;
            ResolvedFight fight = ResolveFight(
                doctrineFight,
                floorFight,
                $"{path}.doctrines.roles.{role.Id}.modes[{modes.Count - 1}]");
            engagements.Add(DoctrineEngagement(
                engagementId,
                role.Id,
                [],
                fight,
                destination.Target,
                new JsonArray(),
                $"{path}.doctrines.roles.{role.Id}",
                maximumAttackers));
            if (supportId.Length > 0)
                profiles[$"{groupId}-profile"]!["supportId"] = supportId;

            string orderId = $"{role.Id}-floor";
            var order = new JsonObject
            {
                ["groupId"] = groupId,
                ["priority"] = 20,
                ["members"] = new JsonObject { ["kind"] = "all" },
                ["movement"] = new JsonObject
                {
                    ["kind"] = destination.Kind,
                    ["target"] = destination.Target,
                    ["arrivalRadius"] = 2,
                    ["completion"] = "continuous",
                    ["stuckTicks"] = 10,
                    ["stuckRecovery"] = "repath",
                    ["chaseLeash"] = fight.ChaseLeash,
                    ["pace"] = "free",
                },
                ["formationId"] = "",
                ["engagementId"] = engagementId,
                ["localState"] = "active",
                ["fallback"] = new JsonObject
                {
                    ["onNoPath"] = "reflow",
                    ["onUnderstrength"] = "continue",
                    ["onInvalidTarget"] = "hold",
                    ["phaseId"] = "",
                },
                ["custodyId"] = doctrine["custody"]!.GetValue<string>(),
            };
            if (supportId.Length > 0)
                order["supportId"] = supportId;
            if (doctrine["conceal"]?.GetValue<bool>() != false)
                order["stance"] = "ambush";
            if (doctrine["scavenge"] is JsonNode collectZones)
                order["collectZones"] = collectZones.DeepClone();
            standingOrders[orderId] = order;
        }
    }

    private static string DirectSupportId(
        string role,
        JsonArray supportPolicies,
        string path)
    {
        string[] matching = supportPolicies
            .Select(node => node!.AsObject())
            .Where(policy => policy["providers"]!.AsArray()
                .Any(provider => string.Equals(
                    provider!.GetValue<string>(), role, StringComparison.Ordinal)))
            .Select(policy => policy["supportId"]!.GetValue<string>())
            .ToArray();
        return matching.Length switch
        {
            0 => "",
            1 => matching[0],
            _ => throw Error($"{path}.doctrines.roles.{role}",
                "one role provides more than one repair policy."),
        };
    }

}
