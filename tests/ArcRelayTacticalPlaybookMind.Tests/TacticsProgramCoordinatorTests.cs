/// <summary>
/// The squad program state machine against the spec's lifecycle rules:
/// ordered selection that latches, forward-only single transitions that never
/// fire on the entry tick and never reverse, per-case jump/finish targets,
/// tactic-wide abort and duration with rearm gating, same-tick reevaluation
/// after a terminal event, and phase-entry anchor ticks for evidence.
/// </summary>
public sealed class TacticsProgramCoordinatorTests
{
    private readonly HashSet<string> _facts = [];
    private readonly List<(string Name, int Anchor)> _asked = [];

    [Fact]
    public void SelectsTheFirstEligibleTacticAndNeverShadowsIt()
    {
        var coordinator = new TacticsProgramCoordinator(Program(
            Tactic("t1", Starts("alpha"), Phase("p1")),
            Tactic("t2", null, Phase("p1"))));

        TacticsProgramCoordinator.View view = Step(coordinator, 0);
        Assert.Equal("begin-t2", view.Marker);
        Assert.Contains("t2/p1", view.EnabledTags);
        Assert.Contains("t2", view.EnabledTags);

        // The higher-priority tactic becomes eligible mid-run: it must not
        // shadow, pause, or terminate the active operation.
        _facts.Add("alpha");
        view = Step(coordinator, 1);
        Assert.Null(view.Marker);
        Assert.Equal("t2", view.TacticId);
    }

    [Fact]
    public void AdvancesOnePhasePerTickAndNeverOnTheEntryTick()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            null,
            Phase("p1", Go("next", "go1")),
            Phase("p2", Go("next", "go2")),
            Phase("p3"))));
        _facts.Add("go1");
        _facts.Add("go2");

        // Entry tick: selected, but no transition even though go1 holds.
        TacticsProgramCoordinator.View view = Step(coordinator, 0);
        Assert.Equal("begin-t1", view.Marker);
        Assert.Equal("p1", view.PhaseId);
        // One transition per tick, even with both rules true.
        view = Step(coordinator, 1);
        Assert.Equal("go-p2[c1]", view.Marker);
        Assert.Equal("p2", view.PhaseId);
        view = Step(coordinator, 2);
        Assert.Equal("go-p3[c1]", view.Marker);
        Assert.Contains("t1/p3", view.EnabledTags);
    }

    [Fact]
    public void PickupSnapshotNamesThePhaseBeforeThisTicksTransition()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            null,
            new OrdersPackage.ProgramPhase(
                "p1", "p1", [Go("next", "picked")], [[3, 4]]),
            new OrdersPackage.ProgramPhase(
                "p2", "p2", [], [[5, 6]]))));
        Step(coordinator, 0);
        _facts.Add("picked");

        TacticsProgramCoordinator.View view = Step(coordinator, 1);

        Assert.Equal("p2", view.PhaseId);
        Assert.Equal("p1", view.PickupPhaseId);
        Assert.Equal([3, 4], Assert.Single(view.PickupCustodyGroups));
    }

    [Fact]
    public void MomentaryConditionLossNeverReversesAPhase()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            Starts("open"),
            Phase("p1", Go("next", "set")),
            Phase("p2", Go("finish", "banked")))));
        _facts.Add("open");
        _facts.Add("set");
        Step(coordinator, 0);
        Assert.Equal("p2", Step(coordinator, 1).PhaseId);

        // Start and transition conditions all lapse: latched, twice over.
        _facts.Clear();
        TacticsProgramCoordinator.View view = Step(coordinator, 30);
        Assert.Null(view.Marker);
        Assert.Equal("t1", view.TacticId);
        Assert.Equal("p2", view.PhaseId);
    }

    [Fact]
    public void ACaseMayJumpForwardToANamedLaterPhase()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            null,
            Phase("p1", Go("p3", "shortcut"), Go("next", "slow")),
            Phase("p2"),
            Phase("p3"))));
        _facts.Add("shortcut");
        _facts.Add("slow");
        Step(coordinator, 0);

        // Cases evaluate in authored order; the first true one names p3.
        TacticsProgramCoordinator.View view = Step(coordinator, 1);
        Assert.Equal("go-p3[c1]", view.Marker);
        Assert.Equal("p3", view.PhaseId);
    }

    [Fact]
    public void FinishReevaluatesTheOrderedListTheSameTick()
    {
        var coordinator = new TacticsProgramCoordinator(Program(
            Tactic("t1", null, Phase("p1", Go("finish", "won"))),
            Tactic("t2", null, Phase("p1"))));
        Step(coordinator, 0);

        _facts.Add("won");
        TacticsProgramCoordinator.View view = Step(coordinator, 1);
        Assert.Equal("finish-t1[c1]+begin-t2", view.Marker);
        Assert.Contains("t2/p1", view.EnabledTags);
        Assert.DoesNotContain("t1/p1", view.EnabledTags);
    }

    [Fact]
    public void AFinishedTacticRestartsFromPhaseOneWhenStillFirstEligible()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            null,
            Phase("p1", Go("next", "set")),
            Phase("p2", Go("finish", "banked")))));
        _facts.Add("set");
        Step(coordinator, 0);
        Step(coordinator, 1);
        _facts.Add("banked");
        TacticsProgramCoordinator.View view = Step(coordinator, 2);
        // Rearm 0 still keeps the ended run off its own final tick, so the
        // dark pass can clear per-body state before a fresh activation.
        Assert.Equal("finish-t1[c1]", view.Marker);
        Assert.Empty(view.EnabledTags);

        _facts.Remove("banked");
        view = Step(coordinator, 3);
        Assert.Equal("begin-t1", view.Marker);
        Assert.Equal("p1", view.PhaseId);
    }

    [Fact]
    public void AbortEndsAnyPhaseAndRearmDelaysTheRestart()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            null,
            Aborts("mauled"),
            maxTicks: null,
            rearm: 5,
            Phase("p1", Go("next", "set")),
            Phase("p2"))));
        _facts.Add("set");
        Step(coordinator, 0);
        Assert.Equal("p2", Step(coordinator, 1).PhaseId);

        _facts.Add("mauled");
        TacticsProgramCoordinator.View view = Step(coordinator, 2);
        Assert.Equal("abort-t1[c1]", view.Marker);
        Assert.Empty(view.EnabledTags);

        // Rearm gates reselection until tick 2 + 1 + 5.
        _facts.Remove("mauled");
        for (int tick = 3; tick < 8; tick++)
            Assert.Null(Step(coordinator, tick).Marker);
        view = Step(coordinator, 8);
        Assert.Equal("begin-t1", view.Marker);
        Assert.Equal("p1", view.PhaseId);
    }

    [Fact]
    public void AbortOutranksATransitionOnTheSameTick()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            null,
            Aborts("mauled"),
            maxTicks: null,
            rearm: 0,
            Phase("p1", Go("next", "set")),
            Phase("p2"))));
        Step(coordinator, 0);
        _facts.Add("set");
        _facts.Add("mauled");

        TacticsProgramCoordinator.View view = Step(coordinator, 1);
        Assert.Equal("abort-t1[c1]", view.Marker);
        Assert.Null(view.PhaseId);
    }

    [Fact]
    public void MaxTicksExpiresTheTacticFromItsEntryTick()
    {
        var coordinator = new TacticsProgramCoordinator(Program(
            Tactic("t1", null, null, maxTicks: 3, rearm: 0, Phase("p1")),
            Tactic("t2", null, Phase("p1"))));
        Step(coordinator, 4);
        Assert.Null(Step(coordinator, 6).Marker);

        TacticsProgramCoordinator.View view = Step(coordinator, 7);
        Assert.Equal("max-duration-t1+begin-t2", view.Marker);
        Assert.Equal("t2", view.TacticId);
    }

    [Fact]
    public void TransitionEvaluationCarriesTicksSincePhaseEntry()
    {
        var coordinator = new TacticsProgramCoordinator(Program(Tactic(
            "t1",
            null,
            Phase("p1", Go("next", "set")),
            Phase("p2", Go("finish", "banked")))));
        Step(coordinator, 0);
        _facts.Add("set");
        Step(coordinator, 5);

        // p2 entered at tick 5: at tick 9 its evidence anchor is 4 ticks —
        // the "outcomes count from Phase entry" contract.
        _asked.Clear();
        Step(coordinator, 9);
        Assert.Contains(("banked", 4), _asked);
    }

    [Fact]
    public void NoEligibleTacticMeansAnEmptyEnabledSet()
    {
        var coordinator = new TacticsProgramCoordinator(Program(
            Tactic("t1", Starts("alpha"), Phase("p1"))));

        TacticsProgramCoordinator.View view = Step(coordinator, 0);
        Assert.Null(view.TacticId);
        Assert.Null(view.Marker);
        Assert.Empty(view.EnabledTags);
        Assert.Equal(0, view.PhaseTicks);
    }

    private TacticsProgramCoordinator.View Step(
        TacticsProgramCoordinator coordinator,
        int tick) => coordinator.Step(tick, Evaluate);

    private OrderTreeRuntime.ConditionEvaluation Evaluate(
        OrdersPackage.ConditionExpression expression,
        int anchorTicks)
    {
        var values = new List<bool>();
        foreach (string name in expression.Names)
        {
            _asked.Add((name, anchorTicks));
            values.Add(_facts.Contains(name));
        }
        foreach (OrdersPackage.ConditionExpression group
            in expression.Groups ?? [])
        {
            values.Add(Evaluate(group, anchorTicks).Value);
        }
        bool value = expression.Kind == "any"
            ? values.Any(entry => entry)
            : values.All(entry => entry);
        return new(value, null, null);
    }

    private static OrdersPackage.SquadProgram Program(
        params OrdersPackage.ProgramTactic[] tactics) => new(tactics);

    private static OrdersPackage.ProgramTactic Tactic(
        string id,
        OrdersPackage.ProgramCase[]? start,
        params OrdersPackage.ProgramPhase[] phases) =>
        Tactic(id, start, null, maxTicks: null, rearm: 0, phases);

    private static OrdersPackage.ProgramTactic Tactic(
        string id,
        OrdersPackage.ProgramCase[]? start,
        OrdersPackage.ProgramCase[]? abort,
        int? maxTicks,
        int rearm,
        params OrdersPackage.ProgramPhase[] phases) =>
        new(id, id, start, abort, maxTicks, rearm, phases);

    private static OrdersPackage.ProgramPhase Phase(
        string id,
        params OrdersPackage.TransitionRule[] rules) => new(id, id, rules);

    private static OrdersPackage.TransitionRule Go(
        string to,
        params string[] requiredAll) => new(Case(requiredAll), to);

    private static OrdersPackage.ProgramCase[] Starts(params string[] all) =>
        [Case(all)];

    private static OrdersPackage.ProgramCase[] Aborts(params string[] all) =>
        [Case(all)];

    private static OrdersPackage.ProgramCase Case(string[] all) =>
        new(new OrdersPackage.ConditionExpression("all", all, []), null);
}
