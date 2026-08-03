using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalFormationPrimitivesTests
{
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
