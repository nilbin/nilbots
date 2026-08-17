public sealed class TacticalTargetCapacityAllocatorTests
{
    [Fact]
    public void SelectedClaimsRemainCountedWhenOtherBodiesTryFallbackTargets()
    {
        const string scope = "two-per-core";
        var candidates = new Dictionary<int, IReadOnlyList<
            TacticalTargetCapacityAllocator.Candidate<string>>>
        {
            [0] = [Capacity("core-a", 0, "a0")],
            [1] = [Capacity("core-a", 1, "a1")],
            [2] = [Capacity("core-b", 0, "b2")],
            [3] = [Capacity("core-b", 1, "b3")],
            [4] = [
                Capacity("core-b", 2, "b4"),
                Capacity("core-a", 2, "a4"),
                Uncapped("patrol-4")],
            [5] = [
                Capacity("core-b", 3, "b5"),
                Capacity("core-a", 3, "a5"),
                Uncapped("patrol-5")],
        };

        Dictionary<int, string> selected =
            TacticalTargetCapacityAllocator.Allocate(candidates);

        Assert.Equal("a0", selected[0]);
        Assert.Equal("a1", selected[1]);
        Assert.Equal("b2", selected[2]);
        Assert.Equal("b3", selected[3]);
        Assert.Equal("patrol-4", selected[4]);
        Assert.Equal("patrol-5", selected[5]);

        TacticalTargetCapacityAllocator.Candidate<string> Capacity(
            string target,
            int distance,
            string value) => new(value, scope, target, 2, distance);
        static TacticalTargetCapacityAllocator.Candidate<string> Uncapped(
            string value) => new(value, null, "patrol", 8, 0);
    }

    [Fact]
    public void EqualDistanceCapacityTiesUseBodyId()
    {
        var candidates = new Dictionary<int, IReadOnlyList<
            TacticalTargetCapacityAllocator.Candidate<string>>>
        {
            [7] = [new("seven", "one", "core", 1, 4)],
            [2] = [new("two", "one", "core", 1, 4)],
        };

        Dictionary<int, string> selected =
            TacticalTargetCapacityAllocator.Allocate(candidates);

        Assert.Equal("two", Assert.Single(selected).Value);
    }

    [Fact]
    public void HighestVeteranSelectionReselectsDeterministicallyFromEligibleBodies()
    {
        var candidates = new Dictionary<int, IReadOnlyList<
            TacticalTargetCapacityAllocator.Candidate<string>>>
        {
            [7] = [new("level-two", "spearhead", "post", 1, 2,
                "highest-veteran", 2)],
            [2] = [new("level-four", "spearhead", "post", 1, 5,
                "highest-veteran", 4)],
            [4] = [new("other-level-four", "spearhead", "post", 1, 3,
                "highest-veteran", 4)],
        };

        Dictionary<int, string> selected =
            TacticalTargetCapacityAllocator.Allocate(candidates);

        Assert.Equal("other-level-four", Assert.Single(selected).Value);

        candidates.Remove(4);
        selected = TacticalTargetCapacityAllocator.Allocate(candidates);
        Assert.Equal("level-four", Assert.Single(selected).Value);
    }
}
