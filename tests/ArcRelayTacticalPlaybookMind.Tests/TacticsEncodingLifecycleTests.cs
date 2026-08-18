using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Sdk;
using BotArena.TacticalPlaybooks;

/// <summary>
/// Proves the materialized Tactics encoding against the real order-tree
/// lifecycle, using the actual generated Pincer bytes: release latches
/// through momentary trigger loss, outcomes count from shared release rather
/// than stage start, exact-post readiness gates on each assigned bot, a fired
/// outcome ends the whole tactic and yields to the next root, and fight,
/// recovery, and Core behavior stay inside the active phase.
/// </summary>
public sealed class TacticsEncodingLifecycleTests
{
    // Scenario clock: guards post up early, flankers assemble by tick 8, the
    // enemies trigger flickers at tick 6, holds from tick 10, and release
    // therefore happens exactly at tick 10.
    private const int ReleaseTick = 10;

    [Fact]
    public void ReleaseLatchesThroughTriggerAndReadinessLoss()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick == 6 || tick >= 10;

        // The early flicker at tick 6 must not release: readiness is not met.
        Assert.Equal("t1-g2-s-move", harness.EligibleLeaf(6));
        Assert.Equal("t1-g2-s-move", harness.EligibleLeaf(8));
        // Tick 10: ready AND triggered — the flankers converge.
        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(10));
        // Trigger and readiness both lapse; the active release branch has an
        // authored until, so its when is never re-consulted: still acting.
        harness.World.EnemiesAtMid = _ => false;
        harness.World.FlankersReady = _ => false;
        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(11));
        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(40));
    }

    [Fact]
    public void ReleaseFiresOnAnyOfSeveralTriggers()
    {
        // Two ticked release rows: enemies at Mid OR any contact. The gate
        // materializes as all(readiness…, any(rows…)) — one nested group —
        // and either trigger alone must release once everyone is ready.
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
        Assert.Equal("t1-g2-s-move", harness.EligibleLeaf(9));
        // Contact alone releases — the enemies row never fired.
        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(10));
    }

    [Fact]
    public void OutcomesCountFromReleaseNotFromStaging()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick >= 10;
        // Three confirmed destructions during staging: the guard fought at
        // Mid before the flankers were ready. They must NOT finish the
        // tactic, because the kills outcome anchors at the release branch.
        harness.World.KillTicks = [3, 4, 5, 12, 13];

        Assert.Equal("t1-g2-s-move", harness.EligibleLeaf(9));
        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(10));
        // Two post-release kills so far — still one short of three.
        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(14));
        harness.World.KillTicks = [3, 4, 5, 12, 13, 15];
        // The third post-release kill fires the outcome: the release branch
        // exits on its until, the tactic stops yielding, and the walk falls
        // through to the baseline band.
        (string root, string leaf) = harness.FirstEligible(16);
        Assert.Equal("hold-ground", root);
        Assert.Equal("hold-ground", leaf);
    }

    [Fact]
    public void CompletedTacticYieldsToTheNextRootAndStaysDown()
    {
        Harness harness = Harness.ForRole("vex");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick >= 10;
        harness.World.KillTicks = [11, 12, 13];

        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(10));
        harness.EligibleLeaf(14); // outcome fires: release branch exits
        // From then on the tactic yields nothing: the stage wrapper died at
        // release and the release branch is on cooldown, so the walk falls
        // through to the next root — the baseline band.
        for (int tick = 15; tick < 60; tick += 9)
        {
            (string root, string leaf) = harness.FirstEligible(tick);
            Assert.NotEqual("t1", root);
            Assert.Equal("hold-ground", leaf);
        }
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
        Assert.Equal("t1-g2-s-move", harness.EligibleLeaf(5));
        harness.World.GuardAtPost = _ => true;
        Assert.Equal("t1-g2-a-move", harness.EligibleLeaf(6));
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
        Assert.Equal("t1-g2-s-patch", harness.EligibleLeaf(4));
        // Recovered: back on the floor within the same tactic.
        Assert.Equal("t1-g2-s-move", harness.EligibleLeaf(8));
        // Acting with a loose Core in reach: the tactic's own grab leaf wins
        // (it sits above the release branch), not the baseline collector.
        harness.World.CoreInReach = tick => tick >= 12;
        (string root, string leaf) = harness.FirstEligible(12);
        Assert.Equal("t1", root);
        Assert.Equal("t1-g2-grab", leaf);
        // Carrying: every tactic leaf gates on empty-handed, so the carrier
        // falls through the whole tactic to the baseline banker.
        harness.World.CoreInReach = _ => false;
        harness.World.Carrying = tick => tick >= 14;
        (root, leaf) = harness.FirstEligible(14);
        Assert.Equal("score-a-core", root);
        Assert.Equal("return-to-reactor", leaf);
    }

    [Fact]
    public void GuardContinuationHoldsThePostsAfterRelease()
    {
        Harness harness = Harness.ForRole("ember");
        harness.World.GuardsAtPosts = _ => true;
        harness.World.FlankersReady = tick => tick >= 8;
        harness.World.EnemiesAtMid = tick => tick >= 10;

        Assert.Equal("t1-g1-s-move", harness.EligibleLeaf(9));
        // After release the stage wrapper is dead, but the act continuation
        // repeats the post assignment: the guard does not wander off.
        Assert.Equal("t1-g1-a-move", harness.EligibleLeaf(10));
        Assert.Equal("t1-g1-a-move", harness.EligibleLeaf(30));
    }

    /// <summary>
    /// Loads the real materialized Pincer orders, converts one role's tree
    /// into runtime nodes (ids, expressions, timeout/cooldown, and structure
    /// verbatim; leaf actions stubbed), and evaluates conditions from a
    /// scripted world so every scenario drives the actual OrderTreeRuntime.
    /// </summary>
    private sealed class Harness
    {
        private readonly OrderTreeRuntime _runtime = new();
        private readonly OrdersPackage.Role _role;
        private readonly ActorIdentity _actor = new(0, 0, 0);

        public ScriptedWorld World { get; } = new();

        private Harness(OrdersPackage.Role role) => _role = role;

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
            return new Harness(new OrdersPackage.Role(
                roleId, ["kestrel"], ["vision"], nodes));
        }

        public string EligibleLeaf(int tick) =>
            Eligible(tick).Leaves[0].Node.Id;

        public (string Root, string Leaf) FirstEligible(int tick)
        {
            OrderTreeRuntime.EligibilityResult result = Eligible(tick);
            OrderTreeRuntime.EligibleLeaf first = result.Leaves[0];
            string root = first.Node.Path.Split(" > ")[1];
            return (root, first.Node.Id);
        }

        private OrderTreeRuntime.EligibilityResult Eligible(int tick) =>
            _runtime.Eligible(_actor, _role, tick,
                (expression, elapsed) => Evaluate(expression, tick, elapsed));

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
                Urgency: TacticalMovementUrgency.Normal,
                Capacity: null,
                Selection: null,
                Children: children,
                Action: children.Length > 0
                    ? null
                    : new OrdersPackage.MoveAction(
                        Kind: "to", TargetKind: "static",
                        Target: "current-position", Via: null, Posture: null,
                        FaceAnchor: null, WithinWalkingSteps: null,
                        CoreBankValue: null));
        }

        private static OrdersPackage.OrderNode ConvertChild(
            JsonElement node,
            string parentPath,
            IReadOnlyDictionary<string, JsonElement> shared)
        {
            if (node.TryGetProperty("shared", out JsonElement reference))
            {
                string id = reference.GetString()!;
                return Convert(shared[id], parentPath, shared, externalId: id);
            }
            return Convert(node, parentPath, shared, externalId: null);
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

    /// <summary>Scripted condition truth for the Pincer's generated names.</summary>
    private sealed class ScriptedWorld
    {
        public Func<int, bool> GuardsAtPosts { get; set; } = _ => false;
        public Func<int, bool> GuardAtPost { get; set; } = _ => true;
        public Func<int, bool> FlankersReady { get; set; } = _ => false;
        public Func<int, bool> EnemiesAtMid { get; set; } = _ => false;
        public Func<int, bool> Contact { get; set; } = _ => false;
        public Func<int, bool> Hurt { get; set; } = _ => false;
        public Func<int, bool> Carrying { get; set; } = _ => false;
        public Func<int, bool> CoreInReach { get; set; } = _ => false;
        public int[] KillTicks { get; set; } = [];

        public bool Truth(string name, int tick, int elapsed) => name switch
        {
            "c-t1-g1-at-post-0" => GuardsAtPosts(tick) && GuardAtPost(0),
            "c-t1-g1-at-post-1" => GuardsAtPosts(tick) && GuardAtPost(1),
            "c-t1-g2-ready" => FlankersReady(tick),
            "c-t1-release-enemies" => EnemiesAtMid(tick),
            "enemy-contact" => Contact(tick),
            // Historical evidence with since node-entry: the runtime passes
            // the node's elapsed ticks, so only kills inside that window
            // count — this is exactly the counts-from-release contract.
            "c-t1-kills" => KillTicks.Count(
                kill => kill > tick - elapsed && kill <= tick) >= 3,
            "c-t1-cleared" => false,
            "we-hold-a-core" => Carrying(tick),
            "carrying-core" => Carrying(tick),
            "empty-handed" => !Carrying(tick),
            "hurt" => Hurt(tick),
            "healthy" => !Hurt(tick),
            "patched-up" => !Hurt(tick),
            "enemy-nearby" => false,
            "signature-ready" => false,
            "core-in-reach" => CoreInReach(tick),
            "loose-core-known" => CoreInReach(tick),
            _ => false,
        };
    }
}
