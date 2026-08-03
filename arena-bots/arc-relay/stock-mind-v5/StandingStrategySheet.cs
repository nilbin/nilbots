using System.Collections.Immutable;
using System.Text.Json;
using BotArena.Sdk;

internal sealed class StandingStrategySheet
{
    private const int EnvelopeMagic = 0x31535241;
    private const string Schema = "arc-relay-evaluation-sheet-v3";
    private readonly bool _mirror;
    private readonly int _maxX;
    private readonly StandingStrategyDocument _document;
    private static readonly HashSet<string> KnownClasses =
    [
        "kestrel", "palisade", "towline", "patchbay", "lantern", "mortar",
        "minesmith", "hush", "relay", "switchback", "longshot", "mason",
        "sunder", "repulsor", "veil", "nest",
    ];
    private static readonly HashSet<string> KnownBehaviors =
    ["advance", "escort", "guard", "occupy", "regroup", "score", "support"];
    private static readonly HashSet<string> KnownCorePolicies =
    ["avoid", "collect", "deliver", "drop", "guard", "transfer"];
    private static readonly HashSet<string> KnownCoreFallbacks =
    ["deliver", "drop", "hold"];
    private static readonly HashSet<string> KnownEngagements =
    ["advance-under-fire", "avoid", "evade", "focus", "hold"];
    private static readonly HashSet<string> KnownFacings =
    [
        "adaptive", "north", "east", "south", "west", "toward-target",
        "toward-own-reactor", "toward-enemy-reactor",
    ];
    private static readonly HashSet<string> KnownSignatures =
    ["advance", "conserve", "control", "normal", "none", "support"];
    private static readonly HashSet<string> KnownFacts =
    [
        "always", "live-friendlies", "known-enemies-unavailable",
        "secured-cores", "visible-loose-cores", "friendly-carriers",
        "ticks-without-objective-progress", "well-has-outstanding",
        "friendlies-in-zone", "stable-friendlies-in-zone",
        "visible-enemies-in-zone", "remembered-enemies-in-zone",
    ];
    private static readonly HashSet<string> ZoneFacts =
    [
        "friendlies-in-zone", "stable-friendlies-in-zone",
        "visible-enemies-in-zone", "remembered-enemies-in-zone",
    ];
    private static readonly HashSet<string> KnownOperators =
    ["at-least", "at-most", "equals", "less-than", "greater-than"];

    private StandingStrategySheet(
        StandingStrategyDocument document,
        string sourceSha256,
        bool mirror,
        int maxX)
    {
        _document = document;
        SourceSha256 = sourceSha256;
        _mirror = mirror;
        _maxX = maxX;
    }

    internal string SheetId => _document.SheetId;
    internal string MapId => _document.MapId;
    internal string SourceSha256 { get; }
    internal StandingStrategyPlan Strategy => _document.StandingStrategy;
    internal string[] Composition => _document.Composition;

    internal static StandingStrategySheet Load(
        ImmutableArray<byte> evaluationData,
        GenericActorResolvedMatchContract contract,
        bool mirror)
    {
        if (evaluationData.IsDefaultOrEmpty)
            throw new InvalidDataException("The standing mind requires sheet data.");
        using var reader = new BinaryReader(
            new MemoryStream(evaluationData.ToArray()));
        if (reader.ReadInt32() != EnvelopeMagic)
            throw new InvalidDataException("Unknown strategy sheet envelope.");
        string sourceSha256 = reader.ReadString();
        string schema = reader.ReadString();
        if (!string.Equals(schema, Schema, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported standing sheet schema '{schema}'.");
        }
        int length = reader.ReadInt32();
        if (length <= 0 || length > 64 * 1024)
            throw new InvalidDataException("Invalid standing sheet payload length.");
        byte[] json = reader.ReadBytes(length);
        if (json.Length != length || reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Truncated or trailing standing sheet data.");
        using JsonDocument parsed = JsonDocument.Parse(json);
        StandingStrategyDocument document = ParseDocument(parsed.RootElement);
        Validate(document, contract);
        return new StandingStrategySheet(
            document, sourceSha256, mirror, contract.Map.Width - 1);
    }

    private static StandingStrategyDocument ParseDocument(JsonElement root) =>
        new()
        {
            Schema = RequiredString(root, "schema"),
            SheetId = RequiredString(root, "sheetId"),
            MapId = RequiredString(root, "mapId"),
            Composition = StringArray(root.GetProperty("composition")),
            Zones = root.GetProperty("zones").EnumerateObject().ToDictionary(
                value => value.Name,
                value => IntArray(value.Value),
                StringComparer.Ordinal),
            Paths = PointDictionary(root.GetProperty("paths")),
            Formations = PointDictionary(root.GetProperty("formations")),
            StandingStrategy = ParseStrategy(
                root.GetProperty("standingStrategy")),
        };

    private static StandingStrategyPlan ParseStrategy(JsonElement value)
    {
        JsonElement memory = value.GetProperty("memory");
        return new StandingStrategyPlan
        {
            InitialPhase = RequiredString(value, "initialPhase"),
            Parameters = value.GetProperty("parameters")
                .EnumerateObject().ToDictionary(
                    item => item.Name,
                    item => item.Value.GetString() ?? "",
                    StringComparer.Ordinal),
            FocusPolicy = OptionalString(value, "focusPolicy", "carrier-first"),
            Memory = new StandingMemoryPolicy
            {
                EnemyUnavailableTicks = OptionalInt(
                    memory, "enemyUnavailableTicks", 21),
                LastSeenEnemyTicks = OptionalInt(
                    memory, "lastSeenEnemyTicks", 30),
                SecuredCoreMemoryTicks = OptionalInt(
                    memory, "securedCoreMemoryTicks", 40),
                ObjectiveProgressMemoryTicks = OptionalInt(
                    memory, "objectiveProgressMemoryTicks", 90),
                StableControlTicks = OptionalInt(
                    memory, "stableControlTicks", 3),
            },
            Phases = value.GetProperty("phases").EnumerateArray()
                .Select(ParsePhase).ToArray(),
        };
    }

    private static StandingPhasePlan ParsePhase(JsonElement value) => new()
    {
        Id = RequiredString(value, "id"),
        MinimumTicks = OptionalInt(value, "minimumTicks", 0),
        Entry = OptionalArray(value, "entry", ParseGroup),
        Assignments = value.GetProperty("assignments").EnumerateArray()
            .Select(ParseAssignment).ToArray(),
        Transitions = value.GetProperty("transitions").EnumerateArray()
            .Select(ParseTransition).ToArray(),
    };

    private static StandingTransitionPlan ParseTransition(JsonElement value) =>
        new()
        {
            Priority = OptionalInt(value, "priority", 0),
            To = RequiredString(value, "to"),
            Cause = OptionalString(value, "cause", "condition"),
            StableTicks = OptionalInt(value, "stableTicks", 1),
            When = value.GetProperty("when").EnumerateArray()
                .Select(ParseGroup).ToArray(),
        };

    private static StandingAssignmentPlan ParseAssignment(JsonElement value)
    {
        JsonElement position = value.GetProperty("position");
        return new StandingAssignmentPlan
        {
            Priority = OptionalInt(value, "priority", 0),
            Id = RequiredString(value, "id"),
            Resilience = RequiredString(value, "resilience"),
            Count = OptionalInt(value, "count", -1),
            CandidateClasses = OptionalStringArray(value, "candidateClasses"),
            CandidateRoles = OptionalStringArray(value, "candidateRoles"),
            CarrierOnly = OptionalBool(value, "carrierOnly"),
            Behavior = RequiredString(value, "behavior"),
            Position = new StandingPositionIntent
            {
                Kind = RequiredString(position, "kind"),
                Target = OptionalString(position, "target", ""),
            },
            Formation = OptionalString(value, "formation", ""),
            Facing = OptionalString(value, "facing", "adaptive"),
            Engagement = OptionalString(value, "engagement", "focus"),
            Signature = OptionalString(value, "signature", "normal"),
            CorePolicy = OptionalString(value, "corePolicy", "avoid"),
            CoreFallback = OptionalString(value, "coreFallback", "hold"),
            PreferCarrier = OptionalBool(value, "preferCarrier"),
            CoreSource = OptionalString(value, "coreSource", ""),
            Respawn = OptionalString(value, "respawn", "rejoin"),
            When = OptionalArray(value, "when", ParseGroup),
        };
    }

    private static StandingConditionGroup ParseGroup(JsonElement value) => new()
    {
        All = OptionalArray(value, "all", ParseCondition),
        Any = OptionalArray(value, "any", ParseCondition),
    };

    private static StandingCondition ParseCondition(JsonElement value) => new()
    {
        Fact = RequiredString(value, "fact"),
        Operator = OptionalString(value, "operator", "at-least"),
        Value = OptionalInt(value, "value", 1),
        Zone = OptionalString(value, "zone", ""),
        Subject = OptionalString(value, "subject", ""),
    };

    private static Dictionary<string, int[][]> PointDictionary(
        JsonElement value) => value.EnumerateObject().ToDictionary(
            item => item.Name,
            item => item.Value.EnumerateArray().Select(IntArray).ToArray(),
            StringComparer.Ordinal);

    private static string RequiredString(JsonElement value, string name) =>
        value.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"'{name}' must be a string.");

    private static string OptionalString(
        JsonElement value,
        string name,
        string fallback) => value.TryGetProperty(name, out JsonElement item)
            ? item.GetString() ?? fallback
            : fallback;

    private static int OptionalInt(
        JsonElement value,
        string name,
        int fallback) => value.TryGetProperty(name, out JsonElement item)
            ? item.GetInt32()
            : fallback;

    private static bool OptionalBool(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement item) && item.GetBoolean();

    private static string[] StringArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetString()
            ?? throw new InvalidDataException("Expected string array item."))
            .ToArray();

    private static string[] OptionalStringArray(
        JsonElement value,
        string name) => value.TryGetProperty(name, out JsonElement item)
            ? StringArray(item)
            : [];

    private static int[] IntArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    private static T[] OptionalArray<T>(
        JsonElement value,
        string name,
        Func<JsonElement, T> parse) =>
        value.TryGetProperty(name, out JsonElement item)
            ? item.EnumerateArray().Select(parse).ToArray()
            : [];

    internal string Resolve(string name)
    {
        string lane = Strategy.Parameters.GetValueOrDefault("lane", "");
        if (_mirror
            && string.Equals(
                Strategy.Parameters.GetValueOrDefault("lanePerspective", ""),
                "team-relative", StringComparison.Ordinal))
        {
            lane = lane switch
            {
                "north" => "south",
                "south" => "north",
                _ => lane,
            };
        }
        string parameterized = string.IsNullOrEmpty(lane)
            ? name
            : $"{name}.{lane}";
        return _document.Zones.ContainsKey(parameterized)
            || _document.Paths.ContainsKey(parameterized)
            || _document.Formations.ContainsKey(parameterized)
                ? parameterized
                : name;
    }

    internal string Parameter(string name)
    {
        string value = Strategy.Parameters.GetValueOrDefault(name, "");
        if (_mirror
            && string.Equals(
                Strategy.Parameters.GetValueOrDefault("lanePerspective", ""),
                "team-relative", StringComparison.Ordinal))
        {
            return value switch
            {
                "north" => "south",
                "south" => "north",
                _ => value,
            };
        }
        return value;
    }

    internal bool Contains(string zoneName, Position position)
    {
        string resolved = Resolve(zoneName);
        int[] zone = _document.Zones.TryGetValue(resolved, out int[]? value)
            ? value
            : throw new InvalidDataException($"Unknown zone '{zoneName}'.");
        Position canonical = Canonical(position);
        return canonical.X >= zone[0] && canonical.X <= zone[2]
            && canonical.Y >= zone[1] && canonical.Y <= zone[3];
    }

    internal Position[] Path(string name) =>
        Points(_document.Paths, Resolve(name), "path");

    internal Position[] Formation(string name) =>
        Points(_document.Formations, Resolve(name), "formation");

    internal Position ZoneCenter(string name)
    {
        string resolved = Resolve(name);
        int[] zone = _document.Zones.TryGetValue(resolved, out int[]? value)
            ? value
            : throw new InvalidDataException($"Unknown zone '{name}'.");
        return World(new Position((zone[0] + zone[2]) / 2, (zone[1] + zone[3]) / 2));
    }

    private Position[] Points(
        Dictionary<string, int[][]> source,
        string name,
        string kind) => source.TryGetValue(name, out int[][]? values)
        ? values.Select(value => World(new Position(value[0], value[1]))).ToArray()
        : throw new InvalidDataException($"Unknown {kind} '{name}'.");

    private Position Canonical(Position position) => _mirror
        ? new Position(_maxX - position.X, position.Y)
        : position;

    private Position World(Position position) => Canonical(position);

    private static void Validate(
        StandingStrategyDocument document,
        GenericActorResolvedMatchContract contract)
    {
        if (!string.Equals(document.Schema, Schema, StringComparison.Ordinal)
            || !string.Equals(document.MapId, contract.Map.MapId, StringComparison.Ordinal)
            || document.Composition.Length != 8
            || document.StandingStrategy.Phases.Length == 0)
        {
            throw new InvalidDataException(
                "Standing strategy schema, map, composition, or phases are invalid.");
        }
        if (string.IsNullOrWhiteSpace(document.SheetId)
            || document.Composition.Any(value => !KnownClasses.Contains(value))
            || document.Composition.GroupBy(value => value, StringComparer.Ordinal)
                .Any(group => group.Count() > 2))
        {
            throw new InvalidDataException(
                "Standing strategy identity or two-copy composition is invalid.");
        }
        StandingStrategyPlan strategy = document.StandingStrategy;
        if (strategy.Memory.EnemyUnavailableTicks is < 1 or > 600
            || strategy.Memory.LastSeenEnemyTicks is < 1 or > 600
            || strategy.Memory.SecuredCoreMemoryTicks is < 1 or > 600
            || strategy.Memory.ObjectiveProgressMemoryTicks is < 1 or > 600
            || strategy.Memory.StableControlTicks is < 1 or > 120
            || strategy.FocusPolicy is not
                ("carrier-first" or "weakest" or "home-threat"))
        {
            throw new InvalidDataException(
                "Standing strategy memory or focus policy is invalid.");
        }
        StandingPhasePlan[] phases = document.StandingStrategy.Phases;
        HashSet<string> phaseIds = phases.Select(value => value.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (phaseIds.Count != phases.Length
            || !phaseIds.Contains(document.StandingStrategy.InitialPhase))
            throw new InvalidDataException("Standing strategy phase graph is invalid.");
        foreach ((string name, int[] zone) in document.Zones)
        {
            if (string.IsNullOrWhiteSpace(name)
                || zone.Length != 4 || zone[0] > zone[2] || zone[1] > zone[3]
                || zone[0] < 0 || zone[1] < 0
                || zone[2] >= contract.Map.Width
                || zone[3] >= contract.Map.Height)
                throw new InvalidDataException($"Zone '{name}' is invalid.");
        }
        ValidatePointSets(document.Paths, "path", contract);
        ValidatePointSets(document.Formations, "formation", contract);
        foreach (StandingPhasePlan phase in phases)
        {
            if (string.IsNullOrWhiteSpace(phase.Id)
                || phase.MinimumTicks < 0 || phase.Assignments.Length == 0
                || phase.Assignments.Select(value => value.Id)
                    .Distinct(StringComparer.Ordinal).Count()
                    != phase.Assignments.Length
                || phase.Transitions.Any(value =>
                    !phaseIds.Contains(value.To)
                    || value.StableTicks is < 1 or > 120
                    || value.Priority < 0
                    || value.Cause is not ("success" or "failure" or "recovery")
                    || value.When.Length == 0))
            {
                throw new InvalidDataException($"Phase '{phase.Id}' is invalid.");
            }
            ValidateGroups(phase.Entry, document);
            foreach (StandingTransitionPlan transition in phase.Transitions)
                ValidateGroups(transition.When, document);
            foreach (StandingAssignmentPlan assignment in phase.Assignments)
            {
                if (string.IsNullOrWhiteSpace(assignment.Id)
                    || assignment.Priority < 0
                    || assignment.Count == 0 || assignment.Count < -1
                    || assignment.Count > 8
                    || assignment.Resilience is not
                        ("essential" or "replaceable" or "optional")
                    || assignment.Respawn is not
                        ("rejoin" or "rally" or "replace" or "baseline"))
                {
                    throw new InvalidDataException(
                        $"Assignment '{assignment.Id}' is invalid.");
                }
                if (!KnownBehaviors.Contains(assignment.Behavior)
                    || !KnownCorePolicies.Contains(assignment.CorePolicy)
                    || !KnownCoreFallbacks.Contains(assignment.CoreFallback)
                    || !KnownEngagements.Contains(assignment.Engagement)
                    || !KnownFacings.Contains(assignment.Facing)
                    || !KnownSignatures.Contains(assignment.Signature)
                    || assignment.CandidateClasses.Any(value =>
                        !KnownClasses.Contains(value))
                    || assignment.CandidateClasses.Distinct(
                        StringComparer.Ordinal).Count()
                        != assignment.CandidateClasses.Length
                    || assignment.Position is null)
                {
                    throw new InvalidDataException(
                        $"Assignment '{assignment.Id}' policy is invalid.");
                }
                switch (assignment.Position.Kind)
                {
                    case "path":
                        RequireReference(document.Paths,
                            assignment.Position.Target, "path");
                        break;
                    case "zone":
                        RequireReference(document.Zones,
                            assignment.Position.Target, "zone");
                        break;
                    case "own-reactor":
                    case "enemy-reactor":
                        if (!string.IsNullOrEmpty(assignment.Position.Target))
                            throw new InvalidDataException(
                                $"Assignment '{assignment.Id}' position is invalid.");
                        break;
                    default:
                        throw new InvalidDataException(
                            $"Assignment '{assignment.Id}' position is invalid.");
                }
                if (!string.IsNullOrEmpty(assignment.Formation))
                    RequireReference(document.Formations,
                        assignment.Formation, "formation");
                ValidateGroups(assignment.When, document);
            }
        }
    }

    private static void ValidatePointSets(
        IReadOnlyDictionary<string, int[][]> source,
        string kind,
        GenericActorResolvedMatchContract contract)
    {
        foreach ((string name, int[][] points) in source)
        {
            if (string.IsNullOrWhiteSpace(name) || points.Length == 0
                || points.Any(point => point.Length != 2
                    || point[0] < 0 || point[1] < 0
                    || point[0] >= contract.Map.Width
                    || point[1] >= contract.Map.Height))
            {
                throw new InvalidDataException($"{kind} '{name}' is invalid.");
            }
        }
    }

    private static void ValidateGroups(
        IEnumerable<StandingConditionGroup> groups,
        StandingStrategyDocument document)
    {
        foreach (StandingConditionGroup group in groups)
        {
            StandingCondition[] conditions = group.All.Concat(group.Any)
                .ToArray();
            if (conditions.Length == 0)
                throw new InvalidDataException("Standing condition group is empty.");
            foreach (StandingCondition condition in conditions)
            {
                if (!KnownFacts.Contains(condition.Fact)
                    || !KnownOperators.Contains(condition.Operator)
                    || (ZoneFacts.Contains(condition.Fact)
                        && string.IsNullOrWhiteSpace(condition.Zone)))
                {
                    throw new InvalidDataException(
                        $"Standing condition '{condition.Fact}' is invalid.");
                }
                if (ZoneFacts.Contains(condition.Fact))
                    RequireReference(document.Zones, condition.Zone, "zone");
                if (string.Equals(condition.Fact, "well-has-outstanding",
                        StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(condition.Subject))
                {
                    throw new InvalidDataException(
                        "Well condition requires a subject or parameter.");
                }
            }
        }
    }

    private static void RequireReference<T>(
        IReadOnlyDictionary<string, T> source,
        string name,
        string kind)
    {
        if (string.IsNullOrWhiteSpace(name)
            || (!source.ContainsKey(name)
                && !source.Keys.Any(value => value.StartsWith(
                    $"{name}.", StringComparison.Ordinal))))
        {
            throw new InvalidDataException($"Unknown {kind} '{name}'.");
        }
    }
}
