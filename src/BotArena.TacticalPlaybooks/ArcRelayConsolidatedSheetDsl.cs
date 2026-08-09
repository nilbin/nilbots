using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private sealed record ConsolidatedRole(
        string Id,
        JsonObject Runtime,
        string[] Tags,
        string FormationPosture);

    /// <summary>
    /// Expands the Phase-3 source language into the exhaustive executor IR
    /// source consumed by the existing compiler passes. This is deliberately
    /// a source-to-source pass: groups, formations, engagements, support
    /// policies, tasks and phase orders remain runtime implementation detail,
    /// while the tracked sheet has one vocabulary -- role doctrines and their
    /// ordered calls.
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
        JsonObject doctrinePlane = RequiredObject(
            sourceRoot, "doctrines", path);
        ConsolidatedKeys(doctrinePlane, $"{path}.doctrines",
            ["roles", "operations", "tempo"]);
        JsonObject roleDoctrineSource = RequiredObject(
            doctrinePlane, "roles", $"{path}.doctrines");
        JsonObject operationSource = RequiredObject(
            doctrinePlane, "operations", $"{path}.doctrines");
        ConsolidatedTempo tempo = ParseTempo(
            RequiredObject(doctrinePlane, "tempo", $"{path}.doctrines"),
            $"{path}.doctrines.tempo");
        if (operationSource.Count == 0)
            throw Error($"{path}.doctrines.operations",
                "at least one coordinated operation is required.");

        JsonElement sourceRoles = source.GetProperty("roles");
        JsonElement[] roleElements = BoundedArray(
            sourceRoles, $"{path}.roles", 1, 32);
        var roles = new List<ConsolidatedRole>();
        var roleIds = new HashSet<string>(StringComparer.Ordinal);
        var tagRoles = new Dictionary<string, List<string>>(
            StringComparer.Ordinal);
        foreach (JsonElement role in roleElements)
        {
            string at = $"{path}.roles";
            Object(role, at,
                [
                    "roleId", "candidateClasses", "minimum", "preferred",
                    "maximum", "carrierPreference", "deathPolicy",
                    "respawnPolicy", "overflowRoleId", "tags",
                    "formationPosture",
                ],
                ["build"]);
            string roleId = Identifier(role, "roleId", at);
            if (!roleIds.Add(roleId))
                throw Error(at, $"duplicate role '{roleId}'.");
            string[] tags = StringArray(
                role.GetProperty("tags"), $"{at}.{roleId}.tags", 1, 8);
            if (tags.Distinct(StringComparer.Ordinal).Count() != tags.Length)
                throw Error(at, $"role '{roleId}' repeats a tag.");
            foreach (string tag in tags)
            {
                IdentifierValue(tag, at, "tag");
                if (roleIds.Contains(tag))
                    throw Error(at,
                        $"tag '{tag}' collides with a role id; roles and tags "
                        + "share one selector namespace.");
                if (!tagRoles.TryGetValue(tag, out List<string>? members))
                {
                    members = [];
                    tagRoles.Add(tag, members);
                }
                members.Add(roleId);
            }
            OneOf(role, "formationPosture", at,
                "scout", "vanguard", "core", "support", "rear", "wide",
                "tail");
            JsonObject runtime = JsonNode.Parse(role.GetRawText())!.AsObject();
            runtime.Remove("tags");
            runtime.Remove("formationPosture");
            roles.Add(new ConsolidatedRole(
                roleId,
                runtime,
                tags,
                role.GetProperty("formationPosture").GetString()!));
        }
        foreach (string tag in tagRoles.Keys)
        {
            if (roleIds.Contains(tag))
                throw Error($"{path}.roles",
                    $"tag '{tag}' collides with a role id; roles and tags "
                    + "share one selector namespace.");
        }

        string[] operationIds = operationSource.Select(value => value.Key)
            .ToArray();
        var operationIdSet = operationIds.ToHashSet(StringComparer.Ordinal);
        foreach (string operationId in operationIds)
            IdentifierValue(operationId, $"{path}.doctrines.operations", "operation");

        var ladders = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var roleTempoModes = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        var oldDoctrines = new JsonObject();
        var supportPolicies = new JsonArray();
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
                ["fight", "conceal", "scavenge", "repair", "tempo"]);
            var oldDoctrine = doctrine.DeepClone().AsObject();
            oldDoctrine["role"] = role.Id;
            JsonObject tempoModes = doctrine["tempo"] as JsonObject
                ?? new JsonObject();
            foreach ((string modeId, JsonNode? mode) in tempoModes)
            {
                if (!tempo.ModeIds.Contains(modeId, StringComparer.Ordinal))
                    throw Error($"{at}.tempo", $"unknown tempo mode '{modeId}'.");
                if (mode is not JsonObject)
                    throw Error($"{at}.tempo.{modeId}",
                        "tempo override must be an object.");
            }
            roleTempoModes.Add(role.Id, tempoModes);
            oldDoctrine.Remove("tempo");
            JsonArray modes = RequiredArray(doctrine, "modes", at);
            if (modes.Count is < 1 or > 8)
                throw Error($"{at}.modes", "expected between 1 and 8 modes.");
            var oldModes = new JsonArray();
            string[] operationLadder = [];
            for (int index = 0; index < modes.Count; index++)
            {
                if (modes[index] is not JsonObject mode)
                    throw Error($"{at}.modes[{index}]", "mode must be an object.");
                if (mode["muster"] is JsonArray operationList)
                {
                    ConsolidatedKeys(mode, $"{at}.modes[{index}]", ["muster"]);
                    if (index != modes.Count - 1)
                    {
                        throw Error($"{at}.modes[{index}]",
                            "the coordinated-operation ladder must be the final "
                            + "mode; stronger body-local modes sit above it.");
                    }
                    operationLadder = operationList
                        .Select(node => node?.GetValue<string>()
                            ?? throw Error($"{at}.modes[{index}].muster",
                                "operation id must be a string."))
                        .ToArray();
                    if (operationLadder.Length == 0
                        || operationLadder.Distinct(StringComparer.Ordinal).Count()
                            != operationLadder.Length)
                    {
                        throw Error($"{at}.modes[{index}].muster",
                            "operation ladder must be non-empty and contain "
                            + "each operation once.");
                    }
                    foreach (string operationId in operationLadder)
                    {
                        if (!operationIdSet.Contains(operationId))
                        {
                            throw Error($"{at}.modes[{index}].muster",
                                $"unknown operation '{operationId}'.");
                        }
                    }
                    // The old executor's exhaustive plane still needs its
                    // bridge floor. It is compiler IR only: every operation
                    // it stands for is named in the authored ladder above.
                    oldModes.Add(new JsonObject { ["squad"] = true });
                    continue;
                }
                oldModes.Add(mode.DeepClone());
            }
            ladders.Add(role.Id, operationLadder);
            if (operationLadder.Length > 0 && tempoModes.Count > 0)
            {
                throw Error($"{at}.tempo",
                    "a role assigned through coordinated operations cannot "
                    + "also declare a body-local tempo override.");
            }
            oldDoctrine["modes"] = oldModes;
            if (oldDoctrine["repair"] is JsonObject repair)
            {
                supportPolicies.Add(RepairPolicy(role.Id, repair, at));
                oldDoctrine.Remove("repair");
            }
            oldDoctrines[role.Id] = oldDoctrine;
        }
        foreach (string unknownDoctrine in roleDoctrineSource.Select(value => value.Key)
                     .Where(id => !roleIds.Contains(id)))
        {
            throw Error($"{path}.doctrines.roles",
                $"doctrine '{unknownDoctrine}' names no role.");
        }

        var operationCandidates = new Dictionary<string, string[]>(
            StringComparer.Ordinal);
        foreach ((string operationId, JsonNode? operationNode) in operationSource)
        {
            JsonObject operation = operationNode?.AsObject()
                ?? throw Error($"{path}.doctrines.operations.{operationId}",
                    "operation must be an object.");
            ConsolidatedKeys(operation,
                $"{path}.doctrines.operations.{operationId}",
                ["custody", "formation", "fight", "staff", "variants"],
                [
                    "pace", "orientation", "conceal", "arrivalRadius",
                    "stuckTicks", "reach", "hold", "repair", "traffic",
                ]);
            OneOfNode(operation, "formation",
                $"{path}.doctrines.operations.{operationId}", "file", "screen");
            if (operation["pace"] is not null)
            {
                OneOfNode(operation, "pace",
                    $"{path}.doctrines.operations.{operationId}",
                    "free", "slowest");
            }
            if (operation["orientation"] is not null)
            {
                OneOfNode(operation, "orientation",
                    $"{path}.doctrines.operations.{operationId}",
                    "enemy-reactor", "own-reactor");
            }
            if (operation["traffic"] is not null)
            {
                OneOfNode(operation, "traffic",
                    $"{path}.doctrines.operations.{operationId}",
                    "priority", "normal", "yield");
            }
            OptionalBoolNode(operation, "conceal",
                $"{path}.doctrines.operations.{operationId}");
            OptionalBoolNode(operation, "repair",
                $"{path}.doctrines.operations.{operationId}");
            ValidateOperationNumbers(operation,
                $"{path}.doctrines.operations.{operationId}");
            JsonArray variants = RequiredArray(operation, "variants",
                $"{path}.doctrines.operations.{operationId}");
            if (variants.Count is < 1 or > 12)
                throw Error($"{path}.doctrines.operations.{operationId}.variants",
                    "expected between 1 and 12 variants.");
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonObject row in StaffRows(
                         RequiredArray(operation, "staff",
                             $"{path}.doctrines.operations.{operationId}"),
                         $"{path}.doctrines.operations.{operationId}.staff"))
            {
                candidates.UnionWith(ResolveSelectorRoles(
                    row, roleIds, tagRoles,
                    $"{path}.doctrines.operations.{operationId}.staff"));
            }
            foreach (JsonNode? variantNode in variants)
            {
                JsonObject variant = variantNode?.AsObject()
                    ?? throw Error($"{path}.doctrines.operations.{operationId}",
                    "variant must be an object.");
                ValidateOperationNumbers(variant,
                    $"{path}.doctrines.operations.{operationId}.variants");
                if (variant["staff"] is not JsonArray variantStaff)
                    continue;
                foreach (JsonObject row in StaffRows(variantStaff,
                             $"{path}.doctrines.operations.{operationId}.variants"))
                {
                    candidates.UnionWith(ResolveSelectorRoles(
                        row, roleIds, tagRoles,
                        $"{path}.doctrines.operations.{operationId}.variants"));
                }
            }
            operationCandidates.Add(operationId,
                roles.Select(role => role.Id).Where(candidates.Contains).ToArray());
        }

        foreach ((string operationId, string[] candidates) in operationCandidates)
        {
            foreach (string candidate in candidates)
            {
                if (!ladders[candidate].Contains(operationId,
                        StringComparer.Ordinal))
                {
                    throw Error($"{path}.doctrines.operations.{operationId}",
                        $"operation may select role '{candidate}', but that "
                        + "role's doctrine does not consent to it in its "
                        + "muster ladder.");
                }
            }
            foreach ((string role, string[] ladder) in ladders)
            {
                if (ladder.Contains(operationId, StringComparer.Ordinal)
                    && !candidates.Contains(role, StringComparer.Ordinal))
                {
                    throw Error($"{path}.doctrines.roles.{role}",
                        $"muster names operation '{operationId}', but no staff "
                        + "selector in that operation can select role '{role}'.");
                }
            }
        }

        string[] operationOrder = OrderOperations(
            operationIds, ladders, path);
        Dictionary<string, string> roleGroups = DeriveGroups(
            roles, ladders, operationSource, operationCandidates, tempo, path);
        JsonArray groups = BuildRuntimeGroups(roles, roleGroups);
        Dictionary<string, string> selectorGroups = SelectorGroups(
            roles, tagRoles, roleGroups);

        JsonObject predicates = BuildConditions(
            RequiredObject(sourceRoot, "conditions", path),
            selectorGroups,
            path);
        AddTempoPredicates(predicates, tempo);
        JsonArray formations = BuildFormations(
            roles, operationSource, oldDoctrines, path);
        Dictionary<string, string> formationIds = formations
            .Select(node => node!.AsObject())
            .ToDictionary(
                formation => formation["sourceKey"]!.GetValue<string>(),
                formation => formation["formationId"]!.GetValue<string>(),
                StringComparer.Ordinal);
        foreach (JsonObject formation in formations.Select(node => node!.AsObject()))
            formation.Remove("sourceKey");

        JsonArray custodyPolicies = BuildCustodyPolicies(
            RequiredArray(sourceRoot, "custodyPolicies", path),
            selectorGroups,
            path);

        var engagements = new JsonArray();
        var profiles = BuildProfiles(groups, roles, roleGroups, ladders);
        var maneuvers = new JsonObject();
        var tasks = new JsonArray();
        Dictionary<string, JsonArray> baselineOrderIds = tempo.ModeIds
            .ToDictionary(
                modeId => modeId,
                _ => new JsonArray(),
                StringComparer.Ordinal);
        BuildOperations(
            operationSource,
            operationOrder,
            roles,
            roleGroups,
            tagRoles,
            formationIds,
            profiles,
            maneuvers,
            engagements,
            tasks,
            baselineOrderIds,
            tempo,
            supportPolicies,
            path);
        BuildDirectFloors(
            roles,
            ladders,
            oldDoctrines,
            roleGroups,
            formationIds,
            supportPolicies,
            profiles,
            maneuvers,
            baselineOrderIds,
            tempo,
            roleTempoModes,
            engagements,
            path);

        var runtimeMemory = RequiredObject(sourceRoot, "memory", path)
            .DeepClone().AsObject();
        ConsolidatedKeys(runtimeMemory, $"{path}.memory",
            [
                "enemyUnavailableTicks", "lastSeenEnemyTicks",
                "securedCoreTicks", "objectiveProgressTicks",
            ]);
        // Exhaustive IR compatibility only. No condition may read this in the
        // consolidated language; the authored formation-stability knob is gone.
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
            ["roles"] = new JsonArray(
                roles.Select(role => (JsonNode)role.Runtime.DeepClone()).ToArray()),
            ["groups"] = groups,
            ["formations"] = formations,
            ["engagements"] = engagements,
            ["supportPolicies"] = supportPolicies,
            ["custodyPolicies"] = custodyPolicies,
            ["authoring"] = new JsonObject
            {
                ["kind"] = "maneuver-catalog",
                ["parameters"] = new JsonObject(),
                ["fallbackPolicies"] = StandardFallbacks(),
                ["assignmentProfiles"] = profiles,
                ["standingOrders"] = new JsonObject(),
                ["maneuvers"] = maneuvers,
                ["predicates"] = predicates,
            },
            ["coordination"] = new JsonObject
            {
                ["initialPhase"] = tempo.Cycle[0].Id,
                ["phases"] = BuildTempoPhases(tempo),
                ["tasks"] = tasks,
            },
            ["doctrines"] = oldDoctrines,
        };
        foreach (string modeId in tempo.ModeIds)
        {
            maneuvers[$"tempo-{modeId}-set"] = new JsonObject
            {
                ["tracks"] = FloorTracks(
                    maneuvers,
                    baselineOrderIds[modeId],
                    modeId,
                    path),
            };
        }
        return JsonDocument.Parse(output.ToJsonString());
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

    private static void OneOfNode(
        JsonObject owner,
        string name,
        string path,
        params string[] values)
    {
        string actual = owner[name]?.GetValue<string>()
            ?? throw Error(path, $"missing required string '{name}'.");
        if (!values.Contains(actual, StringComparer.Ordinal))
        {
            throw Error(path,
                $"'{name}' must be one of {string.Join(", ", values)}.");
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

    private static void OptionalBoolNode(
        JsonObject owner,
        string name,
        string path)
    {
        if (owner[name] is null)
            return;
        if (owner[name] is not JsonValue value
            || !value.TryGetValue<bool>(out _))
        {
            throw Error(path, $"'{name}' must be true or false.");
        }
    }

    private static void ValidateOperationNumbers(JsonObject value, string path)
    {
        ValidateOptionalIntNode(value, "arrivalRadius", path, 0, 12);
        ValidateOptionalIntNode(value, "stuckTicks", path, 1, 1200);
        ValidateOptionalIntNode(value, "reach", path, 0, 64);
    }

    private static void ValidateOptionalIntNode(
        JsonObject owner,
        string name,
        string path,
        int minimum,
        int maximum)
    {
        if (owner[name] is null)
            return;
        _ = RequiredIntNode(owner, name, path, minimum, maximum);
    }
}
