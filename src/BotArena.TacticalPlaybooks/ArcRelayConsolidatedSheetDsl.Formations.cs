using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static JsonArray BuildFormations(
        IReadOnlyCollection<ConsolidatedRole> roles,
        JsonObject operations,
        JsonObject oldDoctrines,
        string path)
    {
        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach ((_, JsonNode? node) in operations)
        {
            JsonObject operation = node!.AsObject();
            string shape = operation["formation"]!.GetValue<string>();
            string pace = operation["pace"]?.GetValue<string>() ?? "free";
            string orientation = operation["orientation"]?.GetValue<string>()
                ?? "enemy-reactor";
            requested.Add(FormationKey(shape, pace, orientation));
        }
        // A role whose floor is body-local still needs an executor phase
        // formation for joining/cohesion bookkeeping. The role's authored
        // patrol remains the actual order whenever its doctrine task is live.
        foreach (ConsolidatedRole role in roles)
        {
            JsonArray modes = oldDoctrines[role.Id]!["modes"]!.AsArray();
            if (modes[^1]!["squad"] is null)
            {
                requested.Add(FormationKey(
                    "file", "slowest", "enemy-reactor"));
            }
        }

        var result = new JsonArray();
        foreach (string key in requested.Order(StringComparer.Ordinal))
        {
            string[] parts = key.Split('|');
            string shape = parts[0];
            string pace = parts[1];
            string orientation = parts[2];
            result.Add(BuildFormation(
                shape, pace, orientation, roles, path));
        }
        return result;
    }

    private static string FormationKey(
        string shape,
        string pace,
        string orientation) => $"{shape}|{pace}|{orientation}";

    private static JsonObject BuildFormation(
        string shape,
        string pace,
        string orientation,
        IReadOnlyCollection<ConsolidatedRole> roles,
        string path)
    {
        string formationId = $"dsl-{shape}-{pace}-"
            + (orientation == "own-reactor" ? "home" : "enemy");
        string[] postureOrder = (shape, pace, orientation) switch
        {
            ("screen", _, _) =>
                ["vanguard", "core", "rear", "scout", "support", "tail", "wide"],
            ("file", "free", "own-reactor") =>
                ["tail", "scout", "support", "core", "vanguard", "rear", "wide"],
            ("file", _, _) =>
                ["scout", "support", "core", "vanguard", "rear", "tail", "wide"],
            _ => throw Error(path, $"unsupported formation shape '{shape}'."),
        };
        var placementBands = new JsonArray();
        foreach (string posture in postureOrder)
            foreach (ConsolidatedRole role in roles.Where(role =>
                         role.FormationPosture == posture))
            {
                (int X, int Y)[] slots = FormationSlots(shape, posture);
                int maximum = role.Runtime["maximum"]!.GetValue<int>();
                if (maximum > slots.Length)
                {
                    throw Error($"{path}.roles.{role.Id}",
                        $"formation posture '{posture}' supplies {slots.Length} "
                        + $"slots but role maximum is {maximum}.");
                }
                placementBands.Add(new JsonObject
                {
                    ["roleId"] = role.Id,
                    ["sector"] = posture == "vanguard" ? "front" : "centre",
                    ["offsets"] = new JsonArray(slots
                        .Select(slot => (JsonNode)new JsonArray(slot.X, slot.Y))
                        .ToArray()),
                });
            }
        return new JsonObject
        {
            ["sourceKey"] = FormationKey(shape, pace, orientation),
            ["formationId"] = formationId,
            ["shape"] = "escort",
            ["orientation"] = orientation,
            ["spacing"] = new JsonObject
            {
                ["metric"] = "chebyshev",
                ["minimum"] = 1,
                ["preferred"] = 2,
                ["maximum"] = 4,
            },
            ["cohesion"] = new JsonObject
            {
                ["arrivalRatioPercent"] = 63,
                ["breakRatioPercent"] = 50,
                ["breakTicks"] = 3,
                ["reformTicks"] = pace == "slowest" ? 4 : 3,
                ["pace"] = pace,
            },
            ["reflow"] = new JsonObject
            {
                ["blockedSlot"] = "nearest-legal",
                ["vacancy"] = "compress",
                ["searchRadius"] = 3,
                ["medicSeparation"] = 2,
            },
            ["placementBands"] = placementBands,
        };
    }

    private static (int X, int Y)[] FormationSlots(
        string shape,
        string posture) => (shape, posture) switch
        {
            ("file", "scout") => [(0, 0)],
            ("file", "support") => [(2, 0)],
            ("file", "core") => [(1, 0), (1, -1)],
            ("file", "vanguard") => [(1, 1), (2, -1)],
            ("file", "rear") => [(2, 1)],
            ("file", "wide") => [(2, -2)],
            ("file", "tail") =>
                [(3, 0), (3, -1), (3, 1), (4, 0),
             (4, -1), (4, 1), (5, 0), (5, -1)],
            ("screen", "vanguard") => [(0, -1), (0, 1)],
            ("screen", "core") => [(1, 0), (1, -1)],
            ("screen", "rear") => [(2, 0)],
            ("screen", "scout") => [(1, 1)],
            ("screen", "support") => [(2, 1)],
            ("screen", "wide") => [(2, -2)],
            ("screen", "tail") =>
                [(3, 0), (3, -1), (3, 1), (4, 0),
             (4, -1), (4, 1), (5, 0), (5, -1)],
            _ => [],
        };
}
