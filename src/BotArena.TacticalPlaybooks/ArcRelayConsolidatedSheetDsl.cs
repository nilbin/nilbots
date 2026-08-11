using System.Text.Json;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private static readonly HashSet<string> KnownClasses =
    [
        "kestrel", "palisade", "towline", "patchbay", "lantern", "mortar",
        "minesmith", "hush", "relay", "switchback", "longshot", "mason",
        "sunder", "repulsor", "veil", "nest",
    ];

    private static readonly HashSet<string> TargetPreferences =
    [
        "carrier", "weakest", "anchor", "closest", "strongest-threat",
        "freshest",
    ];

    private static readonly HashSet<string> ConditionFacts =
    [
        "always", "tick", "phase-state-ticks", "live-friendlies",
        "friendlies-in-zone-count", "live-count", "joining-count",
        "in-zone-count", "cohesion", "stuck-ticks",
        "known-enemies-unavailable", "visible-enemies-in-zone",
        "remembered-enemies-in-zone", "visible-enemy-carriers",
        "known-enemy-carriers", "friendly-carriers", "secured-cores",
        "visible-loose-cores", "visible-loose-cores-in-zone",
        "visible-loose-core-value", "visible-loose-core-value-in-zone",
        "well-has-outstanding", "outstanding-well-count",
        "ticks-without-objective-progress", "reactor-integrity",
        "reactor-charge", "custody-state-ticks", "role-live-count",
        "role-health", "max-level", "recover-ready-bodies",
        "own-socket-filled", "enemy-socket-filled", "own-filled-sockets",
        "enemy-filled-sockets", "well-ticks-until-birth",
        "self-health", "self-level", "self-carrying", "self-stuck-ticks",
        "self-mode-ticks",
    ];

    private static readonly HashSet<string> SelfFacts =
    [
        "self-health", "self-level", "self-carrying", "self-stuck-ticks",
        "self-mode-ticks",
    ];

    private static readonly HashSet<string> ZoneFacts =
    [
        "friendlies-in-zone-count", "in-zone-count",
        "visible-enemies-in-zone", "remembered-enemies-in-zone",
        "visible-loose-cores-in-zone", "visible-loose-core-value-in-zone",
    ];

    private static readonly HashSet<string> RoleSubjectFacts =
    [
        "live-count", "joining-count", "in-zone-count", "cohesion",
        "stuck-ticks", "role-live-count", "role-health",
    ];

    private static readonly HashSet<string> WellSubjectFacts =
    [
        "well-has-outstanding", "own-socket-filled", "enemy-socket-filled",
        "well-ticks-until-birth",
    ];

    private sealed record RoleInfo(
        string Id,
        int Minimum,
        int Preferred,
        int Maximum,
        string CarrierPreference,
        HashSet<string> CandidateClasses);

    private sealed record ConditionInfo(string Fact, bool IsSelf);

    private sealed record CohortInfo(string Id, int Take);

    private sealed record CohortConsumer(
        string Role,
        (string Kind, string Id) Destination,
        string Path);

    private static void ValidateConsolidatedSheet(JsonElement root, string path)
    {
        Object(root, path,
            [
                "schema", "playbookId", "auditStatus", "composition",
                "layout", "memory", "roles", "custodyPolicies",
                "doctrines", "conditions",
            ]);
        Exact(root, "schema", PlaybookSchema, path);
        Identifier(root, "playbookId", path);
        ValidateAuditStatus(root.GetProperty("auditStatus"), path);
        string[] composition = ValidateComposition(
            root.GetProperty("composition"), path);
        ValidateLayoutReference(root.GetProperty("layout"), path);
        ValidateMemory(root.GetProperty("memory"), path);

        JsonElement[] roleValues = BoundedArray(
            root.GetProperty("roles"), $"{path}.roles", 1, 16);
        HashSet<string> roleIds = UniqueIds(
            roleValues, "roleId", $"{path}.roles");
        Dictionary<string, RoleInfo> roles = roleValues
            .Select(role => ValidateRole(role, roleIds, path))
            .ToDictionary(role => role.Id, StringComparer.Ordinal);
        ValidateRoleCoverage(roles.Values, composition, path);

        Dictionary<string, int> conditionConsumers = new(
            StringComparer.Ordinal);
        Dictionary<string, ConditionInfo> conditions = ValidateConditions(
            root.GetProperty("conditions"), roleIds, conditionConsumers, path);

        JsonElement[] custodyValues = BoundedArray(
            root.GetProperty("custodyPolicies"),
            $"{path}.custodyPolicies", 1, 16);
        HashSet<string> custodyIds = UniqueIds(
            custodyValues, "custodyId", $"{path}.custodyPolicies");
        foreach (JsonElement custody in custodyValues)
        {
            ValidateCustody(
                custody, roleIds, conditions, conditionConsumers, path);
        }

        JsonElement doctrinePlane = root.GetProperty("doctrines");
        Object(doctrinePlane, $"{path}.doctrines",
            ["roles", "cohorts", "focus"]);
        JsonElement doctrineRoles = doctrinePlane.GetProperty("roles");
        if (doctrineRoles.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.doctrines.roles", "expected object.");
        Dictionary<string, CohortInfo> cohorts = ValidateCohorts(
            doctrinePlane.GetProperty("cohorts"), path);
        Dictionary<string, int> focus = ValidateFocus(
            doctrinePlane.GetProperty("focus"), path);
        var cohortConsumers = cohorts.Keys.ToDictionary(
            id => id,
            _ => new List<CohortConsumer>(),
            StringComparer.Ordinal);
        var focusConsumers = focus.Keys.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        var fetchConsumers = custodyIds.ToDictionary(
            id => id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        var seenDoctrines = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty doctrine in doctrineRoles.EnumerateObject())
        {
            if (!seenDoctrines.Add(doctrine.Name))
                throw Error($"{path}.doctrines.roles",
                    $"duplicate doctrine '{doctrine.Name}'.");
            if (!roles.TryGetValue(doctrine.Name, out RoleInfo? role))
                throw Error($"{path}.doctrines.roles",
                    $"doctrine '{doctrine.Name}' names no role.");
            ValidateRoleDoctrine(
                role,
                doctrine.Value,
                roleIds,
                custodyIds,
                conditions,
                conditionConsumers,
                cohorts,
                cohortConsumers,
                focus,
                focusConsumers,
                fetchConsumers,
                $"{path}.doctrines.roles.{role.Id}");
        }
        foreach (string roleId in roleIds)
        {
            if (!seenDoctrines.Contains(roleId))
                throw Error($"{path}.doctrines.roles",
                    $"role '{roleId}' has no doctrine.");
        }

        ValidateSharedConsumers(
            cohorts, cohortConsumers, focus, focusConsumers, path);
        foreach ((string custodyId, HashSet<string> consumers) in fetchConsumers)
        {
            if (consumers.Count == 0)
                throw Error($"{path}.custodyPolicies.{custodyId}",
                    "custody has no fetch rung; no role can ever accept it.");
        }
        foreach (RoleInfo role in roles.Values)
        {
            bool courier = fetchConsumers.Values.Any(value =>
                value.Contains(role.Id));
            if (role.CarrierPreference == "forbid" && courier)
                throw Error(path,
                    $"role '{role.Id}' forbids Core custody but has a fetch rung.");
            if (role.CarrierPreference == "require" && !courier)
                throw Error(path,
                    $"role '{role.Id}' requires Core custody but has no fetch rung.");
        }
        foreach ((string condition, int consumers) in conditionConsumers)
        {
            if (consumers == 0)
                throw Error($"{path}.conditions.{condition}",
                    "condition is never referenced.");
        }
    }

    private static void ValidateAuditStatus(JsonElement value, string path)
    {
        string at = $"{path}.auditStatus";
        Object(value, at,
            ["provisionalEvaluationOnly", "playerFacingProductSchema"]);
        if (!RequiredBool(value, "provisionalEvaluationOnly", at)
            || RequiredBool(value, "playerFacingProductSchema", at))
        {
            throw Error(at,
                "tactical playbook v1 must remain provisional and non-product.");
        }
    }

    private static string[] ValidateComposition(JsonElement value, string path)
    {
        string[] classes = StringValues(value, $"{path}.composition", 8, 8);
        if (classes.Any(classId => !KnownClasses.Contains(classId))
            || classes.GroupBy(classId => classId, StringComparer.Ordinal)
                .Any(group => group.Count() > 2))
        {
            throw Error($"{path}.composition",
                "composition must use launch classes under the two-copy cap.");
        }
        return classes;
    }

    private static void ValidateLayoutReference(JsonElement value, string path)
    {
        string at = $"{path}.layout";
        Object(value, at, ["path", "sha256"]);
        NonEmptyString(value, "path", at);
        Hash(value, "sha256", at);
    }

    private static void ValidateMemory(JsonElement value, string path)
    {
        string at = $"{path}.memory";
        Object(value, at,
            [
                "enemyUnavailableTicks", "lastSeenEnemyTicks",
                "securedCoreTicks", "objectiveProgressTicks",
            ]);
        foreach (string name in new[]
                 {
                     "enemyUnavailableTicks", "lastSeenEnemyTicks",
                     "securedCoreTicks", "objectiveProgressTicks",
                 })
        {
            Range(value, name, at, 1, 600);
        }
    }

    private static RoleInfo ValidateRole(
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
        string id = Identifier(value, "roleId", at);
        string[] candidates = StringArray(
            value.GetProperty("candidateClasses"),
            $"{at}.{id}.candidateClasses", 1, 16);
        if (candidates.Any(candidate => !KnownClasses.Contains(candidate)))
            throw Error(at, $"role '{id}' has unknown candidate classes.");
        int minimum = Range(value, "minimum", at, 0, 8);
        int preferred = Range(value, "preferred", at, 0, 8);
        int maximum = Range(value, "maximum", at, 0, 8);
        if (minimum > preferred || preferred > maximum)
            throw Error(at,
                $"role '{id}' needs minimum <= preferred <= maximum.");
        string carrier = OneOf(value, "carrierPreference", at,
            "forbid", "allow", "prefer", "require");
        OneOf(value, "deathPolicy", at,
            "hold-vacancy", "promote-best", "rebalance");
        OneOf(value, "respawnPolicy", at,
            "resume", "rejoin", "rally", "replace");
        string overflow = RequiredString(value, "overflowRoleId", at);
        if (overflow.Length > 0 && !roleIds.Contains(overflow))
            throw Error(at, $"role '{id}' has unknown overflow role '{overflow}'.");
        if (value.TryGetProperty("build", out JsonElement build))
        {
            string[] tracks = StringValues(build, $"{at}.{id}.build", 1, 8);
            if (tracks.Any(track => track is not
                    ("damage" or "vision" or "reach" or "vitality")))
            {
                throw Error(at, $"role '{id}' has unknown build tracks.");
            }
        }
        return new RoleInfo(
            id,
            minimum,
            preferred,
            maximum,
            carrier,
            candidates.ToHashSet(StringComparer.Ordinal));
    }

    private static void ValidateRoleCoverage(
        IEnumerable<RoleInfo> roles,
        IReadOnlyCollection<string> composition,
        string path)
    {
        RoleInfo[] values = roles.ToArray();
        if (values.Sum(role => role.Minimum) > 8
            || values.Sum(role => role.Preferred) > 8
            || values.Sum(role => role.Maximum) < 8)
        {
            throw Error($"{path}.roles",
                "role cardinalities cannot assign exactly eight bodies.");
        }
        foreach (string classId in composition.Distinct(StringComparer.Ordinal))
        {
            if (!values.Any(role => role.CandidateClasses.Contains(classId)))
                throw Error($"{path}.roles",
                    $"composition class '{classId}' has no candidate role.");
        }
        foreach (RoleInfo role in values.Where(role => role.Minimum > 0))
        {
            int available = composition.Count(role.CandidateClasses.Contains);
            if (available < role.Minimum)
                throw Error($"{path}.roles",
                    $"role '{role.Id}' minimum exceeds its composition candidates.");
        }
    }

    private static Dictionary<string, ConditionInfo> ValidateConditions(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        Dictionary<string, int> consumers,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.conditions", "expected object.");
        var result = new Dictionary<string, ConditionInfo>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string id = property.Name;
            IdentifierValue(id, $"{path}.conditions", "condition");
            if (id is "always" or "no-live-friendlies")
                throw Error($"{path}.conditions", $"reserved condition '{id}'.");
            if (!result.TryAdd(id,
                    ValidateCondition(property.Value, roleIds,
                        $"{path}.conditions.{id}")))
            {
                throw Error($"{path}.conditions", $"duplicate condition '{id}'.");
            }
            consumers.Add(id, 0);
        }
        return result;
    }

    private static ConditionInfo ValidateCondition(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        string path)
    {
        Object(value, path,
            ["fact", "operator", "value"],
            ["subject", "zone", "freshnessTicks"]);
        string fact = RequiredString(value, "fact", path);
        if (!ConditionFacts.Contains(fact))
            throw Error(path, $"unknown condition fact '{fact}'.");
        OneOf(value, "operator", path,
            "at-least", "at-most", "equals", "less-than", "greater-than");
        _ = RequiredInt(value, "value", path);
        bool hasSubject = value.TryGetProperty("subject", out JsonElement subject);
        bool hasZone = value.TryGetProperty("zone", out JsonElement zone);
        bool hasFreshness = value.TryGetProperty("freshnessTicks", out _);
        if (SelfFacts.Contains(fact))
        {
            if (hasSubject || hasZone || hasFreshness)
                throw Error(path, $"'{fact}' takes no subject or zone.");
            return new ConditionInfo(fact, IsSelf: true);
        }
        if (RoleSubjectFacts.Contains(fact))
        {
            if (!hasSubject)
                throw Error(path, $"'{fact}' needs a role subject.");
            string role = subject.GetString()!;
            if (!roleIds.Contains(role))
                throw Error(path, $"condition references unknown role '{role}'.");
        }
        else if (fact is "max-level" or "recover-ready-bodies")
        {
            if (hasSubject && !roleIds.Contains(subject.GetString()!))
                throw Error(path,
                    $"condition references unknown role '{subject.GetString()}'.");
        }
        else if (WellSubjectFacts.Contains(fact))
        {
            // 'own' and 'enemy' are TEAM-RELATIVE: the Well nearest and
            // furthest from your own reactor. On a chiral map where each side
            // owns a Well they are the only way to say "my income" in a sheet
            // that plays either corner; the compass names stay for maps where
            // both flanks are neutral and the distinction is meaningless.
            if (!hasSubject || subject.GetString() is not
                    ("north" or "centre" or "south" or "own" or "enemy"))
            {
                throw Error(path,
                    $"'{fact}' needs a north/centre/south/own/enemy subject.");
            }
        }
        else if (hasSubject)
        {
            throw Error(path, $"'{fact}' takes no subject.");
        }
        if (ZoneFacts.Contains(fact))
        {
            if (!hasZone)
                throw Error(path, $"'{fact}' needs a zone.");
            IdentifierValue(zone.GetString()!, path, "zone");
        }
        else if (hasZone)
        {
            throw Error(path, $"'{fact}' takes no zone.");
        }
        if (hasFreshness)
        {
            if (fact is not ("remembered-enemies-in-zone" or "secured-cores"))
                throw Error(path, $"'{fact}' takes no freshnessTicks.");
            Range(value, "freshnessTicks", path, 1, 600);
        }
        return new ConditionInfo(fact, IsSelf: false);
    }

    private static Dictionary<string, CohortInfo> ValidateCohorts(
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.doctrines.cohorts", "expected object.");
        var result = new Dictionary<string, CohortInfo>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string id = property.Name;
            IdentifierValue(id, $"{path}.doctrines.cohorts", "cohort");
            Object(property.Value, $"{path}.doctrines.cohorts.{id}",
                ["take", "select"]);
            int take = Range(property.Value, "take",
                $"{path}.doctrines.cohorts.{id}", 1, 8);
            Exact(property.Value, "select", "nearest",
                $"{path}.doctrines.cohorts.{id}");
            if (!result.TryAdd(id, new CohortInfo(id, take)))
                throw Error(path, $"duplicate cohort '{id}'.");
        }
        return result;
    }

    private static Dictionary<string, int> ValidateFocus(
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error($"{path}.doctrines.focus", "expected object.");
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string id = property.Name;
            IdentifierValue(id, $"{path}.doctrines.focus", "focus scope");
            Object(property.Value, $"{path}.doctrines.focus.{id}",
                ["maximumAttackers"]);
            int maximum = Range(property.Value, "maximumAttackers",
                $"{path}.doctrines.focus.{id}", 1, 8);
            if (!result.TryAdd(id, maximum))
                throw Error(path, $"duplicate focus scope '{id}'.");
        }
        return result;
    }

    private static void ValidateRoleDoctrine(
        RoleInfo role,
        JsonElement doctrine,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> custodyIds,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        Dictionary<string, int> conditionConsumers,
        IReadOnlyDictionary<string, CohortInfo> cohorts,
        IReadOnlyDictionary<string, List<CohortConsumer>> cohortConsumers,
        IReadOnlyDictionary<string, int> focus,
        IReadOnlyDictionary<string, List<string>> focusConsumers,
        IReadOnlyDictionary<string, HashSet<string>> fetchConsumers,
        string path)
    {
        Object(doctrine, path,
            ["custody", "modes"],
            ["fight", "conceal", "scavenge", "repair"]);
        string custody = Identifier(doctrine, "custody", path);
        if (!custodyIds.Contains(custody))
            throw Error(path, $"unknown custody '{custody}'.");
        if (doctrine.TryGetProperty("fight", out JsonElement fight))
            ValidateFight(fight, $"{path}.fight");
        if (doctrine.TryGetProperty("conceal", out JsonElement conceal)
            && conceal.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw Error(path, "'conceal' must be a boolean.");
        }
        if (doctrine.TryGetProperty("scavenge", out JsonElement scavenge))
        {
            if (scavenge.ValueKind == JsonValueKind.String)
            {
                IdentifierValue(scavenge.GetString()!, path, "zone");
            }
            else
            {
                foreach (string zone in StringArray(
                             scavenge, $"{path}.scavenge", 1, 16))
                {
                    IdentifierValue(zone, path, "zone");
                }
            }
        }
        if (doctrine.TryGetProperty("repair", out JsonElement repair))
            ValidateRepair(repair, $"{path}.repair");

        JsonElement[] modes = BoundedArray(
            doctrine.GetProperty("modes"), $"{path}.modes", 1, 8);
        var roleFetch = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < modes.Length; index++)
        {
            ValidateMode(
                role,
                modes[index],
                index,
                index == modes.Length - 1,
                roleIds,
                custodyIds,
                conditions,
                conditionConsumers,
                cohorts,
                cohortConsumers,
                focus,
                focusConsumers,
                roleFetch,
                fetchConsumers,
                $"{path}.modes[{index}]");
        }
    }

    private static void ValidateMode(
        RoleInfo role,
        JsonElement mode,
        int index,
        bool isFloor,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> custodyIds,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        Dictionary<string, int> conditionConsumers,
        IReadOnlyDictionary<string, CohortInfo> cohorts,
        IReadOnlyDictionary<string, List<CohortConsumer>> cohortConsumers,
        IReadOnlyDictionary<string, int> focus,
        IReadOnlyDictionary<string, List<string>> focusConsumers,
        ISet<string> roleFetch,
        IReadOnlyDictionary<string, HashSet<string>> fetchConsumers,
        string path)
    {
        if (mode.ValueKind != JsonValueKind.Object)
            throw Error(path, "mode must be an object.");
        string[] verbs = new[]
            {
                "patrol", "intercept", "assault", "recover", "fetch",
                "follow",
            }
            .Where(verb => mode.TryGetProperty(verb, out _))
            .ToArray();
        if (verbs.Length != 1)
        {
            throw Error(path,
                "each mode needs exactly one of patrol, intercept, assault, "
                + "recover, fetch, or follow.");
        }
        string verb = verbs[0];
        string[] optional = verb switch
        {
            "patrol" or "assault" =>
                ["while", "until", "fight", "cohort", "focus", "posture"],
            "intercept" => ["from", "while", "until", "fight", "focus"],
            "recover" => ["fight", "focus"],
            "fetch" => [],
            "follow" => ["while", "until", "fight", "posture", "focus"],
            _ => [],
        };
        Object(mode, path, [verb], optional);

        if (isFloor)
        {
            if (verb != "patrol"
                || mode.TryGetProperty("while", out _)
                || mode.TryGetProperty("until", out _))
            {
                throw Error(path,
                    "the last mode must be an unconditioned patrol floor.");
            }
        }
        else if (verb is not ("recover" or "fetch")
                 && !mode.TryGetProperty("while", out _))
        {
            throw Error(path,
                "every mode above the floor needs 'while'; otherwise it "
                + "permanently shadows lower priorities.");
        }

        (string Kind, string Id)? destination = null;
        if (verb is "patrol" or "assault")
            destination = Destination(mode.GetProperty(verb), $"{path}.{verb}");
        else if (verb == "intercept")
        {
            OneOf(mode, "intercept", path, "enemy-carriers", "inbound");
            Identifier(mode, "from", path);
        }
        else if (verb == "recover")
            Exact(mode, "recover", "auto", path);
        else if (verb == "follow")
        {
            string leader = Identifier(mode, "follow", path);
            if (!roleIds.Contains(leader))
                throw Error(path, $"follow references unknown role '{leader}'.");
            if (leader == role.Id)
                throw Error(path, $"role '{role.Id}' cannot follow itself.");
            OneOf(mode, "posture", path, "trail", "screen");
        }
        else if (verb == "fetch")
        {
            string custody = Identifier(mode, "fetch", path);
            if (!custodyIds.Contains(custody))
                throw Error(path, $"fetch references unknown custody '{custody}'.");
            if (!roleFetch.Add(custody))
                throw Error(path, $"role '{role.Id}' fetches custody '{custody}' twice.");
            fetchConsumers[custody].Add(role.Id);
        }

        foreach (string conditionField in new[] { "while", "until" })
        {
            if (mode.TryGetProperty(conditionField, out JsonElement expression))
            {
                ValidateExpression(
                    expression,
                    AllowSelf: true,
                    conditions,
                    conditionConsumers,
                    $"{path}.{conditionField}");
            }
        }
        if (mode.TryGetProperty("fight", out JsonElement fight))
            ValidateFight(fight, $"{path}.fight");

        if (verb != "fetch")
        {
            if (!mode.TryGetProperty("focus", out JsonElement focusValue))
                throw Error(path, $"{verb} needs focus.");
            if (focusValue.ValueKind == JsonValueKind.String)
            {
                string id = focusValue.GetString()!;
                if (!focus.ContainsKey(id))
                    throw Error(path, $"unknown focus scope '{id}'.");
                focusConsumers[id].Add($"{role.Id}.modes[{index}]");
            }
            else
            {
                Object(focusValue, $"{path}.focus", ["maximumAttackers"]);
                Range(focusValue, "maximumAttackers", $"{path}.focus", 1, 8);
            }
        }

        if (mode.TryGetProperty("cohort", out JsonElement cohortValue))
        {
            if (isFloor || verb is not ("patrol" or "assault"))
                throw Error(path,
                    "cohort is allowed only on a non-floor patrol or assault.");
            if (!mode.TryGetProperty("posture", out _))
                throw Error(path, "a cohort rung needs a posture.");
            if (cohortValue.ValueKind == JsonValueKind.String)
            {
                string id = cohortValue.GetString()!;
                if (!cohorts.ContainsKey(id))
                    throw Error(path, $"unknown cohort '{id}'.");
                if (cohortConsumers[id].Any(value => value.Role == role.Id))
                    throw Error(path,
                        $"role '{role.Id}' references cohort '{id}' twice.");
                cohortConsumers[id].Add(new CohortConsumer(
                    role.Id, destination!.Value, path));
            }
            else
            {
                Object(cohortValue, $"{path}.cohort", ["take", "select"]);
                Range(cohortValue, "take", $"{path}.cohort", 1, 8);
                Exact(cohortValue, "select", "nearest", $"{path}.cohort");
            }
        }
        if (mode.TryGetProperty("posture", out _)
            && verb is "patrol" or "assault")
        {
            OneOf(mode, "posture", path, "front", "core", "rear");
        }
    }

    private static void ValidateExpression(
        JsonElement value,
        bool AllowSelf,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        Dictionary<string, int> consumers,
        string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw Error(path, "condition must be a boolean string.");
        string text = value.GetString()!.Trim();
        if (text.Length == 0)
            throw Error(path, "condition must not be empty.");
        foreach (string alternative in text.Split(" or ", StringSplitOptions.None))
        foreach (string operand in alternative.Split(" and ", StringSplitOptions.None))
        {
            string id = operand.Trim();
            IdentifierValue(id, path, "condition reference");
            if (id is "always" or "no-live-friendlies")
                continue;
            if (!conditions.TryGetValue(id, out ConditionInfo? condition))
                throw Error(path, $"references unknown condition '{id}'.");
            if (condition.IsSelf && !AllowSelf)
                throw Error(path,
                    $"body-scoped condition '{id}' is legal only in a doctrine mode.");
            consumers[id]++;
        }
    }

    private static void ValidateFight(JsonElement fight, string path)
    {
        Object(fight, path, [],
            ["targets", "engage", "chase", "breakOff", "defense", "collect", "heal"]);
        if (fight.TryGetProperty("collect", out _))
            OneOf(fight, "collect", path, "yield", "first");
        if (fight.TryGetProperty("heal", out _))
            OneOf(fight, "heal", path, "yield", "first");
        if (fight.TryGetProperty("targets", out JsonElement targets))
        {
            Object(targets, $"{path}.targets", [], ["lone", "prefer"]);
            if (targets.TryGetProperty("lone", out _))
                Range(targets, "lone", path, 0, 8);
            if (targets.TryGetProperty("prefer", out JsonElement prefer))
            {
                string[] values = StringArray(prefer, $"{path}.targets.prefer", 1, 6);
                if (values.Any(value => !TargetPreferences.Contains(value)))
                    throw Error(path, "fight has an unknown target preference.");
            }
        }
        if (fight.TryGetProperty("engage", out JsonElement engage))
        {
            Object(engage, $"{path}.engage", [],
                ["within", "killableTicks", "from", "positionTicks", "else"]);
            if (engage.TryGetProperty("within", out _))
                Range(engage, "within", path, 0, 12);
            if (engage.TryGetProperty("killableTicks", out _))
                Range(engage, "killableTicks", path, 0, 60);
            if (engage.TryGetProperty("from", out _))
                Exact(engage, "from", "behind", path);
            if (engage.TryGetProperty("positionTicks", out _))
                Range(engage, "positionTicks", path, 1, 64);
            if (engage.TryGetProperty("else", out _))
                OneOf(engage, "else", path, "strike", "breakOff");
        }
        if (fight.TryGetProperty("chase", out JsonElement chase))
        {
            Object(chase, $"{path}.chase", [],
                ["leash", "onlyCatchable", "executeBelowHealth", "persistTicks"]);
            if (chase.TryGetProperty("leash", out _))
                Range(chase, "leash", path, 0, 16);
            if (chase.TryGetProperty("persistTicks", out _))
                Range(chase, "persistTicks", path, 1, 120);
            if (chase.TryGetProperty("onlyCatchable", out JsonElement catchable)
                && catchable.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Error(path, "'onlyCatchable' must be a boolean.");
            }
            if (chase.TryGetProperty("executeBelowHealth", out _))
                Range(chase, "executeBelowHealth", path, 0, 8);
        }
        if (fight.TryGetProperty("defense", out JsonElement defense))
        {
            Object(defense, $"{path}.defense", [], ["radius", "return"]);
            if (defense.TryGetProperty("radius", out _))
                Range(defense, "radius", path, 0, 16);
            if (defense.TryGetProperty("return", out JsonElement result)
                && result.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Error(path, "'return' must be a boolean.");
            }
        }
        if (fight.TryGetProperty("breakOff", out JsonElement breakOff))
        {
            Object(breakOff, $"{path}.breakOff", [],
                ["threats", "within", "memoryTicks", "recoverTicks", "health"]);
            if (breakOff.TryGetProperty("health", out _))
                Range(breakOff, "health", path, 0, 8);
            int threats = breakOff.TryGetProperty("threats", out _)
                ? Range(breakOff, "threats", path, 0, 8) : 0;
            if (breakOff.TryGetProperty("within", out _))
                Range(breakOff, "within", path, 2, 16);
            if (breakOff.TryGetProperty("memoryTicks", out _))
                Range(breakOff, "memoryTicks", path, 1, 120);
            if (breakOff.TryGetProperty("recoverTicks", out _))
                Range(breakOff, "recoverTicks", path, 4, 120);
            if (threats > 0
                && (!breakOff.TryGetProperty("within", out _)
                    || !breakOff.TryGetProperty("memoryTicks", out _)))
            {
                throw Error(path,
                    "breakOff threats requires within and memoryTicks.");
            }
        }
    }

    private static void ValidateRepair(JsonElement repair, string path)
    {
        Object(repair, path,
            [
                "prefer", "maximumProvidersPerTarget",
                "minimumProviderSeparation", "reserveHealthPercent",
                "survivalFallback",
            ]);
        string[] prefer = StringArray(
            repair.GetProperty("prefer"), $"{path}.prefer", 1, 6);
        if (prefer.Any(value => value is not
                ("weakest" or "carrier" or "medic" or "focus" or "anchor" or "any")))
        {
            throw Error(path, "repair has an unknown target preference.");
        }
        Range(repair, "maximumProvidersPerTarget", path, 1, 8);
        Range(repair, "minimumProviderSeparation", path, 0, 8);
        Range(repair, "reserveHealthPercent", path, 0, 100);
        OneOf(repair, "survivalFallback", path,
            "evade", "regroup", "hold", "self-defense");
    }

    private static void ValidateCustody(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        IReadOnlyDictionary<string, ConditionInfo> conditions,
        Dictionary<string, int> conditionConsumers,
        string path)
    {
        string id = Identifier(value, "custodyId", $"{path}.custodyPolicies");
        string at = $"{path}.custodyPolicies.{id}";
        Object(value, at,
            [
                "custodyId", "sourceWells", "pickupReservationTicks",
                "transferTimeoutTicks", "deliveryTimeoutTicks",
                "accidentalPickup", "dropRecovery", "unreachableFallback",
                "safeConversion",
            ],
            [
                "emergencyRecoveryZones", "emergencyPickupRadius",
                "emergencyRecovery", "emergencyRecoveryRoles",
                "emergencyRecoverySourceWells",
                "emergencyRecoveryDisposition",
                "emergencyDisplacementTarget",
                "emergencyDisplacementReleaseRadius", "forwardPass",
                "baitDrop", "deliveryRoutes", "pickupSelection",
            ]);
        ValidateWells(value.GetProperty("sourceWells"), $"{at}.sourceWells");
        Range(value, "pickupReservationTicks", at, 1, 120);
        Range(value, "transferTimeoutTicks", at, 1, 120);
        Range(value, "deliveryTimeoutTicks", at, 1, 1200);
        OneOf(value, "accidentalPickup", at,
            "transfer", "deliver", "drop-safe");
        OneOf(value, "dropRecovery", at,
            "same-carrier", "nearest-authorized", "guard-until-safe");
        if (value.TryGetProperty("pickupSelection", out _))
            OneOf(value, "pickupSelection", at,
                "carrier-preference", "nearest-authorized");
        OneOf(value, "unreachableFallback", at,
            "hold", "guard", "alternate-core", "regroup");
        ValidateExpression(value.GetProperty("safeConversion"), false,
            conditions, conditionConsumers, $"{at}.safeConversion");
        if (value.TryGetProperty("forwardPass", out _))
            OneOf(value, "forwardPass", at, "none", "relay-catcher");
        if (value.TryGetProperty("deliveryRoutes", out JsonElement routes))
        {
            foreach (JsonElement rule in BoundedArray(
                         routes, $"{at}.deliveryRoutes", 1, 8))
            {
                Object(rule, $"{at}.deliveryRoutes", ["zone", "route"]);
                Identifier(rule, "zone", $"{at}.deliveryRoutes");
                Identifier(rule, "route", $"{at}.deliveryRoutes");
            }
        }
        if (value.TryGetProperty("baitDrop", out JsonElement bait))
        {
            Object(bait, $"{at}.baitDrop", ["zone", "reclaim"]);
            Identifier(bait, "zone", $"{at}.baitDrop");
            ValidateExpression(bait.GetProperty("reclaim"), false,
                conditions, conditionConsumers, $"{at}.baitDrop.reclaim");
        }

        string[] emergency =
        [
            "emergencyRecoveryZones", "emergencyPickupRadius",
            "emergencyRecovery", "emergencyRecoveryRoles",
            "emergencyRecoverySourceWells", "emergencyRecoveryDisposition",
        ];
        bool anyEmergency = emergency.Any(name => value.TryGetProperty(name, out _));
        if (anyEmergency && emergency.Any(name => !value.TryGetProperty(name, out _)))
            throw Error(at, "emergency recovery needs its complete six-field bundle.");
        if (!anyEmergency)
        {
            if (value.TryGetProperty("emergencyDisplacementTarget", out _)
                || value.TryGetProperty(
                    "emergencyDisplacementReleaseRadius", out _))
            {
                throw Error(at, "displacement fields need emergency recovery.");
            }
            return;
        }
        StringArray(value.GetProperty("emergencyRecoveryZones"),
            $"{at}.emergencyRecoveryZones", 1, 8);
        Range(value, "emergencyPickupRadius", at, 1, 4);
        ValidateExpression(value.GetProperty("emergencyRecovery"), false,
            conditions, conditionConsumers, $"{at}.emergencyRecovery");
        foreach (string role in StringArray(
                     value.GetProperty("emergencyRecoveryRoles"),
                     $"{at}.emergencyRecoveryRoles", 1, 8))
        {
            if (!roleIds.Contains(role))
                throw Error(at, $"emergency recovery references unknown role '{role}'.");
        }
        ValidateWells(value.GetProperty("emergencyRecoverySourceWells"),
            $"{at}.emergencyRecoverySourceWells");
        string disposition = OneOf(value, "emergencyRecoveryDisposition", at,
            "deliver", "displace");
        bool hasTarget = value.TryGetProperty("emergencyDisplacementTarget", out _);
        bool hasRadius = value.TryGetProperty(
            "emergencyDisplacementReleaseRadius", out _);
        if (disposition == "displace")
        {
            if (!hasTarget || !hasRadius)
                throw Error(at, "displace recovery needs target and release radius.");
            Identifier(value, "emergencyDisplacementTarget", at);
            Range(value, "emergencyDisplacementReleaseRadius", at, 0, 4);
        }
        else if (hasTarget || hasRadius)
        {
            throw Error(at, "deliver recovery cannot declare displacement fields.");
        }
    }

    private static void ValidateWells(JsonElement value, string path)
    {
        string[] wells = StringArray(value, path, 1, 3);
        if (wells.Any(well => well is not ("north" or "centre" or "south")))
            throw Error(path, "Wells must be north, centre, or south.");
    }

    private static void ValidateSharedConsumers(
        IReadOnlyDictionary<string, CohortInfo> cohorts,
        IReadOnlyDictionary<string, List<CohortConsumer>> cohortConsumers,
        IReadOnlyDictionary<string, int> focus,
        IReadOnlyDictionary<string, List<string>> focusConsumers,
        string path)
    {
        foreach (CohortInfo cohort in cohorts.Values)
        {
            List<CohortConsumer> consumers = cohortConsumers[cohort.Id];
            if (consumers.Count < 2)
                throw Error($"{path}.doctrines.cohorts.{cohort.Id}",
                    "a cohort needs at least two role rungs; inline a one-off.");
            (string Kind, string Id) destination = consumers[0].Destination;
            CohortConsumer? mismatch = consumers.FirstOrDefault(
                consumer => consumer.Destination != destination);
            if (mismatch is not null)
            {
                throw Error(mismatch.Path,
                    $"cohort '{cohort.Id}' has more than one destination.");
            }
        }
        foreach (string id in focus.Keys)
        {
            if (focusConsumers[id].Count < 2)
                throw Error($"{path}.doctrines.focus.{id}",
                    "a shared focus scope needs at least two rung consumers; "
                    + "inline a one-off.");
        }
    }
}
