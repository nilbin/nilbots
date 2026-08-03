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
        "friendly-carriers", "secured-cores", "visible-loose-cores",
        "well-has-outstanding", "outstanding-well-count",
        "ticks-without-objective-progress",
        "reactor-integrity", "reactor-charge", "formation-established-ticks",
        "custody-state-ticks", "role-live-count",
    ];

    private static readonly HashSet<string> GroupSubjectFacts =
    [
        "group-live-count", "group-joining-count", "group-in-zone-count",
        "group-cohesion",
        "group-stuck-ticks", "formation-established-ticks",
    ];

    private static readonly HashSet<string> ZoneFacts =
    [
        "friendlies-in-zone-count", "group-in-zone-count",
        "visible-enemies-in-zone", "remembered-enemies-in-zone",
    ];

    public static TacticalPlaybookCompilation Compile(string playbookPath)
    {
        string fullPlaybookPath = Path.GetFullPath(playbookPath);
        byte[] playbookSource = File.ReadAllBytes(fullPlaybookPath);
        using JsonDocument playbookDocument = Parse(
            playbookSource, fullPlaybookPath);
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
        ValidateLayout(layout, fullLayoutPath);

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
        foreach (JsonElement role in roles)
            ValidateRole(role, roleIds, path);

        JsonElement[] groups = BoundedArray(
            root.GetProperty("groups"), $"{path}.groups", 1, 8);
        HashSet<string> groupIds = UniqueIds(
            groups, "groupId", $"{path}.groups");
        foreach (JsonElement group in groups)
            ValidateGroup(group, roleIds, groupIds, path);

        JsonElement[] formations = BoundedArray(
            root.GetProperty("formations"), $"{path}.formations", 1, 24);
        HashSet<string> formationIds = UniqueIds(
            formations, "formationId", $"{path}.formations");
        foreach (JsonElement formation in formations)
            ValidateFormation(formation, roleIds, path);

        JsonElement[] engagements = BoundedArray(
            root.GetProperty("engagements"), $"{path}.engagements", 1, 24);
        HashSet<string> engagementIds = UniqueIds(
            engagements, "engagementId", $"{path}.engagements");
        foreach (JsonElement engagement in engagements)
            ValidateEngagement(engagement, roleIds, path);

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
            ValidateCustody(policy, roleIds, groupIds, path);

        JsonElement[] orders = BoundedArray(
            root.GetProperty("orders"), $"{path}.orders", 1, 64);
        HashSet<string> orderIds = UniqueIds(
            orders, "orderId", $"{path}.orders");
        foreach (JsonElement order in orders)
        {
            ValidateOrder(order, groupIds, formationIds, engagementIds,
                supportIds, custodyIds, path);
        }

        ValidateCoordination(
            root.GetProperty("coordination"),
            groupIds,
            roleIds,
            orderIds,
            path);
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
            ]);
        string roleId = Identifier(value, "roleId", at);
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
            value.GetProperty("localStateMachine"), groupIds, roleIds, at);
    }

    private static void ValidateLocalStateMachine(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
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
                path);
        }
    }

    private static void ValidateFormation(
        JsonElement value,
        IReadOnlySet<string> roleIds,
        string path)
    {
        string at = $"{path}.formations";
        Object(value, at,
            [
                "formationId", "shape", "orientation", "spacing",
                "placements", "cohesion", "reflow",
            ]);
        string id = Identifier(value, "formationId", at);
        OneOf(value, "shape", at,
            "line", "column", "wedge", "arc", "ring", "escort", "custom");
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
                ["roleId", "sector", "order", "offset"]);
            Reference(placement, "roleId", roleIds, at);
            OneOf(placement, "sector", at,
                "front", "rear", "left", "right", "centre", "any");
            Range(placement, "order", at, 0, 15);
            Point(placement.GetProperty("offset"), $"{at}.{id}.offset", -8, 8);
        }
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
        string path)
    {
        string at = $"{path}.engagements";
        Object(value, at,
            [
                "engagementId", "participants", "targetPriorities",
                "tieBreakers", "coordinationScope", "lockTicks",
                "maximumAttackersPerTarget",
                "overkillDamage", "chaseLeash", "signatureCoordination",
                "dodgeCoverage", "release", "selfDefense",
            ]);
        string id = Identifier(value, "engagementId", at);
        References(value.GetProperty("participants"), roleIds,
            $"{at}.{id}.participants", 1, 16);
        OneOfArray(value.GetProperty("targetPriorities"), at, 1, 16,
            "enemy-carrier", "lowest-health", "closest-to-anchor",
            "closest-to-reactor", "highest-threat", "fresh-respawn");
        OneOfArray(value.GetProperty("tieBreakers"), at, 1, 8,
            "health", "distance", "unit-id", "life-id", "position");
        OneOf(value, "coordinationScope", at,
            "order-group", "shared-policy");
        Range(value, "lockTicks", at, 0, 120);
        Range(value, "maximumAttackersPerTarget", at, 1, 8);
        Range(value, "overkillDamage", at, 0, 1000);
        Range(value, "chaseLeash", at, 0, 16);
        OneOf(value, "signatureCoordination", at,
            "none", "control-first", "damage-first", "support-first");
        JsonElement dodge = value.GetProperty("dodgeCoverage");
        Object(dodge, $"{at}.{id}.dodgeCoverage",
            ["mode", "horizonTicks", "minimumCoveredOptions", "fallback"]);
        OneOf(dodge, "mode", at, "current-position", "escape-lanes");
        Range(dodge, "horizonTicks", at, 0, 1);
        Range(dodge, "minimumCoveredOptions", at, 1, 9);
        OneOf(dodge, "fallback", at,
            "current-position", "best-coverage");
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
        string path)
    {
        string at = $"{path}.custodyPolicies";
        Object(value, at,
            [
                "custodyId", "authorizedCarrierRoles", "escortGroups",
                "sourceWells", "pickupReservationTicks", "transferTimeoutTicks",
                "deliveryTimeoutTicks", "accidentalPickup", "dropRecovery",
                "unreachableFallback", "safeConversionAll",
            ]);
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
            value.GetProperty("safeConversionAll"), groupIds, roleIds,
            $"{at}.{id}.safeConversionAll", 1, 8);
    }

    private static void ValidateCoordination(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> orderIds,
        string path)
    {
        string at = $"{path}.coordination";
        Object(value, at, ["initialPhase", "phases"]);
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
                roleIds, at);
        }
    }

    private static void ValidateOrder(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> formationIds,
        IReadOnlySet<string> engagementIds,
        IReadOnlySet<string> supportIds,
        IReadOnlySet<string> custodyIds,
        string path)
    {
        string at = $"{path}.coordination.orders";
        Object(value, at,
            [
                "orderId", "groupId", "priority", "movement", "formationId",
                "engagementId", "localState", "fallback",
            ],
            ["supportId", "custodyId"]);
        Identifier(value, "orderId", at);
        Reference(value, "groupId", groupIds, at);
        Range(value, "priority", at, 0, 1000);
        Reference(value, "formationId", formationIds, at);
        Reference(value, "engagementId", engagementIds, at);
        if (value.TryGetProperty("supportId", out _))
            Reference(value, "supportId", supportIds, at, allowEmpty: false);
        if (value.TryGetProperty("custodyId", out _))
            Reference(value, "custodyId", custodyIds, at, allowEmpty: false);
        Identifier(value, "localState", at);
        ValidateMovement(value.GetProperty("movement"), at);
        JsonElement fallback = value.GetProperty("fallback");
        Object(fallback, $"{at}.fallback",
            ["onNoPath", "onUnderstrength", "onInvalidTarget"]);
        OneOf(fallback, "onNoPath", at, "reflow", "hold", "regroup");
        OneOf(fallback, "onUnderstrength", at,
            "continue", "regroup", "fallback-phase");
        OneOf(fallback, "onInvalidTarget", at,
            "hold", "alternate", "fallback-phase");
    }

    private static void ValidateMovement(JsonElement value, string path)
    {
        Object(value, $"{path}.movement",
            [
                "kind", "target", "arrivalRadius", "completion",
                "stuckTicks", "stuckRecovery", "chaseLeash", "pace",
            ]);
        OneOf(value, "kind", path,
            "route", "zone", "anchor", "reactor", "carrier", "hold");
        NonEmptyString(value, "target", path, allowEmpty: true);
        Range(value, "arrivalRadius", path, 0, 16);
        OneOf(value, "completion", path,
            "leader-arrived", "cohesion-arrived", "all-arrived", "continuous");
        Range(value, "stuckTicks", path, 1, 120);
        OneOf(value, "stuckRecovery", path,
            "repath", "yield", "reflow", "regroup", "hold");
        Range(value, "chaseLeash", path, 0, 16);
        OneOf(value, "pace", path, "slowest", "leader", "free");
    }

    private static void ValidateTransitions(
        JsonElement value,
        IReadOnlySet<string> stateIds,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        string path)
    {
        JsonElement[] transitions = BoundedArray(value, path, 0, 32);
        foreach (JsonElement transition in transitions)
        {
            Object(transition, path,
                ["priority", "to", "cause", "stableTicks", "when"]);
            Range(transition, "priority", path, 0, 1000);
            Reference(transition, "to", stateIds, path);
            OneOf(transition, "cause", path,
                "success", "failure", "recovery", "reaction");
            Range(transition, "stableTicks", path, 1, 120);
            ValidateConditionGroups(
                transition.GetProperty("when"), groupIds, roleIds, path,
                1, 16);
        }
    }

    private static void ValidateConditionGroups(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
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
                ValidateCondition(condition, groupIds, roleIds, path);
        }
    }

    private static void ValidateCondition(
        JsonElement value,
        IReadOnlySet<string> groupIds,
        IReadOnlySet<string> roleIds,
        string path)
    {
        string fact = RequiredString(value, "fact", path);
        if (!ConditionFacts.Contains(fact))
            throw Error(path, $"unknown condition fact '{fact}'.");
        var variantFields = new List<string>();
        if (GroupSubjectFacts.Contains(fact)
            || fact is "role-live-count" or "well-has-outstanding")
            variantFields.Add("subject");
        if (ZoneFacts.Contains(fact))
            variantFields.Add("zone");
        Object(value, path,
            ["fact", "operator", "value", .. variantFields]);
        OneOf(value, "operator", path, [.. ConditionOperators]);
        Range(value, "value", path, 0, 100000);
        string subject = variantFields.Contains("subject", StringComparer.Ordinal)
            ? NonEmptyString(value, "subject", path)
            : "";
        if (variantFields.Contains("zone", StringComparer.Ordinal))
            NonEmptyString(value, "zone", path);
        if (GroupSubjectFacts.Contains(fact) && !groupIds.Contains(subject))
            throw Error(path, $"condition references unknown group '{subject}'.");
        if (fact == "role-live-count" && !roleIds.Contains(subject))
            throw Error(path, $"condition references unknown role '{subject}'.");
    }

    private static void ValidateLayout(JsonElement root, string path)
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
                    "routeAliases"]);
            string fingerprint = Hash(
                binding, "matchContractFingerprint", $"{path}.bindings");
            if (!fingerprints.Add(fingerprint))
                throw Error(path, $"duplicate layout binding '{fingerprint}'.");
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
            if (value.ContainsKey("orderId"))
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
        if (result.Length is < 1 or > 64
            || !char.IsLower(result[0])
            || result.Any(character => !char.IsLower(character)
                && !char.IsDigit(character)
                && character != '-'))
            throw Error(path, $"'{name}' must be a lower-kebab identifier.");
        return result;
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
