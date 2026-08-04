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
            7, "line", live, stable, "preserve"));
        Assert.Equal(1, TacticalFormationPrimitives.FormationOrdinal(
            7, "line", live, stable, "compress"));
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
}
