using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static void BuildOperations(
        JsonObject operationSource,
        string[] operationOrder,
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlyDictionary<string, string> roleGroups,
        IReadOnlyDictionary<string, List<string>> tagRoles,
        IReadOnlyDictionary<string, string> formationIds,
        JsonObject profiles,
        JsonObject maneuvers,
        JsonArray engagements,
        JsonArray tasks,
        IReadOnlyDictionary<string, JsonArray> baselineOrderIds,
        ConsolidatedTempo tempo,
        JsonArray supportPolicies,
        string path)
    {
        HashSet<string> roleIds = roles.Select(role => role.Id)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ResolvedOperationFight> resolvedFights =
            operationOrder.ToDictionary(
                operationId => operationId,
                operationId => ResolveOperationFight(
                    operationSource,
                    operationId,
                    $"{path}.doctrines.operations.{operationId}"),
                StringComparer.Ordinal);
        foreach (IGrouping<string, string> sharedFight in operationOrder
                     .GroupBy(
                         operationId => resolvedFights[operationId].OwnerId,
                         StringComparer.Ordinal))
        {
            string ownerId = sharedFight.Key;
            string[] participants = roles
                .Select(role => role.Id)
                .Where(roleId => sharedFight.Any(operationId =>
                    OperationParticipants(
                            operationSource[operationId]!.AsObject(),
                            roles,
                            roleIds,
                            tagRoles,
                            $"{path}.doctrines.operations.{operationId}")
                        .Contains(roleId, StringComparer.Ordinal)))
                .ToArray();
            engagements.Add(OperationEngagement(
                $"{ownerId}-fight",
                participants,
                resolvedFights[ownerId].Fight,
                $"{path}.doctrines.operations.{ownerId}"));
        }

        for (int operationRank = 0;
             operationRank < operationOrder.Length;
             operationRank++)
        {
            string operationId = operationOrder[operationRank];
            string at = $"{path}.doctrines.operations.{operationId}";
            JsonObject operation = operationSource[operationId]!.AsObject();
            JsonArray commonStaff = operation["staff"]!.AsArray();
            JsonArray variants = operation["variants"]!.AsArray();
            string custody = operation["custody"]!.GetValue<string>();
            string shape = operation["formation"]!.GetValue<string>();
            string pace = operation["pace"]?.GetValue<string>() ?? "free";
            string orientation = operation["orientation"]?.GetValue<string>()
                ?? "enemy-reactor";
            string formationId = formationIds[
                FormationKey(shape, pace, orientation)];
            ResolvedOperationFight resolvedFight = resolvedFights[operationId];
            JsonObject commonFight = resolvedFight.Fight;
            string engagementId = $"{resolvedFight.OwnerId}-fight";
            var variantIds = new HashSet<string>(StringComparer.Ordinal);
            var tempoVariants = new HashSet<string>(StringComparer.Ordinal);

            for (int variantIndex = 0;
                 variantIndex < variants.Count;
                 variantIndex++)
            {
                JsonObject variant = variants[variantIndex]!.AsObject();
                string variantAt = $"{at}.variants[{variantIndex}]";
                ConsolidatedKeys(variant, variantAt,
                    ["variantId"],
                    [
                        "while", "until", "patrol", "assault", "staff",
                        "hold", "arrivalRadius", "stuckTicks", "reach",
                        "completion", "during",
                    ]);
                string variantId = variant["variantId"]!.GetValue<string>();
                IdentifierValue(variantId, variantAt, "variant");
                if (!variantIds.Add(variantId))
                    throw Error(variantAt, $"duplicate variant '{variantId}'.");
                bool hasPatrol = variant["patrol"] is not null;
                bool hasAssault = variant["assault"] is not null;
                if (hasPatrol == hasAssault)
                {
                    throw Error(variantAt,
                        "variant must declare exactly one patrol or assault.");
                }
                bool conditioned = variant["while"] is not null;
                string? during = variant["during"]?.GetValue<string>();
                if (conditioned && during is not null)
                    throw Error(variantAt, "variant cannot use both while and during.");
                if (during is not null
                    && !tempo.ModeIds.Contains(during, StringComparer.Ordinal))
                {
                    throw Error(variantAt, $"unknown tempo mode '{during}'.");
                }
                if (during is not null && !tempoVariants.Add(during))
                    throw Error(variantAt,
                        $"tempo mode '{during}' has more than one variant.");
                if (!conditioned && during is null
                    && variantIndex != variants.Count - 1)
                {
                    throw Error(variantAt,
                        "an unconditioned variant is the operation floor and "
                        + "must be last.");
                }
                if (variant["until"] is not null && !conditioned)
                    throw Error(variantAt, "an unconditioned floor takes no until.");
                if (!conditioned
                    && (variant["hold"] is not null
                        || variant["completion"] is not null))
                {
                    throw Error(variantAt,
                        "a floor variant cannot declare task hold/completion; "
                        + "those fields require a conditioned operation.");
                }
                string orderId = variants.Count == 1
                    ? operationId
                    : $"{operationId}-{variantId}";
                JsonArray staff = variant["staff"] as JsonArray ?? commonStaff;
                JsonObject[] staffRows = StaffRows(staff, $"{variantAt}.staff")
                    .ToArray();
                string[] selectedRoles = staffRows
                    .SelectMany(row => ResolveSelectorRoles(
                        row, roleIds, tagRoles, $"{variantAt}.staff"))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                string[] selectedGroups = selectedRoles
                    .Select(role => roleGroups[role])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (selectedGroups.Length != 1)
                {
                    throw Error(variantAt,
                        "one deployment currently must staff one doctrine "
                        + "cohort; split cross-cohort movement into two "
                        + "operations rather than reviving group tracks.");
                }
                string groupId = selectedGroups[0];
                string profileId = $"{groupId}-profile";
                bool repair = operation["repair"]?.GetValue<bool>() == true;
                string traffic = operation["traffic"]?.GetValue<string>()
                    ?? "normal";
                if (repair || traffic != "normal")
                {
                    profileId = $"{operationId}-{groupId}-profile";
                    if (!profiles.ContainsKey(profileId))
                    {
                        JsonObject profile = profiles[$"{groupId}-profile"]!
                            .DeepClone().AsObject();
                        profile["priority"] = traffic switch
                        {
                            "priority" => 5,
                            "normal" => 10,
                            "yield" => 20,
                            _ => throw Error(at,
                                $"unknown traffic policy '{traffic}'."),
                        };
                        if (repair)
                        {
                            profile["supportId"] =
                                OperationRepairSupportId(
                                    supportPolicies, at);
                        }
                        profiles[profileId] = profile;
                    }
                }
                JsonObject movement = OperationMovement(
                    variant, operation, variantAt);
                string fallback = hasAssault
                    ? "continue-with-alternate"
                    : "continue-and-hold-invalid";
                var assignment = new JsonObject
                {
                    ["arrivalRadius"] = NodeInt(
                        variant, operation, "arrivalRadius", hasAssault ? 2 : 2),
                    ["completion"] = "continuous",
                    ["chaseLeash"] = NodeInt(
                        variant, operation, "reach",
                        FightInt(commonFight, "chase", "leash", 0)),
                    ["engagementId"] = engagementId,
                    ["custodyId"] = custody,
                    ["fallbackId"] = fallback,
                    ["assignmentProfileId"] = profileId,
                };
                if (operation["conceal"]?.GetValue<bool>() == true)
                    assignment["stance"] = "ambush";
                var track = new JsonObject
                {
                    ["formationId"] = formationId,
                    ["movement"] = movement,
                    ["assignments"] = new JsonObject
                    {
                        [orderId] = assignment,
                    },
                };
                if (during is not null)
                {
                    maneuvers[$"__floor|{during}|{orderId}"] = new JsonObject
                    {
                        ["tracks"] = new JsonObject { ["main"] = track },
                    };
                    baselineOrderIds[during].Add(orderId);
                    continue;
                }
                if (!conditioned)
                {
                    foreach (string modeId in tempo.ModeIds)
                    {
                        maneuvers[$"__floor|{modeId}|{orderId}"] = new JsonObject
                        {
                            ["tracks"] = new JsonObject
                            {
                                ["main"] = track.DeepClone(),
                            },
                        };
                        baselineOrderIds[modeId].Add(orderId);
                    }
                    continue;
                }
                maneuvers[$"{orderId}-set"] = new JsonObject
                {
                    ["tracks"] = new JsonObject { ["main"] = track },
                };

                JsonObject hold = MergeHold(operation["hold"], variant["hold"],
                    variantAt);
                var task = new JsonObject
                {
                    ["taskId"] = orderId,
                    // Variants are alternative triggers at ONE authored
                    // priority rung. Equal runtime priority is important:
                    // an already-running north race cannot be preempted by a
                    // centre sibling merely because centre is written first.
                    ["priority"] = OperationPriority(operationRank),
                    ["activation"] = "while-true",
                    ["preemption"] = "higher-priority",
                    ["participantLoss"] = "replace",
                    ["triggerStableTicks"] = HoldInt(
                        hold, "stableTicks", 1, 1, 120, variantAt),
                    ["minimumTicks"] = HoldInt(
                        hold, "minimumTicks", 8, 0, 1200, variantAt),
                    ["timeoutTicks"] = HoldInt(
                        hold, "timeoutTicks", 600, 1, 2400, variantAt),
                    ["cooldownTicks"] = HoldInt(
                        hold, "cooldownTicks", 2, 0, 1200, variantAt),
                    ["minimumPrimaryBodies"] = 1,
                    ["eligiblePhases"] = new JsonArray(
                        tempo.ModeIds.Select(id => (JsonNode)id).ToArray()),
                    ["assignments"] = TaskAssignments(
                        orderId, staffRows, roles, roleIds, tagRoles, variantAt),
                    ["when"] = variant["while"]!.GetValue<string>(),
                    ["complete"] = variant["until"]?.GetValue<string>()
                        ?? "no-live-friendlies",
                    ["fail"] = "no-live-friendlies",
                    ["reintegration"] = new JsonObject
                    {
                        ["mode"] = "primary-order",
                        ["orderIds"] = new JsonArray(),
                        ["timeoutTicks"] = 0,
                    },
                };
                if (variant["completion"] is JsonValue completion)
                {
                    string kind = completion.GetValue<string>();
                    if (kind != "carrier-loss")
                        throw Error(variantAt,
                            $"unknown completion mode '{kind}'.");
                    task["completionArmMode"] = "assigned-carrier";
                    task["completionReleaseMode"] = "armed-carrier-loss";
                }
                tasks.Add(task);
            }
        }
    }

    private sealed record ResolvedOperationFight(
        string OwnerId,
        JsonObject Fight);

    private static ResolvedOperationFight ResolveOperationFight(
        JsonObject operations,
        string operationId,
        string path,
        HashSet<string>? seen = null)
    {
        seen ??= new HashSet<string>(StringComparer.Ordinal);
        if (!seen.Add(operationId))
            throw Error(path, "operation fight references form a cycle.");
        JsonNode? value = operations[operationId]?["fight"];
        if (value is JsonObject fight)
            return new ResolvedOperationFight(operationId, fight);
        if (value is not JsonValue referenceValue)
            throw Error(path, "fight must be an object or operation id.");
        string reference = referenceValue.GetValue<string>();
        if (operations[reference] is null)
            throw Error(path, $"fight references unknown operation '{reference}'.");
        return ResolveOperationFight(operations, reference, path, seen);
    }

    private static string[] OperationParticipants(
        JsonObject operation,
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlySet<string> roleIds,
        IReadOnlyDictionary<string, List<string>> tagRoles,
        string path)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        void Add(JsonArray staff, string at)
        {
            foreach (JsonObject row in StaffRows(staff, at))
                selected.UnionWith(ResolveSelectorRoles(
                    row, roleIds, tagRoles, at));
        }
        Add(operation["staff"]!.AsArray(), $"{path}.staff");
        foreach (JsonObject variant in operation["variants"]!.AsArray()
                     .Select(node => node!.AsObject()))
        {
            if (variant["staff"] is JsonArray staff)
                Add(staff, $"{path}.variants.staff");
        }
        return roles.Select(role => role.Id).Where(selected.Contains).ToArray();
    }

    private static JsonObject OperationMovement(
        JsonObject variant,
        JsonObject operation,
        string path)
    {
        string verb = variant["assault"] is not null ? "assault" : "patrol";
        JsonObject target = variant[verb]?.AsObject()
            ?? throw Error(path, $"'{verb}' must be a route/zone object.");
        ConsolidatedKeys(target, $"{path}.{verb}", [], ["route", "zone"]);
        if ((target["route"] is null) == (target["zone"] is null))
            throw Error(path, $"'{verb}' needs exactly one route or zone.");
        string kind = target["route"] is null ? "zone" : "route";
        string targetId = (target["route"] ?? target["zone"])!.GetValue<string>();
        return new JsonObject
        {
            ["kind"] = kind,
            ["target"] = targetId,
            ["stuckTicks"] = NodeInt(
                variant, operation, "stuckTicks", verb == "assault" ? 10 : 8),
            ["pace"] = operation["pace"]?.GetValue<string>() ?? "free",
        };
    }

    private static int NodeInt(
        JsonObject stronger,
        JsonObject weaker,
        string name,
        int fallback) => stronger[name]?.GetValue<int>()
        ?? weaker[name]?.GetValue<int>()
        ?? fallback;

    private static int FightInt(
        JsonObject fight,
        string block,
        string name,
        int fallback) => fight[block]?[name]?.GetValue<int>() ?? fallback;

    private static JsonObject MergeHold(
        JsonNode? common,
        JsonNode? specific,
        string path)
    {
        var result = new JsonObject();
        foreach (JsonNode? candidate in new[] { common, specific })
        {
            if (candidate is null)
                continue;
            if (candidate is not JsonObject values)
                throw Error(path, "hold must be an object.");
            ConsolidatedKeys(values, $"{path}.hold", [],
                ["stableTicks", "minimumTicks", "timeoutTicks", "cooldownTicks"]);
            foreach ((string key, JsonNode? value) in values)
                result[key] = value?.DeepClone();
        }
        return result;
    }

    private static int HoldInt(
        JsonObject hold,
        string name,
        int fallback,
        int minimum,
        int maximum,
        string path)
    {
        if (hold[name] is null)
            return fallback;
        return RequiredIntNode(hold, name, $"{path}.hold", minimum, maximum);
    }

    // Hunter's accepted legacy tasks occupied 5, 6, 8, 30 while doctrine-local
    // work occupied some of the same integers. The standard six-cell parity
    // proof needs this compatibility bridge (the surge never competes there,
    // so only its relative order is observable). It is intentionally called
    // out in the Phase-3 gate report: this is not a general answer for
    // interleaving local rungs with shared operations, and bulk migration must
    // not grow more special cases here.
    private static int OperationPriority(int rank) => rank switch
    {
        0 => 5,
        1 => 6,
        _ => 6 + rank,
    };

    private static string OperationRepairSupportId(
        JsonArray supportPolicies,
        string path)
    {
        string[] matching = supportPolicies
            .Select(node => node!.AsObject())
            .Select(policy => policy["supportId"]!.GetValue<string>())
            .ToArray();
        return matching.Length switch
        {
            1 => matching[0],
            0 => throw Error(path,
                "operation enables repair but no role declares a repair policy."),
            _ => throw Error(path,
                "operation enables repair with multiple repair policies; "
                + "split the operation so its support intent is unambiguous."),
        };
    }

    private static JsonArray TaskAssignments(
        string orderId,
        IReadOnlyList<JsonObject> staff,
        IReadOnlyCollection<ConsolidatedRole> roles,
        IReadOnlySet<string> roleIds,
        IReadOnlyDictionary<string, List<string>> tagRoles,
        string path)
    {
        Dictionary<string, ConsolidatedRole> roleById = roles.ToDictionary(
            role => role.Id, StringComparer.Ordinal);
        var result = new JsonArray();
        var claimedRoles = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < staff.Count; index++)
        {
            JsonObject row = staff[index];
            string[] selected = ResolveSelectorRoles(
                row, roleIds, tagRoles, $"{path}.staff[{index}]");
            string[] overlap = selected.Where(role => !claimedRoles.Add(role))
                .ToArray();
            if (overlap.Length > 0)
            {
                throw Error($"{path}.staff[{index}]",
                    "staff selectors overlap on role(s): "
                    + string.Join(", ", overlap));
            }
            string[] classes = selected
                .SelectMany(role => roleById[role].Runtime["candidateClasses"]!
                    .AsArray().Select(node => node!.GetValue<string>()))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var distance = new JsonObject
            {
                ["kind"] = "none",
                ["target"] = "",
            };
            if (row["near"] is JsonValue near)
            {
                distance["kind"] = "anchor";
                distance["target"] = near.GetValue<string>();
            }
            else if (row["select"]?.GetValue<string>() == "most-experienced")
            {
                distance["kind"] = "veterancy-rank";
                distance["maximum"] = 7;
            }
            result.Add(new JsonObject
            {
                ["assignmentId"] = $"{orderId}-staff-{index + 1}",
                ["orderId"] = orderId,
                ["roles"] = new JsonArray(
                    selected.Select(role => (JsonNode)role).ToArray()),
                ["classes"] = new JsonArray(
                    classes.Select(value => (JsonNode)value).ToArray()),
                ["minimum"] = row["minimum"]!.GetValue<int>(),
                ["preferred"] = row["preferred"]!.GetValue<int>(),
                ["maximum"] = row["maximum"]!.GetValue<int>(),
                ["carrier"] = row["carrier"]!.GetValue<string>(),
                ["distance"] = distance,
            });
        }
        return result;
    }

    private static JsonObject OperationEngagement(
        string id,
        string[] participants,
        JsonObject fight,
        string path)
    {
        ConsolidatedKeys(fight, $"{path}.fight",
            ["targets"],
            ["chase", "defense", "lockTicks", "signature", "release",
             "collect", "heal", "aim"]);
        JsonObject targets = RequiredObject(fight, "targets", $"{path}.fight");
        ConsolidatedKeys(targets, $"{path}.fight.targets",
            ["prefer", "maximumAttackers"]);
        string[] preferences = RequiredArray(
                targets, "prefer", $"{path}.fight.targets")
            .Select(node => node!.GetValue<string>())
            .Select(value => value switch
            {
                "carrier" => "enemy-carrier",
                "weakest" => "lowest-health",
                "anchor" => "closest-to-anchor",
                "closest" => "closest-to-reactor",
                "strongest-threat" => "highest-threat",
                "freshest" => "fresh-respawn",
                _ => throw Error($"{path}.fight.targets.prefer",
                    $"unknown target preference '{value}'."),
            }).ToArray();
        JsonObject release = fight["release"] as JsonObject ?? new JsonObject();
        JsonObject defense = fight["defense"] as JsonObject ?? new JsonObject();
        var result = new JsonObject
        {
            ["engagementId"] = id,
            ["participants"] = new JsonArray(
                participants.Select(role => (JsonNode)role).ToArray()),
            ["targetPriorities"] = new JsonArray(
                preferences.Select(value => (JsonNode)value).ToArray()),
            ["tieBreakers"] = new JsonArray(
                "distance", "health", "unit-id", "life-id"),
            ["coordinationScope"] = "shared-policy",
            ["lockTicks"] = fight["lockTicks"]?.GetValue<int>() ?? 4,
            ["lockPreemption"] = "urgent-carrier",
            ["maximumAttackersPerTarget"] =
                targets["maximumAttackers"]!.GetValue<int>(),
            ["overkillDamage"] = 0,
            ["chaseLeash"] = FightInt(fight, "chase", "leash", 0),
            ["aimPreparation"] = fight["aim"]?.GetValue<string>() switch
            {
                null or "turn" => "rotate-to-engage",
                "facing" => "current-cone-only",
                string value => throw Error(
                    $"{path}.fight.aim", $"unknown aim policy '{value}'."),
            },
            ["signatureCoordination"] =
                fight["signature"]?.GetValue<string>() ?? "damage-first",
            ["release"] = new JsonObject
            {
                ["hiddenTicks"] = release["hiddenTicks"]?.GetValue<int>() ?? 2,
                ["unreachableTicks"] =
                    release["unreachableTicks"]?.GetValue<int>() ?? 3,
                ["outsideLeash"] = true,
                ["destroyed"] = true,
            },
            ["selfDefense"] = new JsonObject
            {
                ["enabled"] = (defense["radius"]?.GetValue<int>() ?? 2) > 0,
                ["threatDistance"] = defense["radius"]?.GetValue<int>() ?? 2,
                ["returnToFormation"] =
                    defense["return"]?.GetValue<bool>() ?? true,
            },
        };
        if (fight["collect"] is JsonNode collect)
            result["collect"] = collect.DeepClone();
        if (fight["heal"] is JsonNode heal)
            result["heal"] = heal.DeepClone();
        return result;
    }
}
