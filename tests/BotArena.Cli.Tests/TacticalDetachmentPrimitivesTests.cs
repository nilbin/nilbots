namespace BotArena.Cli.Tests;

public sealed class TacticalDetachmentPrimitivesTests
{
    [Fact]
    public void TakeThenRemainderSplitsAStableGroupDeterministically()
    {
        IReadOnlyDictionary<int, string> assigned =
            TacticalDetachmentPrimitives.Assign(
                [
                    new(3, "medic", "patchbay"),
                    new(2, "medic", "patchbay"),
                ],
                [
                    new("field", 20, "take", ["medic"], ["patchbay"], 1),
                    new("collection", 21, "remainder", [], [], 0),
                ]);

        Assert.Equal("field", assigned[2]);
        Assert.Equal("collection", assigned[3]);
    }

    [Fact]
    public void CasualtyLeavesTheSurvivorOnTheHigherPriorityTask()
    {
        IReadOnlyDictionary<int, string> assigned =
            TacticalDetachmentPrimitives.Assign(
                [new(3, "medic", "patchbay")],
                [
                    new("field", 20, "take", ["medic"], ["patchbay"], 1),
                    new("collection", 21, "remainder", [], [], 0),
                ]);

        Assert.Equal("field", assigned[3]);
        Assert.DoesNotContain("collection", assigned.Values);
    }

    [Fact]
    public void WholeGroupSelectionPreservesExistingBehavior()
    {
        IReadOnlyDictionary<int, string> assigned =
            TacticalDetachmentPrimitives.Assign(
                [new(4, "line", "sunder"), new(1, "line", "kestrel")],
                [new("advance", 30, "all", [], [], 0)]);

        Assert.All(assigned.Values, value => Assert.Equal("advance", value));
    }

    [Fact]
    public void MissingRemainderCannotSilentlyDropBodies()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TacticalDetachmentPrimitives.Assign(
                [
                    new(2, "medic", "patchbay"),
                    new(3, "medic", "patchbay"),
                ],
                [new("field", 20, "take", ["medic"], ["patchbay"], 1)]));

        Assert.Contains("unassigned", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderedClassesSelectTheAuthoredCapabilityNotTheLowestUnitId()
    {
        IReadOnlyDictionary<int, string> assigned =
            TacticalDetachmentPrimitives.Assign(
                [
                    new(0, "line", "relay"),
                    new(1, "line", "kestrel"),
                    new(4, "line", "sunder"),
                ],
                [
                    new("scorer", 20, "take", ["line"],
                        ["kestrel", "relay"], 1),
                    new("pressure", 21, "remainder", [], [], 0),
                ]);

        Assert.Equal("scorer", assigned[1]);
        Assert.Equal("pressure", assigned[0]);
        Assert.Equal("pressure", assigned[4]);
    }
}
