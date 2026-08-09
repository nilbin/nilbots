using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private sealed record ConsolidatedRole(string Id, JsonObject Runtime);

    /// <summary>
    /// Expands the condition-coordinated sheet DSL into the exhaustive IR
    /// consumed by the existing compiler and executor. The authored surface
    /// has no operations, staffing rows, formations, escort calls or muster
    /// contracts: every role owns one ordered ladder, and roles coordinate by
    /// naming the same conditions and destinations.
    /// </summary>
    private static JsonDocument ExpandConsolidatedDoctrineSource(
        JsonElement source,
        string path)
    {
        Object(source, path,
            [
                "schema", "playbookId", "auditStatus", "composition",
                "layout", "memory", "roles", "custodyPolicies",
                "doctrines", "conditions",
            ]);

        JsonObject sourceRoot = JsonNode.Parse(source.GetRawText())!.AsObject();
        JsonObject doctrinePlane = RequiredObject(sourceRoot, "doctrines", path);
        ConsolidatedKeys(doctrinePlane, $"{path}.doctrines",
            ["roles", "cohorts", "focus"]);
        JsonObject roleDoctrineSource = RequiredObject(
            doctrinePlane, "roles", $"{path}.doctrines");
        JsonObject cohortSource = RequiredObject(
            doctrinePlane, "cohorts", $"{path}.doctrines");
        JsonObject focusSource = RequiredObject(
            doctrinePlane, "focus", $"{path}.doctrines");
        Dictionary<string, ConsolidatedCohort> cohorts = ParseCohorts(
            cohortSource, $"{path}.doctrines.cohorts");
        Dictionary<string, ConsolidatedFocus> focus = ParseFocus(
            focusSource, $"{path}.doctrines.focus");

        JsonElement[] roleElements = BoundedArray(
            source.GetProperty("roles"), $"{path}.roles", 1, 16);
        var roles = new List<ConsolidatedRole>();
        var roleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement role in roleElements)
        {
            string at = $"{path}.roles";
            Object(role, at,
                [
                    "roleId", "candidateClasses", "minimum", "preferred",
                    "maximum", "carrierPreference", "deathPolicy",
                    "respawnPolicy", "overflowRoleId",
                ],
                ["build"]);
            string roleId = Identifier(role, "roleId", at);
            if (!roleIds.Add(roleId))
                throw Error(at, $"duplicate role '{roleId}'.");
            roles.Add(new ConsolidatedRole(
                roleId,
                JsonNode.Parse(role.GetRawText())!.AsObject()));
        }

        var oldDoctrines = new JsonObject();
        var supportPolicies = new JsonArray();
        var floorTargets = new Dictionary<string, DoctrineTarget>(
            StringComparer.Ordinal);
        var cohortConsumers = cohorts.Keys.ToDictionary(
            id => id, _ => new List<ConsolidatedCohortConsumer>(),
            StringComparer.Ordinal);
        var focusConsumers = focus.Keys.ToDictionary(
            id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (ConsolidatedRole role in roles)
        {
            if (roleDoctrineSource[role.Id] is not JsonObject doctrine)
            {
                throw Error($"{path}.doctrines.roles",
                    $"role '{role.Id}' has no doctrine; every role needs one "
                    + "ordered mode list.");
            }
            string at = $"{path}.doctrines.roles.{role.Id}";
            ConsolidatedKeys(doctrine, at,
                ["custody", "modes"],
                ["fight", "conceal", "scavenge", "repair"]);
            JsonArray modes = RequiredArray(doctrine, "modes", at);
            if (modes.Count is < 1 or > 8)
                throw Error($"{at}.modes", "expected between 1 and 8 modes.");
            for (int index = 0; index < modes.Count; index++)
            {
                if (modes[index] is not JsonObject mode)
                    throw Error($"{at}.modes[{index}]", "mode must be an object.");
                if (mode.ContainsKey("muster") || mode.ContainsKey("escort")
                    || mode.ContainsKey("squad"))
                {
                    throw Error($"{at}.modes[{index}]",
                        "condition-coordinated doctrines have no muster, "
                        + "escort or squad bridge; state this role's own verb.");
                }
                string[] verbs =
                [
                    .. new[]
                    {
                        "patrol", "intercept", "assault", "recover",
                        "fetch", "follow",
                    }.Where(mode.ContainsKey),
                ];
                if (verbs.Length != 1)
                {
                    throw Error($"{at}.modes[{index}]",
                        "each mode must carry exactly one of patrol, "
                        + "intercept, assault, recover, fetch or follow.");
                }
                ValidateModeCoordination(
                    role.Id,
                    index,
                    mode,
                    verbs[0],
                    index == modes.Count - 1,
                    cohorts,
                    focus,
                    cohortConsumers,
                    focusConsumers,
                    $"{at}.modes[{index}]");
            }
            JsonObject floor = modes[^1]!.AsObject();
            if (!floor.ContainsKey("patrol")
                || floor.ContainsKey("while") || floor.ContainsKey("until"))
            {
                throw Error(at,
                    "the last mode must be an unconditioned patrol floor.");
            }
            floorTargets.Add(role.Id, DoctrineDestination(
                floor["patrol"]!, $"{at}.modes[{modes.Count - 1}].patrol"));

            var runtimeDoctrine = doctrine.DeepClone().AsObject();
            runtimeDoctrine["role"] = role.Id;
            JsonArray runtimeModes = runtimeDoctrine["modes"]!.AsArray();
            // The phase order is the floor in executor IR. Leaving the
            // authored patrol as a doctrine task would make an always-active
            // task shadow that phase order and duplicate one rung at two
            // priorities.
            runtimeModes[^1] = new JsonObject { ["squad"] = true };
            if (runtimeDoctrine["repair"] is JsonObject repair)
            {
                supportPolicies.Add(RepairPolicy(role.Id, repair, at));
                runtimeDoctrine.Remove("repair");
            }
            oldDoctrines[role.Id] = runtimeDoctrine;
        }
        ValidateCoordinationConsumers(
            cohorts, focus, cohortConsumers, focusConsumers, path);
        foreach (string unknownDoctrine in roleDoctrineSource
                     .Select(value => value.Key)
                     .Where(id => !roleIds.Contains(id)))
        {
            throw Error($"{path}.doctrines.roles",
                $"doctrine '{unknownDoctrine}' names no role.");
        }

        Dictionary<string, string> roleGroups = roles.ToDictionary(
            role => role.Id,
            role => $"{role.Id}-group",
            StringComparer.Ordinal);
        JsonArray groups = BuildRuntimeGroups(roles, roleGroups);
        JsonObject predicates = BuildConditions(
            RequiredObject(sourceRoot, "conditions", path),
            roleGroups,
            path);
        JsonArray custodyPolicies = BuildCustodyPolicies(
            RequiredArray(sourceRoot, "custodyPolicies", path),
            roleDoctrineSource,
            roleGroups,
            path);
        JsonObject profiles = BuildProfiles(groups);
        var maneuvers = new JsonObject();
        var standingOrders = new JsonObject();
        var engagements = new JsonArray();
        BuildDirectFloors(
            roles,
            roleDoctrineSource,
            floorTargets,
            roleGroups,
            supportPolicies,
            profiles,
            standingOrders,
            engagements,
            focus,
            path);

        var runtimeMemory = RequiredObject(sourceRoot, "memory", path)
            .DeepClone().AsObject();
        ConsolidatedKeys(runtimeMemory, $"{path}.memory",
            [
                "enemyUnavailableTicks", "lastSeenEnemyTicks",
                "securedCoreTicks", "objectiveProgressTicks",
            ]);
        runtimeMemory["formationStableTicks"] = 3;

        var output = new JsonObject
        {
            ["schema"] = sourceRoot["schema"]!.DeepClone(),
            ["playbookId"] = sourceRoot["playbookId"]!.DeepClone(),
            ["auditStatus"] = sourceRoot["auditStatus"]!.DeepClone(),
            ["composition"] = sourceRoot["composition"]!.DeepClone(),
            ["layout"] = sourceRoot["layout"]!.DeepClone(),
            ["perspective"] = "team-relative",
            ["memory"] = runtimeMemory,
            // Compiler-only markers consumed and removed by the doctrine
            // desugarer. They never enter normalized playbook data.
            ["compactDoctrinePriorities"] = true,
            ["doctrineCohorts"] = cohortSource.DeepClone(),
            ["doctrineFocus"] = focusSource.DeepClone(),
            ["doctrineFloorTargets"] = new JsonObject(floorTargets.Select(
                pair => new KeyValuePair<string, JsonNode?>(
                    pair.Key,
                    new JsonObject
                    {
                        ["kind"] = pair.Value.Kind,
                        ["target"] = pair.Value.Target,
                    }))),
            ["roles"] = new JsonArray(roles
                .Select(role => (JsonNode)role.Runtime.DeepClone()).ToArray()),
            ["groups"] = groups,
            ["formations"] = new JsonArray(),
            ["engagements"] = engagements,
            ["supportPolicies"] = supportPolicies,
            ["custodyPolicies"] = custodyPolicies,
            ["authoring"] = new JsonObject
            {
                ["kind"] = "maneuver-catalog",
                ["parameters"] = new JsonObject(),
                ["fallbackPolicies"] = StandardFallbacks(),
                ["assignmentProfiles"] = profiles,
                ["standingOrders"] = standingOrders,
                ["maneuvers"] = maneuvers,
                ["predicates"] = predicates,
            },
            ["coordination"] = new JsonObject
            {
                ["initialPhase"] = "steady",
                ["phases"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["phaseId"] = "steady",
                        ["minimumTicks"] = 0,
                        ["standingOrderIds"] = new JsonArray(
                            standingOrders.Select(order => (JsonNode)order.Key)
                                .ToArray()),
                        ["maneuverId"] = "",
                        ["transitions"] = new JsonArray(),
                    },
                },
                ["tasks"] = new JsonArray(),
            },
            ["doctrines"] = oldDoctrines,
        };
        return JsonDocument.Parse(output.ToJsonString());
    }

    private readonly record struct DoctrineTarget(string Kind, string Target);

    private static DoctrineTarget DoctrineDestination(
        JsonNode value,
        string path)
    {
        if (value is JsonValue scalar
            && scalar.TryGetValue<string>(out string? route)
            && !string.IsNullOrWhiteSpace(route))
        {
            IdentifierValue(route, path, "route");
            return new DoctrineTarget("route", route);
        }
        if (value is not JsonObject target)
            throw Error(path, "destination must be a route id or {route|zone}.");
        ConsolidatedKeys(target, path, [], ["route", "zone"]);
        if ((target["route"] is null) == (target["zone"] is null))
            throw Error(path, "destination needs exactly one route or zone.");
        string kind = target["route"] is null ? "zone" : "route";
        string id = (target["route"] ?? target["zone"])!.GetValue<string>();
        IdentifierValue(id, path, kind);
        return new DoctrineTarget(kind, id);
    }

    private static JsonObject RequiredObject(
        JsonObject owner,
        string name,
        string path) => owner[name] as JsonObject
        ?? throw Error(path, $"missing required object '{name}'.");

    private static JsonArray RequiredArray(
        JsonObject owner,
        string name,
        string path) => owner[name] as JsonArray
        ?? throw Error(path, $"missing required array '{name}'.");

    private static void ConsolidatedKeys(
        JsonObject value,
        string path,
        IReadOnlyCollection<string> required,
        IReadOnlyCollection<string>? optional = null)
    {
        optional ??= [];
        foreach (string key in required)
        {
            if (!value.ContainsKey(key))
                throw Error(path, $"missing required field '{key}'.");
        }
        var allowed = required.Concat(optional).ToHashSet(StringComparer.Ordinal);
        foreach (string key in value.Select(item => item.Key))
        {
            if (!allowed.Contains(key))
                throw Error(path, $"unknown field '{key}'.");
        }
    }

    private static JsonObject RepairPolicy(
        string role,
        JsonObject repair,
        string path)
    {
        ConsolidatedKeys(repair, $"{path}.repair",
            [
                "prefer", "maximumProvidersPerTarget",
                "minimumProviderSeparation", "reserveHealthPercent",
                "survivalFallback",
            ]);
        string[] prefer = RequiredArray(repair, "prefer", $"{path}.repair")
            .Select(node => node?.GetValue<string>()
                ?? throw Error($"{path}.repair.prefer",
                    "repair target must be a string."))
            .Select(value => value switch
            {
                "weakest" => "lowest-health",
                "carrier" => "carrier",
                "medic" => "medic",
                "focus" => "focus-participant",
                "anchor" => "formation-anchor",
                "any" => "any",
                _ => throw Error($"{path}.repair.prefer",
                    $"unknown repair target '{value}'."),
            })
            .ToArray();
        return new JsonObject
        {
            ["supportId"] = $"{role}-repair",
            ["providers"] = new JsonArray(role),
            ["targetPriorities"] = new JsonArray(
                prefer.Select(value => (JsonNode)value).ToArray()),
            ["maximumProvidersPerTarget"] = RequiredIntNode(
                repair, "maximumProvidersPerTarget", path, 1, 8),
            ["minimumProviderSeparation"] = RequiredIntNode(
                repair, "minimumProviderSeparation", path, 0, 12),
            ["reserveHealthPercent"] = RequiredIntNode(
                repair, "reserveHealthPercent", path, 0, 100),
            ["survivalFallback"] = repair["survivalFallback"]!.GetValue<string>(),
        };
    }

    private static int RequiredIntNode(
        JsonObject owner,
        string name,
        string path,
        int minimum,
        int maximum)
    {
        int value;
        try
        {
            value = owner[name]?.GetValue<int>()
                ?? throw Error(path, $"missing integer '{name}'.");
        }
        catch (InvalidOperationException)
        {
            throw Error(path, $"'{name}' must be an integer.");
        }
        if (value < minimum || value > maximum)
            throw Error(path, $"'{name}' must be in [{minimum}, {maximum}].");
        return value;
    }
}
