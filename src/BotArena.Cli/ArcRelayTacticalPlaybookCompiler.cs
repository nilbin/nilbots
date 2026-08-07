using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Cli;

/// <summary>
/// Strict source compiler for the provisional Arc Relay tactical-playbook
/// format. Authoring JSON is validated and canonicalized before it crosses the
/// mind boundary; the runtime never guesses at misspelled or omitted intent.
/// </summary>
public static class ArcRelayTacticalPlaybookCompiler
{
    public const string PlaybookSchema = "arc-relay-tactical-playbook-v1";
    public const string LayoutSchema = "arc-relay-tactical-layout-v1";
    public const int EnvelopeMagic = 0x31505441; // ATP1, little-endian.
    private const int MaximumLinkedBytes = 64 * 1024;

    private static readonly HashSet<string> KnownClasses =
    [
        "kestrel", "palisade", "towline", "patchbay", "lantern", "mortar",
        "minesmith", "hush", "relay", "switchback", "longshot", "mason",
        "sunder", "repulsor", "veil", "nest",
    ];

    private static readonly HashSet<string> ConditionOperators =
    ["at-least", "at-most", "equals", "less-than", "greater-than"];

    private static readonly HashSet<string> ConditionFacts =
    [
        "always", "tick", "phase-state-ticks", "live-friendlies",
        "friendlies-in-zone-count",
        "group-live-count", "group-joining-count",
        "group-in-zone-count", "group-cohesion", "group-stuck-ticks",
        "known-enemies-unavailable", "visible-enemies-in-zone",
        "remembered-enemies-in-zone", "visible-enemy-carriers",
        "known-enemy-carriers",
        "friendly-carriers", "secured-cores", "visible-loose-cores",
        "visible-loose-cores-in-zone",
        // Ripening rulesets (value-inert elsewhere: every core reads 1).
        "visible-loose-core-value", "visible-loose-core-value-in-zone",
        "well-has-outstanding", "outstanding-well-count",
        "ticks-without-objective-progress",
        "reactor-integrity", "reactor-charge", "formation-established-ticks",
        "group-formation-broken", "movement-complete", "custody-state-ticks",
        "role-live-count", "group-max-level",
        // Threefold sockets (subject = the contract's absolute well id).
        "own-socket-filled", "enemy-socket-filled",
        "own-filled-sockets", "enemy-filled-sockets",
        "well-ticks-until-birth",
    ];

    private static readonly HashSet<string> GroupSubjectFacts =
    [
        "group-live-count", "group-joining-count", "group-in-zone-count",
        "group-cohesion",
        "group-stuck-ticks", "formation-established-ticks",
        "group-formation-broken", "group-max-level",
    ];

    private static readonly HashSet<string> OrderSubjectFacts =
    ["movement-complete"];

    private static readonly HashSet<string> ZoneFacts =
    [
        "friendlies-in-zone-count", "group-in-zone-count",
        "visible-enemies-in-zone", "remembered-enemies-in-zone",
        "visible-loose-cores-in-zone", "visible-loose-core-value-in-zone",
    ];

    private static readonly HashSet<string> FreshnessFacts =
    [
        "remembered-enemies-in-zone", "secured-cores",
    ];

    public static TacticalPlaybookCompilation Compile(string playbookPath)
    {
        string fullPlaybookPath = Path.GetFullPath(playbookPath);
        byte[] playbookSource = File.ReadAllBytes(fullPlaybookPath);
        using JsonDocument sourceDocument = Parse(
            playbookSource, fullPlaybookPath);
        using JsonDocument playbookDocument = ExpandAuthoring(
            sourceDocument.RootElement,
            fullPlaybookPath,
            out Dictionary<string, (int Minimum, int Maximum)>
                parameterRanges);
        JsonElement playbook = playbookDocument.RootElement;
        ValidatePlaybook(playbook, fullPlaybookPath);

        JsonElement layoutReference = playbook.GetProperty("layout");
        string relativeLayoutPath = RequiredString(
            layoutReference, "path", "playbook.layout");
        string fullLayoutPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(fullPlaybookPath)
                ?? throw new InvalidDataException(
                    $"{fullPlaybookPath}: playbook has no parent directory."),
            relativeLayoutPath));
        byte[] layoutSource = File.ReadAllBytes(fullLayoutPath);
        string layoutSha256 = Sha256(layoutSource);
        string expectedLayoutSha256 = RequiredString(
            layoutReference, "sha256", "playbook.layout");
        if (!string.Equals(
                expectedLayoutSha256,
                layoutSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{fullPlaybookPath}: layout hash mismatch; expected "
                + $"{expectedLayoutSha256}, found {layoutSha256}.");
        }

        using JsonDocument layoutDocument = Parse(layoutSource, fullLayoutPath);
        JsonElement layout = layoutDocument.RootElement;
        ValidateLayout(layout, fullLayoutPath, parameterRanges);
        ValidateLayoutReferences(playbook, layout, fullPlaybookPath);

        byte[] canonicalPlaybook = NormalizePlaybook(playbook);
        byte[] canonicalLayout = Canonicalize(layout);
        string playbookSha256 = Sha256(playbookSource);
        byte[] linked = Encode(
            playbookSha256,
            layoutSha256,
            canonicalPlaybook,
            canonicalLayout);
        string[] composition = playbook.GetProperty("composition")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        return new TacticalPlaybookCompilation(
            fullPlaybookPath,
            playbookSha256,
            fullLayoutPath,
            layoutSha256,
            composition,
            canonicalPlaybook,
            canonicalLayout,
            linked);
    }

    /// <summary>
    /// The runtime IR deliberately remains exhaustive, while the authoring
    /// shape may name reusable maneuvers, fallback policies, and condition
    /// sets. Expansion is strict and deterministic: every value in an order
    /// is supplied by one named source field, never by an implicit default.
    /// </summary>
    private static JsonDocument ExpandAuthoring(
        JsonElement source,
        string path,
        out Dictionary<string, (int Minimum, int Maximum)> parameterRanges)
    {
        parameterRanges = new Dictionary<string, (int, int)>(
            StringComparer.Ordinal);
        bool hasOrders = source.TryGetProperty("orders", out _);
        bool hasAuthoring = source.TryGetProperty("authoring", out _);
        if (hasOrders == hasAuthoring)
        {
            throw Error(path,
                "playbook must declare exactly one of 'orders' or "
                + "'authoring'.");
        }
        if (hasOrders)
            return JsonDocument.Parse(source.GetRawText());

        using JsonDocument? libraryMerged = MergeAuthoringLibrary(source, path);
        if (libraryMerged is not null)
            source = libraryMerged.RootElement;

        using JsonDocument? doctrineDesugared = DesugarDoctrines(source, path);
        if (doctrineDesugared is not null)
            source = doctrineDesugared.RootElement;

        Object(source, path,
            [
                "schema", "playbookId", "auditStatus", "composition",
                "layout", "perspective", "memory", "arbitration", "roles",
                "groups", "formations", "engagements", "supportPolicies",
                "custodyPolicies", "authoring", "coordination",
            ]);

        JsonElement authoring = source.GetProperty("authoring");
        string authoringAt = $"{path}.authoring";
        Object(authoring, authoringAt,
            [
                "kind", "parameters", "fallbackPolicies",
                "assignmentProfiles", "standingOrders", "maneuvers",
                "predicates", "conditionSets",
            ]);
        Exact(authoring, "kind", "maneuver-catalog", authoringAt);

        CatalogEntry[] parameters = Catalog(
            authoring.GetProperty("parameters"),
            $"{authoringAt}.parameters", 1, 64);
        var parameterById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (CatalogEntry parameter in parameters)
        {
            Object(parameter.Value, $"{authoringAt}.parameters.{parameter.Id}",
                ["value", "minimum", "maximum"]);
            Range(parameter.Value, "minimum", path, 0, 100000);
            Range(parameter.Value, "maximum", path, 0, 100000);
            Range(parameter.Value, "value", path, 0, 100000);
            int minimum = parameter.Value.GetProperty("minimum").GetInt32();
            int maximum = parameter.Value.GetProperty("maximum").GetInt32();
            int selected = parameter.Value.GetProperty("value").GetInt32();
            if (minimum > maximum || selected < minimum || selected > maximum)
            {
                throw Error(authoringAt,
                    $"parameter '{parameter.Id}' value {selected} is outside "
                    + $"its explicit [{minimum}, {maximum}] range.");
            }
            parameterById.Add(parameter.Id, selected);
            parameterRanges.Add(parameter.Id, (minimum, maximum));
        }

        CatalogEntry[] fallbackPolicies = Catalog(
            authoring.GetProperty("fallbackPolicies"),
            $"{authoringAt}.fallbackPolicies", 1, 32);
        foreach (CatalogEntry fallback in fallbackPolicies)
            ValidateAuthoredFallback(fallback.Id, fallback.Value, authoringAt);

        CatalogEntry[] assignmentProfiles = Catalog(
            authoring.GetProperty("assignmentProfiles"),
            $"{authoringAt}.assignmentProfiles", 1, 32);
        foreach (CatalogEntry profile in assignmentProfiles)
            ValidateAuthoredAssignmentProfile(
                profile.Id, profile.Value, authoringAt);

        CatalogEntry[] standingOrders = Catalog(
            authoring.GetProperty("standingOrders"),
            $"{authoringAt}.standingOrders", 0, 32);

        CatalogEntry[] maneuvers = Catalog(
            authoring.GetProperty("maneuvers"),
            $"{authoringAt}.maneuvers", 1, 24);
        foreach (CatalogEntry maneuver in maneuvers)
            ValidateAuthoredManeuver(
                maneuver.Id, maneuver.Value, authoringAt);

        CatalogEntry[] predicates = Catalog(
            authoring.GetProperty("predicates"),
            $"{authoringAt}.predicates", 1, 128);
        foreach (CatalogEntry predicate in predicates)
            ValidateAuthoredPredicate(
                predicate.Id, predicate.Value, authoringAt);

        CatalogEntry[] conditionSets = Catalog(
            authoring.GetProperty("conditionSets"),
            $"{authoringAt}.conditionSets", 1, 64);
        foreach (CatalogEntry conditionSet in conditionSets)
        {
            JsonElement[] alternatives = BoundedArray(
                conditionSet.Value,
                $"{authoringAt}.conditionSets.{conditionSet.Id}", 1, 16);
            foreach (JsonElement alternative in alternatives)
            {
                JsonElement[] allOf = BoundedArray(
                    alternative,
                    $"{authoringAt}.conditionSets.{conditionSet.Id}", 1, 32);
                foreach (JsonElement predicateId in allOf)
                    StringValue(predicateId,
                        $"{authoringAt}.conditionSets.{conditionSet.Id}");
            }
        }

        JsonElement coordination = source.GetProperty("coordination");
        ValidateAuthoredCoordination(coordination, authoringAt);

        var fallbackById = fallbackPolicies.ToDictionary(
            value => value.Id,
            value => value.Value,
            StringComparer.Ordinal);
        LintDoctrine(source, coordination, maneuvers, fallbackById);
        var assignmentProfileById = assignmentProfiles.ToDictionary(
            value => value.Id,
            value => value.Value,
            StringComparer.Ordinal);
        var maneuverById = maneuvers.ToDictionary(
            value => value.Id,
            value => value.Value,
            StringComparer.Ordinal);
        var predicateById = predicates.ToDictionary(
            value => value.Id,
            value => value.Value,
            StringComparer.Ordinal);
        var conditionSetById = conditionSets.ToDictionary(
            value => value.Id,
            value => value.Value,
            StringComparer.Ordinal);
        var expandedConditionSetById = conditionSetById.ToDictionary(
            item => item.Key,
            item => ExpandConditionSet(
                item.Key, item.Value, predicateById, authoringAt),
            StringComparer.Ordinal);
        HashSet<string> standingOrderIds = standingOrders
            .Select(value => value.Id)
            .ToHashSet(StringComparer.Ordinal);

        JsonObject expanded = JsonNode.Parse(source.GetRawText())!.AsObject();
        expanded.Remove("authoring");
        ExpandPlacementBands(expanded, authoringAt);
        ExpandConditionReferences(
            expanded, expandedConditionSetById, authoringAt);
        var expandedOrders = new JsonArray();
        foreach (CatalogEntry standingOrder in standingOrders)
        {
            JsonObject order = JsonNode.Parse(standingOrder.Value.GetRawText())!
                .AsObject();
            order["orderId"] = standingOrder.Id;
            expandedOrders.Add(order);
        }
        foreach (CatalogEntry maneuver in maneuvers)
        foreach (CatalogEntry track in Catalog(
                     maneuver.Value.GetProperty("tracks"),
                     $"{authoringAt}.maneuvers.{maneuver.Id}.tracks", 1, 8))
        foreach (CatalogEntry assignment in Catalog(
                     track.Value.GetProperty("assignments"),
                     $"{authoringAt}.maneuvers.{maneuver.Id}.tracks."
                     + $"{track.Id}.assignments", 1, 32))
        {
            string assignmentProfileId = assignment.Value
                .GetProperty("assignmentProfileId").GetString()!;
            if (!assignmentProfileById.TryGetValue(
                    assignmentProfileId, out JsonElement assignmentProfile))
            {
                throw Error(authoringAt,
                    $"maneuver assignment references unknown assignment "
                    + $"profile '{assignmentProfileId}'.");
            }
            string fallbackId = assignment.Value.GetProperty("fallbackId")
                .GetString()!;
            if (!fallbackById.TryGetValue(fallbackId, out JsonElement fallback))
            {
                throw Error(authoringAt,
                    $"maneuver assignment references unknown fallback "
                    + $"'{fallbackId}'.");
            }
            expandedOrders.Add(ExpandOrder(
                assignment.Id,
                track.Value,
                assignment.Value,
                assignmentProfile,
                fallback));
        }
        expanded["orders"] = expandedOrders;

        var expandedPhases = new JsonArray();
        foreach (JsonElement phase in coordination.GetProperty("phases")
                     .EnumerateArray())
        {
            string maneuverId = phase.GetProperty("maneuverId").GetString()!;
            if (!maneuverById.TryGetValue(
                    maneuverId, out JsonElement maneuver))
            {
                throw Error(authoringAt,
                    $"phase references unknown maneuver '{maneuverId}'.");
            }
            var orderIds = new JsonArray();
            foreach (CatalogEntry track in Catalog(
                         maneuver.GetProperty("tracks"),
                         $"{authoringAt}.maneuvers.{maneuverId}.tracks",
                         1, 8))
            foreach (CatalogEntry assignment in Catalog(
                         track.Value.GetProperty("assignments"),
                         $"{authoringAt}.maneuvers.{maneuverId}.tracks."
                         + $"{track.Id}.assignments", 1, 32))
            {
                orderIds.Add(assignment.Id);
            }
            foreach (JsonElement standingOrderId in phase
                         .GetProperty("standingOrderIds").EnumerateArray())
            {
                string orderId = standingOrderId.GetString()!;
                if (!standingOrderIds.Contains(orderId))
                {
                    throw Error(authoringAt,
                        $"phase references unknown standing order "
                        + $"'{orderId}'.");
                }
                orderIds.Add(orderId);
            }

            var transitions = new JsonArray();
            foreach (JsonElement transition in phase
                         .GetProperty("transitions").EnumerateArray())
            {
                string conditionSetId = transition
                    .GetProperty("conditionSetId").GetString()!;
                if (!expandedConditionSetById.TryGetValue(
                        conditionSetId, out JsonArray? expandedConditions))
                {
                    throw Error(authoringAt,
                        $"transition references unknown condition set "
                        + $"'{conditionSetId}'.");
                }
                var expandedTransition = new JsonObject
                {
                    ["priority"] = transition.GetProperty("priority").GetInt32(),
                    ["to"] = transition.GetProperty("to").GetString(),
                    ["cause"] = transition.GetProperty("cause").GetString(),
                    ["minimumPolicy"] = transition
                        .GetProperty("minimumPolicy").GetString(),
                    ["stableTicks"] = transition
                        .GetProperty("stableTicks").GetInt32(),
                    ["when"] = expandedConditions.DeepClone(),
                };
                transitions.Add(expandedTransition);
            }
            expandedPhases.Add(new JsonObject
            {
                ["phaseId"] = phase.GetProperty("phaseId").GetString(),
                ["minimumTicks"] = phase.GetProperty("minimumTicks").GetInt32(),
                ["orderIds"] = orderIds,
                ["transitions"] = transitions,
            });
        }
        var expandedTasks = new JsonArray();
        foreach (JsonElement task in coordination.GetProperty("tasks")
                     .EnumerateArray())
        {
            JsonObject expandedTask = JsonNode.Parse(task.GetRawText())!
                .AsObject();
            ExpandTaskCondition(
                expandedTask,
                "whenConditionSetId",
                "when",
                expandedConditionSetById,
                authoringAt,
                allowEmpty: false);
            ExpandTaskCondition(
                expandedTask,
                "completeConditionSetId",
                "completeWhen",
                expandedConditionSetById,
                authoringAt,
                allowEmpty: false);
            if (expandedTask.ContainsKey("completionArmConditionSetId"))
            {
                ExpandTaskCondition(
                    expandedTask,
                    "completionArmConditionSetId",
                    "completionArmWhen",
                    expandedConditionSetById,
                    authoringAt,
                    allowEmpty: false);
            }
            ExpandTaskCondition(
                expandedTask,
                "failConditionSetId",
                "failWhen",
                expandedConditionSetById,
                authoringAt,
                allowEmpty: true);
            JsonObject reintegration = expandedTask["reintegration"]!
                .AsObject();
            ExpandTaskCondition(
                reintegration,
                "completeConditionSetId",
                "completeWhen",
                expandedConditionSetById,
                authoringAt,
                allowEmpty: true);
            expandedTasks.Add(expandedTask);
        }
        expanded["coordination"] = new JsonObject
        {
            ["initialPhase"] = coordination.GetProperty("initialPhase")
                .GetString(),
            ["phases"] = expandedPhases,
            ["tasks"] = expandedTasks,
        };
        ResolveConditionParameters(expanded, parameterById, authoringAt);

        return JsonDocument.Parse(expanded.ToJsonString(
            new JsonSerializerOptions { WriteIndented = false }));
    }

    private static void ExpandTaskCondition(
        JsonObject owner,
        string referenceName,
        string expandedName,
        IReadOnlyDictionary<string, JsonArray> conditionSets,
        string path,
        bool allowEmpty)
    {
        string conditionSetId = owner[referenceName]!.GetValue<string>();
        owner.Remove(referenceName);
        if (conditionSetId.Length == 0 && allowEmpty)
        {
            owner[expandedName] = new JsonArray();
            return;
        }
        if (!conditionSets.TryGetValue(
                conditionSetId,
                out JsonArray? conditions))
        {
            throw Error(path,
                $"task lifecycle references unknown condition set "
                + $"'{conditionSetId}'.");
        }
        owner[expandedName] = conditions.DeepClone();
    }

    private static JsonArray ExpandConditionSet(
        string conditionSetId,
        JsonElement conditionSet,
        IReadOnlyDictionary<string, JsonElement> predicates,
        string path)
    {
        var expanded = new JsonArray();
        foreach (JsonElement alternative in conditionSet.EnumerateArray())
        {
            var all = new JsonArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement reference in alternative.EnumerateArray())
            {
                string predicateId = reference.GetString()!;
                if (!seen.Add(predicateId))
                {
                    throw Error(path,
                        $"condition set '{conditionSetId}' repeats predicate "
                        + $"'{predicateId}'.");
                }
                if (!predicates.TryGetValue(
                        predicateId, out JsonElement predicate))
                {
                    throw Error(path,
                        $"condition set references unknown predicate "
                        + $"'{predicateId}'.");
                }
                JsonObject leaf = JsonNode.Parse(predicate.GetRawText())!
                    .AsObject();
                all.Add(leaf);
            }
            expanded.Add(new JsonObject { ["all"] = all });
        }
        return expanded;
    }

    private static void ExpandConditionReferences(
        JsonObject expanded,
        IReadOnlyDictionary<string, JsonArray> conditionSets,
        string path)
    {
        foreach (JsonObject custody in expanded["custodyPolicies"]!
                     .AsArray().Select(value => value!.AsObject()))
        {
            bool hasReference = custody.ContainsKey(
                "safeConversionConditionSetId");
            bool hasExpanded = custody.ContainsKey("safeConversionAll");
            if (hasReference == hasExpanded)
            {
                throw Error(path,
                    "authored custody policy must declare exactly one "
                    + "'safeConversionConditionSetId'.");
            }
            string conditionSetId = custody[
                "safeConversionConditionSetId"]!.GetValue<string>();
            if (!conditionSets.TryGetValue(
                    conditionSetId, out JsonArray? conditions))
            {
                throw Error(path,
                    $"custody policy references unknown condition set "
                    + $"'{conditionSetId}'.");
            }
            custody.Remove("safeConversionConditionSetId");
            custody["safeConversionAll"] = conditions.DeepClone();
            if (custody["baitDrop"] is JsonObject baitDrop
                && baitDrop.ContainsKey("reclaimConditionSetId"))
            {
                string reclaimId = baitDrop["reclaimConditionSetId"]!
                    .GetValue<string>();
                if (!conditionSets.TryGetValue(
                        reclaimId, out JsonArray? reclaim))
                {
                    throw Error(path,
                        "custody bait drop references unknown condition set "
                        + $"'{reclaimId}'.");
                }
                baitDrop.Remove("reclaimConditionSetId");
                baitDrop["reclaimAll"] = reclaim.DeepClone();
            }
            if (custody.ContainsKey("emergencyRecoveryConditionSetId"))
            {
                string emergencyConditionSetId = custody[
                    "emergencyRecoveryConditionSetId"]!.GetValue<string>();
                if (!conditionSets.TryGetValue(
                        emergencyConditionSetId,
                        out JsonArray? emergencyConditions))
                {
                    throw Error(path,
                        "custody policy references unknown emergency recovery "
                        + $"condition set '{emergencyConditionSetId}'.");
                }
                custody.Remove("emergencyRecoveryConditionSetId");
                custody["emergencyRecoveryAll"] =
                    emergencyConditions.DeepClone();
            }
        }

        foreach (JsonObject transition in expanded["groups"]!.AsArray()
                     .SelectMany(group => group!["localStateMachine"]![
                         "states"]!.AsArray())
                     .SelectMany(state => state!["transitions"]!.AsArray())
                     .Select(value => value!.AsObject()))
        {
            bool hasReference = transition.ContainsKey("conditionSetId");
            bool hasExpanded = transition.ContainsKey("when");
            if (hasReference == hasExpanded)
            {
                throw Error(path,
                    "authored group transition must declare exactly one "
                    + "'conditionSetId'.");
            }
            string conditionSetId = transition["conditionSetId"]!
                .GetValue<string>();
            if (!conditionSets.TryGetValue(
                    conditionSetId, out JsonArray? conditions))
            {
                throw Error(path,
                    $"group transition references unknown condition set "
                    + $"'{conditionSetId}'.");
            }
            transition.Remove("conditionSetId");
            transition["when"] = conditions.DeepClone();
        }

        foreach (JsonObject engagement in expanded["engagements"]!.AsArray()
                     .Select(value => value!.AsObject()))
        {
            if (engagement["holdFire"] is not JsonObject holdFire
                || !holdFire.ContainsKey("releaseConditionSetId"))
            {
                continue;
            }
            string releaseId = holdFire["releaseConditionSetId"]!
                .GetValue<string>();
            if (!conditionSets.TryGetValue(releaseId, out JsonArray? release))
            {
                throw Error(path,
                    "engagement hold-fire references unknown condition set "
                    + $"'{releaseId}'.");
            }
            holdFire.Remove("releaseConditionSetId");
            holdFire["releaseAll"] = release.DeepClone();
        }
    }

    private static void ExpandPlacementBands(
        JsonObject expanded,
        string path)
    {
        foreach (JsonObject formation in expanded["formations"]!.AsArray()
                     .Select(value => value!.AsObject()))
        {
            bool hasBands = formation.ContainsKey("placementBands");
            bool hasPlacements = formation.ContainsKey("placements");
            if (hasBands == hasPlacements)
            {
                throw Error(path,
                    "authored formation must declare exactly one "
                    + "'placementBands'.");
            }
            JsonArray bands = formation["placementBands"]!.AsArray();
            using JsonDocument document = JsonDocument.Parse(
                bands.ToJsonString());
            JsonElement[] values = BoundedArray(
                document.RootElement,
                $"{path}.formations.placementBands", 1, 16);
            var placements = new JsonArray();
            int order = 0;
            foreach (JsonElement band in values)
            {
                Object(band, $"{path}.formations.placementBands",
                    ["roleId", "sector", "offsets"], ["facing"]);
                string roleId = Identifier(band, "roleId", path);
                OneOf(band, "sector", path,
                    "front", "rear", "left", "right", "centre", "any");
                string? facing = band.TryGetProperty(
                    "facing", out JsonElement facingValue)
                        ? facingValue.GetString()
                        : null;
                if (facing is not null)
                {
                    OneOf(band, "facing", path,
                        "route", "enemy-reactor", "own-reactor",
                        "focus-target", "fixed", "watch-own-reactor",
                        "watch-enemy-reactor");
                }
                JsonElement[] offsets = BoundedArray(
                    band.GetProperty("offsets"),
                    $"{path}.formations.placementBands.offsets", 1, 16);
                foreach (JsonElement offset in offsets)
                {
                    Point(offset,
                        $"{path}.formations.placementBands.offsets", -8, 8);
                    var placement = new JsonObject
                    {
                        ["roleId"] = roleId,
                        ["sector"] = band.GetProperty("sector").GetString(),
                        ["order"] = order++,
                        ["offset"] = JsonNode.Parse(offset.GetRawText()),
                    };
                    if (facing is not null)
                        placement["facing"] = facing;
                    placements.Add(placement);
                }
            }
            formation.Remove("placementBands");
            formation["placements"] = placements;
        }
    }

    private static void ResolveConditionParameters(
        JsonNode node,
        IReadOnlyDictionary<string, int> parameters,
        string path)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("fact"))
            {
                bool hasValue = value.ContainsKey("value");
                bool hasParameter = value.ContainsKey("valueParameter");
                if (hasValue == hasParameter)
                {
                    throw Error(path,
                        "authored condition must declare exactly one of "
                        + "'value' or 'valueParameter'.");
                }
                if (hasParameter)
                {
                    string parameterId = value["valueParameter"]?
                        .GetValue<string>() ?? "";
                    if (!parameters.TryGetValue(parameterId, out int selected))
                    {
                        throw Error(path,
                            $"condition references unknown parameter "
                            + $"'{parameterId}'.");
                    }
                    // The parameter name stays beside the baked value so a
                    // side-keyed binding can re-bake it at load time
                    // (parameterOverrides). Consumers that do not override
                    // simply ignore the provenance field.
                    value["value"] = selected;
                }
            }
            foreach (JsonNode? child in value.Select(item => item.Value)
                         .ToArray())
            {
                if (child is not null)
                    ResolveConditionParameters(child, parameters, path);
            }
            return;
        }
        if (node is not JsonArray array)
            return;
        foreach (JsonNode? child in array)
        {
            if (child is not null)
                ResolveConditionParameters(child, parameters, path);
        }
    }

    private static JsonObject ExpandOrder(
        string orderId,
        JsonElement track,
        JsonElement assignment,
        JsonElement assignmentProfile,
        JsonElement fallback)
    {
        JsonElement commonMovement = track.GetProperty("movement");
        var movement = new JsonObject
        {
            ["kind"] = commonMovement.GetProperty("kind").GetString(),
            ["target"] = commonMovement.GetProperty("target").GetString(),
            ["arrivalRadius"] = assignment.GetProperty("arrivalRadius")
                .GetInt32(),
            ["completion"] = assignment.GetProperty("completion").GetString(),
            ["stuckTicks"] = commonMovement.GetProperty("stuckTicks")
                .GetInt32(),
            ["stuckRecovery"] = assignmentProfile.GetProperty("stuckRecovery")
                .GetString(),
            ["chaseLeash"] = assignment.GetProperty("chaseLeash").GetInt32(),
            ["pace"] = commonMovement.GetProperty("pace").GetString(),
        };
        if (commonMovement.TryGetProperty(
                "leadTiles", out JsonElement leadTiles))
        {
            movement["leadTiles"] = leadTiles.GetInt32();
        }
        var expanded = new JsonObject
        {
            ["orderId"] = orderId,
            ["groupId"] = assignmentProfile.GetProperty("groupId").GetString(),
            ["priority"] = assignmentProfile.GetProperty("priority").GetInt32(),
            ["members"] = JsonNode.Parse(
                assignmentProfile.GetProperty("members").GetRawText()),
            ["movement"] = movement,
            ["formationId"] = track.GetProperty("formationId").GetString(),
            ["engagementId"] = assignment.GetProperty("engagementId")
                .GetString(),
            ["localState"] = assignmentProfile.GetProperty("localState")
                .GetString(),
            ["fallback"] = new JsonObject
            {
                ["onNoPath"] = fallback.GetProperty("onNoPath").GetString(),
                ["onUnderstrength"] = fallback
                    .GetProperty("onUnderstrength").GetString(),
                ["onInvalidTarget"] = fallback
                    .GetProperty("onInvalidTarget").GetString(),
                ["phaseId"] = fallback.GetProperty("phaseId").GetString(),
            },
        };
        string supportId = assignmentProfile.GetProperty("supportId")
            .GetString()!;
        string custodyId = assignment.GetProperty("custodyId").GetString()!;
        if (supportId.Length > 0)
            expanded["supportId"] = supportId;
        if (custodyId.Length > 0)
            expanded["custodyId"] = custodyId;
        if (assignment.TryGetProperty("stance", out JsonElement stance))
            expanded["stance"] = stance.GetString();
        if (assignment.TryGetProperty(
                "patienceTicks", out JsonElement patienceTicks))
        {
            expanded["patienceTicks"] = patienceTicks.GetInt32();
        }
        return expanded;
    }

    /// <summary>
    /// Desugars the consolidated doctrine grammar (ghost doctrine v3,
    /// SPEC-GHOST-DOCTRINE-V3 / DECISIONS #216) into the legacy
    /// task/maneuver/engagement machinery BEFORE validation, so the mind
    /// contract is untouched and every generated artifact still passes the
    /// full strict validation pass. A doctrine binds one role and answers
    /// two questions exactly once:
    ///
    /// - `modes` is an ordered priority list of verb-keyed movement modes
    ///   (`patrol`, `intercept`, `assault`), first match wins, re-evaluated
    ///   continuously; the LAST mode must be an unconditioned patrol - the
    ///   floor a body always falls back to, so no predicate gap can park it.
    ///   `while`/`until` are boolean strings over authored predicates
    ///   ("carrier-known or enemy-remembered-deep"): `and` binds tighter
    ///   than `or`, and an unknown predicate is a compile error downstream.
    /// - `fight` answers when to shoot in four sub-blocks (targets, engage,
    ///   chase, breakOff) that desugar into one generated engagement per
    ///   mode: holdFire from engage.within, isolation from targets.lone,
    ///   the commit block from chase/breakOff, and ONE chase leash feeding
    ///   all three legacy homes (movement, engagement, commit). A mode's
    ///   `fight` overrides individual keys; a resolved fight with no gates
    ///   at all generates no commit/holdFire/isolation, which is exactly
    ///   the committed-assault shape. Zero clears a gate.
    ///
    /// Everything the v2 grammar authored explicitly is now derived: the
    /// group and local state from the role, the assignment profile
    /// (priority 20, members all, repath), support from the escort roles'
    /// kit, the withdraw rally from the floor patrol route, formations by
    /// convention (`{id}-solo`, and `{id}-escort` when any mode rides an
    /// escort), and stance - concealment is the doctrine plane's character,
    /// every generated assignment is an ambusher (backstab physics live in
    /// the engine; there is no sheet knob).
    /// Returns null when the playbook authors no doctrines.
    /// </summary>
    private static JsonDocument? DesugarDoctrines(
        JsonElement source,
        string path)
    {
        if (!source.TryGetProperty("doctrines", out JsonElement doctrines))
            return null;
        JsonObject root = JsonNode.Parse(source.GetRawText())!.AsObject();
        root.Remove("doctrines");
        JsonObject profiles =
            root["authoring"]!["assignmentProfiles"]!.AsObject();
        JsonObject maneuvers = root["authoring"]!["maneuvers"]!.AsObject();
        JsonObject conditionSets =
            root["authoring"]!["conditionSets"]!.AsObject();
        JsonArray engagements = root["engagements"]!.AsArray();
        JsonArray tasks = root["coordination"]!["tasks"]!.AsArray();
        string[] phaseIds = root["coordination"]!["phases"]!.AsArray()
            .Select(phase => phase!["phaseId"]!.GetValue<string>())
            .ToArray();

        foreach (JsonProperty doctrine in doctrines.EnumerateObject())
        {
            string id = doctrine.Name;
            string at = $"{path}.doctrines.{id}";
            JsonElement value = doctrine.Value;
            Object(value, at, ["role", "custody", "modes"],
                ["fight", "conceal"]);
            string role = value.GetProperty("role").GetString()!;
            string custody = value.GetProperty("custody").GetString()!;
            // Concealment micro (slip out of seen facing cones, skip the
            // flank walk, long ambush patience) is the doctrine's default
            // character; `"conceal": false` turns the ghost into an open
            // fighter. Exposed 2026-08-08 (owner: the sheet mechanism must
            // own its behavior; collapse is acceptable, opacity is not).
            bool conceal = !value.TryGetProperty(
                    "conceal", out JsonElement concealValue)
                || concealValue.ValueKind != JsonValueKind.False;
            JsonElement doctrineFight = value.TryGetProperty(
                "fight", out JsonElement fightValue)
                ? fightValue
                : default;
            if (doctrineFight.ValueKind == JsonValueKind.Object)
                ValidateDoctrineFight(doctrineFight, at);

            (string GroupId, string LocalState) home =
                DoctrineHome(root, role, at);
            JsonElement[] modes = BoundedArray(
                value.GetProperty("modes"), at, 1, 8);

            // Pre-scan the modes for the derivations that need the whole
            // list: the floor route (withdraw rally), the escort union
            // (support kit + escort formation requirement), and per-verb
            // occurrence counts for stable generated names.
            var escortRoles = new List<string>();
            bool anyEscort = false;
            var verbCounts = new Dictionary<string, int>(
                StringComparer.Ordinal);
            string[] verbs = new string[modes.Length];
            for (int index = 0; index < modes.Length; index++)
            {
                string verb = DoctrineVerb(modes[index], at);
                verbs[index] = verb;
                verbCounts[verb] = verbCounts.GetValueOrDefault(verb) + 1;
                if (TryDoctrineEscort(modes[index], at,
                        out string[] modeEscorts))
                {
                    anyEscort = true;
                    foreach (string escortRole in modeEscorts)
                    {
                        if (!escortRoles.Contains(escortRole,
                                StringComparer.Ordinal))
                            escortRoles.Add(escortRole);
                    }
                }
            }
            JsonElement floor = modes[^1];
            if (verbs[^1] != "patrol"
                || floor.TryGetProperty("while", out _)
                || floor.TryGetProperty("until", out _))
            {
                throw Error(at,
                    "the last mode must be an unconditioned 'patrol' - the "
                    + "floor the body always falls back to.");
            }
            string floorRoute = floor.GetProperty("patrol").GetString()!;
            string soloFormation = $"{id}-solo";
            RequireFormation(root, soloFormation, at);
            string escortFormation = $"{id}-escort";
            if (anyEscort)
                RequireFormation(root, escortFormation, at);
            string supportId = DoctrineSupport(root, role, escortRoles, at);

            profiles[$"{id}-doctrine"] = new JsonObject
            {
                ["groupId"] = home.GroupId,
                ["localState"] = home.LocalState,
                ["priority"] = 20,
                ["members"] = new JsonObject { ["kind"] = "all" },
                ["stuckRecovery"] = "repath",
                ["supportId"] = supportId,
            };

            var seenVerbs = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < modes.Length; index++)
            {
                JsonElement mode = modes[index];
                string verb = verbs[index];
                int occurrence = seenVerbs.GetValueOrDefault(verb) + 1;
                seenVerbs[verb] = occurrence;
                string name = verbCounts[verb] > 1
                    ? $"{id}-{verb}-{occurrence}"
                    : $"{id}-{verb}";
                string modeAt = $"{at}.modes[{index}]";
                bool isFloor = index == modes.Length - 1;
                ValidateDoctrineMode(mode, verb, isFloor, modeAt);

                string whenSet = "convert-freely";
                if (mode.TryGetProperty("while", out JsonElement whileText))
                {
                    whenSet = $"{name}-on";
                    conditionSets[whenSet] = ParseConditionString(
                        whileText.GetString()!, modeAt);
                }
                string failSet = "task-never-done";
                if (mode.TryGetProperty("until", out JsonElement untilText))
                {
                    failSet = $"{name}-off";
                    conditionSets[failSet] = ParseConditionString(
                        untilText.GetString()!, modeAt);
                }
                else if (whenSet != "convert-freely")
                {
                    Console.Error.WriteLine(
                        $"lint: doctrine '{id}' mode '{verb}' has 'while' "
                        + "but no 'until' - once active it only yields to a "
                        + "later mode, never back to an earlier one.");
                }

                ResolvedFight fight = ResolveFight(
                    doctrineFight,
                    mode.TryGetProperty("fight", out JsonElement modeFight)
                        ? modeFight
                        : default,
                    modeAt);
                bool hasEscort = TryDoctrineEscort(
                    mode, modeAt, out string[] escorts);
                engagements.Add(DoctrineEngagement(
                    name, role, escorts, fight, floorRoute, modeAt));

                var assignment = new JsonObject
                {
                    ["arrivalRadius"] = verb == "intercept" ? 1 : 2,
                    ["completion"] = "continuous",
                    ["chaseLeash"] = fight.ChaseLeash,
                    ["engagementId"] = name,
                    ["custodyId"] = custody,
                    ["fallbackId"] = "continue-with-alternate",
                    ["assignmentProfileId"] = $"{id}-doctrine",
                };
                if (conceal)
                    assignment["stance"] = "ambush";
                if (mode.TryGetProperty(
                        "patienceTicks", out JsonElement patience))
                {
                    assignment["patienceTicks"] = patience.GetInt32();
                }
                maneuvers[$"{name}-set"] = new JsonObject
                {
                    ["tracks"] = new JsonObject
                    {
                        ["main"] = new JsonObject
                        {
                            ["formationId"] = hasEscort
                                ? escortFormation
                                : soloFormation,
                            ["movement"] = DoctrineMovement(
                                mode, verb, hasEscort),
                            ["assignments"] = new JsonObject
                            {
                                [name] = assignment,
                            },
                        },
                    },
                };

                var rows = new JsonArray
                {
                    new JsonObject
                    {
                        ["assignmentId"] = name,
                        ["orderId"] = name,
                        ["roles"] = new JsonArray(role),
                        ["classes"] = new JsonArray(),
                        ["minimum"] = 0,
                        ["preferred"] = 1,
                        ["maximum"] = 1,
                        ["carrier"] = "allow",
                        ["distance"] = new JsonObject
                        {
                            ["kind"] = "own-reactor",
                            ["target"] = "",
                        },
                    },
                };
                if (hasEscort)
                {
                    rows.Add(new JsonObject
                    {
                        ["assignmentId"] = $"{name}-escort",
                        ["orderId"] = name,
                        ["roles"] = new JsonArray(escorts
                            .Select(escortRole => (JsonNode)escortRole)
                            .ToArray()),
                        ["classes"] = new JsonArray(),
                        ["minimum"] = 0,
                        ["preferred"] = 1,
                        ["maximum"] = 1,
                        ["carrier"] = "forbid",
                        ["distance"] = new JsonObject
                        {
                            ["kind"] = "own-reactor",
                            ["target"] = "",
                        },
                    });
                }
                // First mode outranks by construction (lower number wins in
                // the task band); the band stays under 8 so every doctrine
                // mode preempts the legacy standing tasks (8+).
                tasks.Add(new JsonObject
                {
                    ["taskId"] = name,
                    ["priority"] = 8 - modes.Length + index,
                    ["activation"] = "while-true",
                    ["preemption"] = "higher-priority",
                    ["participantLoss"] = "replace",
                    ["triggerStableTicks"] = 1,
                    ["minimumTicks"] = 8,
                    ["timeoutTicks"] = 600,
                    ["cooldownTicks"] = 2,
                    ["minimumPrimaryBodies"] = 1,
                    ["eligiblePhases"] = new JsonArray(
                        phaseIds.Select(phase => (JsonNode)phase!).ToArray()),
                    ["assignments"] = rows,
                    ["whenConditionSetId"] = whenSet,
                    ["completeConditionSetId"] = "task-never-done",
                    ["failConditionSetId"] = failSet,
                    ["reintegration"] = new JsonObject
                    {
                        ["mode"] = "primary-order",
                        ["orderIds"] = new JsonArray(),
                        ["completeConditionSetId"] = "",
                        ["timeoutTicks"] = 0,
                    },
                });
            }
        }
        return JsonDocument.Parse(root.ToJsonString());
    }

    /// <summary>The verb key names the mode; exactly one must be present.
    /// </summary>
    private static string DoctrineVerb(JsonElement mode, string path)
    {
        string[] present = new[] { "patrol", "intercept", "assault" }
            .Where(verb => mode.TryGetProperty(verb, out _))
            .ToArray();
        if (present.Length != 1)
        {
            throw Error(path,
                "each doctrine mode must carry exactly one of 'patrol', "
                + "'intercept', or 'assault'.");
        }
        return present[0];
    }

    private static void ValidateDoctrineMode(
        JsonElement mode,
        string verb,
        bool isFloor,
        string path)
    {
        string[] optional = verb switch
        {
            "patrol" => ["while", "until", "fight"],
            "intercept" =>
                ["from", "while", "until", "fight", "patienceTicks"],
            _ => ["while", "until", "fight", "escort"],
        };
        Object(mode, path, [verb], optional);
        NonEmptyString(mode, verb, path);
        if (verb == "intercept")
        {
            OneOf(mode, "intercept", path, "enemy-carriers", "inbound");
            if (mode.TryGetProperty("from", out _))
                Identifier(mode, "from", path);
            if (mode.TryGetProperty("patienceTicks", out _))
                Range(mode, "patienceTicks", path, 2, 120);
        }
        if (mode.TryGetProperty("while", out JsonElement whileText)
            && whileText.ValueKind != JsonValueKind.String)
        {
            throw Error(path, "'while' must be a boolean condition string.");
        }
        if (mode.TryGetProperty("until", out JsonElement untilText)
            && untilText.ValueKind != JsonValueKind.String)
        {
            throw Error(path, "'until' must be a boolean condition string.");
        }
        if (!isFloor && !mode.TryGetProperty("while", out _))
        {
            throw Error(path,
                "every mode above the floor needs a 'while' - an "
                + "unconditioned mode would permanently shadow everything "
                + "below it.");
        }
        if (mode.TryGetProperty("fight", out JsonElement fight))
            ValidateDoctrineFight(fight, path);
    }

    /// <summary>
    /// Boolean condition strings compile to the legacy condition-set groups
    /// (disjunctive normal form): 'and' binds tighter than 'or', operands
    /// are authored predicate ids, and there are no parentheses - a policy
    /// that needs them wants a named predicate, not a longer formula. An
    /// operand that names no authored predicate fails downstream in
    /// ExpandConditionSet, so a typo is a compile error, never a
    /// silently-false gate.
    /// </summary>
    private static JsonArray ParseConditionString(string text, string path)
    {
        var groups = new JsonArray();
        foreach (string alternative in text.Split(
                     " or ", StringSplitOptions.None))
        {
            var group = new JsonArray();
            foreach (string operand in alternative.Split(
                         " and ", StringSplitOptions.None))
            {
                string token = operand.Trim();
                if (token.Length == 0
                    || token.Any(character => !char.IsAsciiLetterLower(
                        character) && !char.IsAsciiDigit(character)
                        && character != '-'))
                {
                    throw Error(path,
                        $"condition string '{text}' is not of the form "
                        + "'predicate [and predicate ...] [or ...]'.");
                }
                group.Add((JsonNode)token);
            }
            groups.Add(group);
        }
        return groups;
    }

    private static bool TryDoctrineEscort(
        JsonElement mode,
        string path,
        out string[] escortRoles)
    {
        escortRoles = [];
        if (!mode.TryGetProperty("escort", out JsonElement escort))
            return false;
        if (escort.ValueKind != JsonValueKind.String
            || escort.GetString() is not { Length: > 0 } escortRole)
        {
            throw Error(path, "'escort' must name a single role.");
        }
        escortRoles = [escortRole];
        return true;
    }

    /// <summary>The doctrine's home group: the role must belong to exactly
    /// one group, whose initial state the generated orders inherit.
    /// </summary>
    private static (string GroupId, string LocalState) DoctrineHome(
        JsonObject root,
        string role,
        string path)
    {
        var homes = root["groups"]!.AsArray()
            .Where(group => group!["roleIds"]!.AsArray()
                .Any(roleId => roleId!.GetValue<string>() == role))
            .Select(group => (
                GroupId: group!["groupId"]!.GetValue<string>(),
                LocalState: group["localStateMachine"]!["initialState"]!
                    .GetValue<string>()))
            .ToArray();
        if (homes.Length != 1)
        {
            throw Error(path,
                $"doctrine role '{role}' must belong to exactly one group "
                + $"(found {homes.Length}).");
        }
        return homes[0];
    }

    /// <summary>Escorts bring their kit: the generated orders carry the one
    /// support policy provided by the doctrine role or its escorts, if any.
    /// </summary>
    private static string DoctrineSupport(
        JsonObject root,
        string role,
        IReadOnlyCollection<string> escortRoles,
        string path)
    {
        string[] matching = root["supportPolicies"]!.AsArray()
            .Where(policy => policy!["providers"]!.AsArray()
                .Any(provider => provider!.GetValue<string>() == role
                    || escortRoles.Contains(provider.GetValue<string>(),
                        StringComparer.Ordinal)))
            .Select(policy => policy!["supportId"]!.GetValue<string>())
            .ToArray();
        if (matching.Length > 1)
        {
            throw Error(path,
                "the doctrine's role and escorts provide more than one "
                + $"support policy ({string.Join(", ", matching)}); the "
                + "grammar cannot choose between them.");
        }
        return matching.Length == 1 ? matching[0] : "";
    }

    private static void RequireFormation(
        JsonObject root,
        string formationId,
        string path)
    {
        bool exists = root["formations"]!.AsArray()
            .Any(formation => formation!["formationId"]!
                .GetValue<string>() == formationId);
        if (!exists)
        {
            throw Error(path,
                $"doctrine formations are conventional: author "
                + $"'{formationId}' in the formations section.");
        }
    }

    private static JsonObject DoctrineMovement(
        JsonElement mode,
        string verb,
        bool hasEscort)
    {
        string pace = hasEscort ? "slowest" : "free";
        switch (verb)
        {
            case "patrol":
                string route = mode.GetProperty("patrol").GetString()!;
                var patrol = new JsonObject
                {
                    ["kind"] = route == "traffic" ? "shadow-traffic" : "route",
                    ["target"] = route == "traffic" ? "" : route,
                    ["stuckTicks"] = 10,
                    ["pace"] = pace,
                    ["scan"] = "sweep",
                };
                return patrol;
            case "intercept":
                return new JsonObject
                {
                    ["kind"] = mode.GetProperty("intercept").GetString()
                        == "inbound"
                        ? "incoming-cutoff"
                        : "enemy-carrier-cutoff",
                    ["target"] = mode.TryGetProperty(
                        "from", out JsonElement perch)
                        ? perch.GetString()
                        : "",
                    ["stuckTicks"] = 8,
                    ["pace"] = pace,
                    ["leadTiles"] = 1,
                };
            default:
                return new JsonObject
                {
                    ["kind"] = "route",
                    ["target"] = mode.GetProperty("assault").GetString(),
                    ["stuckTicks"] = 10,
                    ["pace"] = pace,
                };
        }
    }

    /// <summary>The four fight questions with a mode's overrides applied
    /// per key. Zero clears a gate; a cleared gate generates nothing.
    /// </summary>
    private readonly record struct ResolvedFight(
        int Lone,
        int Within,
        int KillableTicks,
        string? From,
        int PositionTicks,
        string Else,
        int ChaseLeash,
        bool OnlyCatchable,
        int ExecuteBelowHealth,
        int Threats,
        int ThreatsWithin,
        int MemoryTicks,
        int RecoverTicks,
        int PersistTicks,
        int DefenseRadius,
        bool DefenseReturn);

    private static void ValidateDoctrineFight(JsonElement fight, string path)
    {
        Object(fight, $"{path}.fight", [],
            ["targets", "engage", "chase", "breakOff", "defense"]);
        if (fight.TryGetProperty("targets", out JsonElement targets))
        {
            Object(targets, $"{path}.fight.targets", [], ["lone"]);
            if (targets.TryGetProperty("lone", out _))
                Range(targets, "lone", path, 0, 8);
        }
        if (fight.TryGetProperty("engage", out JsonElement engage))
        {
            Object(engage, $"{path}.fight.engage", [],
                ["within", "killableTicks", "from", "positionTicks", "else"]);
            if (engage.TryGetProperty("within", out _))
                Range(engage, "within", path, 0, 12);
            if (engage.TryGetProperty("killableTicks", out _))
                Range(engage, "killableTicks", path, 0, 60);
            if (engage.TryGetProperty("from", out _))
                OneOf(engage, "from", path, "behind");
            if (engage.TryGetProperty("positionTicks", out _))
                Range(engage, "positionTicks", path, 1, 64);
            if (engage.TryGetProperty("else", out _))
                OneOf(engage, "else", path, "strike", "breakOff");
        }
        if (fight.TryGetProperty("chase", out JsonElement chase))
        {
            Object(chase, $"{path}.fight.chase", [],
                ["leash", "onlyCatchable", "executeBelowHealth",
                 "persistTicks"]);
            if (chase.TryGetProperty("leash", out _))
                Range(chase, "leash", path, 0, 16);
            // How long a chase survives the prey being hidden or
            // unreachable before the focus lock releases. The old silent
            // constants (hidden 2, unreachable 3) ended most warren chases
            // the moment prey crossed a wall - the owner's "the enemy ran
            // off" catch.
            if (chase.TryGetProperty("persistTicks", out _))
                Range(chase, "persistTicks", path, 1, 120);
            if (chase.TryGetProperty("onlyCatchable", out JsonElement catchable)
                && catchable.ValueKind is not JsonValueKind.True
                    and not JsonValueKind.False)
            {
                throw Error(path, "'onlyCatchable' must be a boolean.");
            }
            if (chase.TryGetProperty("executeBelowHealth", out _))
                Range(chase, "executeBelowHealth", path, 0, 8);
        }
        // Self-preservation: the radius at which a body may fight back
        // outside its gates, and whether it is pulled back to formation
        // afterwards (the pull was a silent disengager - Class A in
        // docs/EXECUTOR-SILENT-POLICY.md).
        if (fight.TryGetProperty("defense", out JsonElement defense))
        {
            Object(defense, $"{path}.fight.defense", [],
                ["radius", "return"]);
            if (defense.TryGetProperty("radius", out _))
                Range(defense, "radius", path, 0, 16);
            if (defense.TryGetProperty("return", out JsonElement ret)
                && ret.ValueKind is not JsonValueKind.True
                    and not JsonValueKind.False)
            {
                throw Error(path, "'return' must be a boolean.");
            }
        }
        if (fight.TryGetProperty("breakOff", out JsonElement breakOff))
        {
            Object(breakOff, $"{path}.fight.breakOff", [],
                ["threats", "within", "memoryTicks", "recoverTicks"]);
            if (breakOff.TryGetProperty("threats", out _))
                Range(breakOff, "threats", path, 0, 8);
            if (breakOff.TryGetProperty("within", out _))
                Range(breakOff, "within", path, 2, 16);
            if (breakOff.TryGetProperty("memoryTicks", out _))
                Range(breakOff, "memoryTicks", path, 1, 120);
            if (breakOff.TryGetProperty("recoverTicks", out _))
                Range(breakOff, "recoverTicks", path, 4, 120);
        }
    }

    private static int FightValue(
        JsonElement doctrineFight,
        JsonElement modeFight,
        string block,
        string key,
        int fallback)
    {
        foreach (JsonElement fight in (JsonElement[])[modeFight, doctrineFight])
        {
            if (fight.ValueKind == JsonValueKind.Object
                && fight.TryGetProperty(block, out JsonElement sub)
                && sub.TryGetProperty(key, out JsonElement found))
            {
                return found.GetInt32();
            }
        }
        return fallback;
    }

    private static JsonElement FightElement(
        JsonElement doctrineFight,
        JsonElement modeFight,
        string block,
        string key)
    {
        foreach (JsonElement fight in (JsonElement[])[modeFight, doctrineFight])
        {
            if (fight.ValueKind == JsonValueKind.Object
                && fight.TryGetProperty(block, out JsonElement sub)
                && sub.TryGetProperty(key, out JsonElement found))
            {
                return found;
            }
        }
        return default;
    }

    private static ResolvedFight ResolveFight(
        JsonElement doctrineFight,
        JsonElement modeFight,
        string path)
    {
        JsonElement from = FightElement(
            doctrineFight, modeFight, "engage", "from");
        JsonElement catchable = FightElement(
            doctrineFight, modeFight, "chase", "onlyCatchable");
        var resolved = new ResolvedFight(
            Lone: FightValue(doctrineFight, modeFight, "targets", "lone", 0),
            Within: FightValue(doctrineFight, modeFight, "engage", "within", 0),
            KillableTicks: FightValue(
                doctrineFight, modeFight, "engage", "killableTicks", 0),
            From: from.ValueKind == JsonValueKind.String
                ? from.GetString()
                : null,
            PositionTicks: FightValue(
                doctrineFight, modeFight, "engage", "positionTicks", 16),
            Else: FightElement(doctrineFight, modeFight, "engage", "else")
                    is { ValueKind: JsonValueKind.String } elseValue
                ? elseValue.GetString()!
                : "strike",
            ChaseLeash: FightValue(
                doctrineFight, modeFight, "chase", "leash", 0),
            OnlyCatchable: catchable.ValueKind == JsonValueKind.True,
            ExecuteBelowHealth: FightValue(
                doctrineFight, modeFight, "chase", "executeBelowHealth", 0),
            Threats: FightValue(
                doctrineFight, modeFight, "breakOff", "threats", 0),
            ThreatsWithin: FightValue(
                doctrineFight, modeFight, "breakOff", "within", 0),
            MemoryTicks: FightValue(
                doctrineFight, modeFight, "breakOff", "memoryTicks", 0),
            RecoverTicks: FightValue(
                doctrineFight, modeFight, "breakOff", "recoverTicks", 24),
            PersistTicks: FightValue(
                doctrineFight, modeFight, "chase", "persistTicks", 0),
            DefenseRadius: FightValue(
                doctrineFight, modeFight, "defense", "radius", 2),
            DefenseReturn: FightElement(
                    doctrineFight, modeFight, "defense", "return")
                .ValueKind != JsonValueKind.False);
        if (resolved.Threats > 0
            && (resolved.ThreatsWithin == 0 || resolved.MemoryTicks == 0))
        {
            throw Error(path,
                "an active breakOff needs its whole threat picture: "
                + "'threats' requires 'within' and 'memoryTicks'.");
        }
        return resolved;
    }

    /// <summary>
    /// One generated engagement per mode. The scaffolding (priorities, tie
    /// breaks, locks, release, self-defense) is fixed - the engine's
    /// declared-strike physics made the old per-sheet arithmetic noise -
    /// and the fight block maps onto the three proven gates: holdFire
    /// (engage.within), isolation (targets.lone), commit (the rest). A
    /// fight with no gates generates none of them: the body takes every
    /// fight its movement reaches, which is the committed-assault shape.
    /// </summary>
    private static JsonObject DoctrineEngagement(
        string name,
        string role,
        IReadOnlyCollection<string> escortRoles,
        ResolvedFight fight,
        string floorRoute,
        string path)
    {
        var engagement = new JsonObject
        {
            ["engagementId"] = name,
            ["participants"] = new JsonArray(
                ((string[])[role, .. escortRoles])
                .Select(participant => (JsonNode)participant)
                .ToArray()),
            ["targetPriorities"] = new JsonArray(
                "enemy-carrier", "lowest-health"),
            ["tieBreakers"] = new JsonArray(
                "distance", "health", "unit-id", "life-id"),
            ["coordinationScope"] = "shared-policy",
            ["lockTicks"] = 4,
            ["lockPreemption"] = "urgent-carrier",
            ["maximumAttackersPerTarget"] = 2,
            ["overkillDamage"] = 0,
            ["chaseLeash"] = Math.Min(fight.ChaseLeash, 16),
            ["aimPreparation"] = "rotate-to-engage",
            ["signatureCoordination"] = "damage-first",
            ["release"] = new JsonObject
            {
                ["hiddenTicks"] = fight.PersistTicks > 0
                    ? fight.PersistTicks : 2,
                ["unreachableTicks"] = fight.PersistTicks > 0
                    ? fight.PersistTicks : 3,
                ["outsideLeash"] = true,
                ["destroyed"] = true,
            },
            ["selfDefense"] = new JsonObject
            {
                ["enabled"] = fight.DefenseRadius > 0,
                ["threatDistance"] = fight.DefenseRadius,
                ["returnToFormation"] = fight.DefenseReturn,
            },
        };
        if (fight.Within > 0)
        {
            engagement["holdFire"] = new JsonObject
            {
                ["withinDistance"] = fight.Within,
            };
        }
        if (fight.Lone > 0)
        {
            engagement["isolation"] = new JsonObject
            {
                ["supportRange"] = fight.Lone,
            };
        }
        bool hasChaseGates = fight.OnlyCatchable
            || fight.ExecuteBelowHealth > 0;
        bool hasGates = fight.KillableTicks > 0
            || fight.From is not null
            || hasChaseGates
            || fight.Threats > 0;
        if (!hasGates)
            return engagement;
        var commit = new JsonObject
        {
            ["roles"] = new JsonArray(role),
            ["engageWhen"] = fight.KillableTicks > 0
                ? new JsonObject
                {
                    ["killWithinTicks"] = fight.KillableTicks,
                }
                : new JsonObject(),
        };
        if (fight.Threats > 0)
        {
            commit["awareness"] = new JsonObject
            {
                ["radius"] = fight.ThreatsWithin,
                ["memoryTicks"] = fight.MemoryTicks,
            };
        }
        if (fight.ChaseLeash > 0 || hasChaseGates)
        {
            var chase = new JsonObject();
            if (fight.ChaseLeash > 0)
                chase["leash"] = fight.ChaseLeash;
            if (fight.OnlyCatchable)
                chase["onlyCatchable"] = true;
            if (fight.ExecuteBelowHealth > 0)
                chase["executeBelowHealth"] = fight.ExecuteBelowHealth;
            commit["chase"] = chase;
        }
        if (fight.Threats > 0)
        {
            if (floorRoute == "traffic")
            {
                throw Error(path,
                    "an active breakOff rallies to the floor patrol route, "
                    + "but the floor patrols computed traffic; author a "
                    + "route floor or clear breakOff.threats.");
            }
            commit["disengageWhen"] = new JsonObject
            {
                ["threats"] = fight.Threats,
                ["withdrawTo"] = floorRoute,
                ["recoverTicks"] = fight.RecoverTicks,
            };
        }
        if (fight.From is not null)
        {
            commit["approach"] = new JsonObject
            {
                ["from"] = fight.From,
                ["positionTicks"] = fight.PositionTicks,
                ["else"] = fight.Else,
            };
        }
        engagement["commit"] = commit;
        return engagement;
    }

    /// <summary>
    /// Doctrine coherence lints — warnings, never errors, because frozen
    /// clean-slate opponents must keep compiling byte-identically. Each lint
    /// is a bug class that shipped silently before it was caught on replay:
    /// a strike task that can never preempt the standing task holding its
    /// only candidates (the parked ghost's first cause), and a hold-on-
    /// invalid fallback on a dynamic-target movement, which parks the unit
    /// wherever it happens to stand when its prey predicate flickers false
    /// (the parked ghost's second cause).
    /// </summary>
    private static void LintDoctrine(
        JsonElement source,
        JsonElement coordination,
        CatalogEntry[] maneuvers,
        IReadOnlyDictionary<string, JsonElement> fallbackById)
    {
        string[] dynamicKinds =
            ["enemy-carrier-cutoff", "enemy-carrier", "carrier",
             "secured-core", "incoming-cutoff"];
        foreach (CatalogEntry maneuver in maneuvers)
        foreach (CatalogEntry track in Catalog(
                     maneuver.Value.GetProperty("tracks"), "lint", 1, 8))
        {
            string kind = track.Value.GetProperty("movement")
                .GetProperty("kind").GetString()!;
            if (!dynamicKinds.Contains(kind))
                continue;
            foreach (CatalogEntry assignment in Catalog(
                         track.Value.GetProperty("assignments"),
                         "lint", 1, 32))
            {
                string fallbackId = assignment.Value
                    .GetProperty("fallbackId").GetString()!;
                if (fallbackById.TryGetValue(
                        fallbackId, out JsonElement fallback)
                    && fallback.GetProperty("onInvalidTarget").GetString()
                        == "hold")
                {
                    Console.Error.WriteLine(
                        $"lint: maneuver '{maneuver.Id}' assignment "
                        + $"'{assignment.Id}' pairs dynamic movement "
                        + $"'{kind}' with onInvalidTarget=hold — the body "
                        + "parks where it stands whenever the target "
                        + "predicate is false; prefer a fallback with "
                        + "onInvalidTarget=alternate.");
                }
            }
        }

        Dictionary<string, int> roleMaxima = source.GetProperty("roles")
            .EnumerateArray()
            .ToDictionary(
                role => role.GetProperty("roleId").GetString()!,
                role => role.GetProperty("maximum").GetInt32(),
                StringComparer.Ordinal);
        var tasks = coordination.GetProperty("tasks").EnumerateArray()
            .Select(task => (
                Id: task.GetProperty("taskId").GetString()!,
                Priority: task.GetProperty("priority").GetInt32(),
                Timeout: task.GetProperty("timeoutTicks").GetInt32(),
                When: task.GetProperty("whenConditionSetId").GetString()!,
                Assignments: task.GetProperty("assignments")
                    .EnumerateArray()
                    .Select(value => (
                        Roles: value.GetProperty("roles").EnumerateArray()
                            .Select(role => role.GetString()!).ToArray(),
                        Maximum: value.GetProperty("maximum").GetInt32()))
                    .ToArray()))
            .ToArray();
        JsonElement conditionSets = source.GetProperty("authoring")
            .GetProperty("conditionSets");
        bool AlwaysOn(string setId) =>
            conditionSets.TryGetProperty(setId, out JsonElement set)
            && set.EnumerateArray().Any(group =>
                group.EnumerateArray().Count() == 1
                && group.EnumerateArray().First().GetString() == "always");
        foreach (var task in tasks)
        foreach (var assignment in task.Assignments)
        foreach (string role in assignment.Roles)
        {
            if (!roleMaxima.TryGetValue(role, out int unitMaximum))
                continue;
            int standingHold = tasks
                .Where(other => other.Id != task.Id
                    && other.Priority < task.Priority
                    && other.Timeout >= 600
                    && AlwaysOn(other.When))
                .SelectMany(other => other.Assignments)
                .Where(other => other.Roles.Contains(role, StringComparer.Ordinal))
                .Sum(other => other.Maximum);
            if (standingHold >= unitMaximum && standingHold > 0)
            {
                Console.Error.WriteLine(
                    $"lint: task '{task.Id}' (priority {task.Priority}) can "
                    + $"never claim role '{role}': standing higher-priority "
                    + "tasks can hold every unit of that role and never "
                    + "complete. Preemption requires the claimant to "
                    + "OUTRANK the owner (lower priority number).");
            }
        }
    }

    private static void ValidateAuthoredFallback(
        string fallbackId,
        JsonElement fallback,
        string path)
    {
        Object(fallback, $"{path}.fallbackPolicies.{fallbackId}",
            [
                "onNoPath", "onUnderstrength", "onInvalidTarget", "phaseId",
            ]);
        OneOf(fallback, "onNoPath", path, "reflow", "hold", "regroup");
        OneOf(fallback, "onUnderstrength", path,
            "continue", "regroup", "fallback-phase");
        OneOf(fallback, "onInvalidTarget", path,
            "hold", "alternate", "fallback-phase");
        NonEmptyString(fallback, "phaseId", path, allowEmpty: true);
    }

    private static void ValidateAuthoredAssignmentProfile(
        string assignmentProfileId,
        JsonElement profile,
        string path)
    {
        Object(profile, $"{path}.assignmentProfiles.{assignmentProfileId}",
            [
                "groupId", "localState", "priority", "stuckRecovery",
                "supportId", "members",
            ]);
        Identifier(profile, "groupId", path);
        Identifier(profile, "localState", path);
        Range(profile, "priority", path, 0, 1000);
        OneOf(profile, "stuckRecovery", path,
            "repath", "yield", "reflow", "regroup", "hold");
        NonEmptyString(profile, "supportId", path, allowEmpty: true);
        ValidateMemberSelection(profile.GetProperty("members"), path);
    }

    private static void ValidateAuthoredPredicate(
        string predicateId,
        JsonElement predicate,
        string path)
    {
        Object(predicate, $"{path}.predicates.{predicateId}",
            ["fact", "operator"],
            [
                "value", "valueParameter", "subject", "zone",
                "freshnessTicks",
            ]);
        string fact = RequiredString(predicate, "fact", path);
        if (!ConditionFacts.Contains(fact))
            throw Error(path, $"unknown authored condition fact '{fact}'.");
        OneOf(predicate, "operator", path,
            "at-least", "at-most", "equals", "less-than", "greater-than");
        bool hasValue = predicate.TryGetProperty("value", out _);
        bool hasParameter = predicate.TryGetProperty("valueParameter", out _);
        if (hasValue == hasParameter)
        {
            throw Error(path,
                "authored predicate must declare exactly one of 'value' or "
                + "'valueParameter'.");
        }
        if (hasValue)
            Range(predicate, "value", path, 0, 100000);
        else
            Identifier(predicate, "valueParameter", path);
        if (predicate.TryGetProperty("subject", out JsonElement subject))
            StringValue(subject, $"{path}.predicates.subject");
        if (predicate.TryGetProperty("zone", out JsonElement zone))
            StringValue(zone, $"{path}.predicates.zone");
        if (predicate.TryGetProperty("freshnessTicks", out _))
            Range(predicate, "freshnessTicks", path, 0, 1200);
    }

    private static void ValidateAuthoredManeuver(
        string maneuverId,
        JsonElement maneuver,
        string path)
    {
        Object(maneuver, $"{path}.maneuvers.{maneuverId}", ["tracks"]);
        CatalogEntry[] tracks = Catalog(
            maneuver.GetProperty("tracks"),
            $"{path}.maneuvers.{maneuverId}.tracks", 1, 8);
        foreach (CatalogEntry track in tracks)
        {
            Object(track.Value,
                $"{path}.maneuvers.{maneuverId}.tracks.{track.Id}",
                ["formationId", "movement", "assignments"]);
            Identifier(track.Value, "formationId", path);
            JsonElement movement = track.Value.GetProperty("movement");
            Object(movement, $"{path}.maneuvers.tracks.movement",
                ["kind", "target", "stuckTicks", "pace"],
                ["leadTiles", "scan"]);
            OneOf(movement, "kind", path,
                "route", "zone", "anchor", "reactor", "carrier",
                "enemy-carrier", "enemy-carrier-cutoff", "secured-core",
                "shadow-traffic", "incoming-cutoff", "hold");
            // Patrol scan (ghost doctrine v2): a body on post under this
            // movement sweeps its facing to cover the approaches instead of
            // staring down one corridor.
            if (movement.TryGetProperty("scan", out _))
                OneOf(movement, "scan", path, "sweep");
            NonEmptyString(movement, "target", path, allowEmpty: true);
            Range(movement, "stuckTicks", path, 1, 120);
            OneOf(movement, "pace", path, "slowest", "leader", "free");
            if (movement.TryGetProperty("leadTiles", out _))
                Range(movement, "leadTiles", path, 0, 8);

            CatalogEntry[] assignments = Catalog(
                track.Value.GetProperty("assignments"),
                $"{path}.maneuvers.{maneuverId}.tracks.{track.Id}.assignments",
                1, 32);
            foreach (CatalogEntry assignment in assignments)
            {
                Object(assignment.Value,
                    $"{path}.maneuvers.{maneuverId}.tracks.{track.Id}."
                    + $"assignments.{assignment.Id}",
                    [
                        "assignmentProfileId", "arrivalRadius", "completion",
                        "chaseLeash", "engagementId", "custodyId",
                        "fallbackId",
                    ], ["stance", "patienceTicks"]);
                if (assignment.Value.TryGetProperty("stance", out _))
                    OneOf(assignment.Value, "stance", path, "ambush");
                if (assignment.Value.TryGetProperty("patienceTicks", out _))
                    Range(assignment.Value, "patienceTicks", path, 2, 120);
                Identifier(assignment.Value, "assignmentProfileId", path);
                Range(assignment.Value, "arrivalRadius", path, 0, 16);
                OneOf(assignment.Value, "completion", path,
                    "leader-arrived", "cohesion-arrived", "all-arrived",
                    "continuous");
                Range(assignment.Value, "chaseLeash", path, 0, 16);
                Identifier(assignment.Value, "engagementId", path);
                NonEmptyString(
                    assignment.Value, "custodyId", path, allowEmpty: true);
                Identifier(assignment.Value, "fallbackId", path);
            }
        }
    }

    private static void ValidateAuthoredCoordination(
        JsonElement coordination,
        string path)
    {
        Object(coordination, $"{path}.coordination",
            ["initialPhase", "phases", "tasks"]);
        Identifier(coordination, "initialPhase", path);
        JsonElement[] tasks = BoundedArray(
            coordination.GetProperty("tasks"),
            $"{path}.coordination.tasks", 0, 32);
        UniqueIds(tasks, "taskId", $"{path}.coordination.tasks");
        foreach (JsonElement task in tasks)
            ValidateAuthoredTask(task, path);
        JsonElement[] phases = BoundedArray(
            coordination.GetProperty("phases"),
            $"{path}.coordination.phases", 1, 24);
        UniqueIds(phases, "phaseId", $"{path}.coordination.phases");
        foreach (JsonElement phase in phases)
        {
            Object(phase, $"{path}.coordination.phases",
                [
                    "phaseId", "minimumTicks", "maneuverId",
                    "standingOrderIds", "transitions",
                ]);
            Identifier(phase, "phaseId", path);
            Range(phase, "minimumTicks", path, 0, 1200);
            Identifier(phase, "maneuverId", path);
            JsonElement[] standingOrderIds = BoundedArray(
                phase.GetProperty("standingOrderIds"),
                $"{path}.coordination.standingOrderIds", 0, 32);
            foreach (JsonElement orderId in standingOrderIds)
                StringValue(orderId, $"{path}.coordination.standingOrderIds");
            JsonElement[] transitions = BoundedArray(
                phase.GetProperty("transitions"),
                $"{path}.coordination.transitions", 0, 32);
            foreach (JsonElement transition in transitions)
            {
                Object(transition, $"{path}.coordination.transitions",
                    [
                        "priority", "to", "cause", "minimumPolicy",
                        "stableTicks", "conditionSetId",
                    ]);
                Range(transition, "priority", path, 0, 1000);
                Identifier(transition, "to", path);
                OneOf(transition, "cause", path,
                    "success", "failure", "recovery", "reaction");
                OneOf(transition, "minimumPolicy", path,
                    "respect", "interrupt");
                Range(transition, "stableTicks", path, 1, 120);
                Identifier(transition, "conditionSetId", path);
            }
        }
    }

    private static void ValidateAuthoredTask(JsonElement task, string path)
    {
        string at = $"{path}.coordination.tasks";
        Object(task, at,
            [
                "taskId", "priority", "activation", "preemption",
                "participantLoss", "triggerStableTicks", "minimumTicks",
                "timeoutTicks", "cooldownTicks", "minimumPrimaryBodies",
                "eligiblePhases",
                "assignments", "whenConditionSetId",
                "completeConditionSetId", "failConditionSetId",
                "reintegration",
            ], [
                "minimumParticipants", "maximumParticipants",
                "completionArmConditionSetId", "completionArmMode",
                "completionReleaseMode", "cancellationMode",
            ]);
        Identifier(task, "taskId", at);
        Range(task, "priority", at, 0, 1000);
        OneOf(task, "activation", at, "rising-edge", "while-true");
        OneOf(task, "preemption", at, "never", "higher-priority");
        OneOf(task, "participantLoss", at, "abort", "continue", "replace");
        Range(task, "triggerStableTicks", at, 1, 120);
        Range(task, "minimumTicks", at, 0, 1200);
        Range(task, "timeoutTicks", at, 1, 2400);
        Range(task, "cooldownTicks", at, 0, 1200);
        Range(task, "minimumPrimaryBodies", at, 0, 8);
        int minimumParticipants = task.TryGetProperty(
            "minimumParticipants", out _)
            ? RequiredInt(task, "minimumParticipants", at)
            : 0;
        int maximumParticipants = task.TryGetProperty(
            "maximumParticipants", out _)
            ? RequiredInt(task, "maximumParticipants", at)
            : 0;
        if (minimumParticipants is < 0 or > 8
            || maximumParticipants is < 0 or > 8)
        {
            throw Error(at,
                "minimumParticipants and maximumParticipants must be 0..8.");
        }
        if (maximumParticipants > 0
            && minimumParticipants > maximumParticipants)
        {
            throw Error(at,
                "minimumParticipants cannot exceed maximumParticipants.");
        }
        foreach (JsonElement phase in BoundedArray(
                     task.GetProperty("eligiblePhases"),
                     $"{at}.eligiblePhases", 1, 24))
        {
            StringValue(phase, $"{at}.eligiblePhases");
        }
        JsonElement[] assignments = BoundedArray(
            task.GetProperty("assignments"), $"{at}.assignments", 1, 8);
        UniqueIds(assignments, "assignmentId", $"{at}.assignments");
        foreach (JsonElement assignment in assignments)
        {
            Object(assignment, $"{at}.assignments",
                [
                    "assignmentId", "orderId", "roles", "classes",
                    "minimum", "preferred", "maximum", "carrier",
                    "distance",
                ]);
            Identifier(assignment, "assignmentId", at);
            Identifier(assignment, "orderId", at);
            foreach (JsonElement role in BoundedArray(
                         assignment.GetProperty("roles"),
                         $"{at}.assignments.roles", 0, 8))
            {
                StringValue(role, $"{at}.assignments.roles");
            }
            foreach (JsonElement candidateClass in BoundedArray(
                         assignment.GetProperty("classes"),
                         $"{at}.assignments.classes", 0, 16))
            {
                StringValue(candidateClass, $"{at}.assignments.classes");
            }
            Cardinality(assignment, $"{at}.assignments", 0, 8);
            OneOf(assignment, "carrier", at, "forbid", "allow", "require");
            JsonElement distance = assignment.GetProperty("distance");
            Object(distance, $"{at}.assignments.distance", ["kind", "target"],
                ["maximum"]);
            OneOf(distance, "kind", at,
                "none", "veterancy-rank",
                "anchor", "own-reactor", "enemy-reactor",
                "visible-loose-core-in-zone");
            NonEmptyString(distance, "target", at, allowEmpty: true);
            if (distance.TryGetProperty("maximum", out _))
                Range(distance, "maximum", at, 1, 64);
        }
        NonEmptyString(task, "whenConditionSetId", at);
        NonEmptyString(task, "completeConditionSetId", at);
        if (task.TryGetProperty("completionArmConditionSetId", out _))
            NonEmptyString(task, "completionArmConditionSetId", at);
        bool hasCompletionArmMode = task.TryGetProperty(
            "completionArmMode", out _);
        bool hasCompletionReleaseMode = task.TryGetProperty(
            "completionReleaseMode", out _);
        if (hasCompletionArmMode != hasCompletionReleaseMode)
        {
            throw Error(at,
                "completionArmMode and completionReleaseMode must be authored "
                + "together.");
        }
        if (hasCompletionArmMode)
        {
            OneOf(task, "completionArmMode", at,
                "assigned-carrier", "any-friendly-carrier");
            OneOf(task, "completionReleaseMode", at,
                "assigned-carrier-loss", "armed-carrier-loss",
                "armed-carrier-retarget-or-loss");
        }
        if (task.TryGetProperty("cancellationMode", out _))
            OneOf(task, "cancellationMode", at, "alternate-carrier");
        NonEmptyString(task, "failConditionSetId", at, allowEmpty: true);
        JsonElement reintegration = task.GetProperty("reintegration");
        Object(reintegration, $"{at}.reintegration",
            [
                "mode", "orderIds", "completeConditionSetId",
                "timeoutTicks",
            ]);
        OneOf(reintegration, "mode", at, "primary-order", "release-orders");
        foreach (JsonElement orderId in BoundedArray(
                     reintegration.GetProperty("orderIds"),
                     $"{at}.reintegration.orderIds", 0, 16))
        {
            StringValue(orderId, $"{at}.reintegration.orderIds");
        }
        NonEmptyString(
            reintegration,
            "completeConditionSetId",
            at,
            allowEmpty: true);
        Range(reintegration, "timeoutTicks", at, 0, 1200);
    }

    private static void ValidateLayoutReferences(
        JsonElement playbook,
        JsonElement layout,
        string path)
    {
        HashSet<string> formations = playbook.GetProperty("formations")
            .EnumerateArray()
            .Select(value => value.GetProperty("formationId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> zones = layout.GetProperty("zones").EnumerateArray()
            .Select(value => value.GetProperty("zoneId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> routes = layout.GetProperty("routes").EnumerateArray()
            .Select(value => value.GetProperty("routeId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> anchors = layout.GetProperty("anchors").EnumerateArray()
            .Select(value => value.GetProperty("anchorId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (JsonElement binding in layout.GetProperty("bindings")
                     .EnumerateArray())
        {
            if (!binding.TryGetProperty(
                    "formationAliases", out JsonElement aliases))
            {
                continue;
            }
            foreach (JsonProperty alias in aliases.EnumerateObject())
            {
                string target = alias.Value.GetString()!;
                if (!formations.Contains(alias.Name)
                    || !formations.Contains(target))
                {
                    throw Error(path,
                        $"formation alias '{alias.Name}' -> '{target}' "
                        + "references an unknown formation.");
                }
            }
        }
        foreach (JsonElement order in playbook.GetProperty("orders")
                     .EnumerateArray())
        {
            string orderId = order.GetProperty("orderId").GetString()!;
            JsonElement movement = order.GetProperty("movement");
            string kind = movement.GetProperty("kind").GetString()!;
            string target = movement.GetProperty("target").GetString()!;
            bool valid = kind switch
            {
                "route" => routes.Contains(target),
                "zone" => zones.Contains(target),
                "anchor" or "carrier" or "enemy-carrier"
                    or "enemy-carrier-cutoff" or "secured-core"
                    or "incoming-cutoff" =>
                        anchors.Contains(target),
                // Traffic shadowing computes its own corridor waypoints
                // from the contract; the target names the fallback anchor.
                "shadow-traffic" => anchors.Contains(target),
                "reactor" => target is "own" or "enemy",
                "hold" => target.Length == 0,
                _ => false,
            };
            if (!valid)
                throw Error(path,
                    $"order '{orderId}' has invalid {kind} movement target "
                    + $"'{target}'.");
        }
        foreach (JsonElement condition in Descendants(playbook)
                     .Where(value => value.ValueKind == JsonValueKind.Object
                         && value.TryGetProperty("fact", out _)
                         && value.TryGetProperty("zone", out JsonElement zone)
                         && zone.ValueKind == JsonValueKind.String
                         && zone.GetString()!.Length > 0))
        {
            string zone = condition.GetProperty("zone").GetString()!;
            if (!zones.Contains(zone))
                throw Error(path,
                    $"condition references unknown tactical zone '{zone}'.");
        }
        foreach (JsonElement custody in playbook.GetProperty("custodyPolicies")
                     .EnumerateArray())
        {
            if (!custody.TryGetProperty(
                    "emergencyRecoveryZones", out JsonElement recoveryZones))
                continue;
            foreach (string zone in recoveryZones.EnumerateArray()
                         .Select(value => value.GetString()!))
            {
                if (!zones.Contains(zone))
                {
                    throw Error(path,
                        "custody policy references unknown emergency recovery "
                        + $"zone '{zone}'.");
                }
            }
            if (custody.TryGetProperty(
                    "emergencyDisplacementTarget",
                    out JsonElement displacementTarget)
                && !anchors.Contains(displacementTarget.GetString()!))
            {
                throw Error(path,
                    "custody policy references unknown emergency "
                    + "displacement anchor "
                    + $"'{displacementTarget.GetString()}'.");
            }
        }
        foreach (JsonElement task in playbook.GetProperty("coordination")
                     .GetProperty("tasks").EnumerateArray())
        foreach (JsonElement assignment in task.GetProperty("assignments")
                     .EnumerateArray())
        {
            JsonElement distance = assignment.GetProperty("distance");
            if (string.Equals(
                    distance.GetProperty("kind").GetString(),
                    "anchor",
                    StringComparison.Ordinal)
                && !anchors.Contains(
                    distance.GetProperty("target").GetString()!))
            {
                throw Error(path,
                    $"task '{task.GetProperty("taskId").GetString()}' "
                    + "references unknown selection anchor "
                    + $"'{distance.GetProperty("target").GetString()}'.");
            }
            if (string.Equals(
                    distance.GetProperty("kind").GetString(),
                    "visible-loose-core-in-zone",
                    StringComparison.Ordinal)
                && !zones.Contains(
                    distance.GetProperty("target").GetString()!))
            {
                throw Error(path,
                    $"task '{task.GetProperty("taskId").GetString()}' "
                    + "references unknown selection zone "
                    + $"'{distance.GetProperty("target").GetString()}'.");
            }
        }
    }

    private static IEnumerable<JsonElement> Descendants(JsonElement value)
    {
        yield return value;
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            foreach (JsonElement descendant in Descendants(property.Value))
                yield return descendant;
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            foreach (JsonElement descendant in Descendants(item))
                yield return descendant;
        }
    }

    private static byte[] Encode(
        string playbookSha256,
        string layoutSha256,
        byte[] canonicalPlaybook,
        byte[] canonicalLayout)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(EnvelopeMagic);
        writer.Write(PlaybookSchema);
        writer.Write(playbookSha256);
        writer.Write(layoutSha256);
        writer.Write(canonicalPlaybook.Length);
        writer.Write(canonicalPlaybook);
        writer.Write(canonicalLayout.Length);
        writer.Write(canonicalLayout);
        writer.Flush();
        if (stream.Length > MaximumLinkedBytes)
        {
            throw new InvalidDataException(
                $"Compiled tactical playbook is {stream.Length} bytes; "
                + $"the runtime limit is {MaximumLinkedBytes} bytes.");
        }
        return stream.ToArray();
    }

    private static void ValidatePlaybook(JsonElement root, string path)
    {
        Object(root, path,
            [
                "schema", "playbookId", "auditStatus", "composition",
                "layout", "perspective", "memory", "arbitration", "roles",
                "groups", "formations", "engagements", "supportPolicies",
                "custodyPolicies", "orders", "coordination",
            ]);
        Exact(root, "schema", PlaybookSchema, path);
        Identifier(root, "playbookId", path);
        Exact(root, "perspective", "team-relative", path);
        ValidateAuditStatus(root.GetProperty("auditStatus"), path);
        ValidateComposition(root.GetProperty("composition"), path);

        JsonElement layout = root.GetProperty("layout");
        Object(layout, $"{path}.layout", ["path", "sha256"]);
        NonEmptyString(layout, "path", $"{path}.layout");
        Hash(layout, "sha256", $"{path}.layout");

        ValidateMemory(root.GetProperty("memory"), path);
        ValidateArbitration(root.GetProperty("arbitration"), path);

        JsonElement[] roles = BoundedArray(
            root.GetProperty("roles"), $"{path}.roles", 1, 16);
        HashSet<string> roleIds = UniqueIds(roles, "roleId", $"{path}.roles");
        Dictionary<string, int> roleMaximums = roles.ToDictionary(
            role => role.GetProperty("roleId").GetString()!,
            role => role.GetProperty("maximum").GetInt32(),
            StringComparer.Ordinal);
        Dictionary<string, int> roleMinimums = roles.ToDictionary(
            role => role.GetProperty("roleId").GetString()!,
            role => role.GetProperty("minimum").GetInt32(),
            StringComparer.Ordinal);
        Dictionary<string, int> rolePreferred = roles.ToDictionary(
            role => role.GetProperty("roleId").GetString()!,
            role => role.GetProperty("preferred").GetInt32(),
            StringComparer.Ordinal);
        foreach (JsonElement role in roles)
            ValidateRole(role, roleIds, path);

        JsonElement[] orders = BoundedArray(
            root.GetProperty("orders"), $"{path}.orders", 1, 64);
        HashSet<string> orderIds = UniqueIds(
            orders, "orderId", $"{path}.orders");
        HashSet<string> phaseIds = UniqueIds(
            BoundedArray(
                root.GetProperty("coordination").GetProperty("phases"),
                $"{path}.coordination.phases", 1, 24),
            "phaseId",
            $"{path}.coordination.phases");

        JsonElement[] groups = BoundedArray(
            root.GetProperty("groups"), $"{path}.groups", 1, 8);
        HashSet<string> groupIds = UniqueIds(
            groups, "groupId", $"{path}.groups");
        foreach (JsonElement group in groups)
            ValidateGroup(group, roleIds, groupIds, orderIds, path);
        foreach (JsonElement role in roles)
        {
            string roleId = role.GetProperty("roleId").GetString()!;
            JsonElement[] owners = groups.Where(group => group
                    .GetProperty("roleIds").EnumerateArray()
                    .Any(value => string.Equals(
                        value.GetString(), roleId, StringComparison.Ordinal)))
                .ToArray();
            if (owners.Length != 1)
            {
                throw Error(path,
                    $"role '{roleId}' must belong to exactly one group.");
            }
            string roleCasualty = role.GetProperty("deathPolicy").GetString()!;
            string groupCasualty = owners[0].GetProperty("membership")
                .GetProperty("casualty").GetString()!;
            string expected = roleCasualty switch
            {
                "hold-vacancy" => "hold-vacancy",
                "promote-best" => "promote-role",
                "rebalance" => "rebalance",
                _ => throw Error(path,
                    $"role '{roleId}' has unknown casualty policy."),
            };
            if (!string.Equals(
                    groupCasualty, expected, StringComparison.Ordinal))
            {
                throw Error(path,
                    $"role '{roleId}' deathPolicy '{roleCasualty}' conflicts "
                    + $"with group casualty '{groupCasualty}'.");
            }
        }
        if (groups.Sum(group => group.GetProperty("maximum").GetInt32()) < 8)
            throw Error(path,
                "group maximum cardinalities cannot own all eight bodies.");
        foreach (JsonElement group in groups)
        {
            string groupId = group.GetProperty("groupId").GetString()!;
            string[] ownedRoles = group.GetProperty("roleIds")
                .EnumerateArray().Select(value => value.GetString()!).ToArray();
            int minimumRoles = ownedRoles.Sum(role => roleMinimums[role]);
            int preferredRoles = ownedRoles.Sum(role => rolePreferred[role]);
            int maximumRoles = ownedRoles.Sum(role => roleMaximums[role]);
            int groupPreferred = group.GetProperty("preferred").GetInt32();
            int groupMaximum = group.GetProperty("maximum").GetInt32();
            if (minimumRoles > groupPreferred)
                throw Error(path,
                    $"group '{groupId}' preferred cardinality cannot satisfy "
                    + "its roles' minimum cardinalities.");
            if (preferredRoles > groupMaximum)
                throw Error(path,
                    $"group '{groupId}' maximum cardinality cannot satisfy "
                    + "its roles' preferred cardinalities.");
            if (groupPreferred > maximumRoles || groupMaximum > maximumRoles)
                throw Error(path,
                    $"group '{groupId}' cardinality exceeds the capacity of "
                    + "its owned roles.");
        }

        JsonElement[] formations = BoundedArray(
            root.GetProperty("formations"), $"{path}.formations", 1, 24);
        HashSet<string> formationIds = UniqueIds(
            formations, "formationId", $"{path}.formations");
        foreach (JsonElement formation in formations)
            ValidateFormation(
                formation, roleIds, roleMaximums, path);

        JsonElement[] engagements = BoundedArray(
            root.GetProperty("engagements"), $"{path}.engagements", 1, 24);
        HashSet<string> engagementIds = UniqueIds(
            engagements, "engagementId", $"{path}.engagements");
        foreach (JsonElement engagement in engagements)
            ValidateEngagement(engagement, roleIds, groupIds, orderIds, path);

        JsonElement[] supports = BoundedArray(
            root.GetProperty("supportPolicies"),
            $"{path}.supportPolicies", 1, 16);
        HashSet<string> supportIds = UniqueIds(
            supports, "supportId", $"{path}.supportPolicies");
        foreach (JsonElement support in supports)
            ValidateSupport(support, roleIds, path);

        JsonElement[] custody = BoundedArray(
            root.GetProperty("custodyPolicies"),
            $"{path}.custodyPolicies", 1, 16);
        HashSet<string> custodyIds = UniqueIds(
            custody, "custodyId", $"{path}.custodyPolicies");
        foreach (JsonElement policy in custody)
            ValidateCustody(policy, roleIds, groupIds, orderIds, path);
        HashSet<string> custodyCarrierRoles = custody
            .SelectMany(policy => policy
                .GetProperty("authorizedCarrierRoles")
                .EnumerateArray())
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (JsonElement role in roles)
        {
            string roleId = role.GetProperty("roleId").GetString()!;
            string preference = role.GetProperty("carrierPreference")
                .GetString()!;
            if (string.Equals(preference, "forbid", StringComparison.Ordinal)
                && custodyCarrierRoles.Contains(roleId))
            {
                throw Error(path,
                    $"role '{roleId}' forbids Core custody but is an "
                    + "authorized carrier.");
            }
            if (string.Equals(preference, "require", StringComparison.Ordinal)
                && !custodyCarrierRoles.Contains(roleId))
            {
                throw Error(path,
                    $"role '{roleId}' requires Core custody but no custody "
                    + "policy authorizes it.");
            }
        }
        foreach (JsonElement order in orders)
        {
            ValidateOrder(order, groupIds, formationIds, engagementIds,
                supportIds, custodyIds, phaseIds, path);
            string formationId = order.GetProperty("formationId").GetString()!;
            string formationPace = formations.Single(formation =>
                    string.Equals(
                        formation.GetProperty("formationId").GetString(),
                        formationId,
                        StringComparison.Ordinal))
                .GetProperty("cohesion").GetProperty("pace").GetString()!;
            string movementPace = order.GetProperty("movement")
                .GetProperty("pace").GetString()!;
            if (!string.Equals(
                    formationPace, movementPace, StringComparison.Ordinal))
            {
                throw Error(path,
                    $"order '{order.GetProperty("orderId").GetString()}' "
                    + $"pace '{movementPace}' conflicts with formation "
                    + $"'{formationId}' pace '{formationPace}'.");
            }
        }
        ValidateEscortOrders(orders, custody, path);

        ValidateCoordination(
            root.GetProperty("coordination"),
            groupIds,
            roleIds,
            orderIds,
            groups,
            orders,
            path);
        ValidatePhaseOrderCoverage(
            root.GetProperty("coordination"), groups, orders, path);
    }

    private static void ValidatePhaseOrderCoverage(
        JsonElement coordination,
        IReadOnlyCollection<JsonElement> groups,
        IReadOnlyCollection<JsonElement> orders,
        string path)
    {
        Dictionary<string, JsonElement> ordersById = orders.ToDictionary(
            order => order.GetProperty("orderId").GetString()!,
            StringComparer.Ordinal);
        var groupStates = new Dictionary<string, string[]>(
            StringComparer.Ordinal);
        var groupRoles = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);
        foreach (JsonElement group in groups)
        {
            string groupId = group.GetProperty("groupId").GetString()!;
            string[] states = group.GetProperty("localStateMachine")
                .GetProperty("states").EnumerateArray()
                .Select(state => state.GetProperty("stateId").GetString()!)
                .ToArray();
            groupStates[groupId] = states;
            groupRoles[groupId] = group.GetProperty("roleIds")
                .EnumerateArray()
                .Select(role => role.GetString()!)
                .ToHashSet(StringComparer.Ordinal);
        }
        foreach (JsonElement order in orders)
        {
            string orderId = order.GetProperty("orderId").GetString()!;
            string groupId = order.GetProperty("groupId").GetString()!;
            string localState = order.GetProperty("localState").GetString()!;
            if (!string.Equals(localState, "joining", StringComparison.Ordinal)
                && !groupStates[groupId].Contains(
                    localState, StringComparer.Ordinal))
            {
                throw Error(path,
                    $"order '{orderId}' references unknown local state "
                    + $"'{localState}' in group '{groupId}'.");
            }
            JsonElement members = order.GetProperty("members");
            if (string.Equals(
                    members.GetProperty("kind").GetString(),
                    "take",
                    StringComparison.Ordinal))
            {
                string[] unknownRoles = members.GetProperty("roles")
                    .EnumerateArray()
                    .Select(role => role.GetString()!)
                    .Where(role => !groupRoles[groupId].Contains(role))
                    .ToArray();
                if (unknownRoles.Length > 0)
                {
                    throw Error(path,
                        $"order '{orderId}' selects roles not owned by "
                        + $"group '{groupId}': "
                        + string.Join(",", unknownRoles));
                }
            }
        }
        foreach (JsonElement phase in coordination.GetProperty("phases")
                     .EnumerateArray())
        {
            string phaseId = phase.GetProperty("phaseId").GetString()!;
            JsonElement[] phaseOrders = phase.GetProperty("orderIds")
                .EnumerateArray()
                .Select(value => ordersById[value.GetString()!])
                .ToArray();
            foreach ((string groupId, string[] states) in groupStates)
            foreach (string state in states)
            {
                JsonElement[] matching = phaseOrders.Where(order => string.Equals(
                        order.GetProperty("groupId").GetString(),
                        groupId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        order.GetProperty("localState").GetString(),
                        state,
                        StringComparison.Ordinal)).ToArray();
                if (matching.Length == 0)
                {
                    throw Error(path,
                        $"phase '{phaseId}' has no order for group "
                        + $"'{groupId}' local state '{state}'.");
                }
                string[] kinds = matching.Select(order => order
                        .GetProperty("members").GetProperty("kind")
                        .GetString()!)
                    .ToArray();
                if (matching.Length == 1
                    && string.Equals(kinds[0], "all", StringComparison.Ordinal))
                    continue;
                if (kinds.Contains("all", StringComparer.Ordinal)
                    || kinds.Count(kind => string.Equals(
                        kind, "remainder", StringComparison.Ordinal)) != 1)
                {
                    throw Error(path,
                        $"phase '{phaseId}' split orders for group "
                        + $"'{groupId}' local state '{state}' require one or "
                        + "more take selections followed by exactly one "
                        + "remainder selection.");
                }
                int[] priorities = matching.Select(order => order
                    .GetProperty("priority").GetInt32()).ToArray();
                if (priorities.Distinct().Count() != priorities.Length)
                {
                    throw Error(path,
                        $"phase '{phaseId}' split orders for group "
                        + $"'{groupId}' local state '{state}' require unique "
                        + "selection priorities.");
                }
                JsonElement remainder = matching.Single(order => string.Equals(
                    order.GetProperty("members").GetProperty("kind")
                        .GetString(),
                    "remainder",
                    StringComparison.Ordinal));
                if (remainder.GetProperty("priority").GetInt32()
                    != priorities.Max())
                {
                    throw Error(path,
                        $"phase '{phaseId}' split orders for group "
                        + $"'{groupId}' local state '{state}' must put the "
                        + "remainder selection last by priority.");
                }
            }
        }
    }

    private static void ValidateEscortOrders(
        IReadOnlyCollection<JsonElement> orders,
        IReadOnlyCollection<JsonElement> custodyPolicies,
        string path)
    {
        Dictionary<string, HashSet<string>> escortGroups = custodyPolicies
            .ToDictionary(
                policy => policy.GetProperty("custodyId").GetString()!,
                policy => policy.GetProperty("escortGroups")
                    .EnumerateArray()
                    .Select(value => value.GetString()!)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        foreach (JsonElement order in orders.Where(value => string.Equals(
                     value.GetProperty("movement").GetProperty("kind")
                         .GetString(),
                     "carrier",
                     StringComparison.Ordinal)))
        {
            string orderId = order.GetProperty("orderId").GetString()!;
            if (!order.TryGetProperty("custodyId", out JsonElement custody)
                || string.IsNullOrEmpty(custody.GetString()))
            {
                throw Error(path,
                    $"carrier escort order '{orderId}' needs custodyId.");
            }
            string custodyId = custody.GetString()!;
            string groupId = order.GetProperty("groupId").GetString()!;
            if (!escortGroups[custodyId].Contains(groupId))
            {
                throw Error(path,
                    $"carrier escort order '{orderId}' group '{groupId}' "
                    + $"is not authorized by custody '{custodyId}'.");
            }
        }
        foreach (JsonElement order in orders.Where(value => string.Equals(
                     value.GetProperty("movement").GetProperty("kind")
                         .GetString(),
                     "secured-core",
                     StringComparison.Ordinal)))
        {
            string orderId = order.GetProperty("orderId").GetString()!;
            if (!order.TryGetProperty("custodyId", out JsonElement custody)
                || string.IsNullOrEmpty(custody.GetString()))
            {
                throw Error(path,
                    $"secured-core guard order '{orderId}' needs custodyId.");
            }
        }
    }

    private static void ValidateAuditStatus(JsonElement value, string path)
    {
        Object(value, $"{path}.auditStatus",
            ["provisionalEvaluationOnly", "playerFacingProductSchema"]);
        if (!RequiredBool(
                value, "provisionalEvaluationOnly", $"{path}.auditStatus")
            || RequiredBool(
                value, "playerFacingProductSchema", $"{path}.auditStatus"))
        {
            throw Error(path,
                "tactical playbook v1 must remain provisional and non-product.");
        }
    }

    private static void ValidateComposition(JsonElement value, string path)
    {
        JsonElement[] entries = BoundedArray(
            value, $"{path}.composition", 8, 8);
        string[] classes = entries.Select((entry, index) =>
            StringValue(entry, $"{path}.composition[{index}]")).ToArray();
        if (classes.Any(classId => !KnownClasses.Contains(classId))
            || classes.GroupBy(value => value, StringComparer.Ordinal)
                .Any(group => group.Count() > 2))
        {
            throw Error(path,
                "composition must use launch classes under the two-copy cap.");
        }
    }

    private static void ValidateMemory(JsonElement value, string path)
    {
        const string at = "memory";
        Object(value, $"{path}.{at}",
            [
                "enemyUnavailableTicks", "lastSeenEnemyTicks",
                "securedCoreTicks", "objectiveProgressTicks",
                "formationStableTicks",
            ]);
        Range(value, "enemyUnavailableTicks", path, 1, 600);
        Range(value, "lastSeenEnemyTicks", path, 1, 600);
        Range(value, "securedCoreTicks", path, 1, 600);
        Range(value, "objectiveProgressTicks", path, 1, 600);
        Range(value, "formationStableTicks", path, 1, 120);
    }

    private static void ValidateArbitration(JsonElement value, string path)
    {
        Object(value, $"{path}.arbitration",
            ["mode", "channels"]);
        Exact(value, "mode", "first-legal", $"{path}.arbitration");
        string[] channels = StringArray(
            value.GetProperty("channels"), $"{path}.arbitration.channels",
            1, 16);
        string[] required =
        [
            "custody-emergency", "self-preservation", "repair", "signature",
            "focus-fire", "movement", "facing", "hold",
        ];
        if (channels.Length != required.Length
            || channels.Distinct(StringComparer.Ordinal).Count()
                != channels.Length
            || required.Except(channels, StringComparer.Ordinal).Any())
        {
            throw Error(path,
                "arbitration.channels must declare each executable channel once.");
        }
    }

    private static void ValidateRole(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        string path)
    {
        string at = $"{path}.roles";
        Object(value, at,
            [
                "roleId", "candidateClasses", "minimum", "preferred",
                "maximum", "carrierPreference", "deathPolicy",
                "respawnPolicy", "overflowRoleId",
            ],
            ["build"]);
        string roleId = Identifier(value, "roleId", at);
        // Veterancy build order (owner direction 2026-08-06): the playbook
        // owns skill-point selection per role. Optional; a role without one
        // keeps the mind's default. Points past the list repeat its last
        // entry.
        if (value.TryGetProperty("build", out JsonElement build))
        {
            string[] tracks = StringArray(
                build, $"{at}.{roleId}.build", 1, 8);
            string[] known = ["damage", "vision", "reach", "vitality"];
            if (tracks.Any(track => !known.Contains(track)))
                throw Error(at, $"role '{roleId}' has unknown build tracks.");
        }
        string[] candidates = StringArray(
            value.GetProperty("candidateClasses"),
            $"{at}.{roleId}.candidateClasses", 1, 16);
        if (candidates.Any(candidate => !KnownClasses.Contains(candidate))
            || candidates.Distinct(StringComparer.Ordinal).Count()
                != candidates.Length)
            throw Error(at, $"role '{roleId}' has invalid candidate classes.");
        Cardinality(value, at, 0, 8);
        OneOf(value, "carrierPreference", at,
            "forbid", "allow", "prefer", "require");
        OneOf(value, "deathPolicy", at,
            "hold-vacancy", "promote-best", "rebalance");
        OneOf(value, "respawnPolicy", at,
            "resume", "rejoin", "rally", "replace");
        string overflow = RequiredString(value, "overflowRoleId", at);
        if (overflow.Length > 0 && !roleIds.Contains(overflow))
            throw Error(at, $"role '{roleId}' has unknown overflow role '{overflow}'.");
    }

    private static void ValidateGroup(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> orderIds,
        string path)
    {
        string at = $"{path}.groups";
        Object(value, at,
            [
                "groupId", "roleIds", "minimum", "preferred", "maximum",
                "membership", "localStateMachine",
            ]);
        string id = Identifier(value, "groupId", at);
        References(value.GetProperty("roleIds"), roleIds,
            $"{at}.{id}.roleIds", 1, 16);
        Cardinality(value, at, 0, 8);
        JsonElement membership = value.GetProperty("membership");
        Object(membership, $"{at}.{id}.membership",
            ["persistence", "casualty", "preemption", "overflow"]);
        OneOf(membership, "persistence", at, "stable-slot", "best-fit");
        OneOf(membership, "casualty", at,
            "hold-vacancy", "rebalance", "promote-role");
        OneOf(membership, "preemption", at,
            "never", "higher-priority", "phase-boundary");
        OneOf(membership, "overflow", at,
            "unassigned", "lowest-count", "declared-role");
        ValidateLocalStateMachine(
            value.GetProperty("localStateMachine"), groupIds, roleIds,
            orderIds, at);
    }

    private static void ValidateLocalStateMachine(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> orderIds,
        string path)
    {
        Object(value, $"{path}.localStateMachine",
            ["initialState", "states"]);
        string initial = Identifier(
            value, "initialState", $"{path}.localStateMachine");
        JsonElement[] states = BoundedArray(
            value.GetProperty("states"), $"{path}.localStateMachine.states",
            1, 16);
        HashSet<string> ids = UniqueIds(
            states, "stateId", $"{path}.localStateMachine.states");
        if (!ids.Contains(initial))
            throw Error(path, $"unknown initial local state '{initial}'.");
        foreach (JsonElement state in states)
        {
            Object(state, $"{path}.localStateMachine.states",
                ["stateId", "minimumTicks", "transitions"]);
            Range(state, "minimumTicks", path, 0, 1200);
            ValidateTransitions(
                state.GetProperty("transitions"), ids, groupIds, roleIds,
                orderIds, path);
        }
    }

    private static void ValidateFormation(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        IReadOnlyDictionary<string, int> roleMaximums,
        string path)
    {
        string at = $"{path}.formations";
        Object(value, at,
            [
                "formationId", "shape", "orientation", "spacing",
                "placements", "cohesion", "reflow",
            ]);
        string id = Identifier(value, "formationId", at);
        // "swarm" (DECISIONS #212): no placement slots at all - every member
        // heads for the authored target and tile reservations do the spacing.
        // Slot contention was the dance factory; a swarm has nothing to
        // contend.
        OneOf(value, "shape", at,
            "line", "column", "wedge", "arc", "ring", "escort", "custom",
            "swarm");
        OneOf(value, "orientation", at,
            "route", "enemy-reactor", "own-reactor", "focus-target", "fixed");
        JsonElement spacing = value.GetProperty("spacing");
        Object(spacing, $"{at}.{id}.spacing",
            ["metric", "minimum", "preferred", "maximum"]);
        Exact(spacing, "metric", "chebyshev", $"{at}.{id}.spacing");
        Cardinality(spacing, $"{at}.{id}.spacing", 0, 8);

        JsonElement[] placements = BoundedArray(
            value.GetProperty("placements"), $"{at}.{id}.placements", 1, 16);
        foreach (JsonElement placement in placements)
        {
            Object(placement, $"{at}.{id}.placements",
                ["roleId", "sector", "order", "offset"], ["facing"]);
            Reference(placement, "roleId", roleIds, at);
            OneOf(placement, "sector", at,
                "front", "rear", "left", "right", "centre", "any");
            Range(placement, "order", at, 0, 15);
            Point(placement.GetProperty("offset"), $"{at}.{id}.offset", -8, 8);
            if (placement.TryGetProperty("facing", out _))
            {
                OneOf(placement, "facing", at,
                    "route", "enemy-reactor", "own-reactor",
                    "focus-target", "fixed", "watch-own-reactor",
                    "watch-enemy-reactor");
            }
        }
        foreach (string roleId in roleIds)
        {
            int count = placements.Count(placement => string.Equals(
                placement.GetProperty("roleId").GetString(),
                roleId,
                StringComparison.Ordinal));
            if (count < roleMaximums[roleId])
            {
                throw Error(at,
                    $"formation '{id}' needs {roleMaximums[roleId]} "
                    + $"placement slots for role '{roleId}', found {count}.");
            }
        }
        int distinctOffsets = placements
            .Select(placement => string.Join(
                ",", placement.GetProperty("offset").EnumerateArray()
                    .Select(coordinate => coordinate.GetInt32())))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctOffsets != placements.Length)
            throw Error(at, $"formation '{id}' has overlapping slots.");
        JsonElement cohesion = value.GetProperty("cohesion");
        Object(cohesion, $"{at}.{id}.cohesion",
            ["arrivalRatioPercent", "breakRatioPercent", "breakTicks", "reformTicks", "pace"]);
        Range(cohesion, "arrivalRatioPercent", at, 1, 100);
        Range(cohesion, "breakRatioPercent", at, 0, 100);
        Range(cohesion, "breakTicks", at, 1, 120);
        Range(cohesion, "reformTicks", at, 1, 120);
        OneOf(cohesion, "pace", at, "slowest", "leader", "free");
        JsonElement reflow = value.GetProperty("reflow");
        Object(reflow, $"{at}.{id}.reflow",
            ["blockedSlot", "vacancy", "searchRadius", "medicSeparation"]);
        OneOf(reflow, "blockedSlot", at,
            "nearest-legal", "rotate-shape", "hold");
        OneOf(reflow, "vacancy", at,
            "compress", "preserve", "rebalance-role");
        Range(reflow, "searchRadius", at, 0, 8);
        Range(reflow, "medicSeparation", at, 0, 8);
    }

    private static void ValidateEngagement(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> orderIds,
        string path)
    {
        string at = $"{path}.engagements";
        Object(value, at,
            [
                "engagementId", "participants", "targetPriorities",
                "tieBreakers", "coordinationScope", "lockTicks",
                "lockPreemption",
                "maximumAttackersPerTarget",
                "overkillDamage", "chaseLeash", "aimPreparation",
                "signatureCoordination",
                "release", "selfDefense",
            ],
            // dodgeCoverage and posture are DELETED from the schema
            // (SPEC-GHOST-DOCTRINE-V3): the engine's declared-strike wedge
            // owns dodge judgment and the kite cadence is gone. They stay
            // accepted-and-inert so frozen clean-slate sheets keep
            // compiling byte-identically.
            ["holdFire", "posture", "isolation", "commit", "dodgeCoverage"]);
        string id = Identifier(value, "engagementId", at);
        // Skirmish posture (owner direction 2026-08-06): participants fire
        // when their weapon is ready and back away from close threats on the
        // cooldown ticks, keeping the front arc on the enemy - the kiting
        // discipline the disruption doctrine needs. Committed is the
        // historical default.
        if (value.TryGetProperty("posture", out _))
            OneOf(value, "posture", at, "committed", "skirmish");
        // Assassin discipline: targets with an ally inside the support
        // range are not acquirable by this engagement's participants.
        if (value.TryGetProperty("isolation", out JsonElement isolation))
        {
            Object(isolation, $"{at}.{id}.isolation", ["supportRange"]);
            Range(isolation, "supportRange", at, 1, 8);
        }
        // Commit discipline (owner design 2026-08, ghost doctrine v2): the
        // sheet owns when its participants pick a fight, how far they chase,
        // and where they break off to. Every predicate reads from the
        // contract and the mind's remembered-enemy picture; the executor
        // keeps only the verbs.
        if (value.TryGetProperty("commit", out JsonElement commit))
        {
            Object(commit, $"{at}.{id}.commit",
                ["engageWhen"],
                ["awareness", "chase", "disengageWhen", "roles", "approach"]);
            // Role scoping: the discipline gates only the named roles
            // (a lone hunter's discretion must not become the escort
            // pack's rules of engagement - the ab60 lesson).
            if (commit.TryGetProperty("roles", out JsonElement commitRoles))
            {
                References(commitRoles, roleIds,
                    $"{at}.{id}.commit.roles", 1, 16);
            }
            if (commit.TryGetProperty("awareness", out JsonElement awareness))
            {
                Object(awareness, $"{at}.{id}.commit.awareness",
                    ["radius", "memoryTicks"]);
                Range(awareness, "radius", at, 2, 16);
                Range(awareness, "memoryTicks", at, 1, 120);
            }
            JsonElement engageWhen = commit.GetProperty("engageWhen");
            Object(engageWhen, $"{at}.{id}.commit.engageWhen",
                [], ["maxThreats", "killWithinTicks"]);
            if (engageWhen.TryGetProperty("maxThreats", out _))
                Range(engageWhen, "maxThreats", at, 1, 8);
            if (engageWhen.TryGetProperty("killWithinTicks", out _))
                Range(engageWhen, "killWithinTicks", at, 1, 60);
            if (commit.TryGetProperty("chase", out JsonElement chase))
            {
                Object(chase, $"{at}.{id}.commit.chase",
                    [], ["leash", "onlyCatchable", "executeBelowHealth"]);
                if (chase.TryGetProperty("leash", out _))
                    Range(chase, "leash", at, 1, 24);
                if (chase.TryGetProperty("onlyCatchable", out JsonElement oc)
                    && oc.ValueKind is not JsonValueKind.True
                        and not JsonValueKind.False)
                {
                    throw Error($"{at}.{id}.commit.chase",
                        "'onlyCatchable' must be a boolean.");
                }
                if (chase.TryGetProperty("executeBelowHealth", out _))
                    Range(chase, "executeBelowHealth", at, 1, 8);
            }
            if (commit.TryGetProperty(
                    "disengageWhen", out JsonElement disengage))
            {
                Object(disengage, $"{at}.{id}.commit.disengageWhen",
                    ["threats"], ["withdrawTo", "recoverTicks"]);
                Range(disengage, "threats", at, 1, 8);
                if (disengage.TryGetProperty("recoverTicks", out _))
                    Range(disengage, "recoverTicks", at, 4, 120);
                if (disengage.TryGetProperty("withdrawTo", out _))
                    Identifier(disengage, "withdrawTo", at);
            }
            // Approach discipline (ghost doctrine v3): suppress frontal
            // declares and maneuver to the target's blind rear inside a
            // tick budget, then either strike anyway or break off.
            if (commit.TryGetProperty("approach", out JsonElement approach))
            {
                Object(approach, $"{at}.{id}.commit.approach",
                    ["from", "positionTicks", "else"]);
                OneOf(approach, "from", at, "behind");
                Range(approach, "positionTicks", at, 1, 64);
                OneOf(approach, "else", at, "strike", "breakOff");
            }
        }
        if (value.TryGetProperty("holdFire", out JsonElement holdFire))
        {
            Object(holdFire, $"{at}.{id}.holdFire",
                ["withinDistance"], ["releaseAll"]);
            Range(holdFire, "withinDistance", at, 1, 12);
            if (holdFire.TryGetProperty(
                    "releaseAll", out JsonElement holdFireRelease))
            {
                ValidateConditionGroups(
                    holdFireRelease, groupIds, roleIds, orderIds,
                    $"{at}.{id}.holdFire.releaseAll", 1, 8);
            }
        }
        References(value.GetProperty("participants"), roleIds,
            $"{at}.{id}.participants", 1, 16);
        PriorityArray(value.GetProperty("targetPriorities"), at, 1, 16,
            "enemy-carrier", "lowest-health", "closest-to-anchor",
            "closest-to-reactor", "highest-threat", "fresh-respawn");
        OneOfArray(value.GetProperty("tieBreakers"), at, 1, 8,
            "health", "distance", "enemy-reactor-distance",
            "own-reactor-distance", "unit-id", "life-id", "position");
        OneOf(value, "coordinationScope", at,
            "order-group", "shared-policy");
        Range(value, "lockTicks", at, 0, 120);
        OneOf(value, "lockPreemption", at,
            "never", "higher-priority", "urgent-carrier");
        Range(value, "maximumAttackersPerTarget", at, 1, 8);
        Range(value, "overkillDamage", at, 0, 1000);
        Range(value, "chaseLeash", at, 0, 16);
        OneOf(value, "aimPreparation", at,
            "current-cone-only", "rotate-to-engage");
        OneOf(value, "signatureCoordination", at,
            "none", "control-first", "damage-first", "support-first");
        if (value.TryGetProperty("dodgeCoverage", out JsonElement dodge))
        {
            Object(dodge, $"{at}.{id}.dodgeCoverage",
                [
                    "mode", "horizonTicks", "minimumDirectShots",
                    "minimumCoveredOptions", "fallback",
                ]);
            OneOf(dodge, "mode", at, "current-position", "escape-lanes");
            Range(dodge, "horizonTicks", at, 0, 8);
            Range(dodge, "minimumDirectShots", at, 0, 8);
            Range(dodge, "minimumCoveredOptions", at, 1, 9);
            OneOf(dodge, "fallback", at,
                "current-position", "best-coverage");
        }
        JsonElement release = value.GetProperty("release");
        Object(release, $"{at}.{id}.release",
            ["hiddenTicks", "unreachableTicks", "outsideLeash", "destroyed"]);
        Range(release, "hiddenTicks", at, 0, 120);
        Range(release, "unreachableTicks", at, 0, 120);
        RequiredBool(release, "outsideLeash", at);
        RequiredBool(release, "destroyed", at);
        JsonElement defense = value.GetProperty("selfDefense");
        Object(defense, $"{at}.{id}.selfDefense",
            ["enabled", "threatDistance", "returnToFormation"]);
        RequiredBool(defense, "enabled", at);
        Range(defense, "threatDistance", at, 0, 16);
        RequiredBool(defense, "returnToFormation", at);
    }

    private static void ValidateSupport(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        string path)
    {
        string at = $"{path}.supportPolicies";
        Object(value, at,
            [
                "supportId", "providers", "targetPriorities",
                "maximumProvidersPerTarget", "minimumProviderSeparation",
                "reserveHealthPercent", "survivalFallback",
            ]);
        string id = Identifier(value, "supportId", at);
        References(value.GetProperty("providers"), roleIds,
            $"{at}.{id}.providers", 1, 8);
        OneOfArray(value.GetProperty("targetPriorities"), at, 1, 16,
            "carrier", "medic", "lowest-health", "focus-participant",
            "formation-anchor", "any");
        Range(value, "maximumProvidersPerTarget", at, 1, 8);
        Range(value, "minimumProviderSeparation", at, 0, 8);
        Range(value, "reserveHealthPercent", at, 0, 100);
        OneOf(value, "survivalFallback", at,
            "evade", "regroup", "hold", "self-defense");
    }

    private static void ValidateCustody(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> orderIds,
        string path)
    {
        string at = $"{path}.custodyPolicies";
        Object(value, at,
            [
                "custodyId", "authorizedCarrierRoles", "escortGroups",
                "sourceWells", "pickupReservationTicks", "transferTimeoutTicks",
                "deliveryTimeoutTicks", "accidentalPickup", "dropRecovery",
                "unreachableFallback", "safeConversionAll",
            ], [
                "emergencyRecoveryZones", "emergencyPickupRadius",
                "emergencyRecoveryAll", "emergencyRecoveryRoles",
                "emergencyRecoverySourceWells",
                "emergencyRecoveryDisposition",
                "emergencyDisplacementTarget",
                "emergencyDisplacementReleaseRadius",
                "forwardPass", "baitDrop",
            ]);
        // forwardPass is opt-in so frozen sheets keep their exact executor
        // behavior: absent means the carrier never volunteers a pass.
        if (value.TryGetProperty("forwardPass", out _))
            OneOf(value, "forwardPass", at, "none", "relay-catcher");
        string id = Identifier(value, "custodyId", at);
        References(value.GetProperty("authorizedCarrierRoles"), roleIds,
            $"{at}.{id}.authorizedCarrierRoles", 1, 8);
        References(value.GetProperty("escortGroups"), groupIds,
            $"{at}.{id}.escortGroups", 0, 8);
        OneOfArray(value.GetProperty("sourceWells"),
            $"{at}.{id}.sourceWells", 1, 3,
            "north", "centre", "south");
        Range(value, "pickupReservationTicks", at, 1, 120);
        Range(value, "transferTimeoutTicks", at, 1, 120);
        Range(value, "deliveryTimeoutTicks", at, 1, 1200);
        OneOf(value, "accidentalPickup", at,
            "transfer", "deliver", "drop-safe");
        OneOf(value, "dropRecovery", at,
            "same-carrier", "nearest-authorized", "guard-until-safe");
        OneOf(value, "unreachableFallback", at,
            "hold", "guard", "alternate-core", "regroup");
        ValidateConditionGroups(
            value.GetProperty("safeConversionAll"), groupIds, roleIds, orderIds,
            $"{at}.{id}.safeConversionAll", 1, 8);
        if (value.TryGetProperty("baitDrop", out JsonElement baitDrop))
        {
            Object(baitDrop, $"{at}.{id}.baitDrop", ["zone", "reclaimAll"]);
            Identifier(baitDrop, "zone", $"{at}.{id}.baitDrop");
            ValidateConditionGroups(
                baitDrop.GetProperty("reclaimAll"), groupIds, roleIds,
                orderIds, $"{at}.{id}.baitDrop.reclaimAll", 1, 8);
        }
        bool hasEmergencyZones = value.TryGetProperty(
            "emergencyRecoveryZones", out JsonElement emergencyZones);
        bool hasEmergencyRadius = value.TryGetProperty(
            "emergencyPickupRadius", out _);
        bool hasEmergencyConditions = value.TryGetProperty(
            "emergencyRecoveryAll", out JsonElement emergencyConditions);
        bool hasEmergencyRoles = value.TryGetProperty(
            "emergencyRecoveryRoles", out JsonElement emergencyRoles);
        bool hasEmergencySourceWells = value.TryGetProperty(
            "emergencyRecoverySourceWells",
            out JsonElement emergencySourceWells);
        bool hasEmergencyDisposition = value.TryGetProperty(
            "emergencyRecoveryDisposition", out JsonElement disposition);
        bool hasEmergencyDisplacementTarget = value.TryGetProperty(
            "emergencyDisplacementTarget", out _);
        bool hasEmergencyDisplacementRadius = value.TryGetProperty(
            "emergencyDisplacementReleaseRadius", out _);
        if (hasEmergencyZones || hasEmergencyRadius || hasEmergencyConditions
            || hasEmergencyRoles || hasEmergencySourceWells
            || hasEmergencyDisposition || hasEmergencyDisplacementTarget
            || hasEmergencyDisplacementRadius)
        {
            if (!hasEmergencyZones || !hasEmergencyRadius
                || !hasEmergencyConditions || !hasEmergencyRoles
                || !hasEmergencySourceWells || !hasEmergencyDisposition)
            {
                throw Error(at,
                    $"custody policy '{id}' emergency recovery needs zones, "
                    + "pickup radius, conditions, roles, source Wells, and a "
                    + "disposition together.");
            }
            StringArray(emergencyZones, $"{at}.{id}.emergencyRecoveryZones",
                1, 8);
            Range(value, "emergencyPickupRadius", at, 1, 4);
            ValidateConditionGroups(
                emergencyConditions, groupIds, roleIds, orderIds,
                $"{at}.{id}.emergencyRecoveryAll", 1, 8);
            References(emergencyRoles, roleIds,
                $"{at}.{id}.emergencyRecoveryRoles", 1, 8);
            OneOfArray(emergencySourceWells,
                $"{at}.{id}.emergencyRecoverySourceWells", 1, 3,
                "north", "centre", "south");
            OneOf(value, "emergencyRecoveryDisposition", at,
                "deliver", "displace");
            bool displace = string.Equals(
                disposition.GetString(), "displace", StringComparison.Ordinal);
            if (displace != hasEmergencyDisplacementTarget
                || displace != hasEmergencyDisplacementRadius)
            {
                throw Error(at,
                    $"custody policy '{id}' displace recovery needs exactly "
                    + "one displacement target and release radius bundle.");
            }
            if (displace)
                Range(value, "emergencyDisplacementReleaseRadius", at, 0, 4);
        }
    }

    private static void ValidateCoordination(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> orderIds,
        IReadOnlyCollection<JsonElement> groups,
        IReadOnlyCollection<JsonElement> orders,
        string path)
    {
        string at = $"{path}.coordination";
        Object(value, at, ["initialPhase", "phases", "tasks"]);
        string initial = Identifier(value, "initialPhase", at);
        JsonElement[] phases = BoundedArray(
            value.GetProperty("phases"), $"{at}.phases", 1, 24);
        HashSet<string> phaseIds = UniqueIds(phases, "phaseId", $"{at}.phases");
        if (!phaseIds.Contains(initial))
            throw Error(at, $"unknown initial phase '{initial}'.");
        foreach (JsonElement phase in phases)
        {
            Object(phase, $"{at}.phases",
                ["phaseId", "minimumTicks", "orderIds", "transitions"]);
            Range(phase, "minimumTicks", at, 0, 1200);
            References(phase.GetProperty("orderIds"), orderIds,
                $"{at}.orderIds", 1, 32);
            ValidateTransitions(
                phase.GetProperty("transitions"), phaseIds, groupIds,
                roleIds, orderIds, at);
        }
        ValidateTasks(
            value.GetProperty("tasks"),
            phaseIds,
            groupIds,
            roleIds,
            orderIds,
            groups,
            orders,
            at);
    }

    private static void ValidateTasks(
        JsonElement value,
        IReadOnlySet<string> phaseIds,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> orderIds,
        IReadOnlyCollection<JsonElement> groups,
        IReadOnlyCollection<JsonElement> orders,
        string path)
    {
        JsonElement[] tasks = BoundedArray(value, $"{path}.tasks", 0, 32);
        UniqueIds(tasks, "taskId", $"{path}.tasks");
        Dictionary<string, JsonElement> ordersById = orders.ToDictionary(
            order => order.GetProperty("orderId").GetString()!,
            StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> rolesByGroup = groups.ToDictionary(
            group => group.GetProperty("groupId").GetString()!,
            group => group.GetProperty("roleIds").EnumerateArray()
                .Select(role => role.GetString()!)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Dictionary<string, string[]> statesByGroup = groups.ToDictionary(
            group => group.GetProperty("groupId").GetString()!,
            group => group.GetProperty("localStateMachine")
                .GetProperty("states").EnumerateArray()
                .Select(state => state.GetProperty("stateId").GetString()!)
                .ToArray(),
            StringComparer.Ordinal);
        foreach (JsonElement task in tasks)
        {
            string at = $"{path}.tasks";
            Object(task, at,
                [
                    "taskId", "priority", "activation", "preemption",
                    "participantLoss", "triggerStableTicks", "minimumTicks",
                    "timeoutTicks", "cooldownTicks", "minimumPrimaryBodies",
                    "eligiblePhases",
                    "assignments", "when", "completeWhen", "failWhen",
                    "reintegration",
                ], [
                    "minimumParticipants", "maximumParticipants",
                    "completionArmWhen", "completionArmMode",
                    "completionReleaseMode", "cancellationMode",
                ]);
            string taskId = Identifier(task, "taskId", at);
            Range(task, "priority", at, 0, 1000);
            OneOf(task, "activation", at, "rising-edge", "while-true");
            OneOf(task, "preemption", at, "never", "higher-priority");
            OneOf(task, "participantLoss", at,
                "abort", "continue", "replace");
            Range(task, "triggerStableTicks", at, 1, 120);
            Range(task, "minimumTicks", at, 0, 1200);
            Range(task, "timeoutTicks", at, 1, 2400);
            Range(task, "cooldownTicks", at, 0, 1200);
            Range(task, "minimumPrimaryBodies", at, 0, 8);
            int aggregateMinimum = task.TryGetProperty(
                "minimumParticipants", out _)
                ? RequiredInt(task, "minimumParticipants", at)
                : 0;
            int aggregateMaximum = task.TryGetProperty(
                "maximumParticipants", out _)
                ? RequiredInt(task, "maximumParticipants", at)
                : 0;
            if (aggregateMinimum is < 0 or > 8
                || aggregateMaximum is < 0 or > 8
                || aggregateMaximum > 0
                && aggregateMinimum > aggregateMaximum)
            {
                throw Error(at,
                    $"task '{taskId}' has invalid aggregate participant bounds.");
            }
            References(task.GetProperty("eligiblePhases"), phaseIds,
                $"{at}.{taskId}.eligiblePhases", 1, 24);
            ValidateConditionGroups(
                task.GetProperty("when"), groupIds, roleIds, orderIds,
                $"{at}.{taskId}.when", 1, 16);
            ValidateConditionGroups(
                task.GetProperty("completeWhen"), groupIds, roleIds, orderIds,
                $"{at}.{taskId}.completeWhen", 1, 16);
            if (task.TryGetProperty(
                    "completionArmWhen", out JsonElement completionArmWhen))
            {
                ValidateConditionGroups(
                    completionArmWhen, groupIds, roleIds, orderIds,
                    $"{at}.{taskId}.completionArmWhen", 1, 16);
            }
            bool hasCompletionArmMode = task.TryGetProperty(
                "completionArmMode", out _);
            bool hasCompletionReleaseMode = task.TryGetProperty(
                "completionReleaseMode", out _);
            if (hasCompletionArmMode != hasCompletionReleaseMode)
            {
                throw Error(at,
                    $"task '{taskId}' must declare participant completion "
                    + "arm and release modes together.");
            }
            if (hasCompletionArmMode)
            {
                OneOf(task, "completionArmMode", at,
                    "assigned-carrier", "any-friendly-carrier");
                OneOf(task, "completionReleaseMode", at,
                    "assigned-carrier-loss", "armed-carrier-loss",
                    "armed-carrier-retarget-or-loss");
            }
            if (task.TryGetProperty("cancellationMode", out _))
                OneOf(task, "cancellationMode", at, "alternate-carrier");
            ValidateConditionGroups(
                task.GetProperty("failWhen"), groupIds, roleIds, orderIds,
                $"{at}.{taskId}.failWhen", 0, 16);

            JsonElement[] assignments = BoundedArray(
                task.GetProperty("assignments"),
                $"{at}.{taskId}.assignments", 1, 8);
            UniqueIds(assignments, "assignmentId",
                $"{at}.{taskId}.assignments");
            foreach (JsonElement assignment in assignments)
            {
                Object(assignment, $"{at}.{taskId}.assignments",
                    [
                        "assignmentId", "orderId", "roles", "classes",
                        "minimum", "preferred", "maximum", "carrier",
                        "distance",
                    ]);
                string assignmentId = Identifier(
                    assignment, "assignmentId", at);
                Reference(assignment, "orderId", orderIds, at);
                string orderId = assignment.GetProperty("orderId").GetString()!;
                JsonElement order = ordersById[orderId];
                string groupId = order.GetProperty("groupId").GetString()!;
                string[] selectedRoles = StringArray(
                    assignment.GetProperty("roles"),
                    $"{at}.{taskId}.{assignmentId}.roles", 0, 8);
                // Cross-group claims (owner doctrine 2026-08-07): a task
                // may pull an authorized body from ANY group into this
                // order - tasks are explicit detachments, and merit
                // promotion needs the levelled towline to take the knife.
                // Role existence still validates; group ownership does not.
                _ = groupId;
                if (selectedRoles.Any(role => !roleIds.Contains(role)))
                {
                    throw Error(at,
                        $"task '{taskId}' assignment '{assignmentId}' "
                        + $"selects an unknown role for order '{orderId}'.");
                }
                string[] classes = StringArray(
                    assignment.GetProperty("classes"),
                    $"{at}.{taskId}.{assignmentId}.classes", 0, 16);
                if (classes.Any(value => !KnownClasses.Contains(value))
                    || classes.Distinct(StringComparer.Ordinal).Count()
                        != classes.Length)
                {
                    throw Error(at,
                        $"task '{taskId}' assignment '{assignmentId}' has "
                        + "invalid class preferences.");
                }
                Cardinality(assignment,
                    $"{at}.{taskId}.{assignmentId}", 0, 8);
                OneOf(assignment, "carrier", at,
                    "forbid", "allow", "require");
                JsonElement distance = assignment.GetProperty("distance");
                Object(distance, $"{at}.{taskId}.{assignmentId}.distance",
                    ["kind", "target"], ["maximum"]);
                string kind = RequiredString(distance, "kind", at);
                OneOf(distance, "kind", at,
                    "none", "veterancy-rank",
                    "anchor", "own-reactor", "enemy-reactor",
                    "visible-loose-core-in-zone");
                string target = RequiredString(distance, "target", at);
                bool needsTarget = kind is "anchor"
                    or "visible-loose-core-in-zone";
                if (needsTarget != (target.Length > 0))
                {
                    throw Error(at,
                        $"task '{taskId}' assignment '{assignmentId}' "
                        + "distance target does not match its kind.");
                }
                if (distance.TryGetProperty("maximum", out _))
                    Range(distance, "maximum", at, 1, 64);
                if (!string.Equals(
                        order.GetProperty("members").GetProperty("kind")
                            .GetString(),
                        "all",
                        StringComparison.Ordinal))
                {
                    throw Error(at,
                        $"task order '{orderId}' must use members.kind 'all'; "
                        + "the task assignment owns participant selection.");
                }
            }

            int assignmentMinimum = assignments.Sum(assignment =>
                assignment.GetProperty("minimum").GetInt32());
            int assignmentMaximum = assignments.Sum(assignment =>
                assignment.GetProperty("maximum").GetInt32());
            if (aggregateMaximum > 0
                && (aggregateMaximum < assignmentMinimum
                    || aggregateMaximum > assignmentMaximum)
                || aggregateMinimum > assignmentMaximum)
            {
                throw Error(at,
                    $"task '{taskId}' aggregate participant bounds conflict "
                    + "with its assignment cardinalities.");
            }
            int minimumParticipants = Math.Max(
                assignmentMinimum, aggregateMinimum);
            int minimumPrimaryBodies = task
                .GetProperty("minimumPrimaryBodies").GetInt32();
            if (minimumParticipants + minimumPrimaryBodies > 8)
            {
                throw Error(at,
                    $"task '{taskId}' participant minimums plus its primary "
                    + "force reserve exceed the eight-body composition.");
            }

            JsonElement reintegration = task.GetProperty("reintegration");
            Object(reintegration, $"{at}.{taskId}.reintegration",
                ["mode", "orderIds", "completeWhen", "timeoutTicks"]);
            string mode = RequiredString(reintegration, "mode", at);
            OneOf(reintegration, "mode", at,
                "primary-order", "release-orders");
            References(
                reintegration.GetProperty("orderIds"), orderIds,
                $"{at}.{taskId}.reintegration.orderIds", 0, 16);
            string[] releaseOrders = StringArray(
                reintegration.GetProperty("orderIds"),
                $"{at}.{taskId}.reintegration.orderIds", 0, 16);
            ValidateConditionGroups(
                reintegration.GetProperty("completeWhen"),
                groupIds,
                roleIds,
                orderIds,
                $"{at}.{taskId}.reintegration.completeWhen",
                0,
                16);
            Range(reintegration, "timeoutTicks", at, 0, 1200);
            int releaseTimeout = reintegration.GetProperty("timeoutTicks")
                .GetInt32();
            int releaseConditionCount = reintegration
                .GetProperty("completeWhen").GetArrayLength();
            if (mode == "primary-order"
                && (releaseOrders.Length != 0
                    || releaseConditionCount != 0
                    || releaseTimeout != 0))
            {
                throw Error(at,
                    $"task '{taskId}' primary-order reintegration cannot "
                    + "declare release orders, conditions, or timeout.");
            }
            if (mode == "release-orders"
                && (releaseOrders.Length == 0
                    || releaseConditionCount == 0
                    || releaseTimeout == 0))
            {
                throw Error(at,
                    $"task '{taskId}' release-orders reintegration requires "
                    + "orders, a completion condition, and a timeout.");
            }
            if (mode == "release-orders")
            {
                HashSet<string> assignmentGroups = assignments
                    .Select(assignment => ordersById[assignment
                        .GetProperty("orderId").GetString()!]
                        .GetProperty("groupId").GetString()!)
                    .ToHashSet(StringComparer.Ordinal);
                JsonElement[] releases = releaseOrders
                    .Select(orderId => ordersById[orderId])
                    .ToArray();
                string? irrelevantGroup = releases
                    .Select(order => order.GetProperty("groupId").GetString()!)
                    .FirstOrDefault(groupId =>
                        !assignmentGroups.Contains(groupId));
                if (irrelevantGroup is not null)
                {
                    throw Error(at,
                        $"task '{taskId}' release order targets unassigned "
                        + $"group '{irrelevantGroup}'.");
                }
                foreach (string groupId in assignmentGroups)
                foreach (string stateId in statesByGroup[groupId])
                {
                    int matches = releases.Count(order =>
                        string.Equals(
                            order.GetProperty("groupId").GetString(),
                            groupId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            order.GetProperty("localState").GetString(),
                            stateId,
                            StringComparison.Ordinal));
                    if (matches != 1)
                    {
                        throw Error(at,
                            $"task '{taskId}' release orders must cover "
                            + $"group '{groupId}' local state '{stateId}' "
                            + $"exactly once; found {matches}.");
                    }
                }
            }
        }
    }

    private static void ValidateOrder(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> formationIds,
        IReadOnlySet<string> engagementIds,
        IReadOnlySet<string> supportIds,
        IReadOnlySet<string> custodyIds,
        IReadOnlySet<string> phaseIds,
        string path)
    {
        string at = $"{path}.coordination.orders";
        Object(value, at,
            [
                "orderId", "groupId", "priority", "members", "movement",
                "formationId", "engagementId", "custodyId", "localState",
                "fallback",
            ],
            ["supportId", "stance", "patienceTicks"]);
        if (value.TryGetProperty("stance", out _))
            OneOf(value, "stance", at, "ambush");
        if (value.TryGetProperty("patienceTicks", out _))
            Range(value, "patienceTicks", at, 2, 120);
        Identifier(value, "orderId", at);
        Reference(value, "groupId", groupIds, at);
        Range(value, "priority", at, 0, 1000);
        ValidateMemberSelection(value.GetProperty("members"), at);
        Reference(value, "formationId", formationIds, at);
        Reference(value, "engagementId", engagementIds, at);
        if (value.TryGetProperty("supportId", out _))
            Reference(value, "supportId", supportIds, at, allowEmpty: false);
        Reference(value, "custodyId", custodyIds, at, allowEmpty: false);
        Identifier(value, "localState", at);
        ValidateMovement(value.GetProperty("movement"), at);
        JsonElement fallback = value.GetProperty("fallback");
        Object(fallback, $"{at}.fallback",
            ["onNoPath", "onUnderstrength", "onInvalidTarget", "phaseId"]);
        OneOf(fallback, "onNoPath", at, "reflow", "hold", "regroup");
        OneOf(fallback, "onUnderstrength", at,
            "continue", "regroup", "fallback-phase");
        OneOf(fallback, "onInvalidTarget", at,
            "hold", "alternate", "fallback-phase");
        string phaseId = RequiredString(fallback, "phaseId", at);
        bool needsPhase = fallback.GetProperty("onNoPath").GetString()
                == "regroup"
            || fallback.GetProperty("onUnderstrength").GetString()
                is "regroup" or "fallback-phase"
            || fallback.GetProperty("onInvalidTarget").GetString()
                == "fallback-phase";
        if (needsPhase && !phaseIds.Contains(phaseId))
            throw Error(at, $"fallback references unknown phase '{phaseId}'.");
        if (!needsPhase && phaseId.Length > 0)
            throw Error(at,
                $"fallback phase '{phaseId}' is irrelevant to its actions.");
    }

    private static void ValidateMemberSelection(
        JsonElement value,
        string path)
    {
        string kind = RequiredString(value, "kind", path);
        switch (kind)
        {
            case "all":
            case "remainder":
                Object(value, $"{path}.members", ["kind"]);
                break;
            case "take":
                Object(value, $"{path}.members",
                    ["kind", "roles", "classes", "count"]);
                foreach (JsonElement role in BoundedArray(
                             value.GetProperty("roles"),
                             $"{path}.members.roles", 1, 8))
                {
                    StringValue(role, $"{path}.members.roles");
                }
                foreach (JsonElement candidateClass in BoundedArray(
                             value.GetProperty("classes"),
                             $"{path}.members.classes", 1, 16))
                {
                    StringValue(candidateClass, $"{path}.members.classes");
                }
                Range(value, "count", $"{path}.members", 1, 8);
                break;
            default:
                throw Error(path,
                    $"unknown member selection kind '{kind}'.");
        }
    }

    private static void ValidateMovement(JsonElement value, string path)
    {
        Object(value, $"{path}.movement",
            [
                "kind", "target", "arrivalRadius", "completion",
                "stuckTicks", "stuckRecovery", "chaseLeash", "pace",
            ], ["leadTiles", "scan"]);
        OneOf(value, "kind", path,
            "route", "zone", "anchor", "reactor", "carrier",
            "enemy-carrier", "enemy-carrier-cutoff", "secured-core",
            "shadow-traffic", "incoming-cutoff", "hold");
        if (value.TryGetProperty("scan", out _))
            OneOf(value, "scan", path, "sweep");
        NonEmptyString(value, "target", path, allowEmpty: true);
        Range(value, "arrivalRadius", path, 0, 16);
        OneOf(value, "completion", path,
            "leader-arrived", "cohesion-arrived", "all-arrived", "continuous");
        Range(value, "stuckTicks", path, 1, 120);
        OneOf(value, "stuckRecovery", path,
            "repath", "yield", "reflow", "regroup", "hold");
        Range(value, "chaseLeash", path, 0, 16);
        OneOf(value, "pace", path, "slowest", "leader", "free");
        if (value.TryGetProperty("leadTiles", out _))
            Range(value, "leadTiles", path, 0, 8);
    }

    private static void ValidateTransitions(
        JsonElement value,
        IReadOnlySet<string> stateIds,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> orderIds,
        string path)
    {
        JsonElement[] transitions = BoundedArray(value, path, 0, 32);
        foreach (JsonElement transition in transitions)
        {
            Object(transition, path,
                ["priority", "to", "cause", "minimumPolicy", "stableTicks", "when"]);
            Range(transition, "priority", path, 0, 1000);
            Reference(transition, "to", stateIds, path);
            OneOf(transition, "cause", path,
                "success", "failure", "recovery", "reaction");
            OneOf(transition, "minimumPolicy", path,
                "respect", "interrupt");
            Range(transition, "stableTicks", path, 1, 120);
            ValidateConditionGroups(
                transition.GetProperty("when"), groupIds, roleIds, orderIds,
                path,
                1, 16);
        }
    }

    private static void ValidateConditionGroups(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> orderIds,
        string path,
        int minimum,
        int maximum)
    {
        JsonElement[] groups = BoundedArray(value, path, minimum, maximum);
        foreach (JsonElement group in groups)
        {
            bool hasAll = group.TryGetProperty("all", out JsonElement allValue);
            bool hasAny = group.TryGetProperty("any", out JsonElement anyValue);
            if (hasAll == hasAny)
                throw Error(path,
                    "condition group must declare exactly one of 'all' or 'any'.");
            Object(group, path, [hasAll ? "all" : "any"]);
            JsonElement[] conditions = BoundedArray(
                hasAll ? allValue : anyValue,
                $"{path}.{(hasAll ? "all" : "any")}", 1, 16);
            foreach (JsonElement condition in conditions)
                ValidateCondition(
                    condition, groupIds, roleIds, orderIds, path);
        }
    }

    private static void ValidateCondition(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> orderIds,
        string path)
    {
        string fact = RequiredString(value, "fact", path);
        if (!ConditionFacts.Contains(fact))
            throw Error(path, $"unknown condition fact '{fact}'.");
        var variantFields = new List<string>();
        if (GroupSubjectFacts.Contains(fact)
            || fact is "role-live-count" or "well-has-outstanding"
                or "own-socket-filled" or "enemy-socket-filled"
                or "well-ticks-until-birth")
            variantFields.Add("subject");
        if (OrderSubjectFacts.Contains(fact))
            variantFields.Add("subject");
        if (ZoneFacts.Contains(fact))
            variantFields.Add("zone");
        Object(value, path,
            ["fact", "operator", "value", .. variantFields],
            FreshnessFacts.Contains(fact)
                ? ["freshnessTicks", "valueParameter"]
                : ["valueParameter"]);
        OneOf(value, "operator", path, [.. ConditionOperators]);
        Range(value, "value", path, 0, 100000);
        // Optional provenance written by authoring expansion: the parameter
        // this value was baked from, kept so side-keyed binding overrides
        // can re-bake it at load time.
        if (value.TryGetProperty("valueParameter", out _))
            Identifier(value, "valueParameter", path);
        string subject = variantFields.Contains("subject", StringComparer.Ordinal)
            ? NonEmptyString(value, "subject", path)
            : "";
        if (variantFields.Contains("zone", StringComparer.Ordinal))
            NonEmptyString(value, "zone", path);
        if (value.TryGetProperty("freshnessTicks", out _))
            Range(value, "freshnessTicks", path, 1, 600);
        if (GroupSubjectFacts.Contains(fact) && !groupIds.Contains(subject))
            throw Error(path, $"condition references unknown group '{subject}'.");
        if (fact == "role-live-count" && !roleIds.Contains(subject))
            throw Error(path, $"condition references unknown role '{subject}'.");
        if (OrderSubjectFacts.Contains(fact) && !orderIds.Contains(subject))
            throw Error(path, $"condition references unknown order '{subject}'.");
    }

    private static void ValidateLayout(
        JsonElement root,
        string path,
        Dictionary<string, (int Minimum, int Maximum)> parameterRanges)
    {
        Object(root, path,
            [
                "schema", "layoutId", "mapId", "bindings", "zones",
                "routes", "anchors",
            ]);
        Exact(root, "schema", LayoutSchema, path);
        Identifier(root, "layoutId", path);
        NonEmptyString(root, "mapId", path);
        JsonElement[] bindings = BoundedArray(
            root.GetProperty("bindings"), $"{path}.bindings", 1, 16);
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement binding in bindings)
        {
            Object(binding, $"{path}.bindings",
                ["matchContractFingerprint", "ownReactorSide", "transform",
                    "routeAliases"],
                ["formationAliases", "parameterOverrides"]);
            // Side-keyed overrides: a binding may re-value declared authoring
            // parameters within their explicit ranges, so the two
            // orientations of a map can run different sensitivities without
            // forking the sheet.
            if (binding.TryGetProperty(
                    "parameterOverrides", out JsonElement overrides))
            {
                if (overrides.ValueKind != JsonValueKind.Object
                    || overrides.EnumerateObject().Count() > 16)
                {
                    throw Error(path,
                        "'parameterOverrides' must be an object with at most "
                        + "16 integer values.");
                }
                foreach (JsonProperty overridden in overrides.EnumerateObject())
                {
                    if (!parameterRanges.TryGetValue(
                            overridden.Name,
                            out (int Minimum, int Maximum) range))
                    {
                        throw Error(path,
                            "'parameterOverrides' names unknown parameter "
                            + $"'{overridden.Name}'.");
                    }
                    if (overridden.Value.ValueKind != JsonValueKind.Number
                        || !overridden.Value.TryGetInt32(out int selected)
                        || selected < range.Minimum
                        || selected > range.Maximum)
                    {
                        throw Error(path,
                            $"'parameterOverrides.{overridden.Name}' must be "
                            + $"an integer in [{range.Minimum}, "
                            + $"{range.Maximum}].");
                    }
                }
            }
            // "any-composition" is the side-keyed wildcard: a sheet may meet
            // opponents whose composition pair it has never seen. Exact
            // fingerprints still take precedence at match time.
            string fingerprint = RequiredString(
                binding, "matchContractFingerprint", $"{path}.bindings");
            if (!string.Equals(fingerprint, "any-composition",
                    StringComparison.Ordinal))
            {
                fingerprint = Hash(
                    binding, "matchContractFingerprint", $"{path}.bindings");
            }
            string sideKey = fingerprint + "|" + RequiredString(
                binding, "ownReactorSide", $"{path}.bindings");
            if (!fingerprints.Add(sideKey))
                throw Error(path, $"duplicate layout binding '{sideKey}'.");
            OneOf(binding, "ownReactorSide", path, "west", "east");
            OneOf(binding, "transform", path,
                "identity", "mirror-x", "rotate-180");
            JsonElement aliases = binding.GetProperty("routeAliases");
            if (aliases.ValueKind != JsonValueKind.Object
                || aliases.EnumerateObject().Count() > 32
                || aliases.EnumerateObject().Any(property =>
                    property.Value.ValueKind != JsonValueKind.String))
            {
                throw Error(path,
                    "'routeAliases' must be an object with at most 32 string values.");
            }
            if (binding.TryGetProperty(
                    "formationAliases", out JsonElement formationAliases)
                && (formationAliases.ValueKind != JsonValueKind.Object
                    || formationAliases.EnumerateObject().Count() > 32
                    || formationAliases.EnumerateObject().Any(property =>
                        property.Value.ValueKind != JsonValueKind.String)))
            {
                throw Error(path,
                    "'formationAliases' must be an object with at most 32 "
                    + "string values.");
            }
        }
        JsonElement[] zones = BoundedArray(
            root.GetProperty("zones"), $"{path}.zones", 1, 64);
        UniqueIds(zones, "zoneId", $"{path}.zones");
        foreach (JsonElement zone in zones)
        {
            Object(zone, $"{path}.zones", ["zoneId", "rect"]);
            Rectangle(zone.GetProperty("rect"), $"{path}.zones.rect");
        }
        JsonElement[] routes = BoundedArray(
            root.GetProperty("routes"), $"{path}.routes", 1, 64);
        HashSet<string> routeIds = UniqueIds(
            routes, "routeId", $"{path}.routes");
        foreach (JsonElement route in routes)
        {
            Object(route, $"{path}.routes",
                ["routeId", "waypoints", "corridorWidth"]);
            JsonElement[] points = BoundedArray(
                route.GetProperty("waypoints"), $"{path}.routes.waypoints",
                1, 128);
            foreach (JsonElement point in points)
                Point(point, $"{path}.routes.waypoints", 0, 255);
            Range(route, "corridorWidth", path, 0, 8);
        }
        foreach (JsonElement binding in bindings)
        foreach (JsonProperty alias in binding.GetProperty("routeAliases")
                     .EnumerateObject())
        {
            string target = alias.Value.GetString()!;
            if (!routeIds.Contains(alias.Name) || !routeIds.Contains(target))
            {
                throw Error(path,
                    $"route alias '{alias.Name}' -> '{target}' references an unknown route.");
            }
        }
        JsonElement[] anchors = BoundedArray(
            root.GetProperty("anchors"), $"{path}.anchors", 1, 64);
        UniqueIds(anchors, "anchorId", $"{path}.anchors");
        foreach (JsonElement anchor in anchors)
        {
            Object(anchor, $"{path}.anchors", ["anchorId", "position"]);
            Point(anchor.GetProperty("position"), $"{path}.anchors.position", 0, 255);
        }
    }

    private static JsonDocument Parse(byte[] source, string path)
    {
        try
        {
            return JsonDocument.Parse(source, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path}: invalid JSON.", exception);
        }
    }

    private static byte[] Canonicalize(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
        }))
        {
            WriteCanonical(writer, root);
        }
        return stream.ToArray();
    }

    private static byte[] NormalizePlaybook(JsonElement source)
    {
        JsonNode root = JsonNode.Parse(source.GetRawText())
            ?? throw new InvalidDataException("Tactical playbook is empty.");
        NormalizeNode(root);
        using JsonDocument normalized = JsonDocument.Parse(
            root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false,
            }));
        return Canonicalize(normalized.RootElement);
    }

    private static void NormalizeNode(JsonNode node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("fact"))
            {
                value.TryAdd("subject", "");
                value.TryAdd("zone", "");
                value.TryAdd("freshnessTicks", 0);
            }
            if (value.ContainsKey("all") && !value.ContainsKey("any"))
                value.Add("any", new JsonArray());
            if (value.ContainsKey("any") && !value.ContainsKey("all"))
                value.Add("all", new JsonArray());
            if (value.ContainsKey("orderId") && value.ContainsKey("movement"))
            {
                value.TryAdd("supportId", "");
                value.TryAdd("custodyId", "");
            }
            foreach (JsonNode? child in value.Select(item => item.Value).ToArray())
            {
                if (child is not null)
                    NormalizeNode(child);
            }
            return;
        }
        if (node is not JsonArray array)
            return;
        foreach (JsonNode? child in array)
        {
            if (child is not null)
                NormalizeNode(child);
        }
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteNumberValue(value.GetInt32());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                throw new InvalidDataException(
                    "Tactical playbooks do not permit null or undefined values.");
            default:
                throw new InvalidDataException(
                    $"Unsupported JSON kind '{value.ValueKind}'.");
        }
    }

    private static void Object(
        JsonElement value,
        string path,
        IReadOnlyCollection<string> required)
        => Object(value, path, required, []);

    private static void Object(
        JsonElement value,
        string path,
        IReadOnlyCollection<string> required,
        IReadOnlyCollection<string> optional)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected object.");
        HashSet<string> allowed = required.Concat(optional)
            .ToHashSet(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                throw Error(path, $"unknown field '{property.Name}'.");
        }
        foreach (string name in required)
        {
            if (!value.TryGetProperty(name, out _))
                throw Error(path, $"missing required field '{name}'.");
        }
    }

    private static JsonElement[] BoundedArray(
        JsonElement value,
        string path,
        int minimum,
        int maximum)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw Error(path, "expected array.");
        JsonElement[] entries = value.EnumerateArray().ToArray();
        if (entries.Length < minimum || entries.Length > maximum)
            throw Error(path, $"expected {minimum}..{maximum} entries.");
        return entries;
    }

    public const string LibrarySchema = "arc-relay-tactical-library-v1";

    private static readonly string[] LibrarySections =
    [
        "parameters", "fallbackPolicies", "assignmentProfiles",
        "standingOrders", "maneuvers", "predicates", "conditionSets",
    ];

    /// <summary>
    /// Resolves an optional shared authoring library referenced as
    /// authoring.library {path, sha256} — the exact binding pattern the
    /// layout reference uses. Library catalog entries are unioned into the
    /// playbook's own catalogs; a duplicate id across the two sources is a
    /// hard error, never an override. Returns null when no library is
    /// referenced; otherwise returns the merged document with the library
    /// reference consumed, so downstream validation sees ordinary inline
    /// authoring.
    /// </summary>
    private static JsonDocument? MergeAuthoringLibrary(
        JsonElement source,
        string path)
    {
        if (!source.TryGetProperty("authoring", out JsonElement authoring)
            || authoring.ValueKind != JsonValueKind.Object
            || !authoring.TryGetProperty("library", out JsonElement reference))
        {
            return null;
        }

        string referenceAt = $"{path}.authoring.library";
        Object(reference, referenceAt, ["path", "sha256"]);
        string relativePath = RequiredString(reference, "path", referenceAt);
        string fullLibraryPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(path))
                ?? throw new InvalidDataException(
                    $"{path}: playbook has no parent directory."),
            relativePath));
        byte[] librarySource = File.ReadAllBytes(fullLibraryPath);
        string librarySha256 = Sha256(librarySource);
        string expectedSha256 = RequiredString(
            reference, "sha256", referenceAt);
        if (!string.Equals(expectedSha256, librarySha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{path}: library hash mismatch; expected "
                + $"{expectedSha256}, found {librarySha256}.");
        }

        using JsonDocument libraryDocument = Parse(
            librarySource, fullLibraryPath);
        JsonElement library = libraryDocument.RootElement;
        string libraryAt = fullLibraryPath;
        Object(library, libraryAt,
            ["schema", "libraryId", .. LibrarySections]);
        Exact(library, "schema", LibrarySchema, libraryAt);
        RequiredString(library, "libraryId", libraryAt);

        JsonObject merged = JsonNode.Parse(source.GetRawText())!.AsObject();
        JsonObject mergedAuthoring = merged["authoring"]!.AsObject();
        mergedAuthoring.Remove("library");
        foreach (string section in LibrarySections)
        {
            if (!library.TryGetProperty(section,
                    out JsonElement librarySection))
                continue;
            if (librarySection.ValueKind != JsonValueKind.Object)
                throw Error($"{libraryAt}.{section}",
                    "expected keyed object catalog.");
            JsonObject target = mergedAuthoring[section] is JsonNode existing
                ? existing.AsObject()
                : new JsonObject();
            foreach (JsonProperty entry in librarySection.EnumerateObject())
            {
                if (target.ContainsKey(entry.Name))
                {
                    throw Error($"{path}.authoring.{section}",
                        $"'{entry.Name}' is defined by both the library and "
                        + "the playbook; a playbook may add entries, never "
                        + "override library entries.");
                }
                target[entry.Name] = JsonNode.Parse(
                    entry.Value.GetRawText());
            }
            mergedAuthoring[section] = target;
        }
        return JsonDocument.Parse(merged.ToJsonString());
    }

    private readonly record struct CatalogEntry(string Id, JsonElement Value);

    private static CatalogEntry[] Catalog(
        JsonElement value,
        string path,
        int minimum,
        int maximum)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected keyed object catalog.");
        JsonProperty[] properties = value.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        if (properties.Length < minimum || properties.Length > maximum)
            throw Error(path, $"expected {minimum}..{maximum} entries.");
        foreach (JsonProperty property in properties)
            IdentifierValue(property.Name, path);
        if (properties.Select(property => property.Name)
            .Distinct(StringComparer.Ordinal).Count() != properties.Length)
        {
            throw Error(path, "catalog keys must be unique.");
        }
        return properties
            .Select(property => new CatalogEntry(property.Name, property.Value))
            .ToArray();
    }

    private static string[] StringArray(
        JsonElement value,
        string path,
        int minimum,
        int maximum) => BoundedArray(value, path, minimum, maximum)
        .Select((entry, index) => StringValue(entry, $"{path}[{index}]"))
        .ToArray();

    private static void References(
        JsonElement value,
        IReadOnlySet<string> ids,
        string path,
        int minimum,
        int maximum)
    {
        string[] references = StringArray(value, path, minimum, maximum);
        if (references.Distinct(StringComparer.Ordinal).Count()
            != references.Length)
            throw Error(path, "references must be unique.");
        string? unknown = references.FirstOrDefault(reference => !ids.Contains(reference));
        if (unknown is not null)
            throw Error(path, $"unknown reference '{unknown}'.");
    }

    private static HashSet<string> UniqueIds(
        IEnumerable<JsonElement> values,
        string property,
        string path)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement value in values)
        {
            string id = Identifier(value, property, path);
            if (!ids.Add(id))
                throw Error(path, $"duplicate {property} '{id}'.");
        }
        return ids;
    }

    private static string Identifier(JsonElement value, string name, string path)
    {
        string result = RequiredString(value, name, path);
        IdentifierValue(result, path, name);
        return result;
    }

    private static void IdentifierValue(
        string result,
        string path,
        string name = "catalog key")
    {
        if (result.Length is < 1 or > 64
            || !char.IsLower(result[0])
            || result.Any(character => !char.IsLower(character)
                && !char.IsDigit(character)
                && character != '-'))
            throw Error(path, $"'{name}' must be a lower-kebab identifier.");
    }

    private static string RequiredString(
        JsonElement value,
        string name,
        string path)
    {
        if (!value.TryGetProperty(name, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
            throw Error(path, $"'{name}' must be a string.");
        return property.GetString()!;
    }

    private static string NonEmptyString(
        JsonElement value,
        string name,
        string path,
        bool allowEmpty = false)
    {
        string result = RequiredString(value, name, path);
        if ((!allowEmpty && string.IsNullOrWhiteSpace(result)) || result.Length > 256)
            throw Error(path, $"'{name}' is invalid.");
        return result;
    }

    private static string StringValue(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw Error(path, "expected string.");
        return value.GetString()!;
    }

    private static bool RequiredBool(
        JsonElement value,
        string name,
        string path)
    {
        if (!value.TryGetProperty(name, out JsonElement property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw Error(path, $"'{name}' must be a boolean.");
        return property.GetBoolean();
    }

    private static int RequiredInt(JsonElement value, string name, string path)
    {
        if (!value.TryGetProperty(name, out JsonElement property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out int result))
            throw Error(path, $"'{name}' must be an integer.");
        return result;
    }

    private static void Range(
        JsonElement value,
        string name,
        string path,
        int minimum,
        int maximum)
    {
        int actual = RequiredInt(value, name, path);
        if (actual < minimum || actual > maximum)
            throw Error(path, $"'{name}' must be {minimum}..{maximum}.");
    }

    private static void Cardinality(
        JsonElement value,
        string path,
        int minimum,
        int maximum)
    {
        int low = RequiredInt(value, "minimum", path);
        int preferred = RequiredInt(value, "preferred", path);
        int high = RequiredInt(value, "maximum", path);
        if (low < minimum || high > maximum || low > preferred || preferred > high)
            throw Error(path,
                $"cardinality must satisfy {minimum} <= minimum <= preferred <= maximum <= {maximum}.");
    }

    private static void Exact(
        JsonElement value,
        string name,
        string expected,
        string path)
    {
        string actual = RequiredString(value, name, path);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw Error(path, $"'{name}' must be '{expected}'.");
    }

    private static void OneOf(
        JsonElement value,
        string name,
        string path,
        params string[] permitted)
    {
        string actual = RequiredString(value, name, path);
        if (!permitted.Contains(actual, StringComparer.Ordinal))
            throw Error(path,
                $"'{name}' must be one of: {string.Join(", ", permitted)}.");
    }

    /// <summary>
    /// Like OneOfArray, but additionally accepts "class:&lt;classId&gt;"
    /// qualified terms so an engagement can prefer targets of one chassis
    /// (the kill-the-medics verb) without a per-class vocabulary explosion.
    /// </summary>
    private static void PriorityArray(
        JsonElement value,
        string path,
        int minimum,
        int maximum,
        params string[] permitted)
    {
        string[] values = StringArray(value, path, minimum, maximum);
        foreach (string actual in values)
        {
            if (permitted.Contains(actual, StringComparer.Ordinal))
                continue;
            if (actual.StartsWith("class:", StringComparison.Ordinal)
                && actual.Length > "class:".Length)
            {
                IdentifierValue(actual["class:".Length..], path);
                continue;
            }
            throw Error(path,
                $"invalid priority term '{actual}'; expected one of "
                + $"[{string.Join(", ", permitted)}] or 'class:<classId>'.");
        }
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw Error(path, "duplicate priority term.");
    }

    private static void OneOfArray(
        JsonElement value,
        string path,
        int minimum,
        int maximum,
        params string[] permitted)
    {
        string[] values = StringArray(value, path, minimum, maximum);
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length
            || values.Any(actual => !permitted.Contains(actual, StringComparer.Ordinal)))
            throw Error(path, $"invalid or duplicate value in [{string.Join(", ", values)}].");
    }

    private static void Reference(
        JsonElement value,
        string name,
        IReadOnlySet<string> ids,
        string path,
        bool allowEmpty = false)
    {
        string reference = RequiredString(value, name, path);
        if (allowEmpty && reference.Length == 0)
            return;
        if (!ids.Contains(reference))
            throw Error(path, $"'{name}' references unknown '{reference}'.");
    }

    private static string Hash(JsonElement value, string name, string path)
    {
        string hash = RequiredString(value, name, path);
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character))
            || hash.Any(character => char.IsLetter(character) && char.IsUpper(character)))
            throw Error(path, $"'{name}' must be a lowercase SHA-256.");
        return hash;
    }

    private static void Point(
        JsonElement value,
        string path,
        int minimum,
        int maximum)
    {
        JsonElement[] coordinates = BoundedArray(value, path, 2, 2);
        foreach ((JsonElement coordinate, int index) in coordinates.Select((v, i) => (v, i)))
        {
            if (!coordinate.TryGetInt32(out int number)
                || number < minimum || number > maximum)
                throw Error(path, $"coordinate {index} must be {minimum}..{maximum}.");
        }
    }

    private static void Rectangle(JsonElement value, string path)
    {
        JsonElement[] coordinates = BoundedArray(value, path, 4, 4);
        int[] numbers = coordinates.Select((coordinate, index) =>
        {
            if (!coordinate.TryGetInt32(out int number) || number is < 0 or > 255)
                throw Error(path, $"coordinate {index} must be 0..255.");
            return number;
        }).ToArray();
        if (numbers[0] > numbers[2] || numbers[1] > numbers[3])
            throw Error(path, "rectangle minimum exceeds maximum.");
    }

    private static string Sha256(byte[] source) =>
        Convert.ToHexStringLower(SHA256.HashData(source));

    private static InvalidDataException Error(string path, string message) =>
        new($"{path}: {message}");
}
