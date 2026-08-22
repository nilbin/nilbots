using System.Text;
using System.Text.Json;

namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Deterministic server-side materializer: one validated Tactics document in,
/// one Orders v3 + map plan source pair out. The generated pair is the only
/// executable form; ids are stable and readable because they are the practice
/// attribution contract. The squad program table owns the whole lifecycle —
/// selection, latched forward-only phase transitions, aborts, duration — so
/// leaves carry only their phase tag plus local episode gates. Readiness is
/// exact and per-bot at squad level: every member is at its ground or dead,
/// with no quorum approximation and no per-body release gate to rejoin.
/// </summary>
internal static class ArcRelayTacticsMaterializer
{
    private const int RearmTicks = 0;
    private const int BriefTimeout = 60;
    private const int StandardTimeout = 180;
    private const int LongTimeout = 420;
    private const int RememberedFreshnessTicks = 20;
    private const int ObservedWindowTicks = 120;
    private const int GrabWalkingSteps = 6;

    public static ArcRelayTacticsMaterialization Materialize(
        ArcRelayTacticsDocument document)
    {
        var emission = new Emission(document);
        emission.BuildBaseVocabulary();
        ProgramSpec program = emission.BuildProgram();
        var roles = new List<RoleEmission>(8);
        foreach (ArcRelayTacticsSquadBot bot in document.Squad)
            roles.Add(emission.BuildRole(bot));
        if (emission.Zones.Count > 128 || emission.Routes.Count > 128
            || emission.Anchors.Count > 128)
        {
            throw new InvalidDataException(
                $"tactics: this sheet plots {emission.Zones.Count} distinct "
                + $"areas, {emission.Routes.Count} routes, and "
                + $"{emission.Anchors.Count} exact points; the runtime map "
                + "plan carries at most 128 of each. Reuse the same ground "
                + "across tactics or simplify posts, lines, and marks.");
        }
        byte[] orders = WriteOrders(document, emission, roles, program);
        byte[] mapPlan = WriteMapPlan(document, emission);
        return new ArcRelayTacticsMaterialization(orders, mapPlan);
    }

    private static byte[] WriteOrders(
        ArcRelayTacticsDocument document,
        Emission emission,
        IReadOnlyList<RoleEmission> roles,
        ProgramSpec program)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString(
                "schema", ArcRelayTacticalPlaybookCompiler.ProgramSchema);
            writer.WriteString("sheetId", document.SheetId);
            writer.WriteString("mapPlan", "map-plan.json");
            writer.WriteStartArray("roles");
            foreach (RoleEmission role in roles)
            {
                writer.WriteStartObject();
                writer.WriteString("id", role.Id);
                writer.WriteStartArray("classes");
                writer.WriteStringValue(role.ClassId);
                writer.WriteEndArray();
                writer.WriteStartArray("upgradePath");
                foreach (string track in role.UpgradePath)
                    writer.WriteStringValue(track);
                writer.WriteEndArray();
                writer.WriteStartArray("orders");
                foreach (Node node in role.Orders)
                    node.Write(writer);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartObject("sharedOrders");
            foreach ((string id, Node node) in emission.SharedOrders)
            {
                writer.WritePropertyName(id);
                node.Write(writer, asSharedDefinition: true);
            }
            writer.WriteEndObject();
            writer.WriteStartObject("capacities");
            writer.WriteNumber("pair-per-core", 2);
            writer.WriteEndObject();
            writer.WriteStartObject("conditions");
            foreach (ConditionDef condition in emission.Conditions)
                condition.Write(writer);
            writer.WriteEndObject();
            program.Write(writer);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static byte[] WriteMapPlan(
        ArcRelayTacticsDocument document,
        Emission emission)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", ArcRelayTacticalPlaybookCompiler.LayoutSchema);
            writer.WriteString("mapPlanId", emission.MapPlanId);
            writer.WriteString("mapId", document.MapId);
            writer.WriteStartObject("zones");
            foreach ((string id, int[] rect) in emission.Zones)
            {
                writer.WriteStartObject(id);
                writer.WriteStartArray("rect");
                foreach (int coordinate in rect)
                    writer.WriteNumberValue(coordinate);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteStartObject("routes");
            foreach ((string id, IReadOnlyList<int[]> waypoints) in emission.Routes)
            {
                writer.WriteStartObject(id);
                writer.WriteStartArray("waypoints");
                foreach (int[] waypoint in waypoints)
                {
                    writer.WriteStartArray();
                    writer.WriteNumberValue(waypoint[0]);
                    writer.WriteNumberValue(waypoint[1]);
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteStartObject("anchors");
            foreach ((string id, int[] point) in emission.Anchors)
            {
                writer.WriteStartArray(id);
                writer.WriteNumberValue(point[0]);
                writer.WriteNumberValue(point[1]);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    /// <summary>Single-tactic view of one squad slot's generated tree.</summary>
    private sealed record RoleEmission(
        string Id,
        string ClassId,
        IReadOnlyList<string> UpgradePath,
        IReadOnlyList<Node> Orders);

    /// <summary>The canonical server/compiler carry-forward resolver.
    /// Ground and way-in come from the nearest earlier assignment; behavior
    /// and signature marks come from the current phase.</summary>
    internal static ArcRelayTacticJob? EffectiveJob(
        IReadOnlyList<ArcRelayTacticPhaseDef> phases,
        int phaseIndex,
        string groupId)
    {
        ArcRelayTacticJob raw = phases[phaseIndex].Jobs[groupId];
        if (raw.Kind != "none")
            return raw;
        for (int earlier = phaseIndex - 1; earlier >= 0; earlier--)
        {
            ArcRelayTacticJob candidate = phases[earlier].Jobs[groupId];
            if (candidate.Kind != "none")
            {
                return candidate with
                {
                    Posture = raw.Posture,
                    Travel = raw.Travel,
                    Recovery = raw.Recovery,
                    Sigs = raw.Sigs,
                };
            }
        }
        return null;
    }

    private sealed class Emission
    {
        private readonly ArcRelayTacticsDocument _document;
        private readonly Dictionary<string, string> _conditionsByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _zonesByShape = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _routesByShape = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _anchorsByShape = new(StringComparer.Ordinal);
        private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _sharedLeafIds = new(StringComparer.Ordinal);
        private readonly string[] _roleIds = new string[8];

        public Emission(ArcRelayTacticsDocument document)
        {
            _document = document;
            MapPlanId = EnsureUnique(Kebab($"{document.SheetId}-plan"), _usedIds);
            var roleIdSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (ArcRelayTacticsSquadBot bot in document.Squad)
            {
                string baseId = Kebab(bot.Name);
                if (baseId.Length == 0)
                    baseId = "bot";
                if (baseId.Length > 20)
                    baseId = baseId[..20].TrimEnd('-');
                string candidate = baseId;
                if (!roleIdSet.Add(candidate))
                {
                    candidate = $"{baseId}-{bot.Slot}";
                    roleIdSet.Add(candidate);
                }
                _roleIds[bot.Slot] = candidate;
            }
        }

        public string MapPlanId { get; }
        public List<ConditionDef> Conditions { get; } = [];
        public List<(string Id, int[] Rect)> Zones { get; } = [];
        public List<(string Id, IReadOnlyList<int[]> Waypoints)> Routes { get; } = [];
        public List<(string Id, int[] Point)> Anchors { get; } = [];
        public List<(string Id, Node Node)> SharedOrders { get; } = [];

        public string RoleId(int slot) => _roleIds[slot];

        public void BuildBaseVocabulary()
        {
            SharedOrders.Add(("score-a-core", new Node
            {
                When = Expr.One(CarryingCore()),
                Until = Expr.One(EmptyHanded()),
                Timeout = StandardTimeout,
                Cooldown = 2,
                Urgency = "urgent",
                Children =
                [
                    new Node
                    {
                        Id = "return-to-reactor",
                        Action = MoveToDynamic("own-reactor"),
                    },
                ],
            }));
            SharedOrders.Add(("collect-loose-core", new Node
            {
                When = Expr.All(EmptyHanded(), Condition(
                    new ConditionShape("known-loose-cores", "at-least", 1),
                    "loose-core-known")),
                CapacityName = "pair-per-core",
                Action = MoveToDynamic("nearest-known-loose-core"),
            }));
            SharedOrders.Add(("recover-health", new Node
            {
                When = Expr.One(Hurt()),
                Until = Expr.One(Healthy()),
                Timeout = 16,
                Cooldown = 18,
                Urgency = "urgent",
                Children =
                [
                    new Node
                    {
                        Id = "reach-healing",
                        Action = MoveToDynamic("nearest-heal-zone", posture: "rear"),
                    },
                ],
            }));
            SharedOrders.Add(("fight-visible-enemy", new Node
            {
                When = Expr.All(EmptyHanded(), EnemyNearby()),
                Action = Fight(["weakest", "carrier", "nearest"], "advance", 4),
            }));
            // Holding ground in place: a shared well target would knot eight
            // bodies onto one tile and orbit forever at the destination.
            SharedOrders.Add(("hold-ground", new Node
            {
                Action = MoveToDynamic("current-position", posture: "line"),
            }));
        }

        /// <summary>
        /// The squad program table: tactic order, start/abort cases, duration
        /// and rearm, and each phase's forward-only continue rules. Compiled
        /// first so its interned conditions and zones exist before roles.
        /// </summary>
        public ProgramSpec BuildProgram()
        {
            var tactics = new List<ProgramTacticSpec>();
            for (int index = 0; index < _document.Tactics.Count; index++)
            {
                ArcRelayTactic tactic = _document.Tactics[index];
                string prefix = $"t{index + 1}";
                List<CaseSpec>? start = tactic.Start?
                    .Select(value => BuildCase(prefix, "start", value, phase: null, tactic))
                    .ToList();
                List<CaseSpec>? abort = tactic.Abort?
                    .Select(value => BuildCase(prefix, "abort", value, phase: null, tactic))
                    .ToList();
                var phases = new List<ProgramPhaseSpec>();
                for (int phaseIndex = 0; phaseIndex < tactic.Phases.Count; phaseIndex++)
                {
                    ArcRelayTacticPhaseDef phase = tactic.Phases[phaseIndex];
                    var rules = new List<(CaseSpec When, string To)>();
                    foreach (ArcRelayTacticTransition transition in phase.Continue)
                    {
                        foreach (ArcRelayTacticZoneRef cleared
                                 in PositiveClearanceZones(transition.When))
                        {
                            if (!tactic.Groups.Any(group =>
                                    IsLocalClearanceGroup(
                                        tactic, phase, group.Id,
                                        cleared.Rect)))
                            {
                                throw new InvalidDataException(
                                    $"Tactic '{tactic.Name}' phase "
                                    + $"'{phase.Name}' asks to clear "
                                    + $"'{cleared.Name}', but no phase group "
                                    + "has ground overlapping or touching that area.");
                            }
                        }
                        string to = transition.To is "next" or "finish"
                            ? transition.To
                            : $"p{tactic.Phases.ToList().FindIndex(candidate =>
                                candidate.Id == transition.To) + 1}";
                        rules.Add((BuildCase(
                            prefix, $"p{phaseIndex + 1}", transition.When,
                            phase, tactic), to));
                    }
                    phases.Add(new ProgramPhaseSpec(
                        $"p{phaseIndex + 1}", phase.Name, rules,
                        (phase.Custody?.Plan ?? tactic.Delivery.Plan) == "escort"
                            ? tactic.Groups
                                .Select(group => group.Slots.ToArray())
                                .ToArray()
                            : []));
                }
                tactics.Add(new ProgramTacticSpec(
                    prefix,
                    tactic.Name,
                    start,
                    abort,
                    tactic.MaxDuration switch
                    {
                        "brief" => BriefTimeout,
                        "standard" => StandardTimeout,
                        "long" => LongTimeout,
                        _ => null,
                    },
                    RearmTicks,
                    phases));
            }
            return new ProgramSpec(tactics);
        }

        private CaseSpec BuildCase(
            string prefix,
            string context,
            ArcRelayTacticCase value,
            ArcRelayTacticPhaseDef? phase,
            ArcRelayTactic tactic)
        {
            var result = new CaseSpec();
            if (value.Required is not null)
            {
                AddRowEntries(
                    result.All, prefix, context, value.Required, phase, tactic);
            }
            if (value.Triggers is not null)
            {
                AddRowEntries(
                    result.Any, prefix, context, value.Triggers, phase, tactic);
            }
            if (value.Unless is not null)
            {
                // NONE of the unless rows may hold: each row's one threshold
                // condition inverts by operator flip and joins the all side.
                AddRowEntries(
                    result.All, prefix, $"{context}-un", value.Unless, phase,
                    tactic, invert: true);
            }
            return result;
        }

        /// <summary>Every ordinary row is one threshold condition, so its
        /// negation is an exact operator flip — no runtime grammar change.
        /// Only the multi-entry readiness rows (groupsSet, healed) cannot
        /// invert, and validation keeps them out of unless.</summary>
        private static ConditionShape Invert(ConditionShape shape) =>
            shape.Operator switch
            {
                "at-least" => shape with
                { Operator = "at-most", Value = shape.Value - 1 },
                "at-most" => shape with
                { Operator = "at-least", Value = shape.Value + 1 },
                "equals" when shape.Value == 0 => shape with
                { Operator = "at-least", Value = 1 },
                "equals" => shape with
                { Operator = "at-most", Value = shape.Value - 1 },
                _ => throw new InvalidDataException(
                    $"tactics: cannot invert operator '{shape.Operator}'."),
            };

        /// <summary>One entry per active row; groupsSet expands to the exact
        /// per-bot dead-excused pairs of the phase's effective geometry.
        /// With invert, each row contributes its exact negation.</summary>
        private void AddRowEntries(
            List<object> entries,
            string prefix,
            string context,
            ArcRelayTacticConditionRows rows,
            ArcRelayTacticPhaseDef? phase,
            ArcRelayTactic tactic,
            bool invert = false)
        {
            void Add(ConditionShape shape, string name) => entries.Add(
                Condition(
                    invert ? Invert(shape) : shape,
                    invert ? $"no-{name}" : name));
            if (rows.GroupsSet is ArcRelayTacticReadiness readiness
                && phase is not null && !invert)
            {
                AddReadinessEntry(
                    entries, prefix, context, phase, tactic, readiness);
            }
            if (rows.HeldTicks is int held)
            {
                Add(new ConditionShape("elapsed-ticks", "at-least", held),
                    $"c-held-{held}");
            }
            if (rows.Healed is int healed)
            {
                // Exact and per bot, like readiness: every LIVING bot is at
                // the threshold — a dead role reads 100 and excuses itself.
                // Inverted (unless): NOT(all bots ≥ N) = any bot below N,
                // one nested any of the per-role flips.
                var perRole = new List<string>();
                foreach (ArcRelayTacticsSquadBot bot in _document.Squad)
                {
                    var shape = new ConditionShape(
                        "role-lowest-health-percent", "at-least", healed,
                        Role: RoleId(bot.Slot));
                    perRole.Add(Condition(
                        invert ? Invert(shape) : shape,
                        $"{(invert ? "no-" : "")}hp-{RoleId(bot.Slot)}-{healed}"));
                }
                if (invert)
                    entries.Add(new Expr("any", perRole));
                else
                    entries.AddRange(perRole);
            }
            if (rows.Veterancy is ArcRelayTacticVeterancyRow veteran)
            {
                Add(new ConditionShape(
                        "friendly-veterans-in-roles",
                        "at-least",
                        veteran.Count,
                        Roles: veteran.Slots.Select(RoleId).ToArray(),
                        Level: veteran.Level),
                    $"c-{prefix}-{context}-veterans-{veteran.Count}-at-"
                        + veteran.Level);
            }
            if (rows.EnemiesAt is ArcRelayTacticEnemiesAtRow enemies)
            {
                string[] zones = enemies.Zones.Select((zone, index) => Zone(
                        zone.Rect,
                        $"g-{prefix}-{context}-watch-{index + 1}"))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                Add(new ConditionShape(
                        "remembered-enemies-in-zone", "at-least", enemies.Count,
                        ZoneIds: zones,
                        FreshnessTicks: RememberedFreshnessTicks),
                    $"c-{prefix}-{context}-enemies");
            }
            if (rows.Contact)
            {
                Add(new ConditionShape(
                        "visible-enemies-in-zone", "at-least", 1,
                        ZoneId: AnywhereZone()),
                    "enemy-contact");
            }
            if (rows.CoreLooseWell is string well)
            {
                Add(new ConditionShape(
                        "well-has-outstanding-core", "equals", 1, Well: well),
                    $"core-up-at-{well}");
            }
            if (rows.CoreSoon is ArcRelayTacticCoreSoonRow coreSoon)
            {
                // The Well's one effective public clock (#328): initial
                // opening before the first Core, then the bank-triggered
                // recharge — so "births soon" reads the same field always.
                Add(new ConditionShape(
                        "well-ticks-until-birth", "at-most", coreSoon.Ticks,
                        Well: coreSoon.Well),
                    $"c-{prefix}-{context}-core-soon");
            }
            if (rows.EnemiesDown is int enemiesDown)
            {
                Add(new ConditionShape(
                        "live-enemies", "at-most", 8 - enemiesDown),
                    $"c-{prefix}-{context}-enemies-down");
            }
            if (rows.Carrier)
            {
                Add(new ConditionShape("friendly-carriers", "at-least", 1),
                    "we-hold-a-core");
            }
            if (rows.CoreAcquired)
            {
                // The EVENT, not the snapshot: "a Core was taken since THIS
                // phase began". Compiling this to the carrier fact let a
                // Core inherited from the previous phase finish a fresh
                // claim phase on its second tick. An authored well narrows
                // it to that source's Core — the claim a Claim phase means.
                Add(new ConditionShape(
                        "friendly-core-acquisitions", "at-least", 1,
                        Since: "phase-entry", Well: rows.CoreAcquiredWell),
                    rows.CoreAcquiredWell is null
                        ? $"c-{prefix}-acquired"
                        : $"c-{prefix}-acquired-{rows.CoreAcquiredWell}");
            }
            if (rows.Losses is int losses)
            {
                Add(new ConditionShape("live-friendlies", "at-most", 8 - losses),
                    $"down-{losses}-bots");
            }
            if (rows.LossesWithin is ArcRelayTacticLossesWithinRow window)
            {
                Add(new ConditionShape(
                        "friendly-destructions", "at-least", window.Count,
                        WindowTicks: window.Ticks),
                    $"c-lost-{window.Count}-in-{window.Ticks}");
            }
            if (rows.KillsWithin is ArcRelayTacticLossesWithinRow rout)
            {
                // The rout row: N destroyed within a rolling window — the
                // durable form of "enemies are down", which respawn waves
                // stand back up before a paired event row can meet it.
                Add(new ConditionShape(
                        "confirmed-enemy-destructions-in-zone", "at-least",
                        rout.Count, ZoneId: AnywhereZone(),
                        WindowTicks: rout.Ticks),
                    $"c-killed-{rout.Count}-in-{rout.Ticks}");
            }
            if (rows.Kills is int kills)
            {
                Add(new ConditionShape(
                        "confirmed-enemy-destructions-in-zone", "at-least",
                        kills, ZoneId: AnywhereZone(), Since: "phase-entry"),
                    $"c-{prefix}-kills");
            }
            if (rows.Cleared is { Count: > 0 } cleared)
            {
                string[] zones = cleared.Select((zone, index) => Zone(
                        zone.Rect, $"g-{prefix}-cleared-{index + 1}"))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                Add(new ConditionShape(
                        "zone-cleared", "equals", 1, ZoneIds: zones),
                    $"c-{prefix}-cleared");
            }
            if (rows.NotFighting is { Count: > 0 } notFighting)
            {
                string[] zones = notFighting.Select((zone, index) => Zone(
                        zone.Rect,
                        $"g-{prefix}-not-fighting-{index + 1}"))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                Add(new ConditionShape(
                        "friendly-fights-in-zone", "equals", 0,
                        ZoneIds: zones),
                    $"c-{prefix}-not-fighting");
            }
            if (rows.CoreBanked)
            {
                Add(new ConditionShape(
                        "friendly-core-banks", "at-least", 1,
                        Since: "phase-entry"),
                    $"c-{prefix}-banked");
            }
            if (rows.CoreLost)
            {
                Add(new ConditionShape(
                        "friendly-core-losses", "at-least", 1,
                        Since: "phase-entry"),
                    $"c-{prefix}-core-lost");
            }
            if (rows.Reached && phase is not null)
            {
                (ArcRelayTacticGroup Group, ArcRelayTacticJob Job) mover =
                    tactic.Groups
                        .Select(group => (Group: group,
                            Job: phase.Jobs[group.Id]))
                        .First(pair => pair.Job.Kind is "converge" or "advance");
                int[] destination = mover.Job.Kind == "converge"
                    ? mover.Job.Point!
                    : mover.Job.Path![^1];
                int[] rect = PadBounds(
                    [destination[0], destination[1], destination[0], destination[1]],
                    1);
                string zone = Zone(rect, $"g-{prefix}-goal");
                Add(new ConditionShape(
                        "friendlies-in-zone", "at-least",
                        Math.Max(1, (mover.Group.Slots.Count + 1) / 2),
                        ZoneId: zone),
                    $"c-{prefix}-reached");
            }
            if (rows.Observed)
            {
                List<ArcRelayTacticJob> watchers = phase is null
                    ? []
                    : tactic.Groups
                        .Select(group => phase.Jobs[group.Id])
                        .Where(job => job.Posture == "observe" && job.HasGeometry)
                        .ToList();
                if (watchers.Count == 0)
                {
                    watchers = tactic.Phases
                        .SelectMany(candidate => candidate.Jobs.Values)
                        .Where(job => job.HasGeometry)
                        .ToList();
                }
                int[] rect = BoundingBox(watchers.SelectMany(GeometryPoints));
                string zone = Zone(rect, $"g-{prefix}-watchline");
                Add(new ConditionShape(
                        "zone-fully-observed-throughout-interval", "equals", 1,
                        ZoneId: zone, WindowTicks: ObservedWindowTicks),
                    $"c-{prefix}-observed");
            }
        }

        /// <summary>
        /// Exact readiness, per bot: each member of every group with ground
        /// this phase is inside its footprint OR dead. There is no quorum and
        /// no short-handed approximation — the coordinator evaluates this
        /// once at squad level, where per-body gate budgets no longer bind.
        /// </summary>
        private void AddReadinessEntry(
            List<object> entries,
            string prefix,
            string context,
            ArcRelayTacticPhaseDef phase,
            ArcRelayTactic tactic,
            ArcRelayTacticReadiness readiness)
        {
            var roles = new List<string>();
            var zones = new List<string>();
            for (int groupIndex = 0; groupIndex < tactic.Groups.Count; groupIndex++)
            {
                ArcRelayTacticGroup group = tactic.Groups[groupIndex];
                ArcRelayTacticJob? job = EffectiveJob(tactic, phase, group.Id);
                if (job is null)
                    continue;
                string groupTag = $"g{groupIndex + 1}";
                int[]? footprint = job.Kind switch
                {
                    "posts" => PadBounds(BoundingBox(
                        job.Posts!.Select(post => post.At)), 1),
                    "area" => job.Rect,
                    "line" or "patrol" => PadBounds(BoundingBox(job.Path!), 1),
                    "advance" => PadBounds(
                        [
                            job.Path![^1][0], job.Path[^1][1],
                            job.Path[^1][0], job.Path[^1][1],
                        ], 1),
                    "approach" => PadBounds(
                        [
                            job.Point![0], job.Point[1],
                            job.Point[0], job.Point[1],
                        ], 2),
                    "converge" => PadBounds(
                        [
                            job.Point![0], job.Point[1],
                            job.Point[0], job.Point[1],
                        ], 1),
                    _ => null,
                };
                if (footprint is null)
                    continue;
                for (int member = 0; member < group.Slots.Count; member++)
                {
                    // Posts are always slot-exact, including a full eight-body
                    // group. A large formation is not a bounding-box quorum.
                    int[] rect = job.Kind == "posts"
                        ? PadBounds(
                            [
                                job.Posts![member].At[0], job.Posts[member].At[1],
                                job.Posts[member].At[0], job.Posts[member].At[1],
                            ], 1)
                        : footprint;
                    string zone = job.Kind == "posts"
                        ? Zone(rect, $"g-{prefix}-{groupTag}-near-post-{member}")
                        : Zone(rect, $"g-{prefix}-{groupTag}-set");
                    roles.Add(RoleId(group.Slots[member]));
                    zones.Add(zone);
                }
            }
            if (roles.Count == 0)
                return;
            entries.Add(Condition(
                new ConditionShape(
                    "formation-late-count", "at-most", readiness.AllowedLate,
                    Roles: roles.ToArray(), ZoneIds: zones.ToArray()),
                $"c-{prefix}-{context}-formation-ready"));
        }

        public RoleEmission BuildRole(ArcRelayTacticsSquadBot bot)
        {
            var orders = new List<Node>();
            for (int index = 0; index < _document.Tactics.Count; index++)
            {
                BuildTacticNodes(
                    index, _document.Tactics[index], bot, orders);
            }
            orders.Add(new Node { SharedRef = "score-a-core" });
            orders.Add(new Node { SharedRef = "collect-loose-core" });
            orders.Add(new Node { SharedRef = "recover-health" });
            orders.Add(new Node { SharedRef = "fight-visible-enemy" });
            orders.Add(new Node { SharedRef = "hold-ground" });
            return new RoleEmission(
                RoleId(bot.Slot), bot.ClassId, UpgradePathFor(bot.Build), orders);
        }

        /// <summary>
        /// One tactic's subtree for one bot: a tactic wrapper (tagged with
        /// the tactic id, so everything inside goes dark between runs)
        /// then one wrapper per phase (tagged tactic/phase) with that phase's
        /// custody overlay, recovery, skills, fight, scan, and floor. Leaves are
        /// untagged; the wrappers are the whole gate. There is no release
        /// branch, per-body latch, or rejoin anywhere.
        /// </summary>
        private void BuildTacticNodes(
            int index,
            ArcRelayTactic tactic,
            ArcRelayTacticsSquadBot bot,
            List<Node> orders)
        {
            // Generated ids are ordinal (t3, p2, g1): they stay short enough
            // for the ATL2 envelope at eight tactics and the tactics document
            // resolves them back to authored names for display/attribution.
            string prefix = $"t{index + 1}";
            int groupIndex = 0;
            while (!tactic.Groups[groupIndex].Slots.Contains(bot.Slot))
                groupIndex++;
            ArcRelayTacticGroup group = tactic.Groups[groupIndex];
            string groupTag = $"g{groupIndex + 1}";

            var children = new List<Node>();
            for (int phaseIndex = 0; phaseIndex < tactic.Phases.Count; phaseIndex++)
            {
                ArcRelayTacticPhaseDef phase = tactic.Phases[phaseIndex];
                ArcRelayTacticJob raw = phase.Jobs[group.Id];
                ArcRelayTacticJob? effective = EffectiveJob(
                    tactic, phase, group.Id);
                var scope = new LeafScope(
                    prefix, groupTag, group.Slots.Count, $"p{phaseIndex + 1}");
                var leaves = new List<Node>();
                // Minted lazily: only a heal recovery or a corridor bank
                // consumes the ground anchor, and an unreferenced zone
                // fails the consumer check.
                Func<string?> groundZone = () =>
                {
                    int[]? footprint = effective is null
                        ? null
                        : GroundFootprint(effective);
                    return footprint is null
                        ? null
                        : Zone(footprint, $"g-{prefix}-{groupTag}-set");
                };
                string custodyPlan = phase.Custody?.Plan ?? tactic.Delivery.Plan;
                AddSignatureCommitment(leaves, scope, bot);
                AddCustodyTransfer(
                    leaves, scope, custodyPlan, phase, bot);
                AddCoreRule(
                    leaves, scope, group.CoreRule, group,
                    excludeAttachedEscort: custodyPlan == "escort");
                AddRecovery(
                    leaves, scope, raw.Recovery, groundZone,
                    custodyPlan == "escort" ? group : null);
                // Travel is an independently authored policy even when it
                // names the same posture as the destination. A standing job's
                // destination fight is gated on arrival, so collapsing
                // aggressive/aggressive (or hold/hold) would leave the march
                // with no fight leaf at all. A kind with no arrival
                // (patrol/return, or a carry-forward that resolves to one)
                // still drops travel rather than promising a switch that
                // could never happen; the editors refuse to author it.
                string? travel = raw.Travel;
                bool standing =
                    effective?.Kind is "posts" or "area" or "line" or "approach";
                bool mover = effective?.Kind is "converge" or "advance";
                if (!standing && !mover)
                    travel = null;
                // Standing kinds always gate their fight on the station; a
                // mover's fight is gated on its destination only when a
                // travel posture makes the march and the ground different.
                AddSignatureLeaves(
                    leaves, scope, group, bot, raw.Posture, raw.Sigs,
                    effective);
                AddCustodyFight(
                    leaves, scope, custodyPlan, group, raw.Posture);
                AddPostureFight(leaves, scope, raw.Posture,
                    standing || (mover && travel is not null)
                        ? () => OnStationCondition(
                            prefix, groupTag, effective!, group, bot)
                        : null);
                if (travel is not null)
                {
                    AddTravelFight(leaves, scope, travel,
                        () => OnStationCondition(
                            prefix, groupTag, effective!, group, bot,
                            arrived: false),
                        () => OnStationCondition(
                            prefix, groupTag, effective!, group, bot));
                }
                AddCustodyMovement(
                    leaves, scope, tactic, phase, group, bot,
                    prefix, phaseIndex, groundZone);
                AddScanLeaves(
                    leaves, scope, tactic, phase, group, effective);
                if (effective is not null)
                    AddFloor(leaves, scope, effective, group, bot);
                if (leaves.Count > 0)
                {
                    children.Add(new Node
                    {
                        Id = $"{prefix}-p{phaseIndex + 1}",
                        Phase = $"{prefix}/p{phaseIndex + 1}",
                        Children = leaves,
                    });
                }
            }
            if (children.Count > 0)
            {
                orders.Add(new Node
                {
                    Id = prefix,
                    Phase = prefix,
                    Children = children,
                });
            }
        }

        /// <summary>The job whose ground this group holds in this phase:
        /// its own when it has geometry or is a dynamic mover, else the
        /// nearest earlier geometric job carried forward under the current
        /// posture. Null when there is nothing to stand on or do.</summary>
        private static ArcRelayTacticJob? EffectiveJob(
            ArcRelayTactic tactic,
            ArcRelayTacticPhaseDef phase,
            string groupId)
        {
            int index = tactic.Phases.ToList().IndexOf(phase);
            return ArcRelayTacticsMaterializer.EffectiveJob(
                tactic.Phases, index, groupId);
        }

        /// <summary>The phase ground's footprint rect, as readiness sees
        /// it — the anchor for "nearest to the PHASE, not to the bot".</summary>
        private int[]? GroundFootprint(ArcRelayTacticJob job) =>
            job.Kind switch
            {
                "posts" => PadBounds(BoundingBox(
                    job.Posts!.Select(post => post.At)), 1),
                "area" => job.Rect,
                "line" or "patrol" => PadBounds(BoundingBox(job.Path!), 1),
                "advance" => PadBounds(
                    [
                        job.Path![^1][0], job.Path[^1][1],
                        job.Path[^1][0], job.Path[^1][1],
                    ], 1),
                "approach" or "converge" => PadBounds(
                    [
                        job.Point![0], job.Point[1],
                        job.Point[0], job.Point[1],
                    ], 2),
                _ => null,
            };

        private void AddScanLeaves(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTactic tactic,
            ArcRelayTacticPhaseDef phase,
            ArcRelayTacticGroup group,
            ArcRelayTacticJob? effective)
        {
            if (effective is null)
                return;
            int[]? ground = GroundFootprint(effective);
            if (ground is null)
                return;
            ArcRelayTacticZoneRef[] zones = phase.Continue
                .SelectMany(transition =>
                    PositiveClearanceZones(transition.When))
                .GroupBy(value => string.Join(',', value.Rect),
                    StringComparer.Ordinal)
                .Select(values => values.First())
                .ToArray();
            int index = 0;
            foreach (ArcRelayTacticZoneRef cleared in zones)
            {
                if (!RectsTouch(ground, cleared.Rect))
                    continue;
                string zone = Zone(
                    cleared.Rect,
                    $"g-{scope.Prefix}-{scope.PhaseTag}-scan-{index + 1}");
                string needsScan = Condition(
                    new ConditionShape(
                        "zone-cleared", "equals", 0, ZoneId: zone),
                    $"c-{scope.Prefix}-{scope.PhaseTag}-scan-{index + 1}");
                int ordinal = ++index;
                leaves.Add(new Node
                {
                    Id = scope.Id($"scan-{ordinal}"),
                    When = Expr.One(needsScan),
                    Action = writer =>
                    {
                        writer.WriteStartObject("scan");
                        writer.WriteString("zone", zone);
                        writer.WriteNumber("freshWithinTicks", 20);
                        writer.WriteEndObject();
                    },
                });
            }
        }

        private bool IsLocalClearanceGroup(
            ArcRelayTactic tactic,
            ArcRelayTacticPhaseDef phase,
            string groupId,
            int[] zone)
        {
            ArcRelayTacticJob? effective = EffectiveJob(
                tactic, phase, groupId);
            int[]? ground = effective is null
                ? null : GroundFootprint(effective);
            return ground is not null && RectsTouch(ground, zone);
        }

        private static IEnumerable<ArcRelayTacticZoneRef>
            PositiveClearanceZones(ArcRelayTacticCase value)
        {
            if (value.Required?.Cleared is { } required)
            {
                foreach (ArcRelayTacticZoneRef zone in required)
                    yield return zone;
            }
            if (value.Triggers?.Cleared is { } trigger)
            {
                foreach (ArcRelayTacticZoneRef zone in trigger)
                    yield return zone;
            }
        }

        private static bool RectsTouch(int[] first, int[] second) =>
            first[0] <= second[2] + 1
            && first[2] + 1 >= second[0]
            && first[1] <= second[3] + 1
            && first[3] + 1 >= second[1];

        private void AddRecovery(
            List<Node> leaves,
            LeafScope scope,
            string recovery,
            Func<string?> groundZone,
            ArcRelayTacticGroup? escortGroup)
        {
            Expr RecoveryWhen(params string[] conditions)
            {
                var all = new List<string> { EmptyHanded() };
                all.AddRange(conditions);
                if (escortGroup is not null)
                    all.Add(NoFriendlyCarrier(escortGroup, scope));
                return Expr.All(all.ToArray());
            }
            if (recovery == "heal-tile")
            {
                leaves.Add(GroupLeaf(scope, "heal", () => new Node
                {
                    When = RecoveryWhen(Hurt()),
                    Until = Expr.One(Healthy()),
                    Timeout = 16,
                    Cooldown = 18,
                    Urgency = "urgent",
                    Action = MoveToDynamic(
                        "nearest-heal-zone", posture: "rear",
                        near: groundZone()),
                }));
            }
            else if (recovery == "heal-up")
            {
                // The regroup preset: any damage at all sends the bot to
                // heal and it stays until completely full, so EVERY healed
                // gate 1..100 is eventually satisfiable — a narrower band
                // (90/98) let a bot at 92% block a 95% gate forever.
                leaves.Add(GroupLeaf(scope, "heal-up", () => new Node
                {
                    When = RecoveryWhen(Condition(
                        new ConditionShape(
                            "self-health-percent", "at-most", 99),
                        "below-topped-up")),
                    Until = Expr.One(Condition(
                        new ConditionShape(
                            "self-health-percent", "at-least", 100),
                        "topped-up")),
                    Timeout = 30,
                    Cooldown = 10,
                    Urgency = "urgent",
                    Action = MoveToDynamic(
                        "nearest-heal-zone", posture: "rear",
                        near: groundZone()),
                }));
            }
            else if (recovery == "patch")
            {
                leaves.Add(GroupLeaf(scope, "patch", () => new Node
                {
                    When = RecoveryWhen(Hurt()),
                    Until = Expr.One(Condition(
                        new ConditionShape("self-health-percent", "at-least", 60),
                        "patched-up")),
                    Timeout = 24,
                    Cooldown = 24,
                    Action = MoveToDynamic("current-position", posture: "rear"),
                }));
            }
        }

        /// <summary>One phase's Core custody: the phase's own plan, or the
        /// tactic's delivery plan where none is authored. "keep" is the
        /// deliberate hold — the carrier stays on the job and a later phase
        /// banks. The drawn via guides the carrier's run home.</summary>
        private void AddCustodyMovement(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTactic tactic,
            ArcRelayTacticPhaseDef phase,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot,
            string prefix,
            int phaseIndex,
            Func<string?> groundZone)
        {
            string plan = phase.Custody?.Plan ?? tactic.Delivery.Plan;
            if (plan == "keep")
                return;
            if (plan == "none")
            {
                // The accidental-carrier escape, exactly as the tactic-scoped
                // form: only a final phase that holds forever needs it.
                if (phaseIndex == tactic.Phases.Count - 1
                    && tactic.Phases[^1].Continue.Count == 0)
                {
                    leaves.Add(GroupLeaf(scope, "bank", () => new Node
                    {
                        When = Expr.One(CarryingCore()),
                        Urgency = "urgent",
                        Action = MoveToDynamic("own-reactor"),
                    }));
                }
                return;
            }
            string? via = phase.Custody?.Via is null
                ? null
                : Route(phase.Custody.Via,
                    $"g-{prefix}-p{phaseIndex + 1}-carry");
            // The drawn way home belongs to THIS phase's ground: a carrier
            // standing there banks by the corridor, and one who grabbed a
            // Core elsewhere (a centre pickup mid-fight, or a phase swap
            // mid-run) banks direct instead of marching to the corridor's
            // far mouth first. Compiled as a gated pair so the choice is
            // visible in the orders, not a planner heuristic.
            string? onGround = via is null ? null : groundZone() is string zone
                ? Condition(
                    new ConditionShape("self-in-zone", "equals", 1,
                        ZoneId: zone),
                    $"c-{prefix}-p{phaseIndex + 1}-home-from-ground")
                : null;
            AddDeliveryLeaves(
                leaves, scope, plan, via, group, onGround);
        }

        private void AddSignatureCommitment(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTacticsSquadBot bot)
        {
            ArcRelaySignatureAuthoringDescriptor descriptor =
                ArcRelaySignatureAuthoringCatalog.ForClass(bot.ClassId);
            if (descriptor.DefaultCommitment !=
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault)
                return;
            leaves.Add(new Node
            {
                Id = scope.Id("signature-commitment"),
                When = Expr.One(Condition(
                    new ConditionShape(
                        "signature-commitment-active", "equals", 1),
                    $"c-{scope.Prefix}-{scope.GroupTag}-signature-commitment")),
                Action = MoveToDynamic(
                    "current-position", posture: "line"),
            });
        }

        private void AddCustodyTransfer(
            List<Node> leaves,
            LeafScope scope,
            string plan,
            ArcRelayTacticPhaseDef phase,
            ArcRelayTacticsSquadBot bot)
        {
            // A player-drawn custody route is authoritative. Smart solo Toss
            // currently follows shortest-path progress, so it yields rather
            // than cutting away from an explicit corridor.
            if (plan is "none" or "keep" || bot.ClassId != "relay"
                || phase.Custody?.Via is not null)
                return;
            leaves.Add(new Node
            {
                Id = scope.Id("reclaim-toss"),
                When = Expr.One(EmptyHanded()),
                Urgency = "urgent",
                Action = MoveToDynamic("own-tossed-core"),
            });
            leaves.Add(new Node
            {
                Id = scope.Id("pass"),
                When = Expr.All(CarryingCore(), SignatureReady()),
                Action = UseSkill(
                    "arc-toss",
                    writer => writer.WriteString("target", "own-reactor")),
            });
        }

        private void AddCustodyFight(
            List<Node> leaves,
            LeafScope scope,
            string plan,
            ArcRelayTacticGroup group,
            string posture)
        {
            if (posture is "hold" or "aggressive" or "steal" or "escort")
            {
                leaves.Add(GroupLeaf(scope, "carrier-defence", () => new Node
                {
                    When = Expr.All(CarryingCore(), EnemyNearby()),
                    Until = Expr.One(EnemyNoLongerNearby()),
                    Action = Fight(["nearest", "carrier"], "hold", 0),
                }));
            }
            if (plan != "escort")
                return;
            string carrierPresent = FriendlyCarrier(group, scope);
            leaves.Add(GroupLeaf(scope, "escort-defence", () => new Node
            {
                When = Expr.All(EmptyHanded(), carrierPresent, EnemyNearby()),
                Until = Expr.Any([
                    EnemyNoLongerNearby(), NoFriendlyCarrier(group, scope)]),
                Action = Fight(["nearest", "carrier"], "hold", 2),
            }));
        }

        private void AddDeliveryLeaves(
            List<Node> leaves,
            LeafScope scope,
            string plan,
            string? via,
            ArcRelayTacticGroup group,
            string? viaOnlyFrom = null)
        {
            if (via is not null && viaOnlyFrom is string fromGround)
            {
                leaves.Add(GroupLeaf(scope, "bank", () => new Node
                {
                    When = Expr.All(CarryingCore(), fromGround),
                    Urgency = "urgent",
                    Action = MoveToDynamic("own-reactor", via: via),
                }));
                leaves.Add(GroupLeaf(scope, "bank-direct", () => new Node
                {
                    When = Expr.One(CarryingCore()),
                    Urgency = "urgent",
                    Action = MoveToDynamic("own-reactor"),
                }));
            }
            else
            {
                leaves.Add(GroupLeaf(scope, "bank", () => new Node
                {
                    When = Expr.One(CarryingCore()),
                    Urgency = "urgent",
                    Action = MoveToDynamic("own-reactor", via: via),
                }));
            }
            if (plan == "escort")
            {
                leaves.Add(GroupLeaf(scope, "screen-carrier", () => new Node
                {
                    When = Expr.All(
                        Condition(
                            new ConditionShape(
                                "friendly-carriers-in-roles", "at-least", 1,
                                Roles: group.Slots.Select(RoleId).ToArray()),
                            $"c-{scope.Prefix}-{scope.GroupTag}-has-carrier"),
                        EmptyHanded()),
                    Action = FollowCarrier(),
                }));
            }
        }

        private string FriendlyCarrier(
            ArcRelayTacticGroup group,
            LeafScope scope) => Condition(
                new ConditionShape(
                    "friendly-carriers-in-roles", "at-least", 1,
                    Roles: group.Slots.Select(RoleId).ToArray()),
                $"c-{scope.Prefix}-{scope.GroupTag}-has-carrier");

        private string NoFriendlyCarrier(
            ArcRelayTacticGroup group,
            LeafScope scope) => Condition(
                new ConditionShape(
                    "friendly-carriers-in-roles", "at-most", 0,
                    Roles: group.Slots.Select(RoleId).ToArray()),
                $"c-{scope.Prefix}-{scope.GroupTag}-no-carrier");

        private void AddCoreRule(
            List<Node> leaves,
            LeafScope scope,
            string coreRule,
            ArcRelayTacticGroup group,
            bool excludeAttachedEscort)
        {
            string[] FreeToCollect(params string[] rest) =>
                excludeAttachedEscort
                    ? [EmptyHanded(), NoFriendlyCarrier(group, scope), .. rest]
                    : [EmptyHanded(), .. rest];
            if (coreRule == "grab")
            {
                leaves.Add(GroupLeaf(scope, "grab", () => new Node
                {
                    When = Expr.All(FreeToCollect(Condition(
                        new ConditionShape(
                            "self-nearest-visible-loose-core-distance",
                            "at-most", GrabWalkingSteps),
                        "core-in-reach"))),
                    CapacityName = "pair-per-core",
                    Action = writer =>
                    {
                        writer.WriteStartObject("move");
                        writer.WriteString("to", "nearest-visible-loose-core");
                        writer.WriteNumber("withinWalkingSteps", GrabWalkingSteps);
                        writer.WriteEndObject();
                    },
                }));
            }
            else if (coreRule == "hunt")
            {
                leaves.Add(GroupLeaf(scope, "hunt-carrier", () => new Node
                {
                    When = Expr.All(FreeToCollect(Condition(
                        new ConditionShape(
                            "visible-enemy-carriers", "at-least", 1),
                        "enemy-carrier-visible"))),
                    Action = Fight(["carrier", "nearest"], "advance", 8),
                }));
                leaves.Add(GroupLeaf(scope, "hunt-core", () => new Node
                {
                    When = Expr.All(FreeToCollect(Condition(
                        new ConditionShape("known-loose-cores", "at-least", 1),
                        "loose-core-known"))),
                    CapacityName = "pair-per-core",
                    Action = MoveToDynamic("nearest-known-loose-core"),
                }));
            }
        }

        private void AddPostureFight(
            List<Node> leaves,
            LeafScope scope,
            string posture,
            Func<string?>? stationCondition = null)
        {
            (string[] Targets, string Style, int Chase)? fight = posture switch
            {
                // Hold keeps ordinary enemies from dragging the body away,
                // but a defender is allowed a bounded excursion to stop a
                // visible carrier or close on a gun that can otherwise shoot
                // it unanswered. Carrier-first targeting and the five-tile
                // defence leash make that expectation explicit in Orders.
                "hold" => (["carrier", "nearest", "weakest"], "hold", 5),
                "aggressive" => (["carrier", "weakest", "nearest"], "advance", 6),
                "steal" => (["carrier", "nearest"], "advance", 6),
                "escort" => (["nearest"], "hold", 2),
                _ => null,
            };
            if (fight is null)
                return;
            // A standing posture fights for its ground: gating on being at
            // the station keeps a fight episode from anchoring the bot short
            // of its post on the march in. Minted only when the fight leaf
            // exists — consumer honesty. Posts groups gate per bot (its own
            // post), so the leaf inlines instead of sharing.
            string? onStation = stationCondition?.Invoke();
            Node Define() => new()
            {
                When = onStation is null
                    ? Expr.All(EmptyHanded(), EnemyNearby())
                    : Expr.All(EmptyHanded(), EnemyNearby(), onStation),
                // The station is an admission gate, never a per-tick combat
                // veto. Once any authored fight starts (including from a
                // patrol contact), it owns the executor's fixed fight anchor
                // until contact ends or custody begins. The floor then
                // returns to the post or resumes the existing patrol cursor.
                Until = Expr.Any([CarryingCore(), EnemyNoLongerNearby()]),
                Action = Fight(fight.Value.Targets, fight.Value.Style,
                    fight.Value.Chase),
            };
            if (onStation is not null && onStation.Contains("-at-post-"))
            {
                Node inline = Define();
                inline.Id = scope.Id("fight");
                leaves.Add(inline);
                return;
            }
            leaves.Add(GroupLeaf(scope, "fight", Define));
        }

        /// <summary>The on-the-way answer to being attacked: a fight leaf
        /// gated on being OFF the station, so it owns combat en route and
        /// goes silent the moment the bot arrives — the arrived posture
        /// (which may be a fightless 'wait') owns the ground alone. A travel
        /// fight is a real episode, not a one-tick reaction: momentary route
        /// facing or readiness changes cannot hand control straight back to
        /// Move while contact remains. 'hold' travel answers any actionable
        /// attack with a one-tile leash; aggressive/steal brawl their way in
        /// by authored choice; 'wait' emits nothing — the quiet march IS the
        /// absence of a fight leaf.</summary>
        private void AddTravelFight(
            List<Node> leaves,
            LeafScope scope,
            string travel,
            Func<string?> offStationCondition,
            Func<string?> onStationCondition)
        {
            (string[] Targets, string Style, int Chase)? fight = travel switch
            {
                // Standing Hold deliberately ignores ordinary enemies that
                // cannot threaten its ground. "Answer attacks" is different:
                // it must stop the march and answer contact, but not chase the
                // opponent down a corridor. Advance with a one-tile episode
                // leash expresses exactly that distinction.
                "hold" => (["nearest", "weakest"], "advance", 1),
                "aggressive" => (["carrier", "weakest", "nearest"], "advance", 6),
                "steal" => (["carrier", "nearest"], "advance", 6),
                "escort" => (["nearest"], "hold", 2),
                _ => ((string[], string, int)?)null,
            };
            if (fight is null)
                return;
            string? offStation = offStationCondition();
            string? onStation = onStationCondition();
            var endConditions = new List<string>
            {
                CarryingCore(),
                EnemyNoLongerNearby(),
            };
            if (onStation is not null)
                endConditions.Add(onStation);
            Node Define() => new()
            {
                When = offStation is null
                    ? Expr.All(EmptyHanded(), EnemyNearby())
                    : Expr.All(EmptyHanded(), EnemyNearby(), offStation),
                Until = Expr.Any(endConditions.ToArray()),
                Action = Fight(fight.Value.Targets, fight.Value.Style,
                    fight.Value.Chase),
            };
            if (offStation is not null && offStation.Contains("-off-post-"))
            {
                Node inline = Define();
                inline.Id = scope.Id("travel-fight");
                leaves.Add(inline);
                return;
            }
            leaves.Add(GroupLeaf(scope, "travel-fight", Define));
        }

        /// <summary>The body-scoped “I am at my station” fact (or its
        /// inverse, for the travel fight): a posts job gates each bot on
        /// its own post (±1); standing kinds use the job footprint;
        /// converge/advance use their destination (±2). Null when the job
        /// has no arrival geometry.</summary>
        private string? OnStationCondition(
            string prefix,
            string groupTag,
            ArcRelayTacticJob job,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot,
            bool arrived = true)
        {
            int expected = arrived ? 1 : 0;
            if (job.Kind == "posts" && job.Posts is not null)
            {
                int member = group.Slots.ToList().IndexOf(bot.Slot);
                ArcRelayTacticPost post = job.Posts[member];
                int[] postRect = PadBounds(
                    [post.At[0], post.At[1], post.At[0], post.At[1]], 1);
                string postZone = Zone(
                    postRect, $"g-{prefix}-{groupTag}-post-{member}");
                string state = arrived ? "at" : "off";
                return Condition(
                    new ConditionShape("self-in-zone", "equals", expected,
                        ZoneId: postZone),
                    $"c-{prefix}-{groupTag}-{state}-post-{member}-self");
            }
            int[]? rect = job.Kind switch
            {
                "area" => job.Rect,
                "line" => PadBounds(BoundingBox(job.Path!), 1),
                "approach" or "converge" => PadBounds(
                    [
                        job.Point![0], job.Point[1],
                        job.Point[0], job.Point[1],
                    ], 2),
                "advance" => PadBounds(
                    [
                        job.Path![^1][0], job.Path[^1][1],
                        job.Path[^1][0], job.Path[^1][1],
                    ], 2),
                _ => null,
            };
            if (rect is null)
                return null;
            string zone = Zone(rect, $"g-{prefix}-{groupTag}-set");
            return Condition(
                new ConditionShape("self-in-zone", "equals", expected,
                    ZoneId: zone),
                $"c-{prefix}-{groupTag}-{(arrived ? "on" : "off")}-station");
        }

        private void AddSignatureLeaves(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot,
            string posture,
            IReadOnlyList<ArcRelayTacticSignaturePlan> plans,
            ArcRelayTacticJob? job)
        {
            string geometryPrefix = $"{scope.Prefix}-{scope.GroupTag}-{scope.PhaseTag}";
            ArcRelayTacticSignaturePlan? plan = plans.FirstOrDefault(
                candidate => candidate.Slot == bot.Slot);
            ArcRelaySignatureAuthoringDescriptor descriptor =
                ArcRelaySignatureAuthoringCatalog.ForClass(bot.ClassId);
            string policy = plan?.Policy ?? "smart";
            if (policy == "off" || bot.ClassId == "relay")
                return;
            if (plan?.Points is not null)
            {
                for (int index = 0; index < plan.Points.Count; index++)
                {
                    string anchor = Anchor(
                        plan.Points[index],
                        $"g-{geometryPrefix}-s{bot.Slot}-{index}");
                    leaves.Add(new Node
                    {
                        Id = scope.Id($"s{bot.Slot}-mark-{index}"),
                        When = SignatureWhen(descriptor),
                        Action = UseSkill(descriptor.SkillId, writer =>
                        {
                            writer.WriteString(
                                "target", descriptor.DurableDeployment
                                    ? "self"
                                    : DefaultNamedTarget(descriptor));
                            writer.WriteString("at", anchor);
                        }),
                    });
                }
                if (policy == "only")
                    return;
            }
            if (plan?.Line is not null)
            {
                // Longshot's retired two-point ray is accepted as legacy
                // input but deliberately ignored: phase Posts/Patrol own its
                // position and the contextual Rail leaf below owns targeting.
                if (bot.ClassId != "longshot")
                {
                    string route = Route(
                        plan.Line, $"g-{geometryPrefix}-s{bot.Slot}-dash");
                    leaves.Add(new Node
                    {
                        Id = scope.Id("dash"),
                        When = Expr.All(EmptyHanded(), SignatureReady()),
                        Action = UseSkill(descriptor.SkillId, writer =>
                        {
                            writer.WriteStartObject("target");
                            writer.WriteString("route", route);
                            writer.WriteString("progress", "forward");
                            writer.WriteNumber("minimumForwardTiles", 2);
                            writer.WriteEndObject();
                        }),
                    });
                    if (policy == "only")
                        return;
                }
            }
            // Persistent exact deployments need authored ground. Guessing a
            // choke from nearest-enemy targeting can sever the player's own
            // route; Smart therefore waits until map guidance exists.
            if (descriptor.DurableDeployment
                && descriptor.Allows(
                    ArcRelaySignatureTargetCapability.ExactAnchor))
                return;
            // No authored spatial mark for this class this phase: the posture
            // bundles an ordinary signature use when the phase engages at all.
            if (posture is not ("hold" or "aggressive" or "steal" or "escort"))
                return;
            if (bot.ClassId == "kestrel")
            {
                // A patrol is cyclic and has no unambiguous "ahead" at its
                // seam. It only Dashes when the player draws a separate
                // terminal signature lane (handled above); advance/via paths
                // have one honest forward direction and are safe defaults.
                IReadOnlyList<int[]>? path = job switch
                {
                    { Kind: "advance", Path: not null }
                        => job.Path,
                    { Via: not null } => job.Via,
                    _ => null,
                };
                if (path is null)
                    return;
                string route = Route(
                    path, $"g-{geometryPrefix}-s{bot.Slot}-smart-dash");
                leaves.Add(new Node
                {
                    Id = scope.Id("sig"),
                    When = Expr.All(EmptyHanded(), SignatureReady()),
                    Action = UseSkill(descriptor.SkillId, writer =>
                    {
                        writer.WriteStartObject("target");
                        writer.WriteString("route", route);
                        writer.WriteString("progress", "forward");
                        writer.WriteNumber("minimumForwardTiles", 2);
                        writer.WriteEndObject();
                    }),
                });
                return;
            }
            if (bot.ClassId == "longshot")
            {
                if (job is null)
                    return;
                string? station = OnStationCondition(
                    scope.Prefix, scope.GroupTag, job, group, bot);
                var conditions = new List<string>
                {
                    EmptyHanded(), SignatureReady(),
                };
                if (station is not null)
                    conditions.Add(station);
                leaves.Add(new Node
                {
                    Id = scope.Id("sig"),
                    When = Expr.All(conditions.ToArray()),
                    Action = UseSkill(descriptor.SkillId, writer =>
                        writer.WriteString(
                            "target", DefaultNamedTarget(descriptor))),
                });
                return;
            }
            Expr when = SignatureWhen(descriptor);
            leaves.Add(new Node
            {
                Id = scope.Id("sig"),
                When = when,
                Action = UseSkill(descriptor.SkillId, writer =>
                    writer.WriteString("target", DefaultNamedTarget(descriptor))),
            });
        }

        private Expr SignatureWhen(
            ArcRelaySignatureAuthoringDescriptor descriptor) =>
            descriptor.Custody == ArcRelaySignatureCustodyBehavior.CarrierSafe
                ? Expr.One(SignatureReady())
                : Expr.All(EmptyHanded(), SignatureReady());

        private void AddFloor(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTacticJob job,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot)
        {
            string geometryPrefix = $"{scope.Prefix}-{scope.GroupTag}";
            // The march stance is the on-the-way posture when one is set;
            // arrived combat belongs to the fight leaves either way.
            string posture = (job.Travel ?? job.Posture) switch
            {
                "aggressive" or "steal" => "front",
                "hold" or "escort" => "line",
                _ => "rear",
            };
            // A drawn approach route rides the Orders `via` ingress: each
            // activation walks the route once (progress-aware), then the
            // normal floor takes over. Interned by shape like every route.
            string? via = job.Via is null
                ? null
                : Route(job.Via, $"g-{geometryPrefix}-in");
            switch (job.Kind)
            {
                // Posts and lines are per-bot floors (each bot owns its own
                // tile), so they stay inline; every other floor is
                // group-uniform and shares one definition.
                case "posts":
                {
                    int member = group.Slots.ToList().IndexOf(bot.Slot);
                    ArcRelayTacticPost post = job.Posts![member];
                    string anchor = Anchor(
                        post.At, $"g-{geometryPrefix}-post-{member}");
                    int[]? faceTile = FacingTile(post);
                    string? face = faceTile is null
                        ? null
                        : Anchor(faceTile, $"g-{geometryPrefix}-face-{member}");
                    // A post's own drawn way in overrides the group route:
                    // this bot walks its route, groupmates keep theirs.
                    string? wayIn = post.Via is null
                        ? via
                        : Route(post.Via, $"g-{geometryPrefix}-in-{member}");
                    leaves.Add(new Node
                    {
                        Id = scope.Id("move"),
                        Action = writer =>
                        {
                            writer.WriteStartObject("move");
                            writer.WriteStartObject("to");
                            writer.WriteString("anchor", anchor);
                            writer.WriteEndObject();
                            if (wayIn is not null)
                                writer.WriteString("via", wayIn);
                            if (face is not null)
                            {
                                writer.WriteStartObject("face");
                                writer.WriteString("anchor", face);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndObject();
                        },
                    });
                    break;
                }
                case "line":
                {
                    int member = group.Slots.ToList().IndexOf(bot.Slot);
                    int point = member % job.Path!.Count;
                    string anchor = Anchor(
                        job.Path[point], $"g-{geometryPrefix}-line-{point}");
                    leaves.Add(new Node
                    {
                        Id = scope.Id("move"),
                        Action = writer =>
                        {
                            writer.WriteStartObject("move");
                            writer.WriteStartObject("to");
                            writer.WriteString("anchor", anchor);
                            writer.WriteEndObject();
                            if (via is not null)
                                writer.WriteString("via", via);
                            writer.WriteString("posture", posture);
                            writer.WriteEndObject();
                        },
                    });
                    break;
                }
                case "area":
                {
                    string zone = Zone(job.Rect!, $"g-{geometryPrefix}-hold");
                    leaves.Add(GroupLeaf(scope, "move", () => new Node
                        { Action = MoveToZone(zone, posture, via) }));
                    break;
                }
                case "approach":
                {
                    string anchor = Anchor(
                        job.Point!, $"g-{geometryPrefix}-mark");
                    leaves.Add(GroupLeaf(scope, "move", () => new Node
                    {
                        Action = writer =>
                        {
                            writer.WriteStartObject("move");
                            writer.WriteStartObject("to");
                            writer.WriteString("adjacentTo", anchor);
                            writer.WriteEndObject();
                            if (via is not null)
                                writer.WriteString("via", via);
                            writer.WriteString("posture", "rear");
                            writer.WriteEndObject();
                        },
                    }));
                    break;
                }
                case "converge":
                {
                    string anchor = Anchor(
                        job.Point!, $"g-{geometryPrefix}-goal");
                    leaves.Add(GroupLeaf(scope, "move", () => new Node
                    {
                        Action = writer =>
                        {
                            writer.WriteStartObject("move");
                            writer.WriteStartObject("to");
                            writer.WriteString("adjacentTo", anchor);
                            writer.WriteEndObject();
                            if (via is not null)
                                writer.WriteString("via", via);
                            writer.WriteString("posture", posture);
                            writer.WriteEndObject();
                        },
                    }));
                    break;
                }
                case "advance":
                {
                    int[] last = job.Path![^1];
                    int[] rect = PadBounds([last[0], last[1], last[0], last[1]], 1);
                    string zone = Zone(rect, $"g-{geometryPrefix}-goal");
                    string route = Route(job.Path, $"g-{geometryPrefix}-way");
                    leaves.Add(GroupLeaf(scope, "move", () => new Node
                    {
                        Action = writer =>
                        {
                            writer.WriteStartObject("move");
                            writer.WriteStartObject("to");
                            writer.WriteString("zone", zone);
                            writer.WriteEndObject();
                            writer.WriteString("via", route);
                            writer.WriteString("posture", posture);
                            writer.WriteEndObject();
                        },
                    }));
                    break;
                }
                case "patrol":
                {
                    string route = Route(job.Path!, $"g-{geometryPrefix}-beat");
                    leaves.Add(GroupLeaf(scope, "move", () => new Node
                    {
                        Action = writer =>
                        {
                            writer.WriteStartObject("move");
                            writer.WriteString("patrol", route);
                            writer.WriteString("posture", posture);
                            writer.WriteEndObject();
                        },
                    }));
                    break;
                }
                case "return":
                {
                    leaves.Add(GroupLeaf(scope, "move", () => new Node
                        { Action = MoveToDynamic("own-reactor", via: via) }));
                    break;
                }
                case "escort":
                {
                    leaves.Add(GroupLeaf(scope, "move", () => new Node
                        { Action = FollowCarrier() }));
                    break;
                }
            }
        }

        /// <summary>
        /// Emit one group-uniform leaf: as a shared order referenced by every
        /// member role when the group can honestly consume it twice, inline
        /// for a one-bot group. Leaves carry no phase tag of their own — the
        /// per-role tactic and phase wrappers above them are the gate.
        /// </summary>
        private Node GroupLeaf(LeafScope scope, string kind, Func<Node> define)
        {
            string id = scope.Id(kind);
            if (scope.GroupSize < 2)
            {
                Node inline = define();
                inline.Id = id;
                return inline;
            }
            if (_sharedLeafIds.Add(id))
                SharedOrders.Add((id, define()));
            return new Node { SharedRef = id };
        }

        private int[]? FacingTile(ArcRelayTacticPost post)
        {
            (int dx, int dy) = post.Facing switch
            {
                "n" => (0, -1),
                "s" => (0, 1),
                "w" => (-1, 0),
                _ => (1, 0),
            };
            int x = post.At[0] + dx;
            int y = post.At[1] + dy;
            if (x < 0 || y < 0 || x >= _document.MapWidth
                || y >= _document.MapHeight)
                return null;
            return [x, y];
        }

        private static IEnumerable<int[]> GeometryPoints(ArcRelayTacticJob job)
        {
            if (job.Posts is not null)
            {
                foreach (ArcRelayTacticPost post in job.Posts)
                    yield return post.At;
            }
            if (job.Rect is not null)
            {
                yield return [job.Rect[0], job.Rect[1]];
                yield return [job.Rect[2], job.Rect[3]];
            }
            if (job.Path is not null)
            {
                foreach (int[] point in job.Path)
                    yield return point;
            }
            if (job.Point is not null)
                yield return job.Point;
        }

        private static int[] BoundingBox(IEnumerable<int[]> points)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (int[] point in points)
            {
                minX = Math.Min(minX, point[0]);
                minY = Math.Min(minY, point[1]);
                maxX = Math.Max(maxX, point[0]);
                maxY = Math.Max(maxY, point[1]);
            }
            return [minX, minY, maxX, maxY];
        }

        private int[] PadBounds(int[] rect, int padding)
        {
            return
            [
                Math.Max(0, rect[0] - padding),
                Math.Max(0, rect[1] - padding),
                Math.Min(_document.MapWidth - 1, rect[2] + padding),
                Math.Min(_document.MapHeight - 1, rect[3] + padding),
            ];
        }

        private string AnywhereZone() => Zone(
            [0, 0, _document.MapWidth - 1, _document.MapHeight - 1],
            "g-anywhere");

        private string CarryingCore() => Condition(
            new ConditionShape("self-carrying", "equals", 1), "carrying-core");

        private string EmptyHanded() => Condition(
            new ConditionShape("self-carrying", "equals", 0), "empty-handed");

        private string Hurt() => Condition(
            new ConditionShape("self-health-percent", "at-most", 35), "hurt");

        private string Healthy() => Condition(
            new ConditionShape("self-health-percent", "at-least", 75), "healthy");

        private string EnemyNearby() => Condition(
            new ConditionShape(
                "self-nearest-visible-enemy-distance", "at-most", 7),
            "enemy-nearby");

        private string EnemyNoLongerNearby() => Condition(
            new ConditionShape(
                "self-nearest-visible-enemy-distance", "greater-than", 7),
            "enemy-no-longer-nearby");

        private string SignatureReady() => Condition(
            new ConditionShape("signature-ready", "equals", 1),
            "signature-ready");

        private static string DefaultNamedTarget(
            ArcRelaySignatureAuthoringDescriptor descriptor)
        {
            ArcRelaySignatureNamedTarget? preset = descriptor.Preset.NamedTarget;
            if (preset is ArcRelaySignatureNamedTarget target
                && descriptor.AllowedNamedTargets.Contains(target))
            {
                return ArcRelaySignatureAuthoringCatalog.StableId(target);
            }
            return ArcRelaySignatureAuthoringCatalog.StableId(
                descriptor.AllowedNamedTargets[0]);
        }

        private static Action<Utf8JsonWriter> MoveToDynamic(
            string target,
            string? posture = null,
            string? via = null,
            string? near = null) => writer =>
        {
            writer.WriteStartObject("move");
            writer.WriteString("to", target);
            if (via is not null)
                writer.WriteString("via", via);
            if (posture is not null)
                writer.WriteString("posture", posture);
            if (near is not null)
                writer.WriteString("near", near);
            writer.WriteEndObject();
        };

        private static Action<Utf8JsonWriter> MoveToZone(
            string zone,
            string posture,
            string? via = null) => writer =>
        {
            writer.WriteStartObject("move");
            writer.WriteStartObject("to");
            writer.WriteString("zone", zone);
            writer.WriteEndObject();
            if (via is not null)
                writer.WriteString("via", via);
            writer.WriteString("posture", posture);
            writer.WriteEndObject();
        };


        private static Action<Utf8JsonWriter> FollowCarrier() => writer =>
        {
            writer.WriteStartObject("move");
            writer.WriteStartObject("follow");
            writer.WriteString("selector", "nearest-friendly-core-carrier");
            writer.WriteEndObject();
            writer.WriteEndObject();
        };

        private static Action<Utf8JsonWriter> Fight(
            string[] targets,
            string style,
            int chaseLimit) => writer =>
        {
            writer.WriteStartObject("fight");
            writer.WriteStartArray("targets");
            foreach (string target in targets)
                writer.WriteStringValue(target);
            writer.WriteEndArray();
            writer.WriteString("style", style);
            writer.WriteNumber("chaseLimit", chaseLimit);
            writer.WriteEndObject();
        };

        private static Action<Utf8JsonWriter> UseSkill(
            string skill,
            Action<Utf8JsonWriter> writeTarget) => writer =>
        {
            writer.WriteStartObject("use");
            writer.WriteString("skill", skill);
            writer.WriteString(
                "opportunity",
                ArcRelaySignatureAuthoringCatalog.StableId(
                    ArcRelaySignatureAuthoringCatalog.ForSkill(skill).Opportunity));
            writeTarget(writer);
            writer.WriteEndObject();
        };

        private static IReadOnlyList<string> UpgradePathFor(string build) =>
            build switch
            {
                "durable" => ["vitality", "vitality", "damage", "vision"],
                "aggressive" => ["damage", "damage", "reach", "vitality"],
                "scout" => ["vision", "reach", "damage", "vitality"],
                _ => ["damage", "vitality", "vision", "reach"],
            };

        private string Condition(ConditionShape shape, string nameHint)
        {
            string key = shape.Key;
            if (_conditionsByKey.TryGetValue(key, out string? existing))
                return existing;
            string id = EnsureUnique(Kebab(nameHint), _usedIds);
            _conditionsByKey.Add(key, id);
            Conditions.Add(new ConditionDef(id, shape));
            return id;
        }

        private string Zone(int[] rect, string nameHint)
        {
            string shape = string.Join(',', rect);
            if (_zonesByShape.TryGetValue(shape, out string? existing))
                return existing;
            string id = EnsureUnique(Kebab(nameHint), _usedIds);
            _zonesByShape.Add(shape, id);
            Zones.Add((id, rect));
            return id;
        }

        private string Route(IReadOnlyList<int[]> waypoints, string nameHint)
        {
            string shape = string.Join(';', waypoints.Select(point =>
                $"{point[0]},{point[1]}"));
            if (_routesByShape.TryGetValue(shape, out string? existing))
                return existing;
            string id = EnsureUnique(Kebab(nameHint), _usedIds);
            _routesByShape.Add(shape, id);
            Routes.Add((id, waypoints));
            return id;
        }

        private string Anchor(int[] point, string nameHint)
        {
            string shape = $"{point[0]},{point[1]}";
            if (_anchorsByShape.TryGetValue(shape, out string? existing))
                return existing;
            string id = EnsureUnique(Kebab(nameHint), _usedIds);
            _anchorsByShape.Add(shape, id);
            Anchors.Add((id, point));
            return id;
        }

        private static string Kebab(string value)
        {
            var builder = new StringBuilder(value.Length);
            bool pendingHyphen = false;
            foreach (char character in value.ToLowerInvariant())
            {
                if (char.IsAsciiLetterLower(character)
                    || char.IsAsciiDigit(character))
                {
                    if (pendingHyphen && builder.Length > 0)
                        builder.Append('-');
                    pendingHyphen = false;
                    builder.Append(character);
                }
                else
                {
                    pendingHyphen = true;
                }
            }
            return builder.ToString();
        }

        private static string EnsureUnique(string candidate, HashSet<string> used)
        {
            if (candidate.Length == 0)
                candidate = "g";
            if (candidate.Length > 48)
                candidate = $"{candidate[..40].TrimEnd('-')}-{StableHash(candidate)}";
            string result = candidate;
            int suffix = 2;
            while (!used.Add(result))
            {
                string tail = $"-{suffix}";
                result = candidate.Length + tail.Length > 48
                    ? $"{candidate[..(48 - tail.Length)].TrimEnd('-')}{tail}"
                    : $"{candidate}{tail}";
                suffix++;
            }
            return result;
        }

        private static string StableHash(string value)
        {
            // FNV-1a, culture-free and deterministic across runs.
            ulong hash = 14695981039346656037UL;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
            return (hash & 0xFFFFFF).ToString("x6");
        }
    }

    /// <summary>Names one tactic/phase/group leaf family: phase-scoped ids
    /// read "t{n}-p{k}-g{j}-{kind}", tactic-scoped Core habits
    /// "t{n}-g{j}-{kind}"; both are the practice-attribution contract. Tag
    /// is the node's program gate.</summary>
    private sealed record LeafScope(
        string Prefix,
        string GroupTag,
        int GroupSize,
        string? PhaseTag)
    {
        public string Tag => PhaseTag is null
            ? Prefix
            : $"{Prefix}/{PhaseTag}";

        public string Id(string kind) => PhaseTag is null
            ? $"{Prefix}-{GroupTag}-{kind}"
            : $"{Prefix}-{PhaseTag}-{GroupTag}-{kind}";
    }

    /// <summary>The interned identity of one generated condition.</summary>
    private sealed record ConditionShape(
        string Fact,
        string Operator,
        int Value,
        string? Role = null,
        string? ZoneId = null,
        string? Well = null,
        int? FreshnessTicks = null,
        int? WindowTicks = null,
        string? Since = null,
        string[]? Roles = null,
        string[]? ZoneIds = null,
        int? Level = null)
    {
        public string Key => string.Join('|',
            Fact, Operator, Value, Role, ZoneId, Well, FreshnessTicks,
            WindowTicks, Since,
            Roles is null ? null : string.Join(',', Roles),
            ZoneIds is null ? null : string.Join(',', ZoneIds),
            Level);
    }

    private sealed record ConditionDef(string Id, ConditionShape Shape)
    {
        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject(Id);
            writer.WriteString("fact", Shape.Fact);
            writer.WriteString("operator", Shape.Operator);
            writer.WriteNumber("value", Shape.Value);
            if (Shape.Role is not null)
                writer.WriteString("role", Shape.Role);
            if (Shape.ZoneId is not null)
                writer.WriteString("zone", Shape.ZoneId);
            if (Shape.Level is int level)
                writer.WriteNumber("level", level);
            if (Shape.ZoneIds is not null)
            {
                writer.WriteStartArray("zones");
                foreach (string zone in Shape.ZoneIds)
                    writer.WriteStringValue(zone);
                writer.WriteEndArray();
            }
            if (Shape.Well is not null)
                writer.WriteString("well", Shape.Well);
            if (Shape.FreshnessTicks is int freshness)
                writer.WriteNumber("freshnessTicks", freshness);
            if (Shape.WindowTicks is int window)
                writer.WriteNumber("windowTicks", window);
            if (Shape.Since is not null)
                writer.WriteString("since", Shape.Since);
            if (Shape.Roles is not null)
            {
                writer.WriteStartArray("roles");
                foreach (string role in Shape.Roles)
                    writer.WriteStringValue(role);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
    }

    /// <summary>The program table to be written: everything is already
    /// interned condition names, ordinal ids, and resolved phase targets.</summary>
    private sealed record ProgramSpec(IReadOnlyList<ProgramTacticSpec> Tactics)
    {
        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("program");
            writer.WriteStartArray("tactics");
            foreach (ProgramTacticSpec tactic in Tactics)
            {
                writer.WriteStartObject();
                writer.WriteString("id", tactic.Id);
                writer.WriteString("name", tactic.Name);
                WriteCases(writer, "start", tactic.Start);
                WriteCases(writer, "abort", tactic.Abort);
                if (tactic.MaxTicks is int maxTicks)
                    writer.WriteNumber("maxTicks", maxTicks);
                writer.WriteNumber("rearm", tactic.Rearm);
                writer.WriteStartArray("phases");
                foreach (ProgramPhaseSpec phase in tactic.Phases)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", phase.Id);
                    writer.WriteString("name", phase.Name);
                    if (phase.Continue.Count > 0)
                    {
                        writer.WriteStartArray("continue");
                        foreach ((CaseSpec when, string to) in phase.Continue)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("when");
                            when.Write(writer);
                            writer.WriteString("to", to);
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    if (phase.CustodyGroups.Count > 0)
                    {
                        writer.WriteStartArray("custodyGroups");
                        foreach (IReadOnlyList<int> group in phase.CustodyGroups)
                        {
                            writer.WriteStartArray();
                            foreach (int unit in group)
                                writer.WriteNumberValue(unit);
                            writer.WriteEndArray();
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteCases(
            Utf8JsonWriter writer,
            string name,
            IReadOnlyList<CaseSpec>? cases)
        {
            if (cases is null)
                return;
            writer.WriteStartArray(name);
            foreach (CaseSpec value in cases)
                value.Write(writer);
            writer.WriteEndArray();
        }
    }

    private sealed record ProgramTacticSpec(
        string Id,
        string Name,
        IReadOnlyList<CaseSpec>? Start,
        IReadOnlyList<CaseSpec>? Abort,
        int? MaxTicks,
        int Rearm,
        IReadOnlyList<ProgramPhaseSpec> Phases);

    private sealed record ProgramPhaseSpec(
        string Id,
        string Name,
        IReadOnlyList<(CaseSpec When, string To)> Continue,
        IReadOnlyList<IReadOnlyList<int>> CustodyGroups);

    /// <summary>all(All…) AND any(Any…); entries are interned condition
    /// names or one-level nested any/all groups (readiness pairs).</summary>
    private sealed class CaseSpec
    {
        public List<object> All { get; } = [];
        public List<object> Any { get; } = [];

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            WriteSide(writer, "all", All);
            WriteSide(writer, "any", Any);
            writer.WriteEndObject();
        }

        private static void WriteSide(
            Utf8JsonWriter writer,
            string op,
            List<object> entries)
        {
            if (entries.Count == 0)
                return;
            writer.WriteStartArray(op);
            foreach (object entry in entries)
            {
                if (entry is string name)
                {
                    writer.WriteStringValue(name);
                    continue;
                }
                var group = (Expr)entry;
                writer.WriteStartObject();
                writer.WriteStartArray(group.Op!);
                foreach (string member in group.Names)
                    writer.WriteStringValue(member);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }

    /// <summary>Condition expression: one name, or an all/any of names with
    /// at most one level of nested groups.</summary>
    private sealed record Expr(
        string? Op, IReadOnlyList<string> Names, IReadOnlyList<Expr>? Groups = null)
    {
        public static Expr One(string name) => new(null, [name]);

        public static Expr All(params string[] names) =>
            names.Length == 1 ? One(names[0]) : new Expr("all", names);

        public static Expr Any(IReadOnlyList<string> names) =>
            names.Count == 1 ? One(names[0]) : new Expr("any", names);

        public void Write(Utf8JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            if (Names.Count == 1 && (Groups?.Count ?? 0) == 0)
            {
                writer.WriteStringValue(Names[0]);
                return;
            }
            writer.WriteStartObject();
            writer.WriteStartArray(Op!);
            foreach (string name in Names)
                writer.WriteStringValue(name);
            foreach (Expr group in Groups ?? [])
            {
                writer.WriteStartObject();
                writer.WriteStartArray(group.Op!);
                foreach (string name in group.Names)
                    writer.WriteStringValue(name);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }

    private sealed class Node
    {
        public string? Id { get; set; }
        public string? SharedRef { get; init; }
        public Expr? When { get; set; }
        public Expr? Until { get; set; }
        public int? Timeout { get; set; }
        public int? Cooldown { get; set; }
        public string? Phase { get; set; }
        public string? Urgency { get; set; }
        public string? CapacityName { get; set; }
        public List<Node>? Children { get; set; }
        public Action<Utf8JsonWriter>? Action { get; set; }

        public void Write(Utf8JsonWriter writer, bool asSharedDefinition = false)
        {
            if (SharedRef is not null)
            {
                writer.WriteStartObject();
                writer.WriteString("shared", SharedRef);
                writer.WriteEndObject();
                return;
            }
            writer.WriteStartObject();
            if (!asSharedDefinition)
                writer.WriteString("id", Id!);
            When?.Write(writer, "when");
            Until?.Write(writer, "until");
            if (Timeout is int timeout)
                writer.WriteNumber("timeout", timeout);
            if (Cooldown is int cooldown)
                writer.WriteNumber("cooldown", cooldown);
            if (Phase is not null)
                writer.WriteString("phase", Phase);
            if (CapacityName is not null)
                writer.WriteString("capacity", CapacityName);
            if (Urgency is not null)
                writer.WriteString("urgency", Urgency);
            if (Children is not null)
            {
                writer.WriteStartArray("orders");
                foreach (Node child in Children)
                    child.Write(writer);
                writer.WriteEndArray();
            }
            else
            {
                Action!(writer);
            }
            writer.WriteEndObject();
        }
    }
}
