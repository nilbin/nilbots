using System.Text;
using System.Text.Json;

namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Deterministic server-side materializer: one validated Tactics document in,
/// one Orders v2 + map-plan source pair out. The generated pair is the only
/// executable form; ids are stable and readable because they are the practice
/// attribution contract. The encoding is proved against the order-tree
/// lifecycle: release branches latch through an authored until, outcome
/// evidence anchors at release entry, and finished tactics yield
/// no-child-eligible so the next tactic takes over deterministically.
/// </summary>
internal static class ArcRelayTacticsMaterializer
{
    private const int RootCooldown = 40;
    private const int PhaseCooldown = 120;
    private const int BriefTimeout = 60;
    private const int StandardTimeout = 180;
    private const int LongTimeout = 420;
    private const int RememberedFreshnessTicks = 40;
    private const int ObservedWindowTicks = 120;
    private const int GrabWalkingSteps = 6;

    public static ArcRelayTacticsMaterialization Materialize(
        ArcRelayTacticsDocument document)
    {
        var emission = new Emission(document);
        emission.BuildBaseVocabulary();
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
        byte[] orders = WriteOrders(document, emission, roles);
        byte[] mapPlan = WriteMapPlan(document, emission);
        return new ArcRelayTacticsMaterialization(orders, mapPlan);
    }

    private static byte[] WriteOrders(
        ArcRelayTacticsDocument document,
        Emission emission,
        IReadOnlyList<RoleEmission> roles)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", ArcRelayTacticalPlaybookCompiler.PlaybookSchema);
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

        public RoleEmission BuildRole(ArcRelayTacticsSquadBot bot)
        {
            var orders = new List<Node>();
            for (int index = 0; index < _document.Tactics.Count; index++)
            {
                Node? branch = BuildTacticBranch(
                    index, _document.Tactics[index], bot);
                if (branch is not null)
                    orders.Add(branch);
            }
            orders.Add(new Node { SharedRef = "score-a-core" });
            orders.Add(new Node { SharedRef = "collect-loose-core" });
            orders.Add(new Node { SharedRef = "recover-health" });
            orders.Add(new Node { SharedRef = "fight-visible-enemy" });
            orders.Add(new Node { SharedRef = "hold-ground" });
            return new RoleEmission(
                RoleId(bot.Slot), bot.ClassId, UpgradePathFor(bot.Build), orders);
        }

        private Node? BuildTacticBranch(
            int index,
            ArcRelayTactic tactic,
            ArcRelayTacticsSquadBot bot)
        {
            // Generated ids are ordinal (t3, g1): they stay short enough for
            // the ATL2 envelope at eight tactics and the tactics document
            // resolves them back to authored names for display/attribution.
            string prefix = $"t{index + 1}";
            int groupIndex = 0;
            while (!tactic.Groups[groupIndex].Slots.Contains(bot.Slot))
                groupIndex++;
            ArcRelayTacticGroup group = tactic.Groups[groupIndex];
            string groupTag = $"g{groupIndex + 1}";

            Expr? start = BuildStartExpression(prefix, tactic.Start);
            Expr? release = BuildReleaseExpression(prefix, tactic);
            Expr? outcomes = BuildOutcomeExpression(prefix, tactic);

            var root = new Node
            {
                Id = prefix,
                When = start,
                Timeout = tactic.MaxAttempt switch
                {
                    "brief" => BriefTimeout,
                    "standard" => StandardTimeout,
                    "long" => LongTimeout,
                    _ => null,
                },
                Cooldown = RootCooldown,
            };
            // Phase-invariant Core habits (pickup rule, delivery plan) are
            // emitted once above the release branch: they outrank both
            // floors and apply in both phases. Their empty-handed/carrying
            // gates keep any post-outcome residue benign — fight and
            // recovery must NOT be hoisted here, or a finished tactic would
            // keep fighting in its own name and shade the next tactic.
            var invariantScope = new LeafScope(
                prefix, groupTag, group.Slots.Count, PhaseTag: null);
            var children = new List<Node>();
            AddDelivery(children, invariantScope, tactic, group, bot);
            AddCoreRule(children, invariantScope, group.CoreRule);
            if (release is null)
            {
                // Immediate release: activation IS release, so outcome
                // evidence may anchor at the root itself.
                root.Until = outcomes;
                children.AddRange(BuildActLeaves(
                    prefix, groupTag, tactic, group, bot));
            }
            else
            {
                List<Node> actLeaves = BuildActLeaves(
                    prefix, groupTag, tactic, group, bot);
                if (actLeaves.Count > 0)
                {
                    children.Add(new Node
                    {
                        Id = $"{prefix}-go",
                        When = release,
                        // The until doubles as the release latch: while this
                        // branch is active its when is never re-consulted, so
                        // a lapsed trigger cannot un-release the tactic.
                        Until = outcomes ?? Expr.One(Never()),
                        Cooldown = PhaseCooldown,
                        Children = actLeaves,
                    });
                }
                List<Node> stageLeaves = BuildStageLeaves(
                    prefix, groupTag, tactic, group, bot);
                if (stageLeaves.Count > 0)
                {
                    // The whole stage phase lives under one wrapper whose
                    // until is the release expression: every stage leaf dies
                    // the moment the tactic releases, and a finished release
                    // branch leaves the tactic yielding no-child-eligible
                    // instead of re-staging.
                    children.Add(new Node
                    {
                        Id = $"{prefix}-s",
                        Until = release,
                        Cooldown = PhaseCooldown,
                        Children = stageLeaves,
                    });
                }
            }
            if (children.Count == 0)
                return null;
            root.Children = children;
            return root;
        }

        private Expr? BuildStartExpression(
            string prefix,
            ArcRelayTacticConditionRows? rows)
        {
            if (rows is null)
                return null;
            List<string> names = BuildRowConditions(prefix, "start", rows);
            return names.Count == 0 ? null : Expr.Any(names);
        }

        private Expr? BuildReleaseExpression(string prefix, ArcRelayTactic tactic)
        {
            if (tactic.ReleaseMode == "immediate")
                return null;
            List<string> rowNames = tactic.Release is null
                ? []
                : BuildRowConditions(prefix, "release", tactic.Release);

            // Exact-post readiness binds each assigned bot to its own post via
            // its single-bot role; the aggregate form is the fallback when the
            // flat all[] name budget cannot carry the exact conditions.
            List<ArcRelayTacticGroup> stagedGroups = tactic.Groups
                .Where(group => group.Stage.HasGeometry).ToList();
            int exactCount = stagedGroups.Sum(group =>
                group.Stage.Kind == "posts" ? group.Slots.Count : 1);
            bool exactPosts = stagedGroups.Any(group => group.Stage.Kind == "posts")
                && exactCount + rowNames.Count <= 8;
            var names = new List<string>();
            for (int groupIndex = 0; groupIndex < tactic.Groups.Count; groupIndex++)
            {
                ArcRelayTacticGroup group = tactic.Groups[groupIndex];
                if (!group.Stage.HasGeometry)
                    continue;
                string groupTag = $"g{groupIndex + 1}";
                if (exactPosts && group.Stage.Kind == "posts")
                {
                    // Small posts groups gate on each bot standing at its own
                    // post; larger ones bind each distinct bot to the posts
                    // footprint instead so the zone table stays affordable.
                    bool perPostZones = group.Slots.Count <= 3;
                    for (int post = 0; post < group.Stage.Posts!.Count; post++)
                    {
                        names.Add(PostReadiness(
                            prefix, groupTag, group, post, perPostZones));
                    }
                }
                else if (AggregateReadiness(prefix, groupTag, group)
                    is string aggregate)
                {
                    names.Add(aggregate);
                }
            }
            names.AddRange(rowNames);
            if (names.Count == 0)
                return null;
            return Expr.All(names.ToArray());
        }

        private string? AggregateReadiness(
            string prefix,
            string groupTag,
            ArcRelayTacticGroup group)
        {
            int[]? rect = group.Stage.Kind switch
            {
                "posts" => PadBounds(BoundingBox(
                    group.Stage.Posts!.Select(post => post.At)), 1),
                "area" => group.Stage.Rect,
                "line" => PadBounds(BoundingBox(group.Stage.Path!), 1),
                "approach" => PadBounds(
                    [
                        group.Stage.Point![0], group.Stage.Point[1],
                        group.Stage.Point[0], group.Stage.Point[1],
                    ], 2),
                _ => null,
            };
            if (rect is null)
                return null;
            string zone = Zone(rect, $"g-{prefix}-{groupTag}-set");
            // Two-thirds assembled is “in position”: staging under fire
            // jostles bodies at the footprint edge, and demanding all-but-one
            // let a single straggler hold the whole release hostage.
            int required = Math.Max(1, (group.Slots.Count * 2 + 2) / 3);
            return Condition(
                new ConditionShape(
                    "friendlies-in-zone", "at-least", required, ZoneId: zone),
                $"c-{prefix}-{groupTag}-ready");
        }

        private string PostReadiness(
            string prefix,
            string groupTag,
            ArcRelayTacticGroup group,
            int postIndex,
            bool perPostZones)
        {
            ArcRelayTacticPost post = group.Stage.Posts![postIndex];
            int[] rect = perPostZones
                ? PadBounds(
                    [post.At[0], post.At[1], post.At[0], post.At[1]], 1)
                : PadBounds(BoundingBox(
                    group.Stage.Posts.Select(value => value.At)), 1);
            string zone = perPostZones
                ? Zone(rect, $"g-{prefix}-{groupTag}-near-post-{postIndex}")
                : Zone(rect, $"g-{prefix}-{groupTag}-set");
            return Condition(
                new ConditionShape(
                    "role-in-zone-count", "at-least", 1,
                    Role: RoleId(group.Slots[postIndex]), ZoneId: zone),
                $"c-{prefix}-{groupTag}-at-post-{postIndex}");
        }

        private Expr? BuildOutcomeExpression(string prefix, ArcRelayTactic tactic)
        {
            ArcRelayTacticOutcomes outcomes = tactic.Outcomes;
            var names = new List<string>();
            if (outcomes.Kills is int kills)
            {
                names.Add(Condition(
                    new ConditionShape(
                        "confirmed-enemy-destructions-in-zone", "at-least",
                        kills, ZoneId: AnywhereZone(), Since: "node-entry"),
                    $"c-{prefix}-kills"));
            }
            if (outcomes.Losses is int losses)
            {
                names.Add(Condition(
                    new ConditionShape("live-friendlies", "at-most", 8 - losses),
                    $"down-{losses}-bots"));
            }
            if (outcomes.Cleared is ArcRelayTacticZoneRef cleared)
            {
                string zone = Zone(cleared.Rect, $"g-{prefix}-cleared");
                names.Add(Condition(
                    new ConditionShape(
                        "visible-enemies-in-zone", "equals", 0, ZoneId: zone),
                    $"c-{prefix}-cleared"));
            }
            if (outcomes.CoreAcquired)
            {
                names.Add(Condition(
                    new ConditionShape("friendly-carriers", "at-least", 1),
                    "we-hold-a-core"));
            }
            if (outcomes.CoreBanked)
            {
                names.Add(Condition(
                    new ConditionShape(
                        "friendly-core-banks", "at-least", 1,
                        Since: "node-entry"),
                    $"c-{prefix}-banked"));
            }
            if (outcomes.CoreLost)
            {
                names.Add(Condition(
                    new ConditionShape(
                        "friendly-core-losses", "at-least", 1,
                        Since: "node-entry"),
                    $"c-{prefix}-core-lost"));
            }
            if (outcomes.Reached)
            {
                ArcRelayTacticGroup mover = tactic.Groups.First(
                    group => group.Act.Kind is "converge" or "advance");
                int[] destination = mover.Act.Kind == "converge"
                    ? mover.Act.Point!
                    : mover.Act.Path![^1];
                int[] rect = PadBounds(
                    [destination[0], destination[1], destination[0], destination[1]],
                    1);
                string zone = Zone(rect, $"g-{prefix}-goal");
                names.Add(Condition(
                    new ConditionShape(
                        "friendlies-in-zone", "at-least",
                        Math.Max(1, (mover.Slots.Count + 1) / 2), ZoneId: zone),
                    $"c-{prefix}-reached"));
            }
            if (outcomes.Observed)
            {
                List<ArcRelayTacticGroup> observers = tactic.Groups
                    .Where(group =>
                        (group.Stage.Posture == "observe" && group.Stage.HasGeometry)
                        || (group.Act.Posture == "observe" && group.Act.HasGeometry))
                    .ToList();
                if (observers.Count == 0)
                {
                    observers = tactic.Groups
                        .Where(group => group.HasAnyGeometry).ToList();
                }
                int[] rect = BoundingBox(observers.SelectMany(GeometryPoints));
                string zone = Zone(rect, $"g-{prefix}-watchline");
                names.Add(Condition(
                    new ConditionShape(
                        "zone-fully-observed-throughout-interval", "equals", 1,
                        ZoneId: zone, WindowTicks: ObservedWindowTicks),
                    $"c-{prefix}-observed"));
            }
            if (names.Count == 0)
                return null;
            return Expr.Any(names);
        }

        private List<string> BuildRowConditions(
            string prefix,
            string section,
            ArcRelayTacticConditionRows rows)
        {
            var names = new List<string>();
            if (rows.EnemiesAt is ArcRelayTacticEnemiesAtRow enemies)
            {
                string zone = Zone(
                    enemies.Zone.Rect, $"g-{prefix}-{section}-watch");
                names.Add(Condition(
                    new ConditionShape(
                        "remembered-enemies-in-zone", "at-least", enemies.Count,
                        ZoneId: zone, FreshnessTicks: RememberedFreshnessTicks),
                    $"c-{prefix}-{section}-enemies"));
            }
            if (rows.Contact)
            {
                names.Add(Condition(
                    new ConditionShape(
                        "visible-enemies-in-zone", "at-least", 1,
                        ZoneId: AnywhereZone()),
                    "enemy-contact"));
            }
            if (rows.CoreLooseWell is string well)
            {
                names.Add(Condition(
                    new ConditionShape(
                        "well-has-outstanding-core", "equals", 1, Well: well),
                    $"core-up-at-{well}"));
            }
            if (rows.Carrier)
            {
                names.Add(Condition(
                    new ConditionShape("friendly-carriers", "at-least", 1),
                    "we-hold-a-core"));
            }
            if (rows.Losses is int losses)
            {
                names.Add(Condition(
                    new ConditionShape("live-friendlies", "at-most", 8 - losses),
                    $"down-{losses}-bots"));
            }
            return names;
        }

        private List<Node> BuildStageLeaves(
            string prefix,
            string groupTag,
            ArcRelayTactic tactic,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot)
        {
            // Stage sections use phase tag "s", act sections "a"; a leaf id is
            // "t{n}-g{k}-{phase}-{kind}". Group-uniform leaves are written
            // once as shared orders and referenced by every member role —
            // eight near-identical copies would not fit the ATL2 envelope.
            var scope = new LeafScope(prefix, groupTag, group.Slots.Count, "s");
            var leaves = new List<Node>();
            AddRecovery(leaves, scope, group.Stage.Recovery);
            AddPostureFight(leaves, scope, group.Stage.Posture,
                () => OnStationCondition(prefix, groupTag, group, bot));
            AddSignatureLeaves(
                leaves, scope, group, bot, group.Stage.Posture, group.StageSigs,
                latch: null);
            // No per-leaf latches: the stage wrapper branch above carries the
            // release expression as its until for the whole section.
            AddFloor(leaves, scope, group.Stage, group, bot, latch: null);
            return leaves;
        }

        private List<Node> BuildActLeaves(
            string prefix,
            string groupTag,
            ArcRelayTactic tactic,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot)
        {
            var scope = new LeafScope(prefix, groupTag, group.Slots.Count, "a");
            var leaves = new List<Node>();
            AddRecovery(leaves, scope, group.Act.Recovery);
            // A stage-only group's continuation still fights for its ground,
            // not for every corridor on the way back to it.
            AddPostureFight(leaves, scope, group.Act.Posture,
                group.Act.Kind == "none"
                    ? () => OnStationCondition(prefix, groupTag, group, bot)
                    : null);
            AddSignatureLeaves(
                leaves, scope, group, bot, group.Act.Posture, group.ActSigs,
                latch: null);
            if (group.Act.Kind == "none")
            {
                // A stage-only group keeps its stage job after release; the
                // continuation reuses the stage geometry under the act's
                // posture so nobody wanders off.
                AddFloor(leaves, scope,
                    group.Stage with { Posture = group.Act.Posture },
                    group, bot, latch: null);
            }
            else
            {
                AddFloor(leaves, scope, group.Act, group, bot, latch: null);
            }
            return leaves;
        }

        /// <summary>
        /// Emit one group-uniform leaf: as a shared order referenced by every
        /// member role when the group can honestly consume it twice, inline
        /// for a one-bot group.
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

        private void AddRecovery(List<Node> leaves, LeafScope scope, string recovery)
        {
            if (recovery == "heal-tile")
            {
                leaves.Add(GroupLeaf(scope, "heal", () => new Node
                {
                    When = Expr.One(Hurt()),
                    Until = Expr.One(Healthy()),
                    Timeout = 16,
                    Cooldown = 18,
                    Urgency = "urgent",
                    Action = MoveToDynamic("nearest-heal-zone", posture: "rear"),
                }));
            }
            else if (recovery == "patch")
            {
                leaves.Add(GroupLeaf(scope, "patch", () => new Node
                {
                    When = Expr.One(Hurt()),
                    Until = Expr.One(Condition(
                        new ConditionShape("self-health-percent", "at-least", 60),
                        "patched-up")),
                    Timeout = 24,
                    Cooldown = 24,
                    Action = MoveToDynamic("current-position", posture: "rear"),
                }));
            }
        }

        private void AddDelivery(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTactic tactic,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot)
        {
            if (tactic.Delivery.Plan == "none")
            {
                // "None" means the job continues and banking waits for the
                // baseline banker once the tactic ends — but a persistent
                // tactic never ends, so an accidental carrier (a Core landing
                // on a bot with no grab rule) would hold it forever. The
                // persistent branch carries the same urgent bank escape the
                // organized plans get.
                if (tactic.Outcomes.Persistent)
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
            if (bot.ClassId == "relay")
            {
                int preferred = tactic.Delivery.PreferredSlots
                    .Where(slot => slot != bot.Slot)
                    .DefaultIfEmpty(-1)
                    .First();
                leaves.Add(new Node
                {
                    Id = scope.Id("pass"),
                    When = Expr.All(CarryingCore(), SignatureReady()),
                    Action = UseSkill(
                        "arc-toss",
                        writer =>
                        {
                            if (preferred >= 0)
                            {
                                writer.WriteStartObject("target");
                                writer.WriteString("role", RoleId(preferred));
                                writer.WriteEndObject();
                            }
                            else
                            {
                                writer.WriteString(
                                    "target", "nearest-ally-toward-own-reactor");
                            }
                        }),
                });
            }
            leaves.Add(GroupLeaf(scope, "bank", () => new Node
            {
                When = Expr.One(CarryingCore()),
                Urgency = "urgent",
                Action = MoveToDynamic("own-reactor"),
            }));
            if (tactic.Delivery.Plan == "escort")
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

        private void AddCoreRule(List<Node> leaves, LeafScope scope, string coreRule)
        {
            if (coreRule == "grab")
            {
                leaves.Add(GroupLeaf(scope, "grab", () => new Node
                {
                    When = Expr.All(EmptyHanded(), Condition(
                        new ConditionShape(
                            "self-nearest-visible-loose-core-distance",
                            "at-most", GrabWalkingSteps),
                        "core-in-reach")),
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
                    When = Expr.All(EmptyHanded(), Condition(
                        new ConditionShape(
                            "visible-enemy-carriers", "at-least", 1),
                        "enemy-carrier-visible")),
                    Action = Fight(["carrier", "nearest"], "advance", 8),
                }));
                leaves.Add(GroupLeaf(scope, "hunt-core", () => new Node
                {
                    When = Expr.All(EmptyHanded(), Condition(
                        new ConditionShape("known-loose-cores", "at-least", 1),
                        "loose-core-known")),
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
                "hold" => (["nearest", "weakest"], "hold", 1),
                "aggressive" => (["carrier", "weakest", "nearest"], "advance", 6),
                "steal" => (["carrier", "nearest"], "advance", 6),
                "escort" => (["nearest"], "hold", 2),
                _ => null,
            };
            if (fight is null)
                return;
            // A staged posture fights for its ground: gating on being at the
            // station keeps a fight episode from anchoring the bot short of
            // its post on the march in. Minted only when the fight leaf
            // exists — consumer honesty. Posts groups gate per bot (its own
            // post), so the leaf inlines instead of sharing.
            string? onStation = stationCondition?.Invoke();
            Node Define() => new()
            {
                When = onStation is null
                    ? Expr.All(EmptyHanded(), EnemyNearby())
                    : Expr.All(EmptyHanded(), EnemyNearby(), onStation),
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

        /// <summary>The body-scoped “I am at my station” fact: a posts group
        /// gates each bot on its own post (±1); other kinds use the group
        /// footprint. Null when the group has no stage geometry.</summary>
        private string? OnStationCondition(
            string prefix,
            string groupTag,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot)
        {
            if (group.Stage.Kind == "posts" && group.Stage.Posts is not null)
            {
                int member = group.Slots.ToList().IndexOf(bot.Slot);
                ArcRelayTacticPost post = group.Stage.Posts[member];
                int[] postRect = PadBounds(
                    [post.At[0], post.At[1], post.At[0], post.At[1]], 1);
                string postZone = Zone(
                    postRect, $"g-{prefix}-{groupTag}-post-{member}");
                return Condition(
                    new ConditionShape("self-in-zone", "equals", 1,
                        ZoneId: postZone),
                    $"c-{prefix}-{groupTag}-at-post-{member}-self");
            }
            int[]? rect = group.Stage.Kind switch
            {
                "area" => group.Stage.Rect,
                "line" => PadBounds(BoundingBox(group.Stage.Path!), 1),
                "approach" => PadBounds(
                    [
                        group.Stage.Point![0], group.Stage.Point[1],
                        group.Stage.Point[0], group.Stage.Point[1],
                    ], 2),
                _ => null,
            };
            if (rect is null)
                return null;
            string zone = Zone(rect, $"g-{prefix}-{groupTag}-set");
            return Condition(
                new ConditionShape("self-in-zone", "equals", 1, ZoneId: zone),
                $"c-{prefix}-{groupTag}-on-station");
        }

        private void AddSignatureLeaves(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot,
            string posture,
            IReadOnlyList<ArcRelayTacticSignatureMark> marks,
            Expr? latch)
        {
            string geometryPrefix = $"{scope.Prefix}-{scope.GroupTag}-{scope.PhaseTag}";
            ArcRelayTacticSignatureMark? mark = marks.FirstOrDefault(
                candidate => string.Equals(
                    candidate.ClassId, bot.ClassId, StringComparison.Ordinal));
            ArcRelaySignatureAuthoringDescriptor descriptor =
                ArcRelaySignatureAuthoringCatalog.ForClass(bot.ClassId);
            if (mark is not null && mark.Points is not null)
            {
                for (int index = 0; index < mark.Points.Count; index++)
                {
                    string anchor = Anchor(
                        mark.Points[index],
                        $"g-{geometryPrefix}-{bot.ClassId}-{index}");
                    leaves.Add(new Node
                    {
                        Id = scope.Id($"{bot.ClassId}-mark-{index}"),
                        When = Expr.One(SignatureReady()),
                        Action = UseSkill(descriptor.SkillId, writer =>
                        {
                            writer.WriteString(
                                "target", DefaultNamedTarget(descriptor));
                            writer.WriteString("at", anchor);
                        }),
                    });
                }
                return;
            }
            if (mark is not null && mark.Line is not null)
            {
                if (bot.ClassId == "longshot")
                {
                    leaves.Add(new Node
                    {
                        Id = scope.Id("rail"),
                        When = Expr.All(
                            EmptyHanded(), EnemyNearby(), SignatureReady()),
                        Action = UseSkill("rail-line", writer =>
                            writer.WriteString("target", "weakest-enemy")),
                    });
                    string stand = Anchor(
                        mark.Line[0], $"g-{geometryPrefix}-rail-post");
                    string aim = Anchor(
                        mark.Line[1], $"g-{geometryPrefix}-rail-aim");
                    leaves.Add(new Node
                    {
                        Id = scope.Id("rail-move"),
                        // A rail lane repositions the longshot; on the stage
                        // side it must die at release with the other floors.
                        Until = latch,
                        Cooldown = latch is null ? null : PhaseCooldown,
                        Action = writer =>
                        {
                            writer.WriteStartObject("move");
                            writer.WriteStartObject("to");
                            writer.WriteString("anchor", stand);
                            writer.WriteEndObject();
                            writer.WriteStartObject("face");
                            writer.WriteString("anchor", aim);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        },
                    });
                }
                else
                {
                    string route = Route(
                        mark.Line, $"g-{geometryPrefix}-dash");
                    leaves.Add(new Node
                    {
                        Id = scope.Id("dash"),
                        When = Expr.One(SignatureReady()),
                        Action = UseSkill(descriptor.SkillId, writer =>
                        {
                            writer.WriteStartObject("target");
                            writer.WriteString("route", route);
                            writer.WriteString("progress", "forward");
                            writer.WriteNumber("minimumForwardTiles", 2);
                            writer.WriteEndObject();
                        }),
                    });
                }
                return;
            }
            // No authored spatial mark for this class this phase: the posture
            // bundles an ordinary signature use when the phase engages at all.
            if (posture is not ("hold" or "aggressive" or "steal" or "escort"))
                return;
            Expr when = bot.ClassId switch
            {
                "patchbay" => Expr.One(SignatureReady()),
                "relay" => Expr.All(CarryingCore(), SignatureReady()),
                _ => Expr.All(EmptyHanded(), EnemyNearby(), SignatureReady()),
            };
            leaves.Add(new Node
            {
                Id = scope.Id("sig"),
                When = when,
                Action = UseSkill(descriptor.SkillId, writer =>
                    writer.WriteString("target", DefaultNamedTarget(descriptor))),
            });
        }

        private void AddFloor(
            List<Node> leaves,
            LeafScope scope,
            ArcRelayTacticPhase phase,
            ArcRelayTacticGroup group,
            ArcRelayTacticsSquadBot bot,
            Expr? latch)
        {
            string geometryPrefix = $"{scope.Prefix}-{scope.GroupTag}";
            string posture = phase.Posture switch
            {
                "aggressive" or "steal" => "front",
                "hold" or "escort" => "line",
                _ => "rear",
            };
            // A drawn approach route rides the Orders `via` ingress: each
            // activation walks the route once (progress-aware), then the
            // normal floor takes over. Interned by shape like every route.
            string? via = phase.Via is null
                ? null
                : Route(phase.Via, $"g-{geometryPrefix}-in");
            switch (phase.Kind)
            {
                // Posts and lines are per-bot floors (each bot owns its own
                // tile), so they stay inline; every other floor is
                // group-uniform and shares one definition.
                case "posts":
                {
                    int member = group.Slots.ToList().IndexOf(bot.Slot);
                    ArcRelayTacticPost post = phase.Posts![member];
                    string anchor = Anchor(
                        post.At, $"g-{geometryPrefix}-post-{member}");
                    int[]? faceTile = FacingTile(post);
                    string? face = faceTile is null
                        ? null
                        : Anchor(faceTile, $"g-{geometryPrefix}-face-{member}");
                    leaves.Add(Latched(latch, new Node
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
                            if (face is not null)
                            {
                                writer.WriteStartObject("face");
                                writer.WriteString("anchor", face);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndObject();
                        },
                    }));
                    break;
                }
                case "line":
                {
                    int member = group.Slots.ToList().IndexOf(bot.Slot);
                    int point = member % phase.Path!.Count;
                    string anchor = Anchor(
                        phase.Path[point], $"g-{geometryPrefix}-line-{point}");
                    leaves.Add(Latched(latch, new Node
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
                    }));
                    break;
                }
                case "area":
                {
                    string zone = Zone(phase.Rect!, $"g-{geometryPrefix}-hold");
                    leaves.Add(GroupLeaf(scope, "move", () => Latched(
                        latch, new Node
                        { Action = MoveToZone(zone, posture, via) })));
                    break;
                }
                case "approach":
                {
                    string anchor = Anchor(
                        phase.Point!, $"g-{geometryPrefix}-mark");
                    leaves.Add(GroupLeaf(scope, "move", () => Latched(
                        latch, new Node
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
                        })));
                    break;
                }
                case "converge":
                {
                    string anchor = Anchor(
                        phase.Point!, $"g-{geometryPrefix}-goal");
                    leaves.Add(GroupLeaf(scope, "move", () => Latched(
                        latch, new Node
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
                        })));
                    break;
                }
                case "advance":
                {
                    int[] last = phase.Path![^1];
                    int[] rect = PadBounds([last[0], last[1], last[0], last[1]], 1);
                    string zone = Zone(rect, $"g-{geometryPrefix}-goal");
                    string route = Route(phase.Path, $"g-{geometryPrefix}-way");
                    leaves.Add(GroupLeaf(scope, "move", () => Latched(
                        latch, new Node
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
                        })));
                    break;
                }
                case "patrol":
                {
                    string route = Route(phase.Path!, $"g-{geometryPrefix}-beat");
                    leaves.Add(GroupLeaf(scope, "move", () => Latched(
                        latch, new Node
                        {
                            Action = writer =>
                            {
                                writer.WriteStartObject("move");
                                writer.WriteString("patrol", route);
                                writer.WriteString("posture", posture);
                                writer.WriteEndObject();
                            },
                        })));
                    break;
                }
                case "return":
                {
                    leaves.Add(GroupLeaf(scope, "move", () => Latched(
                        latch, new Node
                        { Action = MoveToDynamic("own-reactor", via: via) })));
                    break;
                }
                case "escort":
                {
                    leaves.Add(GroupLeaf(scope, "move", () => Latched(
                        latch, new Node { Action = FollowCarrier() })));
                    break;
                }
            }
        }

        private static Node Latched(Expr? latch, Node node)
        {
            if (latch is not null)
            {
                node.Until = latch;
                node.Cooldown = PhaseCooldown;
            }
            return node;
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

        private static IEnumerable<int[]> GeometryPoints(ArcRelayTacticGroup group)
        {
            foreach (ArcRelayTacticPhase phase in new[] { group.Stage, group.Act })
            {
                if (phase.Posts is not null)
                {
                    foreach (ArcRelayTacticPost post in phase.Posts)
                        yield return post.At;
                }
                if (phase.Rect is not null)
                {
                    yield return [phase.Rect[0], phase.Rect[1]];
                    yield return [phase.Rect[2], phase.Rect[3]];
                }
                if (phase.Path is not null)
                {
                    foreach (int[] point in phase.Path)
                        yield return point;
                }
                if (phase.Point is not null)
                    yield return phase.Point;
            }
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

        private string SignatureReady() => Condition(
            new ConditionShape("signature-ready", "equals", 1),
            "signature-ready");

        private string Never() => Condition(
            new ConditionShape("live-friendlies", "at-least", 9), "never");

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
            string? via = null) => writer =>
        {
            writer.WriteStartObject("move");
            writer.WriteString("to", target);
            if (via is not null)
                writer.WriteString("via", via);
            if (posture is not null)
                writer.WriteString("posture", posture);
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

    /// <summary>Names one tactic/group/phase leaf family: ids read
    /// "t{n}-g{k}-{s|a}-{kind}" and are the practice-attribution contract.</summary>
    private sealed record LeafScope(
        string Prefix,
        string GroupTag,
        int GroupSize,
        string? PhaseTag)
    {
        public string Id(string kind) => PhaseTag is null
            ? $"{Prefix}-{GroupTag}-{kind}"
            : $"{Prefix}-{GroupTag}-{PhaseTag}-{kind}";
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
        string[]? Roles = null)
    {
        public string Key => string.Join('|',
            Fact, Operator, Value, Role, ZoneId, Well, FreshnessTicks,
            WindowTicks, Since,
            Roles is null ? null : string.Join(',', Roles));
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

    /// <summary>Flat condition expression: one name or an all/any of names.</summary>
    private sealed record Expr(string? Op, IReadOnlyList<string> Names)
    {
        public static Expr One(string name) => new(null, [name]);

        public static Expr All(params string[] names) =>
            names.Length == 1 ? One(names[0]) : new Expr("all", names);

        public static Expr Any(IReadOnlyList<string> names) =>
            names.Count == 1 ? One(names[0]) : new Expr("any", names);

        public void Write(Utf8JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            if (Names.Count == 1)
            {
                writer.WriteStringValue(Names[0]);
                return;
            }
            writer.WriteStartObject();
            writer.WriteStartArray(Op!);
            foreach (string name in Names)
                writer.WriteStringValue(name);
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
