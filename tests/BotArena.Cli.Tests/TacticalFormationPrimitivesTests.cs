using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalFormationPrimitivesTests
{
    [Fact]
    public void FormationOrientationSelectsAnExecutableFacingTarget()
    {
        var body = new Position(5, 5);
        var movement = new Position(7, 5);
        var own = new Position(1, 5);
        var enemy = new Position(9, 5);
        var focus = new Position(6, 3);

        Assert.Equal(movement, TacticalFormationPrimitives.FacingTarget(
            "route", body, movement, own, enemy, null));
        Assert.Equal(enemy, TacticalFormationPrimitives.FacingTarget(
            "enemy-reactor", body, movement, own, enemy, null));
        Assert.Equal(own, TacticalFormationPrimitives.FacingTarget(
            "own-reactor", body, movement, own, enemy, null));
        Assert.Equal(focus, TacticalFormationPrimitives.FacingTarget(
            "focus-target", body, movement, own, enemy, focus));
        Assert.Equal(focus, TacticalFormationPrimitives.FacingTarget(
            "enemy-reactor", body, movement, own, enemy, focus));
        Assert.Null(TacticalFormationPrimitives.FacingTarget(
            "fixed", body, movement, own, enemy, focus));
        Assert.Null(TacticalFormationPrimitives.FacingTarget(
            "route", body, body, own, enemy, null));
        Assert.Null(TacticalFormationPrimitives.FacingTarget(
            "focus-target", body, movement, own, enemy, null));
    }

    [Fact]
    public void UnknownFormationOrientationFailsClosed()
    {
        Assert.Throws<InvalidDataException>(() =>
            TacticalFormationPrimitives.FacingTarget(
                "mystery",
                new Position(0, 0),
                new Position(1, 0),
                new Position(0, 0),
                new Position(2, 0),
                null));
    }

    [Fact]
    public void PreserveVacancyKeepsTheDestroyedStableSlotOpen()
    {
        IReadOnlyDictionary<int, string> stable = new Dictionary<int, string>
        {
            [2] = "line",
            [4] = "line",
            [7] = "line",
        };
        IReadOnlyDictionary<int, string> live = new Dictionary<int, string>
        {
            [2] = "line",
            [7] = "line",
        };

        Assert.Equal(2, TacticalFormationPrimitives.FormationOrdinal(
            7, "line", live, stable, "preserve", placementCount: 3));
        Assert.Equal(1, TacticalFormationPrimitives.FormationOrdinal(
            7, "line", live, stable, "compress", placementCount: 3));
        Assert.Equal(2, TacticalFormationPrimitives.FormationOrdinal(
            7, "line", live, stable, "rebalance-role", placementCount: 3));
    }

    [Fact]
    public void BlockedSlotReflowsToLegalTilesInCanonicalOrder()
    {
        string[] rows =
        [
            "#####",
            "#...#",
            "#.#.#",
            "#...#",
            "#####",
        ];

        Position[] goals = TacticalFormationPrimitives.ReflowGoals(
            width: 5,
            height: 5,
            rows,
            target: new Position(2, 2),
            radius: 1,
            blockedSlotPolicy: "nearest-legal");

        Assert.Equal(
            [
                new Position(1, 1),
                new Position(2, 1),
                new Position(3, 1),
                new Position(1, 2),
                new Position(3, 2),
                new Position(1, 3),
                new Position(2, 3),
                new Position(3, 3),
            ],
            goals);
    }

    [Fact]
    public void HoldPolicyDoesNotInventAReplacementSlot()
    {
        string[] rows = ["###", "###", "###"];
        Position target = new(1, 1);

        Assert.Equal(
            [target],
            TacticalFormationPrimitives.ReflowGoals(
                3, 3, rows, target, 2, "hold"));
    }

    [Fact]
    public void RotateShapeUsesAClockwiseReflowOrder()
    {
        string[] rows = [".....", ".....", ".....", ".....", "....."];
        Position target = new(2, 2);

        Position[] goals = TacticalFormationPrimitives.ReflowGoals(
            5, 5, rows, target, 1, "rotate-shape");

        Assert.Equal(target, goals[0]);
        Assert.Equal(new Position(2, 1), goals[1]);
        Assert.Equal(new Position(3, 1), goals[2]);
        Assert.Equal(new Position(3, 2), goals[3]);
        Assert.Equal(new Position(3, 3), goals[4]);
    }

    [Fact]
    public void FormationTargetHonorsMinimumPreferredAndMaximumSpacing()
    {
        string[] rows = Enumerable.Repeat(".......", 7).ToArray();
        TacticalFormationPrimitives.AssignedTarget[] assigned =
        [new("line", new Position(2, 2))];

        Position selected = TacticalFormationPrimitives
            .SelectFormationTarget(
                7, 7, rows,
                authored: new Position(4, 2),
                roleId: "line",
                minimumSpacing: 1,
                preferredSpacing: 2,
                maximumSpacing: 3,
                searchRadius: 2,
                blockedSlotPolicy: "nearest-legal",
                medicSeparation: 0,
                assigned);

        Assert.Equal(new Position(4, 2), selected);
        Assert.InRange(selected.ChebyshevDistance(assigned[0].Position), 1, 3);
    }

    [Fact]
    public void ReflowKeepsAuthoredMedicSeparation()
    {
        string[] rows = Enumerable.Repeat(".......", 7).ToArray();
        TacticalFormationPrimitives.AssignedTarget[] assigned =
        [new("medic", new Position(3, 3))];

        Position selected = TacticalFormationPrimitives
            .SelectFormationTarget(
                7, 7, rows,
                authored: new Position(4, 3),
                roleId: "medic",
                minimumSpacing: 1,
                preferredSpacing: 1,
                maximumSpacing: 3,
                searchRadius: 3,
                blockedSlotPolicy: "nearest-legal",
                medicSeparation: 3,
                assigned);

        Assert.Equal(3, selected.ChebyshevDistance(assigned[0].Position));
    }

    [Fact]
    public void FormationBreakAndReformUseIndependentStableWindows()
    {
        TacticalFormationPrimitives.Lifecycle state = default;

        state = TacticalFormationPrimitives.AdvanceLifecycle(
            state, 80, 50, breakTicks: 2, reformRatioPercent: 75,
            reformTicks: 2);
        Assert.False(state.Armed);
        state = TacticalFormationPrimitives.AdvanceLifecycle(
            state, 80, 50, 2, 75, 2);
        Assert.True(state.Armed);
        Assert.False(state.Broken);

        state = TacticalFormationPrimitives.AdvanceLifecycle(
            state, 49, 50, breakTicks: 2, reformRatioPercent: 75,
            reformTicks: 3);
        Assert.False(state.Broken);
        Assert.Equal(1, state.BreakStreak);
        state = TacticalFormationPrimitives.AdvanceLifecycle(
            state, 50, 50, 2, 75, 3);
        Assert.True(state.Broken);

        state = TacticalFormationPrimitives.AdvanceLifecycle(
            state, 75, 50, 2, 75, 3);
        state = TacticalFormationPrimitives.AdvanceLifecycle(
            state, 74, 50, 2, 75, 3);
        Assert.True(state.Broken);
        Assert.Equal(0, state.ReformStreak);
        for (int index = 0; index < 3; index++)
        {
            state = TacticalFormationPrimitives.AdvanceLifecycle(
                state, 80, 50, 2, 75, 3);
        }
        Assert.False(state.Broken);
    }

    [Theory]
    [InlineData("continuous", 2, 2, true, 3, 3, 75, false)]
    [InlineData("leader-arrived", 2, 2, true, 1, 3, 75, true)]
    [InlineData("leader-arrived", 3, 2, true, 1, 3, 75, false)]
    [InlineData("all-arrived", 3, 2, true, 3, 3, 75, true)]
    [InlineData("all-arrived", 3, 2, true, 2, 3, 75, false)]
    [InlineData("cohesion-arrived", 3, 2, false, 3, 4, 75, true)]
    [InlineData("cohesion-arrived", 3, 2, true, 2, 4, 75, false)]
    public void EveryMovementCompletionModeHasExactSemantics(
        string completion,
        int unitId,
        int leaderUnitId,
        bool unitArrived,
        int arrived,
        int members,
        int ratio,
        bool expected)
    {
        Assert.Equal(expected, TacticalFormationPrimitives.OrderComplete(
            completion, unitId, leaderUnitId, unitArrived, arrived, members,
            ratio));
    }

    [Theory]
    [InlineData("free", 3, 2, 2, 4, 5, true)]
    [InlineData("slowest", 3, 2, 4, 4, 5, true)]
    [InlineData("slowest", 3, 2, 3, 4, 5, false)]
    [InlineData("leader", 2, 2, 2, 2, 5, true)]
    [InlineData("leader", 3, 2, 2, 3, 5, false)]
    [InlineData("leader", 3, 2, 3, 3, 5, true)]
    public void FormationPacePreventsAuthoredOvertaking(
        string pace,
        int unitId,
        int leaderUnitId,
        int distance,
        int leaderDistance,
        int furthestDistance,
        bool expected)
    {
        Assert.Equal(expected, TacticalFormationPrimitives.CanAdvanceAtPace(
            pace, unitId, leaderUnitId, distance, leaderDistance,
            furthestDistance));
    }
}
