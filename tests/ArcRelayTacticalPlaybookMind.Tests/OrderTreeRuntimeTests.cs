using BotArena.Sdk;

public sealed class OrderTreeRuntimeTests
{
    [Fact]
    public void DurableParentKeepsItsProgramEpisodeAfterTheEntryTriggerEnds()
    {
        var runtime = new OrderTreeRuntime();
        ActorIdentity actor = new(0, 0, 0);
        var parent = Node(
            "heist-program",
            when: new("one", ["heist-trigger"]),
            until: new("one", ["acquired-core-resolved"]),
            children:
            [
                Node("heist-local-custody", action: new OrdersPackage.MoveAction(
                    Kind: "to",
                    TargetKind: "static",
                    Target: "own-reactor",
                    Via: null,
                    Posture: null,
                    FaceAnchor: null,
                    WithinWalkingSteps: null,
                    CoreBankValue: null))
            ]);
        var role = new OrdersPackage.Role(
            "runner", ["relay"], ["reach"], [parent]);

        OrderTreeRuntime.ConditionEvaluation EvaluateAt(
            int tick,
            OrdersPackage.ConditionExpression expression,
            int _)
        {
            string condition = Assert.Single(expression.Names);
            bool value = condition switch
            {
                "heist-trigger" => tick == 0,
                "acquired-core-resolved" => tick >= 2,
                _ => throw new InvalidOperationException(condition),
            };
            return new(value, null, $"{condition}-{value.ToString().ToLowerInvariant()}");
        }

        OrderTreeRuntime.EligibilityResult opened = runtime.Eligible(
            actor, role, 0, (expression, elapsed) => EvaluateAt(0, expression, elapsed));
        Assert.Equal("heist-local-custody", Assert.Single(opened.Leaves).Node.Id);

        // The entry trigger is now false. Because the durable parent has an
        // authored outcome, its active episode remains latched and its local
        // custody Instruction is still eligible.
        OrderTreeRuntime.EligibilityResult committed = runtime.Eligible(
            actor, role, 1, (expression, elapsed) => EvaluateAt(1, expression, elapsed));
        Assert.Equal("heist-local-custody", Assert.Single(committed.Leaves).Node.Id);

        OrderTreeRuntime.EligibilityResult banked = runtime.Eligible(
            actor, role, 2, (expression, elapsed) => EvaluateAt(2, expression, elapsed));
        Assert.Empty(banked.Leaves);
        Assert.Contains("until-acquired-core-resolved-true",
            Assert.Single(banked.IneligibleRoots).Reason);
    }

    [Fact]
    public void CarrierRecoveryFinishesThenResumesTheSamePremiumReturn()
    {
        var runtime = new OrderTreeRuntime();
        ActorIdentity actor = new(0, 0, 0);
        var recovery = Node(
            "premium-carrier-recover",
            when: new("one", ["carrier-hurt"]),
            until: new("one", ["carrier-recovered"]),
            action: new OrdersPackage.MoveAction(
                Kind: "to", TargetKind: "dynamic",
                Target: "nearest-heal-zone", Via: null, Posture: null,
                FaceAnchor: null, WithinWalkingSteps: 8,
                CoreBankValue: null));
        var returnHome = Node(
            "premium-carrier-return",
            action: new OrdersPackage.MoveAction(
                Kind: "to", TargetKind: "dynamic",
                Target: "own-reactor", Via: "premium-safe-return",
                Posture: null, FaceAnchor: null, WithinWalkingSteps: null,
                CoreBankValue: null));
        var custody = Node(
            "premium-four-carrying",
            when: new("one", ["self-carrying"]),
            until: new("one", ["self-empty"]),
            children: [recovery, returnHome]);
        var parent = Node(
            "premium-heist-program",
            when: new("one", ["heist-trigger"]),
            until: new("one", ["same-core-banked"]),
            children: [custody]);
        var role = new OrdersPackage.Role(
            "couriers", ["relay", "relay"], ["reach"], [parent]);

        OrderTreeRuntime.ConditionEvaluation Evaluate(
            int tick,
            OrdersPackage.ConditionExpression expression,
            int _)
        {
            string condition = Assert.Single(expression.Names);
            bool value = condition switch
            {
                "heist-trigger" => tick == 0,
                "same-core-banked" => false,
                "self-carrying" => true,
                "self-empty" => false,
                "carrier-hurt" => tick == 0,
                "carrier-recovered" => tick >= 2,
                _ => throw new InvalidOperationException(condition),
            };
            return new(value, null,
                $"{condition}-{value.ToString().ToLowerInvariant()}");
        }

        Assert.Equal("premium-carrier-recover", runtime.Eligible(
            actor, role, 0, (expression, elapsed) =>
                Evaluate(0, expression, elapsed)).Leaves[0].Node.Id);
        Assert.Equal("premium-carrier-recover", runtime.Eligible(
            actor, role, 1, (expression, elapsed) =>
                Evaluate(1, expression, elapsed)).Leaves[0].Node.Id);
        Assert.Equal("premium-carrier-return", Assert.Single(runtime.Eligible(
            actor, role, 2, (expression, elapsed) =>
                Evaluate(2, expression, elapsed)).Leaves).Node.Id);
    }

    [Fact]
    public void ScopedPassForwardIncludesAnotherBodyInTheSameCarrierRole()
    {
        var handoff = new OrdersPackage.HandoffAction(
            "selector", "nearest-ally-toward-own-reactor", ["couriers"]);

        Assert.True(ArcRelayTacticalPlaybookMind.HandoffAllowsRole(
            handoff, "couriers"));
        Assert.False(ArcRelayTacticalPlaybookMind.HandoffAllowsRole(
            handoff, "support"));
    }

    [Fact]
    public void APhaseTaggedNodeIsDarkOutsideTheActiveTags()
    {
        var runtime = new OrderTreeRuntime();
        ActorIdentity actor = new(0, 0, 0);
        var role = new OrdersPackage.Role(
            "vex", ["kestrel"], ["vision"],
            [Node("t1-p1-move", phase: "t1/p1", action: Move())]);

        OrderTreeRuntime.EligibilityResult dark = runtime.Eligible(
            actor, role, 0, AlwaysTrue,
            new HashSet<string> { "t1", "t1/p2" });
        Assert.Empty(dark.Leaves);
        Assert.Equal("phase-dark", Assert.Single(dark.IneligibleRoots).Reason);

        OrderTreeRuntime.EligibilityResult lit = runtime.Eligible(
            actor, role, 1, AlwaysTrue,
            new HashSet<string> { "t1", "t1/p1" });
        Assert.Equal("t1-p1-move", Assert.Single(lit.Leaves).Node.Id);
    }

    [Fact]
    public void PhaseExitClearsPerBodyStateWithoutACooldown()
    {
        // A phase-tagged branch with a steep cooldown: leaving the phase must
        // evaporate the state rather than exit it, so a fresh run of the same
        // tactic re-enters immediately instead of resuming or waiting.
        var runtime = new OrderTreeRuntime();
        ActorIdentity actor = new(0, 0, 0);
        var role = new OrdersPackage.Role(
            "vex", ["kestrel"], ["vision"],
            [Node("t1-p1-hold", phase: "t1/p1", cooldown: 120,
                children: [Node("t1-p1-move", action: Move())])]);
        var phaseOne = new HashSet<string> { "t1", "t1/p1" };
        var phaseTwo = new HashSet<string> { "t1", "t1/p2" };

        Assert.Single(runtime.Eligible(actor, role, 0, AlwaysTrue, phaseOne)
            .Leaves);
        Assert.Empty(runtime.Eligible(actor, role, 1, AlwaysTrue, phaseTwo)
            .Leaves);
        // Tactic restarted: phase one lights again two ticks later — a
        // 120-tick exit cooldown would hold this hostage.
        Assert.Single(runtime.Eligible(actor, role, 3, AlwaysTrue, phaseOne)
            .Leaves);
    }

    [Fact]
    public void AFreshActorJoinsTheActivePhaseDirectly()
    {
        // A respawn under the program model needs no rejoin latch: the
        // active phase's nodes are lit for every body, and a fresh life
        // simply evaluates them.
        var runtime = new OrderTreeRuntime();
        var role = new OrdersPackage.Role(
            "vex", ["kestrel"], ["vision"],
            [
                Node("t1-p1-move", phase: "t1/p1", action: Move()),
                Node("t1-p3-move", phase: "t1/p3", action: Move()),
            ]);
        var phaseThree = new HashSet<string> { "t1", "t1/p3" };

        ActorIdentity veteran = new(0, 0, 0);
        Assert.Equal("t1-p3-move", Assert.Single(
            runtime.Eligible(veteran, role, 40, AlwaysTrue, phaseThree)
                .Leaves).Node.Id);
        runtime.Prune(new HashSet<ActorIdentity> { new(0, 0, 1) });
        ActorIdentity respawn = new(0, 0, 1);
        Assert.Equal("t1-p3-move", Assert.Single(
            runtime.Eligible(respawn, role, 41, AlwaysTrue, phaseThree)
                .Leaves).Node.Id);
    }

    [Fact]
    public void ANullTagSetLeavesTaggedNodesEnabled()
    {
        // Legacy sheets pass no tag set at all; even a tagged node (which a
        // v2 sheet cannot express) stays enabled rather than dark.
        var runtime = new OrderTreeRuntime();
        ActorIdentity actor = new(0, 0, 0);
        var role = new OrdersPackage.Role(
            "vex", ["kestrel"], ["vision"],
            [Node("t1-p1-move", phase: "t1/p1", action: Move())]);

        Assert.Single(runtime.Eligible(actor, role, 0, AlwaysTrue).Leaves);
    }

    private static OrderTreeRuntime.ConditionEvaluation AlwaysTrue(
        OrdersPackage.ConditionExpression expression,
        int elapsed) => OrderTreeRuntime.ConditionEvaluation.True;

    private static OrdersPackage.MoveAction Move() => new(
        Kind: "to",
        TargetKind: "static",
        Target: "current-position",
        Via: null,
        Posture: null,
        FaceAnchor: null,
        WithinWalkingSteps: null,
        CoreBankValue: null);

    private static OrdersPackage.OrderNode Node(
        string id,
        OrdersPackage.ConditionExpression? when = null,
        OrdersPackage.ConditionExpression? until = null,
        OrdersPackage.OrderNode[]? children = null,
        OrdersPackage.OrderAction? action = null,
        string? phase = null,
        int cooldown = 0) => new(
            Id: id,
            Path: id,
            When: when,
            Until: until,
            Timeout: null,
            Cooldown: cooldown,
            Rejoin: false,
            PhaseTag: phase,
            Urgency: TacticalMovementUrgency.Normal,
            Capacity: null,
            Selection: null,
            Children: children ?? [],
            Action: action);
}
