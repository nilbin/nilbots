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

    private static OrdersPackage.OrderNode Node(
        string id,
        OrdersPackage.ConditionExpression? when = null,
        OrdersPackage.ConditionExpression? until = null,
        OrdersPackage.OrderNode[]? children = null,
        OrdersPackage.OrderAction? action = null) => new(
            Id: id,
            Path: id,
            When: when,
            Until: until,
            Timeout: null,
            Cooldown: 0,
            Urgency: TacticalMovementUrgency.Normal,
            Capacity: null,
            Selection: null,
            Children: children ?? [],
            Action: action);
}
