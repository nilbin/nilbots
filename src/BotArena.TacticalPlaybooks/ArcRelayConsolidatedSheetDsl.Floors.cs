using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static void BuildDirectFloors(
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlyDictionary<string, string[]> ladders,
        JsonObject doctrines,
        IReadOnlyDictionary<string, string> roleGroups,
        IReadOnlyDictionary<string, string> formationIds,
        JsonArray supportPolicies,
        JsonObject profiles,
        JsonObject maneuvers,
        IReadOnlyDictionary<string, JsonArray> baselineOrderIds,
        ConsolidatedTempo tempo,
        IReadOnlyDictionary<string, JsonObject> roleTempoModes,
        JsonArray engagements,
        string path)
    {
        foreach (ConsolidatedRole role in roles.Where(role =>
                     ladders[role.Id].Length == 0))
        {
            JsonObject doctrine = doctrines[role.Id]!.AsObject();
            JsonArray modes = doctrine["modes"]!.AsArray();
            JsonObject floor = modes[^1]!.AsObject();
            if (floor["patrol"] is not JsonValue patrol)
            {
                throw Error($"{path}.doctrines.roles.{role.Id}",
                    "a role outside the coordinated-operation plane needs a "
                    + "body-local patrol floor.");
            }
            string floorRoute = patrol.GetValue<string>();
            int patrolCount = modes.Count(node => node!["patrol"] is not null);
            string engagementId = patrolCount > 1
                ? $"{role.Id}-patrol-{patrolCount}"
                : $"{role.Id}-patrol";
            string orderStem = $"{role.Id}-floor";
            string groupId = roleGroups[role.Id];
            string supportId = DirectSupportId(
                role.Id, doctrine, supportPolicies, path);
            var assignment = new JsonObject
            {
                ["arrivalRadius"] = 2,
                ["completion"] = "continuous",
                ["chaseLeash"] = 3,
                ["engagementId"] = engagementId,
                ["custodyId"] = doctrine["custody"]!.GetValue<string>(),
                ["fallbackId"] = "continue-and-hold-invalid",
                ["assignmentProfileId"] = $"{groupId}-profile",
            };
            if (doctrine["conceal"]?.GetValue<bool>() == true)
                assignment["stance"] = "ambush";
            if (supportId.Length > 0)
                profiles[$"{groupId}-profile"]!["supportId"] = supportId;
            foreach (string modeId in tempo.ModeIds)
            {
                string route = floorRoute;
                string movementKind = route == "traffic"
                    ? "shadow-traffic" : "route";
                int reach = 3;
                string modeEngagementId = engagementId;
                string fallbackId = "continue-and-hold-invalid";
                if (roleTempoModes[role.Id][modeId] is JsonObject tempoMode)
                {
                    string modeAt =
                        $"{path}.doctrines.roles.{role.Id}.tempo.{modeId}";
                    ConsolidatedKeys(tempoMode, modeAt, [],
                        ["patrol", "assault", "reach", "fight"]);
                    ValidateOperationNumbers(tempoMode, modeAt);
                    bool patrolMode = tempoMode["patrol"] is JsonValue;
                    bool assaultMode = tempoMode["assault"] is JsonValue;
                    if (patrolMode == assaultMode)
                    {
                        throw Error(modeAt,
                            "tempo override needs exactly one patrol or assault.");
                    }
                    route = (tempoMode["patrol"] ?? tempoMode["assault"])!
                        .GetValue<string>();
                    movementKind = route == "traffic"
                        ? "shadow-traffic" : "route";
                    reach = tempoMode["reach"]?.GetValue<int>() ?? 3;
                    fallbackId = assaultMode
                        ? "continue-with-alternate"
                        : "continue-and-hold-invalid";
                    if (tempoMode["fight"] is JsonObject fight)
                    {
                        modeEngagementId = $"{role.Id}-tempo-{modeId}-fight";
                        engagements.Add(OperationEngagement(
                            modeEngagementId, [role.Id], fight, modeAt));
                    }
                }
                string orderId = $"{orderStem}-{modeId}";
                JsonObject modeAssignment = assignment.DeepClone().AsObject();
                modeAssignment["engagementId"] = modeEngagementId;
                modeAssignment["chaseLeash"] = reach;
                modeAssignment["fallbackId"] = fallbackId;
                var track = new JsonObject
                {
                    ["formationId"] = formationIds[
                        FormationKey("file", "slowest", "enemy-reactor")],
                    ["movement"] = new JsonObject
                    {
                        ["kind"] = movementKind,
                        ["target"] = route == "traffic" ? "" : route,
                        ["stuckTicks"] = 10,
                        ["pace"] = "slowest",
                    },
                    ["assignments"] = new JsonObject
                    {
                        [orderId] = modeAssignment,
                    },
                };
                maneuvers[$"__floor|{modeId}|{orderId}"] = new JsonObject
                {
                    ["tracks"] = new JsonObject
                    {
                        ["main"] = track.DeepClone(),
                    },
                };
                baselineOrderIds[modeId].Add(orderId);
            }
        }
    }

    private static string DirectSupportId(
        string role,
        JsonObject doctrine,
        JsonArray supportPolicies,
        string path)
    {
        HashSet<string> involved = [role];
        foreach (JsonObject mode in doctrine["modes"]!.AsArray()
                     .Where(node => node is JsonObject)
                     .Select(node => node!.AsObject()))
        {
            if (mode["escort"] is not JsonArray escorts)
                continue;
            foreach (JsonNode? escort in escorts)
            {
                if (escort is JsonValue value)
                {
                    involved.Add(value.GetValue<string>());
                    continue;
                }
                if (escort is JsonObject entry)
                    involved.Add(entry["role"]!.GetValue<string>());
            }
        }
        string[] matching = supportPolicies
            .Select(node => node!.AsObject())
            .Where(policy => policy["providers"]!.AsArray()
                .Any(provider => involved.Contains(provider!.GetValue<string>())))
            .Select(policy => policy["supportId"]!.GetValue<string>())
            .ToArray();
        return matching.Length switch
        {
            0 => "",
            1 => matching[0],
            _ => throw Error($"{path}.doctrines.roles.{role}",
                "body-local escort resolves multiple repair-provider policies; "
                + "split the call or make the provider unambiguous."),
        };
    }

    private static JsonObject FloorTracks(
        JsonObject maneuvers,
        JsonArray baselineOrderIds,
        string modeId,
        string path)
    {
        var tracks = new JsonObject();
        string[] temporary = maneuvers.Select(item => item.Key)
            .Where(key => key.StartsWith(
                $"__floor|{modeId}|", StringComparison.Ordinal))
            .ToArray();
        foreach (string key in temporary)
        {
            JsonObject sourceTracks = maneuvers[key]!["tracks"]!.AsObject();
            JsonObject sourceTrack = sourceTracks.Single().Value!.AsObject();
            string orderId = sourceTrack["assignments"]!.AsObject()
                .Single().Key;
            tracks[orderId] = sourceTrack.DeepClone();
            maneuvers.Remove(key);
        }
        string[] expected = baselineOrderIds
            .Select(node => node!.GetValue<string>()).ToArray();
        if (!expected.ToHashSet(StringComparer.Ordinal)
                .SetEquals(tracks.Select(item => item.Key)))
        {
            throw Error(path,
                "compiler failed to generate exactly one floor order per "
                + "declared doctrine cohort.");
        }
        return tracks;
    }
}
