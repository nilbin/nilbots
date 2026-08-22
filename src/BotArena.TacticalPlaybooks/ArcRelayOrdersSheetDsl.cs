using System.Text.Json;

namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Validation for the Arc Relay Orders v2 source language. This is the only
/// authored grammar: validation walks shared-order expansions directly and
/// never constructs the retired doctrine/custody IR.
/// </summary>
public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static readonly HashSet<string> UpgradeTracks =
        new(StringComparer.Ordinal) { "damage", "vision", "reach", "vitality" };

    private static readonly HashSet<string> ConditionFacts =
        new(StringComparer.Ordinal)
        {
            "self-health-percent", "self-level", "self-carrying",
            "self-carried-core-bank-value",
            "self-carrying-core-from-well",
            "self-stuck-ticks", "self-order-ticks", "elapsed-ticks",
            "self-in-zone",
            "self-nearest-visible-enemy-distance",
            "self-nearest-visible-loose-core-distance",
            "self-nearest-heal-zone-distance",
            "self-ticks-since-signature-use", "signature-ready",
            "signature-commitment-active",
            "deployment-active", "deployment-lifetime-percent",
            "deployment-durability-percent", "self-deployment-in-zone",
            "role-live-count", "role-lowest-health-percent",
            "role-highest-level", "role-in-zone-count",
            "role-deployments-in-zone-count", "live-friendlies",
            "formation-late-count",
            "friendly-carriers", "live-enemies",
            "friendly-carriers-in-roles",
            "friendly-veterans-in-roles",
            "friendly-respawns-next-wave", "enemy-respawns-next-wave",
            "ticks-until-respawn-wave",
            "known-enemy-carriers", "visible-enemy-carriers",
            "known-loose-cores", "visible-loose-cores", "secured-cores",
            "reactor-integrity", "reactor-charge", "reactor-pulses",
            "enemy-reactor-charge",
            "ticks-without-objective-progress", "friendlies-in-zone",
            "visible-enemies-in-zone", "remembered-enemies-in-zone",
            "zone-cleared", "friendly-fights-in-zone",
            "known-loose-cores-in-zone", "well-has-outstanding-core",
            "well-ticks-until-birth", "well-socket-filled",
            "observed-enemy-entries-in-zone",
            "distinct-enemy-lives-observed-in-zone",
            "confirmed-enemy-destructions-in-zone",
            "zone-fully-observed-throughout-interval",
            "friendly-core-pickups",
            "friendly-core-acquisitions", "friendly-core-banks",
            "friendly-core-drops", "friendly-core-losses",
            "friendly-core-resolutions", "friendly-destructions",
            "workshop-ready", "workshop-training", "workshop-training-self",
            "workshop-training-enemy", "workshop-contested",
            "workshop-dormant", "friendly-workshop-completions",
        };

    /// <summary>Facts that read one Workshop's public state and therefore
    /// carry the 'workshop' selector (north/south/either).</summary>
    private static readonly HashSet<string> WorkshopFacts =
        new(StringComparer.Ordinal)
        {
            "workshop-ready", "workshop-training", "workshop-training-self",
            "workshop-training-enemy", "workshop-contested",
            "workshop-dormant", "friendly-workshop-completions",
        };

    // Facts whose answer depends on the evaluating body. They are meaningless
    // in the squad-level program cases, which have no body.
    private static readonly HashSet<string> BodyScopedFacts =
        new(StringComparer.Ordinal)
        {
            "signature-ready", "signature-commitment-active",
            "deployment-active", "deployment-lifetime-percent",
            "deployment-durability-percent",
        };

    private static readonly HashSet<string> RoleFacts =
        new(StringComparer.Ordinal)
        {
            "role-live-count", "role-lowest-health-percent",
            "role-highest-level", "role-in-zone-count",
            "role-deployments-in-zone-count",
        };

    private static readonly HashSet<string> ZoneFacts =
        new(StringComparer.Ordinal)
        {
            "self-in-zone", "self-deployment-in-zone",
            "role-in-zone-count", "role-deployments-in-zone-count",
            "friendlies-in-zone",
            "visible-enemies-in-zone", "remembered-enemies-in-zone",
            "zone-cleared", "friendly-fights-in-zone",
            "known-loose-cores-in-zone", "observed-enemy-entries-in-zone",
            "distinct-enemy-lives-observed-in-zone",
            "confirmed-enemy-destructions-in-zone",
            "zone-fully-observed-throughout-interval",
        };

    private static readonly HashSet<string> HistoricalFacts =
        new(StringComparer.Ordinal)
        {
            "observed-enemy-entries-in-zone",
            "distinct-enemy-lives-observed-in-zone",
            "confirmed-enemy-destructions-in-zone",
            "zone-fully-observed-throughout-interval",
            "friendly-core-pickups",
            "friendly-core-acquisitions", "friendly-core-banks",
            "friendly-core-drops", "friendly-core-losses",
            "friendly-core-resolutions", "friendly-destructions",
            "friendly-workshop-completions",
        };

    private static readonly HashSet<string> RoleListFacts =
        new(StringComparer.Ordinal)
        {
            "friendly-carriers-in-roles", "friendly-core-pickups",
            "friendly-veterans-in-roles",
            "friendly-core-acquisitions", "friendly-core-banks",
            "friendly-core-drops", "friendly-core-losses",
            "friendly-core-resolutions", "formation-late-count",
        };

    /// <summary>Facts whose spatial subject may be a disconnected union.
    /// The singular zone spelling remains valid for ordinary Orders; Tactics
    /// materialization uses zones so overlap is canonicalized at runtime.</summary>
    private static readonly HashSet<string> MultiZoneFacts =
        new(StringComparer.Ordinal)
        {
            "remembered-enemies-in-zone", "zone-cleared",
            "friendly-fights-in-zone", "formation-late-count",
        };

    private static readonly HashSet<string> CoreEvidenceFacts =
        new(StringComparer.Ordinal)
        {
            "friendly-core-pickups", "friendly-core-acquisitions",
            "friendly-core-banks", "friendly-core-drops",
            "friendly-core-losses", "friendly-core-resolutions",
        };

    private static readonly HashSet<string> WellFacts =
        new(StringComparer.Ordinal)
        {
            "well-has-outstanding-core", "well-ticks-until-birth",
            "well-socket-filled", "self-carrying-core-from-well",
        };

    private static readonly HashSet<string> FreshnessFacts =
        new(StringComparer.Ordinal)
        {
            "known-enemy-carriers", "remembered-enemies-in-zone",
        };

    private static SheetValidation ValidateOrdersSheet(
        JsonElement root,
        string path)
    {
        string schema = OneOf(
            root, "schema", path, PlaybookSchema, ProgramSchema);
        bool programSheet = string.Equals(
            schema, ProgramSchema, StringComparison.Ordinal);
        Object(root, path,
            programSheet
                ?
                [
                    "schema", "sheetId", "mapPlan", "roles", "sharedOrders",
                    "capacities", "conditions", "program",
                ]
                :
                [
                    "schema", "sheetId", "mapPlan", "roles", "sharedOrders",
                    "capacities", "conditions",
                ]);
        Identifier(root, "sheetId", path);
        NonEmptyString(root, "mapPlan", path);

        JsonElement[] roleValues = BoundedArray(
            root.GetProperty("roles"), $"{path}.roles", 1, 8);
        var roles = new Dictionary<string, RoleInfo>(StringComparer.Ordinal);
        var composition = new List<string>(8);
        for (int index = 0; index < roleValues.Length; index++)
        {
            JsonElement role = roleValues[index];
            string at = $"{path}.roles[{index}]";
            Object(role, at, ["id", "classes", "upgradePath", "orders"]);
            string id = Identifier(role, "id", at);
            if (id.Length > 24)
                throw Error($"{at}.id", "role id exceeds 24 characters.");
            if (roles.ContainsKey(id))
                throw Error(at, $"duplicate role '{id}'.");
            string[] classes = StringValues(
                role.GetProperty("classes"), $"{at}.classes", 1, 8);
            foreach (string classId in classes)
            {
                if (!ArcRelaySignatureAuthoringCatalog.All.Any(value =>
                        string.Equals(value.ClassId, classId,
                            StringComparison.Ordinal)))
                    throw Error($"{at}.classes", $"unknown class '{classId}'.");
                composition.Add(classId);
            }
            string[] upgrades = StringValues(
                role.GetProperty("upgradePath"), $"{at}.upgradePath", 1, 4);
            foreach (string upgrade in upgrades)
            {
                if (!UpgradeTracks.Contains(upgrade))
                    throw Error($"{at}.upgradePath",
                        $"unknown upgrade track '{upgrade}'.");
            }
            roles.Add(id, new RoleInfo(id, classes, role.GetProperty("orders")));
        }
        if (composition.Count != 8
            || composition.GroupBy(value => value, StringComparer.Ordinal)
                .Any(group => group.Count() > 2))
        {
            throw Error($"{path}.roles",
                "flattened role classes must contain exactly eight bodies "
                + "under the two-copy class cap.");
        }

        Dictionary<string, int> capacities = ValidateCapacities(
            root.GetProperty("capacities"), path);
        Dictionary<string, ConditionInfo> conditions = ValidateConditions(
            root.GetProperty("conditions"), roles.Keys.ToHashSet(
                StringComparer.Ordinal), path, programSheet);
        Dictionary<string, JsonElement> sharedOrders = ValidateSharedOrderTable(
            root.GetProperty("sharedOrders"), path);

        var consumers = new ConsumerLedger(
            sharedOrders.Keys, capacities.Keys, conditions.Keys);
        if (programSheet)
        {
            ValidateProgram(
                root.GetProperty("program"), $"{path}.program", conditions,
                consumers);
        }
        foreach ((string roleId, RoleInfo role) in roles)
        {
            var orderIds = new HashSet<string>(StringComparer.Ordinal);
            JsonElement[] orders = BoundedArray(
                role.Orders, $"{path}.roles.{roleId}.orders", 1, 16);
            for (int index = 0; index < orders.Length; index++)
            {
                ValidateOrder(
                    orders[index],
                    $"{path}.roles.{roleId}.orders[{index}]",
                    role,
                    roles,
                    sharedOrders,
                    capacities,
                    conditions,
                    consumers,
                    orderIds,
                    branchDepth: 0,
                    externalId: null,
                    allowSharedReference: true);
            }
        }
        foreach (string tag in consumers.DeclaredPhaseTags)
        {
            if (!consumers.UsedProgramTags.Contains(tag))
                throw Error($"{path}.program",
                    $"phase '{tag}' owns no phase-tagged order in any role.");
        }

        foreach ((string id, int count) in consumers.SharedOrders)
        {
            if (count < 2)
                throw Error($"{path}.sharedOrders.{id}",
                    "a shared order needs at least two expanded consumers; "
                    + "inline a one-off order.");
        }
        foreach ((string id, int count) in consumers.Capacities)
        {
            if (count < 2)
                throw Error($"{path}.capacities.{id}",
                    "a named capacity needs at least two expanded consumers; "
                    + "use an inline capacity for one leaf.");
        }
        foreach ((string id, int count) in consumers.Conditions)
        {
            if (count == 0)
                throw Error($"{path}.conditions.{id}",
                    "condition has no order consumer.");
        }

        return new SheetValidation(
            [.. composition], consumers.Zones, consumers.Routes,
            consumers.Anchors, consumers.ForwardRoutes);
    }

    private static Dictionary<string, int> ValidateCapacities(
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.capacities", "expected object.");
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            IdentifierValue(property.Name, $"{path}.capacities", "capacity");
            if (property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out int capacity)
                || capacity is < 1 or > 8)
            {
                throw Error($"{path}.capacities.{property.Name}",
                    "capacity must be an integer in [1, 8].");
            }
            if (!result.TryAdd(property.Name, capacity))
                throw Error($"{path}.capacities", "duplicate capacity.");
        }
        return result;
    }

    private static Dictionary<string, ConditionInfo> ValidateConditions(
        JsonElement value,
        IReadOnlySet<string> roles,
        string path,
        bool programSheet)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.conditions", "expected object.");
        var result = new Dictionary<string, ConditionInfo>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string id = property.Name;
            IdentifierValue(id, $"{path}.conditions", "condition");
            if (id == "always")
                throw Error($"{path}.conditions", "'always' is reserved.");
            string at = $"{path}.conditions.{id}";
            Object(property.Value, at,
                ["fact", "operator", "value"],
                [
                    "role", "zone", "well", "freshnessTicks",
                    "windowTicks", "since", "signature", "coreBankValue",
                    "roles", "zones", "correlateAcquisition", "outcomes",
                    "level",
                    "workshop",
                ]);
            string fact = RequiredString(property.Value, "fact", at);
            if (!ConditionFacts.Contains(fact))
                throw Error(at, $"unknown condition fact '{fact}'.");
            bool hasWorkshop = property.Value.TryGetProperty(
                "workshop", out JsonElement workshopSelector);
            if (hasWorkshop != WorkshopFacts.Contains(fact))
            {
                throw Error(at, hasWorkshop
                    ? $"'{fact}' does not take a workshop."
                    : $"'{fact}' needs a workshop selector.");
            }
            if (hasWorkshop
                && StringValue(workshopSelector, $"{at}.workshop")
                    is not ("north" or "south" or "either"))
            {
                throw Error($"{at}.workshop",
                    "workshop must be north/south/either.");
            }
            OneOf(property.Value, "operator", at,
                "at-least", "at-most", "equals", "less-than", "greater-than");
            int comparison = RequiredInt(property.Value, "value", at);
            if ((fact is "self-health-percent" or
                    "role-lowest-health-percent" or
                    "deployment-lifetime-percent" or
                    "deployment-durability-percent")
                && comparison is < 0 or > 100)
            {
                throw Error(at, "health percentages must be in [0, 100].");
            }
            if (fact is "self-nearest-visible-enemy-distance"
                    or "self-nearest-visible-loose-core-distance"
                    or "self-nearest-heal-zone-distance"
                && comparison is < 0 or > 31)
            {
                throw Error(at,
                    "firing-approach distance must be in [0, 31].");
            }
            if (fact == "self-ticks-since-signature-use"
                && comparison is < 0 or > 1200)
            {
                throw Error(at,
                    "signature-use age must be in [0, 1200].");
            }
            if (fact == "elapsed-ticks" && comparison is < 0 or > 1200)
                throw Error(at, "elapsed ticks must be in [0, 1200].");
            if ((fact is "self-carrying" or "self-carrying-core-from-well"
                    or "self-in-zone" or
                    "self-deployment-in-zone" or
                    "signature-ready" or "signature-commitment-active"
                    or "deployment-active")
                && comparison is not (0 or 1))
            {
                throw Error(at, $"'{fact}' compares against 0 or 1.");
            }
            if (fact == "self-carried-core-bank-value"
                && comparison is < 0 or > 4)
            {
                throw Error(at, "carried Core bank value must be in [0, 4].");
            }
            if (fact is "friendly-respawns-next-wave"
                    or "enemy-respawns-next-wave" or "live-enemies"
                && comparison is < 0 or > 8)
            {
                throw Error(at, $"'{fact}' must be in [0, 8].");
            }
            if (fact == "formation-late-count" && comparison is < 0 or > 2)
                throw Error(at, "formation allowed-late must be in [0, 2].");
            if (fact == "friendly-veterans-in-roles"
                && comparison is < 0 or > 8)
            {
                throw Error(at, "veteran count must be in [0, 8].");
            }

            bool hasRole = property.Value.TryGetProperty("role", out JsonElement role);
            bool hasRoles = property.Value.TryGetProperty("roles", out JsonElement roleList);
            bool hasZone = property.Value.TryGetProperty("zone", out JsonElement zone);
            bool hasZones = property.Value.TryGetProperty("zones", out JsonElement zoneList);
            bool hasWell = property.Value.TryGetProperty("well", out JsonElement well);
            bool hasFreshness = property.Value.TryGetProperty(
                "freshnessTicks", out JsonElement freshness);
            bool hasWindow = property.Value.TryGetProperty(
                "windowTicks", out JsonElement window);
            bool hasSince = property.Value.TryGetProperty(
                "since", out JsonElement since);
            bool hasSignature = property.Value.TryGetProperty(
                "signature", out JsonElement signature);
            bool hasCoreBankValue = property.Value.TryGetProperty(
                "coreBankValue", out JsonElement coreBankValue);
            bool correlatesAcquisition = property.Value.TryGetProperty(
                "correlateAcquisition", out JsonElement correlation);
            bool hasOutcomes = property.Value.TryGetProperty(
                "outcomes", out JsonElement outcomes);
            bool hasLevel = property.Value.TryGetProperty(
                "level", out JsonElement level);
            if (RoleFacts.Contains(fact))
            {
                // role-live-count alone may count across a roles list
                // instead of a single role (the release gate's short-handed
                // excuse names a whole group).
                bool listInstead = fact == "role-live-count"
                    && !hasRole && hasRoles;
                if (!listInstead
                    && (!hasRole || role.ValueKind != JsonValueKind.String
                        || !roles.Contains(role.GetString()!)))
                {
                    throw Error(at, $"'{fact}' needs a declared role.");
                }
            }
            else if (hasRole)
                throw Error(at, $"'{fact}' does not take role.");
            if (fact is "friendly-carriers-in-roles"
                    or "friendly-veterans-in-roles"
                    or "formation-late-count"
                && !hasRoles)
                throw Error(at, $"'{fact}' needs declared roles.");
            bool takesRoleList = RoleListFacts.Contains(fact)
                || fact == "role-live-count";
            if (takesRoleList && hasRoles)
            {
                string[] selectedRoles = StringArray(roleList, $"{at}.roles", 1, 8);
                if (selectedRoles.Distinct(StringComparer.Ordinal).Count()
                        != selectedRoles.Length
                    || selectedRoles.Any(value => !roles.Contains(value)))
                    throw Error(at, $"'{fact}' needs unique declared roles.");
            }
            else if (!takesRoleList && hasRoles)
                throw Error(at, $"'{fact}' does not take roles.");
            if (fact == "friendly-veterans-in-roles")
            {
                if (!hasLevel || level.ValueKind != JsonValueKind.Number
                    || !level.TryGetInt32(out int minimumLevel)
                    || minimumLevel is < 2 or > 5)
                {
                    throw Error(at, $"'{fact}' needs level in [2, 5].");
                }
            }
            else if (hasLevel)
            {
                throw Error(at, $"'{fact}' does not take level.");
            }
            if (fact == "friendly-core-resolutions")
            {
                if (!hasOutcomes)
                    throw Error(at, $"'{fact}' needs one or more outcomes.");
                string[] selected = StringArray(outcomes, $"{at}.outcomes", 1, 3);
                if (selected.Distinct(StringComparer.Ordinal).Count() != selected.Length
                    || selected.Any(value => value is not ("bank" or "drop" or "loss")))
                    throw Error(at, $"'{fact}' outcomes must be unique bank/drop/loss values.");
            }
            else if (hasOutcomes)
                throw Error(at, $"'{fact}' does not take outcomes.");
            string[] selectedZones = [];
            if (MultiZoneFacts.Contains(fact))
            {
                if (hasZone == hasZones)
                    throw Error(at, $"'{fact}' needs exactly one of zone or zones.");
                if (hasZone && zone.ValueKind != JsonValueKind.String)
                    throw Error(at, $"'{fact}' needs a valid zone.");
                selectedZones = hasZone
                    ? [zone.GetString()!]
                    : fact == "formation-late-count"
                        ? StringValues(zoneList, $"{at}.zones", 1, 8)
                        : StringArray(zoneList, $"{at}.zones", 1, 8);
                foreach (string selectedZone in selectedZones)
                    IdentifierValue(selectedZone, at, "zone");
                if (fact != "formation-late-count"
                    && selectedZones.Distinct(StringComparer.Ordinal).Count()
                        != selectedZones.Length)
                    throw Error(at, $"'{fact}' needs unique zones.");
            }
            else if (ZoneFacts.Contains(fact))
            {
                if (!hasZone || zone.ValueKind != JsonValueKind.String)
                    throw Error(at, $"'{fact}' needs a zone.");
                selectedZones = [zone.GetString()!];
                IdentifierValue(selectedZones[0], at, "zone");
                if (hasZones)
                    throw Error(at, $"'{fact}' does not take zones.");
            }
            else if (hasZone || hasZones)
            {
                if (fact != "self-nearest-visible-enemy-distance" || hasZones)
                    throw Error(at, $"'{fact}' does not take zone(s).");
                if (zone.ValueKind != JsonValueKind.String)
                    throw Error(at, $"'{fact}' needs a valid zone when supplied.");
                selectedZones = [zone.GetString()!];
                IdentifierValue(selectedZones[0], at, "zone");
            }
            if (fact == "formation-late-count")
            {
                string[] selectedRoles = StringArray(
                    roleList, $"{at}.roles", 1, 8);
                if (selectedRoles.Length != selectedZones.Length)
                {
                    throw Error(at,
                        "formation-late-count pairs each role with one zone.");
                }
            }
            if (WellFacts.Contains(fact))
            {
                if (!hasWell || well.ValueKind != JsonValueKind.String
                    || well.GetString() is not
                        ("north" or "centre" or "south" or "own" or "enemy"))
                {
                    throw Error(at,
                        $"'{fact}' needs north/centre/south/own/enemy well.");
                }
            }
            else if ((CoreEvidenceFacts.Contains(fact)
                    || fact == "friendly-carriers-in-roles") && hasWell)
            {
                if (well.ValueKind != JsonValueKind.String
                    || well.GetString() is not
                        ("north" or "centre" or "south" or "own" or "enemy"))
                    throw Error(at, $"'{fact}' has an invalid Well filter.");
            }
            else if (hasWell)
                throw Error(at, $"'{fact}' does not take well.");
            if (FreshnessFacts.Contains(fact))
            {
                if (!hasFreshness || freshness.ValueKind != JsonValueKind.Number
                    || !freshness.TryGetInt32(out int ticks)
                    || ticks is < 1 or > 600)
                {
                    throw Error(at, $"'{fact}' needs freshnessTicks in [1, 600].");
                }
            }
            else if (hasFreshness)
                throw Error(at, $"'{fact}' does not take freshnessTicks.");

            if (fact == "role-deployments-in-zone-count")
            {
                if (!hasSignature || signature.ValueKind != JsonValueKind.String
                    || !ArcRelaySignatureAuthoringCatalog.TryForSkill(
                        signature.GetString()!, out _))
                {
                    throw Error(at,
                        "'role-deployments-in-zone-count' needs a current signature id.");
                }
            }
            else if (hasSignature)
            {
                throw Error(at, $"'{fact}' does not take signature.");
            }

            if (hasCoreBankValue)
            {
                if (fact is not ("self-nearest-visible-loose-core-distance"
                        or "friendly-carriers-in-roles"
                        or "friendly-core-pickups"
                        or "friendly-core-acquisitions"
                        or "friendly-core-banks"
                        or "friendly-core-drops"
                        or "friendly-core-losses"
                        or "friendly-core-resolutions"))
                    throw Error(at, $"'{fact}' does not take coreBankValue.");
                ValidateCoreBankValue(coreBankValue, $"{at}.coreBankValue");
            }
            if (correlatesAcquisition
                && (fact is not ("friendly-core-banks" or "friendly-core-drops"
                        or "friendly-core-losses" or "friendly-core-resolutions")
                    || correlation.ValueKind != JsonValueKind.True
                    || !hasRoles))
            {
                throw Error(at,
                    "correlateAcquisition is true only for role-filtered bank/drop evidence.");
            }

            if (HistoricalFacts.Contains(fact))
            {
                if (hasWindow == hasSince)
                {
                    throw Error(at,
                        $"'{fact}' needs exactly one of windowTicks or since.");
                }
                if (hasWindow
                    && (window.ValueKind != JsonValueKind.Number
                        || !window.TryGetInt32(out int windowTicks)
                        || windowTicks is < 1 or > 1200))
                {
                    throw Error(at, $"'{fact}' needs windowTicks in [1, 1200].");
                }
                if (hasSince
                    && (since.ValueKind != JsonValueKind.String
                        || since.GetString() is not
                            ("match-start" or "node-entry" or "phase-entry")))
                {
                    throw Error(at,
                        $"'{fact}' since must be match-start, node-entry, "
                        + "or phase-entry.");
                }
                if (hasSince && since.GetString() == "phase-entry"
                    && !programSheet)
                {
                    throw Error(at,
                        "since phase-entry needs the program schema "
                        + $"'{ProgramSchema}'.");
                }
                if (fact == "zone-fully-observed-throughout-interval"
                    && comparison is not (0 or 1))
                {
                    throw Error(at,
                        "zone coverage compares against 0 or 1.");
                }
                if (comparison < 0)
                    throw Error(at, "historical evidence cannot compare below 0.");
            }
            else if (hasWindow || hasSince)
            {
                throw Error(at,
                    $"'{fact}' does not take windowTicks or since.");
            }

            result.Add(id, new ConditionInfo(
                fact,
                selectedZones,
                hasSince ? since.GetString() : null));
        }
        return result;
    }

    /// <summary>
    /// The squad program table: ordered tactics, each with start/abort cases,
    /// an optional duration, and forward-only phase transitions. Declares the
    /// legal node tags ("t1" and "t1/p2") on the ledger and counts every
    /// referenced condition as a consumer.
    /// </summary>
    private static void ValidateProgram(
        JsonElement value,
        string path,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        ConsumerLedger consumers)
    {
        Object(value, path, ["tactics"]);
        JsonElement[] tactics = BoundedArray(
            value.GetProperty("tactics"), $"{path}.tactics", 1, 8);
        var tacticIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < tactics.Length; index++)
        {
            JsonElement tactic = tactics[index];
            string at = $"{path}.tactics[{index}]";
            Object(tactic, at, ["id", "name", "phases"],
                ["start", "abort", "maxTicks", "rearm"]);
            string id = Identifier(tactic, "id", at);
            if (id.Length > 24)
                throw Error($"{at}.id", "tactic id exceeds 24 characters.");
            if (!tacticIds.Add(id))
                throw Error(at, $"duplicate tactic '{id}'.");
            DisplayName(tactic, "name", at);
            if (tactic.TryGetProperty("maxTicks", out _))
                Range(tactic, "maxTicks", at, 1, 1200);
            if (tactic.TryGetProperty("rearm", out _))
                Range(tactic, "rearm", at, 0, 1200);
            foreach (string section in new[] { "start", "abort" })
            {
                if (!tactic.TryGetProperty(section, out JsonElement cases))
                    continue;
                JsonElement[] entries = BoundedArray(
                    cases, $"{at}.{section}", 1, 4);
                for (int c = 0; c < entries.Length; c++)
                {
                    ValidateProgramCase(
                        entries[c], $"{at}.{section}[{c}]", conditions,
                        consumers);
                }
            }
            JsonElement[] phases = BoundedArray(
                tactic.GetProperty("phases"), $"{at}.phases", 1, 8);
            var phaseIds = new List<string>(phases.Length);
            for (int p = 0; p < phases.Length; p++)
            {
                string phaseAt = $"{at}.phases[{p}]";
                Object(phases[p], phaseAt, ["id", "name"],
                    ["continue", "custodyGroups"]);
                string phaseId = Identifier(phases[p], "id", phaseAt);
                if (phaseId.Length > 24)
                    throw Error($"{phaseAt}.id",
                        "phase id exceeds 24 characters.");
                if (phaseIds.Contains(phaseId))
                    throw Error(phaseAt, $"duplicate phase '{phaseId}'.");
                DisplayName(phases[p], "name", phaseAt);
                phaseIds.Add(phaseId);
                consumers.DeclaredPhaseTags.Add($"{id}/{phaseId}");
                consumers.DeclaredProgramTags.Add($"{id}/{phaseId}");
                if (phases[p].TryGetProperty(
                        "custodyGroups", out JsonElement custodyGroups))
                {
                    JsonElement[] groups = BoundedArray(
                        custodyGroups, $"{phaseAt}.custodyGroups", 1, 3);
                    var seen = new HashSet<int>();
                    for (int group = 0; group < groups.Length; group++)
                    {
                        foreach (JsonElement unitValue in BoundedArray(
                                     groups[group],
                                     $"{phaseAt}.custodyGroups[{group}]", 1, 8))
                        {
                            if (unitValue.ValueKind != JsonValueKind.Number
                                || !unitValue.TryGetInt32(out int unit)
                                || unit is < 0 or > 7
                                || !seen.Add(unit))
                            {
                                throw Error(
                                    $"{phaseAt}.custodyGroups[{group}]",
                                    "custody groups contain unique unit ids 0..7.");
                            }
                        }
                    }
                }
            }
            consumers.DeclaredProgramTags.Add(id);
            for (int p = 0; p < phases.Length; p++)
            {
                string phaseAt = $"{at}.phases[{p}]";
                bool last = p == phases.Length - 1;
                if (!phases[p].TryGetProperty("continue", out JsonElement rules))
                {
                    // A last phase with no transition holds forever — the
                    // explicit persistent form. Anywhere earlier it is a
                    // dead end the sequence can never leave.
                    if (!last)
                        throw Error(phaseAt,
                            "every phase before the last needs a continue "
                            + "rule; only the final phase may hold forever.");
                    continue;
                }
                JsonElement[] entries = BoundedArray(
                    rules, $"{phaseAt}.continue", 1, 3);
                for (int r = 0; r < entries.Length; r++)
                {
                    string ruleAt = $"{phaseAt}.continue[{r}]";
                    Object(entries[r], ruleAt, ["when", "to"]);
                    string to = RequiredString(entries[r], "to", ruleAt);
                    if (to == "next")
                    {
                        if (last)
                            throw Error($"{ruleAt}.to",
                                "the final phase has no next; use 'finish'.");
                    }
                    else if (to != "finish")
                    {
                        int target = phaseIds.IndexOf(to);
                        if (target < 0)
                            throw Error($"{ruleAt}.to",
                                $"unknown phase '{to}'.");
                        if (target <= p)
                            throw Error($"{ruleAt}.to",
                                "transitions are forward-only; a phase may "
                                + "not jump to itself or backward.");
                    }
                    ValidateProgramCase(
                        entries[r].GetProperty("when"), $"{ruleAt}.when",
                        conditions, consumers);
                }
            }
        }
    }

    /// <summary>One program case: all(all…) AND any(any…), entries being
    /// condition names or one nested all/any group. Squad-level only —
    /// body-scoped facts and node-entry anchors are rejected here.</summary>
    private static void ValidateProgramCase(
        JsonElement value,
        string path,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        ConsumerLedger consumers)
    {
        Object(value, path, [], ["all", "any"]);
        bool anySide = false;
        foreach (string op in new[] { "all", "any" })
        {
            if (!value.TryGetProperty(op, out JsonElement side))
                continue;
            anySide = true;
            JsonElement[] items = BoundedArray(side, $"{path}.{op}", 1, 24);
            for (int index = 0; index < items.Length; index++)
            {
                string entryPath = $"{path}.{op}[{index}]";
                JsonElement entry = items[index];
                if (entry.ValueKind == JsonValueKind.String)
                {
                    ValidateProgramConditionName(
                        entry.GetString()!, entryPath, conditions, consumers);
                    continue;
                }
                if (entry.ValueKind != JsonValueKind.Object)
                    throw Error(entryPath,
                        "expected condition name or all/any group.");
                string[] operators = ["all", "any"];
                string[] found = operators.Where(name =>
                    entry.TryGetProperty(name, out _)).ToArray();
                if (found.Length != 1)
                    throw Error(entryPath,
                        "group needs exactly one of all/any.");
                Object(entry, entryPath, [found[0]]);
                string[] names = StringArray(
                    entry.GetProperty(found[0]),
                    $"{entryPath}.{found[0]}", 2, 8);
                foreach (string name in names)
                {
                    ValidateProgramConditionName(
                        name, entryPath, conditions, consumers);
                }
            }
        }
        if (!anySide)
            throw Error(path, "a case needs at least one of all/any.");
    }

    private static void ValidateProgramConditionName(
        string name,
        string path,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        ConsumerLedger consumers)
    {
        if (!conditions.TryGetValue(name, out ConditionInfo? info))
            throw Error(path, $"unknown condition '{name}'.");
        if (info.Fact.StartsWith("self-", StringComparison.Ordinal)
            || BodyScopedFacts.Contains(info.Fact))
        {
            throw Error(path,
                $"condition '{name}' reads the body-scoped fact "
                + $"'{info.Fact}'; program cases are squad-level.");
        }
        if (info.Since == "node-entry")
        {
            throw Error(path,
                $"condition '{name}' anchors at node-entry; program cases "
                + "anchor at phase-entry.");
        }
        consumers.Conditions[name]++;
        foreach (string zone in info.Zones)
            consumers.ReferenceZone(zone, path);
    }

    private static void DisplayName(JsonElement owner, string name, string path)
    {
        string value = NonEmptyString(owner, name, path);
        if (value.Length > 48)
            throw Error(path, $"'{name}' exceeds 48 characters.");
    }

    private static Dictionary<string, JsonElement> ValidateSharedOrderTable(
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.sharedOrders", "expected object.");
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            IdentifierValue(property.Name, $"{path}.sharedOrders", "shared order");
            if (property.Value.ValueKind != JsonValueKind.Object)
                throw Error($"{path}.sharedOrders.{property.Name}",
                    "expected order object.");
            if (property.Value.TryGetProperty("id", out _)
                || property.Value.TryGetProperty("shared", out _))
            {
                throw Error($"{path}.sharedOrders.{property.Name}",
                    "the map key is the id and shared orders cannot reference "
                    + "another shared order.");
            }
            result.Add(property.Name, property.Value);
        }
        return result;
    }

    private static void ValidateOrder(
        JsonElement value,
        string path,
        RoleInfo role,
        IReadOnlyDictionary<string, RoleInfo> roles,
        IReadOnlyDictionary<string, JsonElement> sharedOrders,
        IReadOnlyDictionary<string, int> capacities,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        ConsumerLedger consumers,
        HashSet<string> orderIds,
        int branchDepth,
        string? externalId,
        bool allowSharedReference)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected order object.");
        if (value.TryGetProperty("shared", out JsonElement shared))
        {
            if (!allowSharedReference)
                throw Error(path, "shared orders cannot reference shared orders.");
            Object(value, path, ["shared"]);
            string sharedId = StringValue(shared, $"{path}.shared");
            if (!sharedOrders.TryGetValue(sharedId, out JsonElement definition))
                throw Error(path, $"unknown shared order '{sharedId}'.");
            consumers.SharedOrders[sharedId]++;
            ValidateOrder(
                definition, $"{path}.shared({sharedId})", role, roles, sharedOrders,
                capacities, conditions, consumers, orderIds, branchDepth,
                externalId: sharedId, allowSharedReference: false);
            return;
        }

        string id;
        if (externalId is string supplied)
        {
            if (value.TryGetProperty("id", out _))
                throw Error(path, "a shared definition does not repeat its id.");
            id = supplied;
        }
        else
        {
            id = Identifier(value, "id", path);
        }
        if (!orderIds.Add(id))
            throw Error(path, $"duplicate expanded order id '{id}' in role '{role.Id}'.");

        string[] actionKeys =
            ["orders", "move", "fight", "scan", "use", "handoff", "drop"];
        string[] found = actionKeys.Where(name => value.TryGetProperty(name, out _))
            .ToArray();
        if (found.Length != 1)
            throw Error(path, "an order needs exactly one branch or action.");
        var required = externalId is null
            ? new[] { "id", found[0] }
            : new[] { found[0] };
        var optional = new[]
        {
            "when", "until", "timeout", "cooldown", "rejoin", "capacity",
            "urgency", "select", "phase",
        };
        Object(value, path, required, optional);
        if (value.TryGetProperty("phase", out JsonElement phaseTag))
        {
            string tag = StringValue(phaseTag, $"{path}.phase");
            if (consumers.DeclaredProgramTags.Count == 0)
                throw Error(path,
                    $"phase tags need the program schema '{ProgramSchema}'.");
            if (!consumers.DeclaredProgramTags.Contains(tag))
                throw Error($"{path}.phase", $"unknown program tag '{tag}'.");
            consumers.UsedProgramTags.Add(tag);
        }
        ValidateLifecycle(value, path, conditions, consumers);
        if (value.TryGetProperty("urgency", out _))
            OneOf(value, "urgency", path, "routine", "normal", "urgent");
        if (value.TryGetProperty("rejoin", out JsonElement rejoin))
        {
            if (rejoin.ValueKind != JsonValueKind.True)
                throw Error(path, "rejoin must be true when present.");
            if (!value.TryGetProperty("when", out _))
                throw Error(path, "rejoin needs a when gate to bypass.");
        }

        if (found[0] == "orders")
        {
            if (value.TryGetProperty("capacity", out _))
                throw Error(path, "capacity belongs on an action leaf, not a branch.");
            if (value.TryGetProperty("select", out _))
                throw Error(path,
                    "select belongs on a capacity-limited action leaf, not a branch.");
            if (branchDepth >= 2)
                throw Error(path, "orders may contain at most two branch levels.");
            JsonElement[] children = BoundedArray(
                value.GetProperty("orders"), $"{path}.orders", 1, 16);
            for (int index = 0; index < children.Length; index++)
            {
                ValidateOrder(
                    children[index], $"{path}.orders[{index}]", role, roles,
                    sharedOrders, capacities, conditions, consumers, orderIds,
                    branchDepth + 1, externalId: null,
                    allowSharedReference: true);
            }
            return;
        }

        ValidateCapacity(value, path, capacities, consumers);
        ValidateSelection(value, path);
        switch (found[0])
        {
            case "move":
                ValidateMove(value.GetProperty("move"), path, role, roles, consumers);
                break;
            case "fight":
                ValidateFight(value.GetProperty("fight"), path, consumers);
                break;
            case "scan":
                ValidateScan(value.GetProperty("scan"), path, consumers);
                break;
            case "use":
                ValidateUse(
                    value.GetProperty("use"), path, role, roles, consumers);
                break;
            case "handoff":
                ValidateHandoff(value.GetProperty("handoff"), path, roles);
                break;
            case "drop":
                if (value.GetProperty("drop").ValueKind != JsonValueKind.True)
                    throw Error($"{path}.drop", "drop must be true.");
                break;
        }
    }

    private static void ValidateScan(
        JsonElement value,
        string path,
        ConsumerLedger consumers)
    {
        string at = $"{path}.scan";
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(at, "expected object.");
        Object(value, at, ["zone", "freshWithinTicks"]);
        string zone = StringValue(value.GetProperty("zone"), $"{at}.zone");
        IdentifierValue(zone, $"{at}.zone", "zone");
        consumers.ReferenceZone(zone, path);
        int freshness = RequiredInt(value, "freshWithinTicks", at);
        if (freshness is < 1 or > 600)
            throw Error(at, "freshWithinTicks must be in [1, 600].");
    }

    private static void ValidateLifecycle(
        JsonElement value,
        string path,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        ConsumerLedger consumers)
    {
        foreach (string name in new[] { "when", "until" })
        {
            if (value.TryGetProperty(name, out JsonElement expression))
                ValidateConditionExpression(
                    expression, $"{path}.{name}", conditions, consumers,
                    isUntil: name == "until");
        }
        if (value.TryGetProperty("timeout", out JsonElement timeout)
            && (timeout.ValueKind != JsonValueKind.Number
                || !timeout.TryGetInt32(out int timeoutTicks)
                || timeoutTicks is < 1 or > 1200))
        {
            throw Error(path, "timeout must be in [1, 1200].");
        }
        if (value.TryGetProperty("cooldown", out JsonElement cooldown)
            && (cooldown.ValueKind != JsonValueKind.Number
                || !cooldown.TryGetInt32(out int cooldownTicks)
                || cooldownTicks is < 0 or > 1200))
        {
            throw Error(path, "cooldown must be in [0, 1200].");
        }
    }

    private static void ValidateConditionExpression(
        JsonElement value,
        string path,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        ConsumerLedger consumers,
        bool isUntil)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string id = value.GetString()!;
            if (id == "always")
                return;
            if (!conditions.ContainsKey(id))
                throw Error(path, $"unknown condition '{id}'.");
            if (conditions[id].Since == "node-entry" && !isUntil)
            {
                throw Error(path,
                    $"condition '{id}' uses since node-entry and is legal only in until.");
            }
            if (conditions[id].Since == "phase-entry")
            {
                throw Error(path,
                    $"condition '{id}' anchors at phase-entry and is legal only in program cases.");
            }
            consumers.Conditions[id]++;
            foreach (string zone in conditions[id].Zones)
                consumers.ReferenceZone(zone, path);
            return;
        }
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected condition name or all/any object.");
        ValidateCombination(value, path, conditions, consumers, isUntil, depth: 0);
    }

    private static void ValidateCombination(
        JsonElement value,
        string path,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        ConsumerLedger consumers,
        bool isUntil,
        int depth)
    {
        string[] operators = ["all", "any"];
        string[] found = operators.Where(name => value.TryGetProperty(name, out _))
            .ToArray();
        if (found.Length != 1)
            throw Error(path, "condition combination needs exactly one of all/any.");
        Object(value, path, [found[0]]);
        JsonElement entries = value.GetProperty(found[0]);
        string entriesPath = $"{path}.{found[0]}";
        if (entries.ValueKind != JsonValueKind.Array)
            throw Error(entriesPath, "expected array.");
        int count = entries.GetArrayLength();
        if (count is < 2 or > 8)
            throw Error(entriesPath, "expected 2 to 8 entries.");
        int index = 0;
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string entryPath = $"{entriesPath}[{index}]";
            index++;
            if (entry.ValueKind == JsonValueKind.Object)
            {
                // One nesting level: an all may carry an any (and vice
                // versa) so a latch can say "ready AND any trigger", but
                // groups never nest further.
                if (depth > 0)
                    throw Error(entryPath, "condition groups nest at most one level.");
                ValidateCombination(
                    entry, entryPath, conditions, consumers, isUntil, depth + 1);
                continue;
            }
            if (entry.ValueKind != JsonValueKind.String)
                throw Error(entryPath, "expected condition name or all/any object.");
            string name = entry.GetString()!;
            if (name == "always" || !conditions.ContainsKey(name))
                throw Error(path, $"unknown condition '{name}'.");
            if (conditions[name].Since == "node-entry" && !isUntil)
            {
                throw Error(path,
                    $"condition '{name}' uses since node-entry and is legal only in until.");
            }
            if (conditions[name].Since == "phase-entry")
            {
                throw Error(path,
                    $"condition '{name}' anchors at phase-entry and is legal only in program cases.");
            }
            consumers.Conditions[name]++;
            foreach (string zone in conditions[name].Zones)
                consumers.ReferenceZone(zone, path);
        }
    }

    private static void ValidateCapacity(
        JsonElement value,
        string path,
        IReadOnlyDictionary<string, int> capacities,
        ConsumerLedger consumers)
    {
        if (!value.TryGetProperty("capacity", out JsonElement capacity))
            return;
        if (capacity.ValueKind == JsonValueKind.Number
            && capacity.TryGetInt32(out int local)
            && local is >= 1 and <= 8)
        {
            return;
        }
        if (capacity.ValueKind != JsonValueKind.String
            || !capacities.ContainsKey(capacity.GetString()!))
        {
            throw Error($"{path}.capacity",
                "capacity must be an inline integer in [1, 8] or a declared name.");
        }
        consumers.Capacities[capacity.GetString()!]++;
    }

    private static void ValidateSelection(JsonElement value, string path)
    {
        if (!value.TryGetProperty("select", out JsonElement selection))
            return;
        if (selection.ValueKind != JsonValueKind.String
            || !string.Equals(selection.GetString(), "highest-veteran",
                StringComparison.Ordinal))
        {
            throw Error($"{path}.select",
                "select must be 'highest-veteran'.");
        }
        if (!value.TryGetProperty("capacity", out JsonElement capacity)
            || capacity.ValueKind != JsonValueKind.Number)
        {
            throw Error($"{path}.select",
                "highest-veteran selection needs an inline capacity so its selection group is explicit.");
        }
    }

    private static void ValidateMove(
        JsonElement value,
        string path,
        RoleInfo role,
        IReadOnlyDictionary<string, RoleInfo> roles,
        ConsumerLedger consumers)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.move", "expected object.");
        string[] kinds = ["to", "patrol", "follow"];
        string[] found = kinds.Where(name => value.TryGetProperty(name, out _))
            .ToArray();
        if (found.Length != 1)
            throw Error($"{path}.move", "move needs exactly one of to/patrol/follow.");
        Object(value, $"{path}.move", [found[0]],
            ["via", "posture", "face", "withinWalkingSteps", "coreBankValue",
             "well", "near"]);
        if (value.TryGetProperty("near", out JsonElement near))
        {
            // The service anchor: "nearest" resolves from this declared
            // zone's centre instead of the body's own position, so a squad
            // regrouping at authored ground shares ONE heal tile rather
            // than each bot picking whichever is closest to where it
            // happens to stand.
            if (found[0] != "to"
                || value.GetProperty("to").ValueKind != JsonValueKind.String
                || value.GetProperty("to").GetString() != "nearest-heal-zone")
            {
                throw Error($"{path}.move.near",
                    "near anchors nearest-heal-zone only.");
            }
            string nearZone = StringValue(near, $"{path}.move.near");
            IdentifierValue(nearZone, $"{path}.move.near", "zone");
            consumers.ReferenceZone(nearZone, path);
        }
        if (value.TryGetProperty("posture", out _))
            OneOf(value, "posture", $"{path}.move", "front", "line", "rear");
        if (value.TryGetProperty("via", out JsonElement via))
        {
            if (found[0] == "follow")
                throw Error($"{path}.move", "follow cannot have an ingress route.");
            string route = StringValue(via, $"{path}.move.via");
            IdentifierValue(route, $"{path}.move.via", "route");
            consumers.ReferenceRoute(route, path);
        }
        if (found[0] == "patrol")
        {
            if (value.TryGetProperty("withinWalkingSteps", out _)
                || value.TryGetProperty("coreBankValue", out _)
                || value.TryGetProperty("well", out _))
                throw Error($"{path}.move", "patrol has no bounded dynamic target.");
            if (value.TryGetProperty("face", out _))
                throw Error($"{path}.move.face", "face is legal only on move.to.");
            string route = StringValue(value.GetProperty("patrol"),
                $"{path}.move.patrol");
            IdentifierValue(route, $"{path}.move.patrol", "route");
            consumers.ReferenceRoute(route, path);
            return;
        }
        if (found[0] == "follow")
        {
            if (value.TryGetProperty("face", out _))
                throw Error($"{path}.move.face", "face is legal only on move.to.");
            JsonElement follow = value.GetProperty("follow");
            if (follow.ValueKind == JsonValueKind.String)
            {
                if (value.TryGetProperty("withinWalkingSteps", out _)
                    || value.TryGetProperty("coreBankValue", out _)
                    || value.TryGetProperty("well", out _))
                    throw Error($"{path}.move", "role follow has no dynamic target qualifier.");
                string targetRole = StringValue(
                    follow, $"{path}.move.follow");
                if (!roles.ContainsKey(targetRole) || targetRole == role.Id)
                {
                    throw Error(
                        $"{path}.move.follow",
                        "follow another declared role.");
                }
            }
            else
            {
                bool roleVeteran = follow.TryGetProperty("role", out JsonElement roleTarget);
                if (roleVeteran)
                {
                    Object(follow, $"{path}.move.follow", ["role", "select"]);
                    string targetRole = StringValue(roleTarget, $"{path}.move.follow.role");
                    if (!roles.ContainsKey(targetRole) || targetRole == role.Id)
                        throw Error($"{path}.move.follow.role", "follow another declared role.");
                    OneOf(follow, "select", $"{path}.move.follow", "highest-veteran");
                }
                else
                {
                    Object(follow, $"{path}.move.follow", ["selector"],
                        ["coreBankValue", "roles", "well"]);
                    OneOf(follow, "selector", $"{path}.move.follow",
                        "most-experienced-ally", "nearest-friendly-core-carrier");
                }
                if (follow.TryGetProperty("coreBankValue", out JsonElement followValue))
                {
                    if (follow.GetProperty("selector").GetString()
                        != "nearest-friendly-core-carrier")
                        throw Error($"{path}.move.follow",
                            "coreBankValue is legal only for nearest-friendly-core-carrier.");
                    ValidateCoreBankValue(
                        followValue, $"{path}.move.follow.coreBankValue");
                }
                if (follow.TryGetProperty("roles", out JsonElement followedRoles))
                {
                    if (follow.GetProperty("selector").GetString()
                        != "nearest-friendly-core-carrier")
                        throw Error($"{path}.move.follow",
                            "roles are legal only for nearest-friendly-core-carrier.");
                    string[] selectedRoles = StringArray(
                        followedRoles, $"{path}.move.follow.roles", 1, 8);
                    if (selectedRoles.Distinct(StringComparer.Ordinal).Count()
                            != selectedRoles.Length
                        || selectedRoles.Any(selected => !roles.ContainsKey(selected)))
                        throw Error($"{path}.move.follow.roles",
                            "choose unique declared roles.");
                }
                if (follow.TryGetProperty("well", out JsonElement followedWell))
                {
                    if (follow.GetProperty("selector").GetString()
                            != "nearest-friendly-core-carrier"
                        || followedWell.ValueKind != JsonValueKind.String
                        || followedWell.GetString() is not
                            ("north" or "centre" or "south" or "own" or "enemy"))
                        throw Error($"{path}.move.follow.well",
                            "choose a Well for nearest-friendly-core-carrier.");
                }
                if (value.TryGetProperty("withinWalkingSteps", out _)
                    || value.TryGetProperty("coreBankValue", out _)
                    || value.TryGetProperty("well", out _))
                    throw Error($"{path}.move",
                        "follow qualifiers belong inside the follow selector.");
            }
            return;
        }
        JsonElement to = value.GetProperty("to");
        ValidateMoveTarget(to, $"{path}.move.to", consumers);
        string? dynamicTarget = to.ValueKind == JsonValueKind.String
            ? to.GetString()
            : null;
        if (value.TryGetProperty("withinWalkingSteps", out JsonElement within))
        {
            if (dynamicTarget is not ("nearest-visible-loose-core"
                    or "highest-bank-value-visible-loose-core"
                    or "nearest-heal-zone"))
                throw Error($"{path}.move.withinWalkingSteps",
                    "walking bounds are legal only for visible-Core and healing targets.");
            if (within.ValueKind != JsonValueKind.Number
                || !within.TryGetInt32(out int steps) || steps is < 0 or > 31)
                throw Error($"{path}.move.withinWalkingSteps",
                    "walking bound must be in [0, 31].");
        }
        if (value.TryGetProperty("coreBankValue", out JsonElement targetValue))
        {
            if (dynamicTarget is not ("nearest-visible-loose-core"
                    or "highest-bank-value-visible-loose-core"))
                throw Error($"{path}.move.coreBankValue",
                    "Core value filters are legal only for visible-Core targets.");
            ValidateCoreBankValue(targetValue, $"{path}.move.coreBankValue");
        }
        if (value.TryGetProperty("well", out JsonElement targetWell))
        {
            if (dynamicTarget is not ("nearest-visible-loose-core"
                    or "highest-bank-value-visible-loose-core")
                || targetWell.ValueKind != JsonValueKind.String
                || targetWell.GetString() is not
                    ("north" or "centre" or "south" or "own" or "enemy"))
            {
                throw Error($"{path}.move.well",
                    "choose a source Well for a visible-Core target.");
            }
        }
        if (!value.TryGetProperty("face", out JsonElement face))
            return;
        if (to.ValueKind != JsonValueKind.Object
            || !to.EnumerateObject().Any(property => property.Name is
                "zone" or "anchor"))
        {
            throw Error($"{path}.move.face",
                "face requires an authored zone or anchor move.to destination.");
        }
        Object(face, $"{path}.move.face", ["anchor"]);
        string faceAnchor = StringValue(
            face.GetProperty("anchor"), $"{path}.move.face.anchor");
        IdentifierValue(faceAnchor, $"{path}.move.face.anchor", "anchor");
        consumers.ReferenceAnchor(faceAnchor, path);
    }

    private static void ValidateMoveTarget(
        JsonElement value,
        string path,
        ConsumerLedger consumers)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            if (value.GetString() is not
                ("own-reactor" or "nearest-heal-zone" or
                 "nearest-known-loose-core" or
                 "highest-bank-value-known-loose-core" or
                 "nearest-visible-loose-core" or
                 "highest-bank-value-visible-loose-core" or
                 "current-position" or
                 "own-tossed-core"))
            {
                throw Error(path, "unknown dynamic move target.");
            }
            return;
        }
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected dynamic target or map target object.");
        string[] keys = ["zone", "anchor", "adjacentTo", "well", "workshop"];
        string[] found = keys.Where(name => value.TryGetProperty(name, out _))
            .ToArray();
        if (found.Length != 1)
            throw Error(path,
                "move target needs exactly one of "
                + "zone/anchor/adjacentTo/well.");
        Object(value, path, [found[0]]);
        string id = StringValue(value.GetProperty(found[0]), $"{path}.{found[0]}");
        if (found[0] == "well")
        {
            if (id is not ("north" or "centre" or "south" or "own" or "enemy"))
                throw Error(path, "well must be north/centre/south/own/enemy.");
            return;
        }
        if (found[0] == "workshop")
        {
            if (id is not ("north" or "south" or "either"))
                throw Error(path, "workshop must be north/south/either.");
            return;
        }
        IdentifierValue(id, path,
            found[0] == "adjacentTo" ? "anchor" : found[0]);
        if (found[0] == "zone")
            consumers.ReferenceZone(id, path);
        else
            consumers.ReferenceAnchor(id, path);
    }

    private static void ValidateFight(
        JsonElement value,
        string path,
        ConsumerLedger consumers)
    {
        Object(value, $"{path}.fight", ["targets", "style", "chaseLimit"], ["zone"]);
        string[] targets = StringArray(
            value.GetProperty("targets"), $"{path}.fight.targets", 1, 5);
        foreach (string target in targets)
        {
            if (target is not ("carrier" or "weakest" or "nearest" or
                "medic" or "marked" or "veteran" or "deployment"))
                throw Error($"{path}.fight.targets", $"unknown target '{target}'.");
        }
        OneOf(value, "style", $"{path}.fight", "advance", "hold", "kite");
        Range(value, "chaseLimit", $"{path}.fight", 0, 12);
        if (value.TryGetProperty("zone", out JsonElement zone))
        {
            string zoneId = StringValue(zone, $"{path}.fight.zone");
            IdentifierValue(zoneId, $"{path}.fight.zone", "zone");
            consumers.ReferenceZone(zoneId, path);
        }
    }

    private static void ValidateCoreBankValue(JsonElement value, string path)
    {
        Object(value, path, ["operator", "value"]);
        OneOf(value, "operator", path, "equals", "at-least", "at-most");
        Range(value, "value", path, 1, 4);
    }

    private static void ValidateUse(
        JsonElement value,
        string path,
        RoleInfo role,
        IReadOnlyDictionary<string, RoleInfo> roles,
        ConsumerLedger consumers)
    {
        Object(value, $"{path}.use", ["skill", "target"], ["at", "opportunity"]);
        string skill = RequiredString(value, "skill", $"{path}.use");
        bool roleOwnsSkill = role.Classes.Any(classId => string.Equals(
            ArcRelaySignatureAuthoringCatalog.ForClass(classId).SkillId,
            skill,
            StringComparison.Ordinal));
        if (!roleOwnsSkill
            || !ArcRelaySignatureAuthoringCatalog.TryForSkill(
                skill, out ArcRelaySignatureAuthoringDescriptor descriptor))
        {
            throw Error($"{path}.use.skill",
                $"no body in role '{role.Id}' owns '{skill}'.");
        }
        if (value.TryGetProperty("opportunity", out JsonElement opportunity))
        {
            string authored = StringValue(
                opportunity, $"{path}.use.opportunity");
            string expected = ArcRelaySignatureAuthoringCatalog.StableId(
                descriptor.Opportunity);
            if (!string.Equals(authored, expected, StringComparison.Ordinal))
            {
                throw Error(
                    $"{path}.use.opportunity",
                    $"'{skill}' uses opportunity '{expected}'.");
            }
        }
        JsonElement targetValue = value.GetProperty("target");
        string targetKind;
        string target;
        if (targetValue.ValueKind == JsonValueKind.String)
        {
            targetKind = "named";
            target = StringValue(targetValue, $"{path}.use.target");
            if (!descriptor.AllowsNamedTarget(target))
            {
                throw Error(
                    $"{path}.use.target",
                    $"'{skill}' cannot use target '{target}'.");
            }
        }
        else
        {
            if (targetValue.ValueKind != JsonValueKind.Object)
                throw Error($"{path}.use.target", "expected target name or object.");
            string[] kinds = ["role", "selector", "route"];
            string[] found = kinds.Where(name =>
                targetValue.TryGetProperty(name, out _)).ToArray();
            if (found.Length != 1)
            {
                throw Error(
                    $"{path}.use.target",
                    "target object needs exactly one of role/selector/route.");
            }
            bool forwardRouteTarget = found[0] == "role"
                && (targetValue.TryGetProperty("aheadVia", out _)
                    || targetValue.TryGetProperty("minimumForwardTiles", out _));
            Object(
                targetValue,
                $"{path}.use.target",
                found[0] == "route"
                    ? ["route", "progress", "minimumForwardTiles"]
                    : [found[0]],
                found[0] == "role"
                    ? ["aheadVia", "minimumForwardTiles"]
                    : []);
            targetKind = found[0];
            target = RequiredString(
                targetValue, found[0], $"{path}.use.target");
            if (targetKind == "route")
            {
                if (!descriptor.Allows(
                        ArcRelaySignatureTargetCapability.RouteForward))
                {
                    throw Error(
                        $"{path}.use.target",
                        $"'{skill}' cannot aim forward on a route.");
                }
                Exact(
                    targetValue,
                    "progress",
                    "forward",
                    $"{path}.use.target");
                IdentifierValue(
                    target, $"{path}.use.target.route", "route");
                consumers.ReferenceForwardRoute(target, path);
                Range(
                    targetValue,
                    "minimumForwardTiles",
                    $"{path}.use.target",
                    1,
                    5);
            }
            else if (targetKind == "role")
            {
                if (!roles.ContainsKey(target))
                    throw Error($"{path}.use.target", "target an existing role.");
                if (!descriptor.Allows(
                        ArcRelaySignatureTargetCapability.FriendlyRole))
                {
                    throw Error(
                        $"{path}.use.target",
                        $"'{skill}' cannot target a friendly role or selector.");
                }
            }
            else if (target != "most-experienced-ally"
                || !descriptor.Allows(
                    ArcRelaySignatureTargetCapability.MostExperiencedAlly))
            {
                throw Error(
                    $"{path}.use.target",
                    target == "most-experienced-ally"
                        ? $"'{skill}' cannot target a friendly role or selector."
                        : "unknown friendly selector; expected most-experienced-ally.");
            }
            if (forwardRouteTarget)
            {
                if (!descriptor.Allows(
                        ArcRelaySignatureTargetCapability.RouteAheadRole))
                {
                    throw Error(
                        $"{path}.use.target",
                        $"'{skill}' cannot require a receiver ahead on a route.");
                }
                if (!targetValue.TryGetProperty(
                        "aheadVia", out JsonElement aheadVia)
                    || !targetValue.TryGetProperty(
                        "minimumForwardTiles", out _))
                {
                    throw Error(
                        $"{path}.use.target",
                        "forward route targeting needs both aheadVia and "
                        + "minimumForwardTiles.");
                }
                string route = StringValue(
                    aheadVia, $"{path}.use.target.aheadVia");
                IdentifierValue(
                    route, $"{path}.use.target.aheadVia", "route");
                consumers.ReferenceRoute(route, path);
                Range(
                    targetValue,
                    "minimumForwardTiles",
                    $"{path}.use.target",
                    1,
                    5);
            }
        }
        if (value.TryGetProperty("at", out JsonElement at))
        {
            string anchor = StringValue(at, $"{path}.use.at");
            IdentifierValue(anchor, $"{path}.use.at", "anchor");
            consumers.ReferenceAnchor(anchor, path);
            if (!descriptor.Allows(
                    ArcRelaySignatureTargetCapability.ExactAnchor))
            {
                throw Error($"{path}.use.at",
                    $"'{skill}' does not place a construct at an anchor.");
            }
        }
    }

    private static void ValidateHandoff(
        JsonElement value,
        string path,
        IReadOnlyDictionary<string, RoleInfo> roles)
    {
        Object(value, $"{path}.handoff", ["target"]);
        JsonElement target = value.GetProperty("target");
        if (target.ValueKind == JsonValueKind.String)
        {
            if (target.GetString() is not
                ("nearest-ally-toward-own-reactor" or
                 "nearest-empty-handed-ally"))
                throw Error($"{path}.handoff.target", "unknown handoff target.");
            return;
        }
        if (target.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.handoff.target", "expected selector or role object.");
        if (target.TryGetProperty("role", out _))
        {
            Object(target, $"{path}.handoff.target", ["role"]);
            string role = RequiredString(target, "role", $"{path}.handoff.target");
            if (!roles.ContainsKey(role))
                throw Error($"{path}.handoff.target", $"unknown role '{role}'.");
            return;
        }
        Object(target, $"{path}.handoff.target", ["selector", "roles"]);
        Exact(target, "selector", "nearest-ally-toward-own-reactor",
            $"{path}.handoff.target");
        JsonElement[] roleValues = BoundedArray(
            target.GetProperty("roles"), $"{path}.handoff.target.roles", 1, 8);
        foreach (JsonElement roleValue in roleValues)
        {
            string role = StringValue(roleValue, $"{path}.handoff.target.roles");
            if (!roles.ContainsKey(role))
                throw Error($"{path}.handoff.target.roles", $"unknown role '{role}'.");
        }
    }

    private static MapPlanValidation ValidateMapPlan(
        JsonElement root,
        string path)
    {
        Object(root, path,
            ["schema", "mapPlanId", "mapId", "zones", "routes", "anchors"]);
        Exact(root, "schema", LayoutSchema, path);
        Identifier(root, "mapPlanId", path);
        NonEmptyString(root, "mapId", path);

        Dictionary<string, JsonElement> zones = ObjectEntries(
            root.GetProperty("zones"), $"{path}.zones", 0, 128, "zone");
        var zoneShapes = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, JsonElement zone) in zones)
        {
            Object(zone, $"{path}.zones.{id}", ["rect"]);
            JsonElement rect = zone.GetProperty("rect");
            Rectangle(rect, $"{path}.zones.{id}.rect");
            int[] points = rect.EnumerateArray().Select(value => value.GetInt32())
                .ToArray();
            if (points[0] > points[2] || points[1] > points[3])
                throw Error($"{path}.zones.{id}.rect", "rectangle must be normalized.");
            if (!zoneShapes.Add(string.Join(',', points)))
                throw Error($"{path}.zones.{id}", "duplicate zone geometry.");
        }

        Dictionary<string, JsonElement> routes = ObjectEntries(
            root.GetProperty("routes"), $"{path}.routes", 0, 128, "route");
        var routeShapes = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, JsonElement route) in routes)
        {
            Object(route, $"{path}.routes.{id}", ["waypoints"]);
            JsonElement[] waypoints = BoundedArray(
                route.GetProperty("waypoints"),
                $"{path}.routes.{id}.waypoints", 2, 32);
            foreach (JsonElement point in waypoints)
                Point(point, $"{path}.routes.{id}.waypoints");
            string shape = string.Join(';', waypoints.Select(point =>
                string.Join(',', point.EnumerateArray().Select(value =>
                    value.GetInt32()))));
            if (!routeShapes.Add(shape))
                throw Error($"{path}.routes.{id}", "duplicate route geometry.");
        }

        Dictionary<string, JsonElement> anchors = ObjectEntries(
            root.GetProperty("anchors"), $"{path}.anchors", 0, 128, "anchor");
        var anchorShapes = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, JsonElement anchor) in anchors)
        {
            Point(anchor, $"{path}.anchors.{id}");
            string shape = string.Join(',', anchor.EnumerateArray().Select(value =>
                value.GetInt32()));
            if (!anchorShapes.Add(shape))
                throw Error($"{path}.anchors.{id}", "duplicate anchor geometry.");
        }
        return new MapPlanValidation(
            zones.Keys.ToHashSet(StringComparer.Ordinal),
            routes.Keys.ToHashSet(StringComparer.Ordinal),
            anchors.Keys.ToHashSet(StringComparer.Ordinal),
            routes.Where(pair =>
                {
                    JsonElement[] points = pair.Value.GetProperty("waypoints")
                        .EnumerateArray().ToArray();
                    return !points[0].EnumerateArray().Select(value => value.GetInt32())
                        .SequenceEqual(points[^1].EnumerateArray().Select(value => value.GetInt32()));
                })
                .Select(pair => pair.Key)
                .ToHashSet(StringComparer.Ordinal));
    }

    private static void ValidateMapReferences(
        SheetValidation sheet,
        MapPlanValidation mapPlan,
        string sheetPath,
        string mapPlanPath)
    {
        ValidateReferenceSet(sheet.Zones, mapPlan.Zones,
            sheetPath, mapPlanPath, "zone");
        ValidateReferenceSet(sheet.Routes, mapPlan.Routes,
            sheetPath, mapPlanPath, "route");
        ValidateReferenceSet(sheet.Anchors, mapPlan.Anchors,
            sheetPath, mapPlanPath, "anchor");
        foreach (string route in sheet.ForwardRoutes)
        {
            if (mapPlan.Routes.Contains(route)
                && !mapPlan.TerminalRoutes.Contains(route))
            {
                throw Error(sheetPath,
                    $"route-forward target '{route}' must name a terminal route, not a loop.");
            }
        }
    }

    private static void ValidateReferenceSet(
        IReadOnlyDictionary<string, int> used,
        IReadOnlySet<string> declared,
        string sheetPath,
        string mapPlanPath,
        string kind)
    {
        foreach (string id in used.Keys)
        {
            if (!declared.Contains(id))
                throw Error(sheetPath, $"unknown {kind} '{id}'.");
        }
        foreach (string id in declared)
        {
            if (!used.ContainsKey(id))
                throw Error(mapPlanPath, $"{kind} '{id}' has no sheet consumer.");
        }
    }

    private static Dictionary<string, JsonElement> ObjectEntries(
        JsonElement value,
        string path,
        int minimum,
        int maximum,
        string kind)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected object.");
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length < minimum || properties.Length > maximum)
            throw Error(path, $"expected {minimum}..{maximum} entries.");
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in properties)
        {
            IdentifierValue(property.Name, path, kind);
            if (!result.TryAdd(property.Name, property.Value))
                throw Error(path, $"duplicate {kind} '{property.Name}'.");
        }
        return result;
    }

    private sealed record RoleInfo(string Id, string[] Classes, JsonElement Orders);

    private sealed record ConditionInfo(
        string Fact,
        IReadOnlyList<string> Zones,
        string? Since);

    private sealed record SheetValidation(
        string[] Composition,
        IReadOnlyDictionary<string, int> Zones,
        IReadOnlyDictionary<string, int> Routes,
        IReadOnlyDictionary<string, int> Anchors,
        IReadOnlySet<string> ForwardRoutes);

    private sealed record MapPlanValidation(
        IReadOnlySet<string> Zones,
        IReadOnlySet<string> Routes,
        IReadOnlySet<string> Anchors,
        IReadOnlySet<string> TerminalRoutes);

    private sealed class ConsumerLedger
    {
        public ConsumerLedger(
            IEnumerable<string> sharedOrders,
            IEnumerable<string> capacities,
            IEnumerable<string> conditions)
        {
            SharedOrders = sharedOrders.ToDictionary(
                value => value, _ => 0, StringComparer.Ordinal);
            Capacities = capacities.ToDictionary(
                value => value, _ => 0, StringComparer.Ordinal);
            Conditions = conditions.ToDictionary(
                value => value, _ => 0, StringComparer.Ordinal);
        }

        public Dictionary<string, int> SharedOrders { get; }
        public Dictionary<string, int> Capacities { get; }
        public Dictionary<string, int> Conditions { get; }

        // Program vocabulary: tags a node may carry ("t1" tactic-scoped,
        // "t1/p2" phase-scoped), the phase-scoped subset that must each own
        // at least one tagged node, and the tags actually used.
        public HashSet<string> DeclaredProgramTags { get; } =
            new(StringComparer.Ordinal);

        public HashSet<string> DeclaredPhaseTags { get; } =
            new(StringComparer.Ordinal);

        public HashSet<string> UsedProgramTags { get; } =
            new(StringComparer.Ordinal);

        public Dictionary<string, int> Zones { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Routes { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Anchors { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ForwardRoutes { get; } = new(StringComparer.Ordinal);

        public void ReferenceZone(string id, string path) =>
            Reference(Zones, id, path, "zone");

        public void ReferenceRoute(string id, string path) =>
            Reference(Routes, id, path, "route");

        public void ReferenceForwardRoute(string id, string path)
        {
            ReferenceRoute(id, path);
            ForwardRoutes.Add(id);
        }

        public void ReferenceAnchor(string id, string path) =>
            Reference(Anchors, id, path, "anchor");

        private static void Reference(
            Dictionary<string, int> values,
            string id,
            string path,
            string kind)
        {
            IdentifierValue(id, path, kind);
            values[id] = values.GetValueOrDefault(id) + 1;
        }
    }
}
