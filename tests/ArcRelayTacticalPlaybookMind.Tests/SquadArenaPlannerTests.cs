using BotArena.Sdk;

public sealed class SquadArenaPlannerTests
{
    [Fact]
    public void OperationsAllowFollowingButRejectSwap()
    {
        SquadArenaPlanner.Operation leader = Operation(
            new Position(2, 2), Direction.East,
            SquadArenaPlanner.MicroAction.Move(
                new Position(3, 2), ProjectileHeading.East),
            SquadArenaPlanner.MicroAction.Move(
                new Position(4, 2), ProjectileHeading.East),
            SquadArenaPlanner.MicroAction.Wait);
        SquadArenaPlanner.Operation follower = Operation(
            new Position(1, 2), Direction.East,
            SquadArenaPlanner.MicroAction.Move(
                new Position(2, 2), ProjectileHeading.East),
            SquadArenaPlanner.MicroAction.Move(
                new Position(3, 2), ProjectileHeading.East),
            SquadArenaPlanner.MicroAction.Wait);
        SquadArenaPlanner.Operation swapper = Operation(
            new Position(3, 2), Direction.West,
            SquadArenaPlanner.MicroAction.Move(
                new Position(2, 2), ProjectileHeading.West),
            SquadArenaPlanner.MicroAction.Wait,
            SquadArenaPlanner.MicroAction.Wait);

        Assert.False(SquadArenaPlanner.OperationsCollide(leader, follower));
        Assert.True(SquadArenaPlanner.OperationsCollide(leader, swapper));
    }

    [Fact]
    public void AssignmentIsDeterministicAndGloballyMinimumCost()
    {
        long[][] costs =
        [
            [9, 1, 5],
            [2, 8, 4],
            [3, 6, 7],
        ];

        Assert.Equal([1, 2, 0],
            SquadArenaPlanner.SolveMinimumCost(costs));
        Assert.Equal([1, 2, 0],
            SquadArenaPlanner.SolveMinimumCost(costs));
    }

    [Fact]
    public void EqualDistanceOperationPrefersEarlierProgress()
    {
        SquadArenaPlanner.MicroAction rotateEast =
            SquadArenaPlanner.MicroAction.Rotate(Direction.East);
        SquadArenaPlanner.MicroAction rotateNorth =
            SquadArenaPlanner.MicroAction.Rotate(Direction.North);
        SquadArenaPlanner.MicroAction move = SquadArenaPlanner.MicroAction.Move(
            new Position(3, 2), ProjectileHeading.East);

        long progressThenWait = SquadArenaPlanner.OperationActionTieBreak(
            [rotateEast, move, SquadArenaPlanner.MicroAction.Wait]);
        long extraTurnThenProgress = SquadArenaPlanner.OperationActionTieBreak(
            [rotateEast, rotateNorth, move]);

        Assert.True(progressThenWait < extraTurnThenProgress);
    }

    [Fact]
    public void HoldPreferenceCannotOutweighOneStepOfGoalProgress()
    {
        long yielding = SquadArenaPlanner.PreferredHoldPenalty(
            [SquadArenaPlanner.MicroAction.Move(
                new Position(3, 2), ProjectileHeading.East),
             SquadArenaPlanner.MicroAction.Wait,
             SquadArenaPlanner.MicroAction.Wait]);
        long resting = SquadArenaPlanner.PreferredHoldPenalty(
            [SquadArenaPlanner.MicroAction.Wait,
             SquadArenaPlanner.MicroAction.Wait,
             SquadArenaPlanner.MicroAction.Wait]);
        long swivelling = SquadArenaPlanner.PreferredHoldPenalty(
            [SquadArenaPlanner.MicroAction.Rotate(Direction.North),
             SquadArenaPlanner.MicroAction.Wait,
             SquadArenaPlanner.MicroAction.Wait]);

        Assert.Equal(50_000, yielding);
        Assert.Equal(25_000, swivelling);
        Assert.Equal(0, resting);
        Assert.True(yielding < 100_000);
    }

    private static SquadArenaPlanner.Operation Operation(
        Position start,
        Direction facing,
        params SquadArenaPlanner.MicroAction[] actions)
    {
        var states = new SquadArenaPlanner.PlannerState[actions.Length];
        Position position = start;
        for (int index = 0; index < actions.Length; index++)
        {
            if (actions[index].Destination is Position destination)
                position = destination;
            states[index] = new SquadArenaPlanner.PlannerState(
                position, facing);
        }
        return new SquadArenaPlanner.Operation(
            actions, states, Weight: 0, Inherited: false);
    }
}
