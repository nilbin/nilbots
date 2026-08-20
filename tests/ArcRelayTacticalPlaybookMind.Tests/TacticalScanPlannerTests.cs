using BotArena.Sdk;

public sealed class TacticalScanPlannerTests
{
    private static readonly GenericActorRulesContract.VisionProfile Vision =
        new(
            "test", 5, "chebyshev", "facing-quadrant", 1,
            "supercover", 0, 0, "none", "none", [], []);

    [Fact]
    public void VisibilityIsFacingAndCornerStrict()
    {
        HashSet<Position> walls = [new(2, 1)];

        Assert.True(Visible(new Position(1, 1), Direction.East,
            new Position(3, 1), []));
        Assert.False(Visible(new Position(1, 1), Direction.West,
            new Position(3, 1), []));
        Assert.False(Visible(new Position(1, 1), Direction.East,
            new Position(3, 2), walls));
    }

    [Fact]
    public void RotationInPlaceIsPreferredWhenItRefreshesStaleGround()
    {
        TacticalScanPlanner.ViewTarget[] targets = Targets(
            body: new Position(2, 2),
            stale: [new Position(4, 2)],
            blocked: []);

        Assert.Equal(new Position(2, 2), targets[0].Position);
        Assert.Equal(Direction.East, targets[0].Facing);
        Assert.Equal(0, targets[0].WalkingDistance);
    }

    [Fact]
    public void MovementIsSelectedWhenNoFacingAtTheBodyCanSeeTheStaleTile()
    {
        HashSet<Position> walls = [new(3, 2)];
        TacticalScanPlanner.ViewTarget[] targets = Targets(
            body: new Position(2, 2),
            stale: [new Position(4, 2)],
            blocked: walls);

        Assert.NotEmpty(targets);
        Assert.NotEqual(new Position(2, 2), targets[0].Position);
        Assert.True(targets[0].WalkingDistance > 0);
    }

    [Fact]
    public void HeldUsefulTargetSortsAheadOfNewCandidates()
    {
        // The ordering contract itself is pinned at the planner boundary by
        // giving the held key to the same stable sort the runtime uses.
        var values = new[]
        {
            new TacticalScanPlanner.ViewTarget(
                new Position(1, 1), Direction.North, 8, 0),
            new TacticalScanPlanner.ViewTarget(
                new Position(2, 1), Direction.East, 2, 1),
        };
        string held = values[1].Key;

        TacticalScanPlanner.ViewTarget[] ordered =
            TacticalScanPlanner.Order(values, new Position(1, 1), held);

        Assert.Equal(held, ordered[0].Key);
    }

    private static bool Visible(
        Position origin,
        Direction facing,
        Position target,
        HashSet<Position> walls) => TacticalScanPlanner.VisibleFrom(
            7, 7, walls.Contains, Vision, origin, facing, target);

    private static TacticalScanPlanner.ViewTarget[] Targets(
        Position body,
        Position[] stale,
        HashSet<Position> blocked) => TacticalScanPlanner.Targets(
            7,
            7,
            position => !blocked.Contains(position),
            blocked.Contains,
            Vision,
            body,
            stale,
            position => Math.Max(
                Math.Abs(position.X - body.X),
                Math.Abs(position.Y - body.Y)));
}
