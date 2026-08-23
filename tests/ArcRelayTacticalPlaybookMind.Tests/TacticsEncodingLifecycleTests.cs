using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Sdk;
using BotArena.TacticalPlaybooks;

/// <summary>
/// Proves the materialized Tactics encoding against the real squad program
/// coordinator and order-tree runtime, using the actual generated Pincer
/// bytes: the release transition needs every bot at its ground or dead
/// (exactly, per bot — no quorum), fires on any ticked trigger, latches
/// through momentary loss, outcomes count from phase entry, a finished
/// tactic goes dark and re-arms as a standing order, respawns join the
/// current phase directly, and fight, recovery, and Core behavior stay
/// inside the active phase.
/// </summary>
public sealed class TacticsEncodingLifecycleTests
{
    [Fact]
    public void ReleaseLatchesThroughTriggerAndReadinessLoss()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick == 6 || tick >= 10;

        // The early flicker at tick 6 must not release: readiness is not met.
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(6));
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(8));
        // Tick 10: ready AND triggered — the flankers converge.
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(10));
        // Trigger and readiness both lapse. Latching is structural now: the
        // coordinator never re-checks an entered phase's entry condition.
        harness.World.EnemiesAtMid = _ => false;
        harness.World.FlankersReady = _ => false;
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(11));
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(40));
    }

    [Fact]
    public void ReleaseFiresOnAnyOfSeveralTriggers()
    {
        // Two ticked release rows: enemies at Mid OR any contact. They land
        // in the transition case's any() side — either alone must release
        // once every bot is set.
        Harness harness = Harness.ForRole("vex", tactics =>
        {
            JsonObject document = JsonNode.Parse(tactics)!.AsObject();
            document["tactics"]![0]!["release"]!["conds"]!["contact"] = true;
            return document.ToJsonString();
        });
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = _ => false;
        harness.World.Contact = tick => tick >= 10;

        // Ready but neither trigger yet: still staging.
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(9));
        // Contact alone releases — the enemies row never fired.
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(10));
    }

    [Fact]
    public void OutcomesCountFromPhaseEntryNotFromStaging()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick >= 10;
        // Three confirmed destructions during staging: the guard fought at
        // Mid before the flankers were ready. They must NOT finish the
        // tactic, because the kills outcome anchors at the act phase's entry.
        harness.World.KillTicks = [3, 4, 5, 12, 13];

        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(9));
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(10));
        // Two post-release kills so far — still one short of three.
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(14));
        harness.World.KillTicks = [3, 4, 5, 12, 13, 15];
        // The third post-release kill fires the finish transition: the whole
        // tactic goes dark and the walk falls through to the baseline band.
        (string root, string leaf) = harness.FirstEligible(16);
        Assert.Equal("hold-ground", root);
        Assert.Equal("hold-ground", leaf);
    }

    [Fact]
    public void CompletedTacticGoesDarkThenMayRearmOnTheNextTick()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick >= 10;
        harness.World.KillTicks = [11, 12, 13];

        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(9));
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(10));
        (string root, string leaf) = harness.FirstEligible(14);
        Assert.NotEqual("t1", root); // completion evaluation is dark
        Assert.Equal("hold-ground", leaf);
        // There is no hidden gameplay cooldown: if nothing else owns the
        // team, the standing Tactic is eligible again on T+1 with fresh
        // phase-local state.
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(15));
    }

    [Fact]
    public void ADeadGuardExcusesItsOwnReadiness()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        // Gantry's post is empty because Gantry is down; the pair
        // any(at-ground, down) excuses it, so the release fires without him.
        harness.World.GuardAtPost = post => post == 0;
        harness.World.GuardDown = post => post == 1;
        harness.World.FlankersReady = _ => true;
        harness.World.EnemiesAtMid = _ => true;

        harness.EligibleLeaf(4); // the tactic begins; transitions wait a tick
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(5));
    }

    [Fact]
    public void ADeadMajorityStillReleasesWhenTheSurvivorsAreSet()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        // Four of the six flankers are down. Readiness is exact and per bot:
        // each dead bot excuses itself, and the two survivors standing in
        // the footprint are all that remains to demand. There is no quorum
        // to fail and no short-handed approximation anywhere.
        harness.World.FlankersReady = _ => true;
        harness.World.FlankerAt = index => index is 0 or 1;
        harness.World.FlankerDown = index => index >= 2;
        harness.World.EnemiesAtMid = _ => true;

        harness.EligibleLeaf(4);
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(5));
    }

    [Fact]
    public void ARespawnedFlankerJoinsTheCurrentPhaseDirectly()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick == 10;
        var veteran = new ActorIdentity(0, 0, 0);
        var respawn = new ActorIdentity(0, 1, 1);
        var lateWave = new ActorIdentity(0, 2, 0);

        // The veteran releases at tick 10, then trigger and readiness lapse.
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeafFor(veteran, 9));
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeafFor(veteran, 10));
        harness.World.FlankersReady = _ => false;
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeafFor(veteran, 11));
        // The phase is squad state, not a per-body gate: a fresh life simply
        // executes the active phase — no rejoin latch, nothing to bypass.
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeafFor(respawn, 12));
        harness.Prune(respawn);
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeafFor(respawn, 13));
        // Even a full wipe does not rewind the operation: the coordinator
        // owns the phase, and only finish, abort, or duration reselect. The
        // next fresh life joins the act exactly like every respawn before it.
        harness.Prune(lateWave);
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeafFor(lateWave, 14));
    }

    [Fact]
    public void ExactPostReadinessGatesOnEachAssignedGuard()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = _ => true;
        harness.World.EnemiesAtMid = _ => true;
        // Only Ember stands at its post; Gantry's post is empty. Aggregate
        // presence cannot release the tactic — each named guard must be on
        // station.
        harness.World.GuardAtPost = post => post == 0;
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(5));
        harness.World.GuardAtPost = _ => true;
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(6));
    }

    [Fact]
    public void AuthoredVeteranCountTransitionsOnlyForLiveSelectedTeamSlots()
    {
        Harness harness = Harness.ForRole("vex", tactics =>
        {
            JsonObject document = JsonNode.Parse(
                ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                    System.Text.Encoding.UTF8.GetBytes(tactics)))!.AsObject();
            document["tactics"]![0]!["phases"]![0]!["continue"]![0]!
                ["required"] = JsonNode.Parse(
                    """{"veterancy":{"level":3,"slots":[0,3],"count":2}}""");
            return document.ToJsonString();
        });
        harness.World.EnemiesAtMid = _ => true;
        // Same team-local unit id on the opponent, a dead selected veteran,
        // and an unselected friendly veteran are all insufficient.
        harness.World.Veterans =
        [
            new(new ActorIdentity(1, 0, 0), Level: 5, Live: true),
            new(new ActorIdentity(0, 0, 0), Level: 5, Live: false),
            new(new ActorIdentity(0, 2, 0), Level: 5, Live: true),
            new(new ActorIdentity(0, 3, 0), Level: 2, Live: true),
        ];

        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(4));
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(5));

        harness.World.Veterans =
        [
            new(new ActorIdentity(0, 0, 1), Level: 3, Live: true),
            new(new ActorIdentity(0, 3, 0), Level: 4, Live: true),
        ];
        Assert.Equal("t1-p2-g2-move", harness.EligibleLeaf(6));
    }

    [Fact]
    public void SupportBehaviorStaysInsideTheActivePhase()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick >= 10;

        // Staging, hurt: the stage patch leaf outranks the stage floor.
        harness.World.Hurt = tick => tick is >= 4 and <= 6;
        Assert.Equal("t1-p1-g2-patch", harness.EligibleLeaf(4));
        // Recovered: back on the floor within the same phase.
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(8));
        // Acting with a loose Core in reach: the active phase's own grab
        // leaf wins, not the baseline collector. Phase-local emission keeps
        // it composable with pinned Core custody.
        harness.World.CoreInReach = tick => tick >= 12;
        (string root, string leaf) = harness.FirstEligible(12);
        Assert.Equal("t1", root);
        Assert.Equal("t1-p2-g2-grab", leaf);
        // Carrying under a 'none' delivery plan: the tactic HOLDS the
        // carrier — the job continues and banking waits for the baseline
        // banker once the tactic ends. (Holding a Core no longer finishes
        // the phase by itself: "acquired" is the pickup EVENT since phase
        // entry, and this harness records none — the old carrier-snapshot
        // reading made an inherited Core end a phase on its second tick.)
        harness.World.CoreInReach = _ => false;
        harness.World.Carrying = tick => tick >= 14;
        (root, leaf) = harness.FirstEligible(14);
        Assert.Equal("t1", root);
        Assert.Equal("t1-p2-g2-move", leaf);
    }

    [Fact]
    public void GuardContinuationHoldsThePostsAfterRelease()
    {
        Harness harness = Harness.ForRole("ember");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick >= 10;

        Assert.Equal("t1-p1-g1-move", harness.EligibleLeaf(9));
        // The act job is 'none': the guard carries its stage ground forward
        // under the act's posture — it does not wander off.
        Assert.Equal("t1-p2-g1-move", harness.EligibleLeaf(10));
        Assert.Equal("t1-p2-g1-move", harness.EligibleLeaf(30));
    }

    [Fact]
    public void ObserveNeverVolunteersForFightWhileAggressiveDoesThenResumesGround()
    {
        Harness observe = Harness.ForRole("vex", tactics => MutateFlankerStage(
            tactics, posture: "observe", travel: "observe", recovery: "fight-on"));
        observe.World.AtStation = _ => true;
        observe.World.EnemyNearby = _ => true;
        Assert.Equal("t1-p1-g2-move", observe.EligibleLeaf(4));
        Assert.Equal("t1-p1-g2-move", observe.EligibleLeaf(5));

        Harness aggressive = Harness.ForRole("vex", tactics => MutateFlankerStage(
            tactics, posture: "aggressive", travel: "observe", recovery: "fight-on"));
        aggressive.World.AtStation = _ => true;
        aggressive.World.EnemyNearby = tick => tick == 4;
        Assert.Equal("t1-p1-g2-fight", aggressive.EligibleLeaf(4));
        Assert.Equal("t1-p1-g2-move", aggressive.EligibleLeaf(5));
    }

    [Fact]
    public void AggressiveFightLeavesItsPostWithoutDisarmingItsEpisode()
    {
        Harness harness = Harness.ForRole("vex", tactics => MutateFlankerStage(
            tactics, posture: "aggressive", travel: "observe",
            recovery: "fight-on"));
        harness.World.AtStation = tick => tick == 4;
        harness.World.EnemyNearby = tick => tick <= 5;

        Assert.Equal("t1-p1-g2-fight", harness.EligibleLeaf(4));
        // The first pursuit step leaves the station. The authored station is
        // the episode's origin/return floor, not a per-tick eligibility veto.
        Assert.Equal("t1-p1-g2-fight", harness.EligibleLeaf(5));
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(6));
    }

    [Theory]
    [InlineData("observe", "t1-p1-g2-move")]
    [InlineData("hold", "t1-p1-g2-travel-fight")]
    [InlineData("aggressive", "t1-p1-g2-travel-fight")]
    public void EveryTravelBehaviorOwnsContactExactlyAsAuthored(
        string travel,
        string expected)
    {
        Harness harness = Harness.ForRole("vex", tactics => MutateFlankerStage(
            tactics, posture: "observe", travel: travel, recovery: "fight-on"));
        harness.World.AtStation = _ => false;
        harness.World.EnemyNearby = _ => true;
        Assert.Equal(expected, harness.EligibleLeaf(4));

        // Arrival ends travel behavior; the quiet destination posture owns
        // the same visible enemy without a hidden continuation fight.
        harness.World.AtStation = _ => true;
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(5));
    }

    [Theory]
    [InlineData("fight-on", "t1-p1-g2-move")]
    [InlineData("heal-tile", "t1-p1-g2-heal")]
    [InlineData("heal-up", "t1-p1-g2-heal-up")]
    public void EveryRecoveryChoicePreemptsAndReturnsToTheSameGround(
        string recovery,
        string expected)
    {
        Harness harness = Harness.ForRole("vex", tactics => MutateFlankerStage(
            tactics, posture: "observe", travel: "observe", recovery: recovery));
        harness.World.Hurt = tick => tick == 4;
        harness.World.BelowToppedUp = tick => tick == 4;
        harness.World.ToppedUp = tick => tick > 4;
        Assert.Equal(expected, harness.EligibleLeaf(4));
        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(5));
    }

    [Fact]
    public void ACarrierStaysOnItsCustodyTaskInsteadOfSeekingRecovery()
    {
        Harness harness = Harness.ForRole("vex", tactics => MutateFlankerStage(
            tactics, posture: "observe", travel: "observe",
            recovery: "heal-tile"));
        harness.World.Hurt = _ => true;
        harness.World.Carrying = _ => true;

        Assert.Equal("t1-p1-g2-move", harness.EligibleLeaf(4));
    }

    private static string MutateFlankerStage(
        string tactics,
        string posture,
        string travel,
        string recovery)
    {
        JsonObject document = JsonNode.Parse(tactics)!.AsObject();
        JsonObject stage = document["tactics"]![0]!["groups"]![1]!["stage"]!
            .AsObject();
        stage["posture"] = posture;
        stage["travel"] = travel;
        stage["recovery"] = recovery;
        return document.ToJsonString();
    }

    /// <summary>
    /// Loads the real materialized Pincer orders (v1 fixture migrated and
    /// compiled through the real pipeline), parses the program table with
    /// the mind's own parser, converts one role's tree into runtime nodes,
    /// and evaluates conditions from a scripted world — so every scenario
    /// drives the actual TacticsProgramCoordinator and OrderTreeRuntime.
    /// </summary>
    private sealed class Harness
    {
        private readonly OrderTreeRuntime _runtime = new();
        private readonly TacticsProgramCoordinator _coordinator;
        private readonly OrdersPackage.Role _role;
        private readonly ActorIdentity _actor = new(0, 0, 0);
        private int _steppedTick = -1;
        private TacticsProgramCoordinator.View? _view;

        public ScriptedWorld World { get; } = new();

        private Harness(
            OrdersPackage.Role role,
            OrdersPackage.SquadProgram program)
        {
            _role = role;
            _coordinator = new TacticsProgramCoordinator(program);
        }

        public static Harness ForRole(
            string roleId,
            Func<string, string>? mutateTactics = null)
        {
            string root = RepositoryRoot();
            byte[] tactics = File.ReadAllBytes(Path.Combine(
                root, "tests", "BotArena.Cli.Tests", "Fixtures", "tactics",
                "pincer.tactics.json"));
            if (mutateTactics is not null)
            {
                tactics = System.Text.Encoding.UTF8.GetBytes(
                    mutateTactics(System.Text.Encoding.UTF8.GetString(tactics)));
            }
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.CompileTactics(tactics);
            using JsonDocument orders = JsonDocument.Parse(
                compilation.MaterializedOrders!);
            JsonElement role = orders.RootElement.GetProperty("roles")
                .EnumerateArray()
                .Single(value => value.GetProperty("id").GetString() == roleId);
            Dictionary<string, JsonElement> shared = orders.RootElement
                .GetProperty("sharedOrders").EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => property.Value,
                    StringComparer.Ordinal);
            OrdersPackage.OrderNode[] nodes = role.GetProperty("orders")
                .EnumerateArray()
                .Select(node => Convert(node, roleId, shared))
                .ToArray();
            return new Harness(
                new OrdersPackage.Role(roleId, ["kestrel"], ["vision"], nodes),
                OrdersPackage.ParseProgram(
                    orders.RootElement.GetProperty("program")));
        }

        public string EligibleLeaf(int tick) =>
            Eligible(_actor, tick).Leaves[0].Node.Id;

        public string EligibleLeafFor(ActorIdentity actor, int tick) =>
            Eligible(actor, tick).Leaves[0].Node.Id;

        public void Prune(params ActorIdentity[] live) =>
            _runtime.Prune(live.ToHashSet());

        public (string Root, string Leaf) FirstEligible(int tick)
        {
            OrderTreeRuntime.EligibilityResult result = Eligible(_actor, tick);
            OrderTreeRuntime.EligibleLeaf first = result.Leaves[0];
            string root = first.Node.Path.Split(" > ")[1];
            return (root, first.Node.Id);
        }

        private OrderTreeRuntime.EligibilityResult Eligible(
            ActorIdentity actor,
            int tick)
        {
            // The mind steps the coordinator exactly once per tick, before
            // any body evaluates — the harness mirrors that contract.
            if (tick != _steppedTick)
            {
                _view = _coordinator.Step(
                    tick,
                    (expression, anchor) => Evaluate(expression, tick, anchor));
                _steppedTick = tick;
            }
            return _runtime.Eligible(actor, _role, tick,
                (expression, elapsed) => Evaluate(expression, tick, elapsed),
                _view!.EnabledTags);
        }

        private OrderTreeRuntime.ConditionEvaluation Evaluate(
            OrdersPackage.ConditionExpression expression,
            int tick,
            int elapsed)
        {
            List<bool> values = expression.Names
                .Select(name => World.Truth(name, tick, elapsed))
                .ToList();
            foreach (OrdersPackage.ConditionExpression group
                in expression.Groups ?? [])
            {
                values.Add(Evaluate(group, tick, elapsed).Value);
            }
            bool value = expression.Kind switch
            {
                "all" => values.All(entry => entry),
                "any" => values.Any(entry => entry),
                _ => values[0],
            };
            string explanation = expression.Kind == "one"
                ? $"{expression.Names[0]}-{(value ? "true" : "false")}"
                : string.Join('-', expression.Names.Where(
                    (name, index) => values[index] == value).Take(2));
            return new(value, null, explanation);
        }

        private static OrdersPackage.OrderNode Convert(
            JsonElement node,
            string path,
            IReadOnlyDictionary<string, JsonElement> shared)
        {
            if (node.TryGetProperty("shared", out JsonElement reference))
            {
                string id = reference.GetString()!;
                return Convert(shared[id], path, shared, externalId: id);
            }
            return Convert(node, path, shared, externalId: null);
        }

        private static OrdersPackage.OrderNode Convert(
            JsonElement node,
            string path,
            IReadOnlyDictionary<string, JsonElement> shared,
            string? externalId)
        {
            string id = externalId
                ?? node.GetProperty("id").GetString()!;
            string nodePath = $"{path} > {id}";
            OrdersPackage.OrderNode[] children =
                node.TryGetProperty("orders", out JsonElement branch)
                    ? branch.EnumerateArray()
                        .Select(child => ConvertChild(child, nodePath, shared))
                        .ToArray()
                    : [];
            return new OrdersPackage.OrderNode(
                Id: id,
                Path: nodePath,
                When: Expression(node, "when"),
                Until: Expression(node, "until"),
                Timeout: node.TryGetProperty("timeout", out JsonElement timeout)
                    ? timeout.GetInt32()
                    : null,
                Cooldown: node.TryGetProperty("cooldown", out JsonElement cooldown)
                    ? cooldown.GetInt32()
                    : 0,
                Rejoin: node.TryGetProperty("rejoin", out JsonElement rejoin)
                    && rejoin.ValueKind == JsonValueKind.True,
                PhaseTag: node.TryGetProperty("phase", out JsonElement phaseTag)
                    ? phaseTag.GetString()
                    : null,
                Urgency: TacticalMovementUrgency.Normal,
                Capacity: null,
                Selection: null,
                Children: children,
                Action: children.Length > 0
                    ? null
                    : new OrdersPackage.MoveAction(
                        Kind: "to", TargetKind: "static",
                        Target: "current-position", Via: null, Posture: null,
                        FaceAnchor: null, FaceDirection: null,
                        WithinWalkingSteps: null,
                        CoreBankValue: null));
        }

        private static OrdersPackage.OrderNode ConvertChild(
            JsonElement node,
            string path,
            IReadOnlyDictionary<string, JsonElement> shared)
        {
            if (node.TryGetProperty("shared", out JsonElement reference))
            {
                string id = reference.GetString()!;
                return Convert(shared[id], path, shared, externalId: id);
            }
            return Convert(node, path, shared, externalId: null);
        }

        private static OrdersPackage.ConditionExpression? Expression(
            JsonElement node,
            string name)
        {
            if (!node.TryGetProperty(name, out JsonElement value))
                return null;
            if (value.ValueKind == JsonValueKind.String)
                return new OrdersPackage.ConditionExpression(
                    "one", [value.GetString()!]);
            JsonProperty combined = value.EnumerateObject().Single();
            // Mirror the mind's parser: entries are names plus at most one
            // level of nested all/any groups.
            var names = new List<string>();
            var groups = new List<OrdersPackage.ConditionExpression>();
            foreach (JsonElement entry in combined.Value.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    names.Add(entry.GetString()!);
                    continue;
                }
                JsonProperty nested = entry.EnumerateObject().Single();
                groups.Add(new OrdersPackage.ConditionExpression(
                    nested.Name,
                    nested.Value.EnumerateArray()
                        .Select(inner => inner.GetString()!).ToArray()));
            }
            return new OrdersPackage.ConditionExpression(
                combined.Name, names.ToArray(), groups.ToArray());
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(
                       Path.Combine(directory.FullName, "BotArena.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException(
                "Repository root not found.");
        }
    }

    /// <summary>Scripted condition truth for the Pincer's generated names.
    /// Readiness is per bot now: guards pair at-0/at-1 with dn-ember and
    /// dn-gantry; the six flankers pair at-0..5 with their own down
    /// excuses. FlankerAt scripts individuals; FlankersReady sweeps the
    /// group — a flanker is present when both agree.</summary>
    private sealed class ScriptedWorld
    {
        public Func<int, bool> GuardsAtPosts { get; set; } = _ => false;
        public Func<int, bool> GuardAtPost { get; set; } = _ => true;
        public Func<int, bool> GuardDown { get; set; } = _ => false;
        public Func<int, bool> FlankersReady { get; set; } = _ => false;
        public Func<int, bool> FlankerAt { get; set; } = _ => true;
        public Func<int, bool> FlankerDown { get; set; } = _ => false;
        public Func<int, bool> EnemiesAtMid { get; set; } = _ => false;
        public Func<int, bool> Contact { get; set; } = _ => false;
        public Func<int, bool> Hurt { get; set; } = _ => false;
        public Func<int, bool> AtStation { get; set; } = _ => false;
        public Func<int, bool> EnemyNearby { get; set; } = _ => false;
        public Func<int, bool> BelowToppedUp { get; set; } = _ => false;
        public Func<int, bool> ToppedUp { get; set; } = _ => true;
        public Func<int, bool> Carrying { get; set; } = _ => false;
        public Func<int, bool> CoreInReach { get; set; } = _ => false;
        public int[] KillTicks { get; set; } = [];
        public Veteran[] Veterans { get; set; } = [];

        public bool Truth(string name, int tick, int elapsed) => name switch
        {
            "c-t1-p1-formation-ready" =>
                (GuardsAtPosts(tick) && GuardAtPost(0) || GuardDown(0))
                && (GuardsAtPosts(tick) && GuardAtPost(1) || GuardDown(1))
                && Enumerable.Range(0, 6).All(index =>
                    FlankersReady(tick) && FlankerAt(index)
                    || FlankerDown(index)),
            "c-t1-g1-at-0" => GuardsAtPosts(tick) && GuardAtPost(0),
            "c-t1-g1-at-1" => GuardsAtPosts(tick) && GuardAtPost(1),
            "dn-ember" => GuardDown(0),
            "dn-gantry" => GuardDown(1),
            "c-t1-g2-at-0" => FlankersReady(tick) && FlankerAt(0),
            "c-t1-g2-at-1" => FlankersReady(tick) && FlankerAt(1),
            "c-t1-g2-at-2" => FlankersReady(tick) && FlankerAt(2),
            "c-t1-g2-at-3" => FlankersReady(tick) && FlankerAt(3),
            "c-t1-g2-at-4" => FlankersReady(tick) && FlankerAt(4),
            "c-t1-g2-at-5" => FlankersReady(tick) && FlankerAt(5),
            "dn-vex" => FlankerDown(0),
            "dn-solder" => FlankerDown(1),
            "dn-flux" => FlankerDown(2),
            "dn-ratchet" => FlankerDown(3),
            "dn-piston" => FlankerDown(4),
            "dn-cinder" => FlankerDown(5),
            "c-t1-p1-enemies" => EnemiesAtMid(tick),
            var value when value.Contains("-veterans-", StringComparison.Ordinal) =>
                Veterans.Count(veteran =>
                    veteran.Live
                    && veteran.Actor.TeamId == 0
                    && veteran.Actor.UnitId is 0 or 3
                    && veteran.Level >= 3) >= 2,
            "enemy-contact" => Contact(tick),
            // Historical evidence anchored at phase entry: the coordinator
            // passes ticks-in-phase, so only kills inside that window count
            // — this is exactly the counts-from-phase-entry contract.
            "c-t1-kills" => KillTicks.Count(
                kill => kill > tick - elapsed && kill <= tick) >= 3,
            "c-t1-cleared" => false,
            "we-hold-a-core" => Carrying(tick),
            "carrying-core" => Carrying(tick),
            "empty-handed" => !Carrying(tick),
            "hurt" => Hurt(tick),
            "healthy" => !Hurt(tick),
            "patched-up" => !Hurt(tick),
            "enemy-nearby" => EnemyNearby(tick),
            "enemy-no-longer-nearby" => !EnemyNearby(tick),
            var value when value.Contains("-on-station", StringComparison.Ordinal) =>
                AtStation(tick),
            var value when value.Contains("-off-station", StringComparison.Ordinal) =>
                !AtStation(tick),
            "below-topped-up" => BelowToppedUp(tick),
            "topped-up" => ToppedUp(tick),
            "signature-ready" => false,
            "core-in-reach" => CoreInReach(tick),
            "loose-core-known" => CoreInReach(tick),
            _ => false,
        };

        public sealed record Veteran(ActorIdentity Actor, int Level, bool Live);
    }
}
