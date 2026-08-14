using BotArena.Sdk;

public sealed class SquadArenaPlannerTests
{
    [Fact]
    public void PlannerObstacleModelMatchesHardlightAndTripNodeSemantics()
    {
        GenericActorContext.ArcRelaySignatureState Hardlight(bool suppressed) =>
            new(
                "hardlight-1", "hardlight-block", "hardlight-block",
                new ActorIdentity(0, 0, 0), 0,
                GenericActorContext.ArcRelaySignaturePhase.Active,
                1, null, 10, [new Position(3, 3)], null, 3, suppressed);
        GenericActorContext.ArcRelaySignatureState TripNode() => new(
            "mine-1", "trip-node", "trip-node",
            new ActorIdentity(0, 1, 0), 0,
            GenericActorContext.ArcRelaySignaturePhase.Active,
            1, null, 10, [new Position(4, 4)], null, 1, false);

        Assert.True(ArenaBasics.SignatureBlocksMovement(Hardlight(false), 0));
        Assert.True(ArenaBasics.SignatureBlocksMovement(Hardlight(false), 1));
        Assert.False(ArenaBasics.SignatureBlocksMovement(Hardlight(true), 1));
        Assert.False(ArenaBasics.SignatureBlocksMovement(TripNode(), 0));
        Assert.True(ArenaBasics.SignatureBlocksMovement(TripNode(), 1));
    }

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
    public void JointOperationsAllowChainIntoVacancyButRejectThreeBodyCycle()
    {
        SquadArenaPlanner.Operation first = Operation(
            new Position(1, 1), Direction.East,
            SquadArenaPlanner.MicroAction.Move(
                new Position(2, 1), ProjectileHeading.East),
            SquadArenaPlanner.MicroAction.Wait,
            SquadArenaPlanner.MicroAction.Wait);
        SquadArenaPlanner.Operation second = Operation(
            new Position(2, 1), Direction.South,
            SquadArenaPlanner.MicroAction.Move(
                new Position(2, 2), ProjectileHeading.South),
            SquadArenaPlanner.MicroAction.Wait,
            SquadArenaPlanner.MicroAction.Wait);
        SquadArenaPlanner.Operation vacancy = Operation(
            new Position(2, 2), Direction.East,
            SquadArenaPlanner.MicroAction.Move(
                new Position(3, 2), ProjectileHeading.East),
            SquadArenaPlanner.MicroAction.Wait,
            SquadArenaPlanner.MicroAction.Wait);
        SquadArenaPlanner.Operation closesCycle = Operation(
            new Position(2, 2), Direction.West,
            SquadArenaPlanner.MicroAction.Move(
                new Position(1, 1), ProjectileHeading.NorthWest),
            SquadArenaPlanner.MicroAction.Wait,
            SquadArenaPlanner.MicroAction.Wait);

        Assert.True(SquadArenaPlanner.OperationsHaveAcyclicMovement(
            [first, second, vacancy]));
        Assert.False(SquadArenaPlanner.OperationsHaveAcyclicMovement(
            [first, second, closesCycle]));
        Assert.True(SquadArenaPlanner.FirstStepIsExecutable(
            [first, second, vacancy]));
        Assert.False(SquadArenaPlanner.FirstStepIsExecutable(
            [first, second, closesCycle]));
    }

    [Fact]
    public void JointFirstStepRejectsSharedDestination()
    {
        SquadArenaPlanner.Operation west = Operation(
            new Position(1, 1), Direction.East,
            SquadArenaPlanner.MicroAction.Move(
                new Position(2, 1), ProjectileHeading.East),
            SquadArenaPlanner.MicroAction.Wait,
            SquadArenaPlanner.MicroAction.Wait);
        SquadArenaPlanner.Operation east = Operation(
            new Position(3, 1), Direction.West,
            SquadArenaPlanner.MicroAction.Move(
                new Position(2, 1), ProjectileHeading.West),
            SquadArenaPlanner.MicroAction.Wait,
            SquadArenaPlanner.MicroAction.Wait);

        Assert.False(SquadArenaPlanner.FirstStepIsExecutable([west, east]));
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
    public void OneTileServiceQueueKeepsTheNearestBodyAhead()
    {
        Dictionary<int, int> predecessors =
            SquadArenaPlanner.ResolveServiceQueuePredecessors(
            [
                new(0, "move:to:heal:21:7", CanStage: true, Distance: 4),
                new(2, "move:to:heal:21:7", CanStage: true, Distance: 3),
                new(4, "move:to:heal:21:7", CanStage: true, Distance: 6),
                new(6, "fight:0:1:0", CanStage: true, Distance: 1),
                new(7, "move:to:well:north", CanStage: false, Distance: 1),
            ]);

        Assert.Equal(2, predecessors[0]);
        Assert.Equal(0, predecessors[4]);
        Assert.DoesNotContain(2, predecessors.Keys);
        Assert.DoesNotContain(6, predecessors.Keys);
        Assert.DoesNotContain(7, predecessors.Keys);
    }

    [Fact]
    public void OneTileServiceQueueBreaksDistanceTiesByUnitId()
    {
        Dictionary<int, int> predecessors =
            SquadArenaPlanner.ResolveServiceQueuePredecessors(
            [
                new(5, "move:to:heal:21:7", CanStage: true, Distance: 4),
                new(1, "move:to:heal:21:7", CanStage: true, Distance: 4),
            ]);

        Assert.Equal(1, predecessors[5]);
        Assert.DoesNotContain(1, predecessors.Keys);
    }

    [Fact]
    public void CarrierReturnUsesEveryOwnedAutomaticReturnTile()
    {
        GenericActorResolvedMatchContract.LifecycleAssignment[] assignments =
        [
            new(0, 0, "return", 0, ["form"],
                GenericActorResolvedMatchContract.InitialAvailability.ActiveAtTickZero,
                null, "west-a"),
            new(0, 1, "return", 0, ["form"],
                GenericActorResolvedMatchContract.InitialAvailability.ActiveAtTickZero,
                null, "west-b"),
            new(1, 0, "return", 0, ["form"],
                GenericActorResolvedMatchContract.InitialAvailability.ActiveAtTickZero,
                null, "east-a"),
            new(0, 2, "single", 0, ["form"],
                GenericActorResolvedMatchContract.InitialAvailability.ActiveAtTickZero,
                null, null),
        ];
        GenericActorMapContract.SpawnAnchor[] spawns =
        [
            new("west-b", new Position(3, 5), Direction.East,
                [GenericActorMapContract.MovementLayer.Ground]),
            new("east-a", new Position(27, 21), Direction.West,
                [GenericActorMapContract.MovementLayer.Ground]),
            new("west-a", new Position(1, 4), Direction.East,
                [GenericActorMapContract.MovementLayer.Ground]),
            new("unused", new Position(9, 9), Direction.North,
                [GenericActorMapContract.MovementLayer.Ground]),
        ];

        Assert.Equal(
            [new Position(1, 4), new Position(3, 5)],
            SquadArenaPlanner.CarrierReturnSpawnTiles(
                assignments, spawns, teamId: 0));
        Assert.Equal(
            [new Position(27, 21)],
            SquadArenaPlanner.CarrierReturnSpawnTiles(
                assignments, spawns, teamId: 1));
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

    [Fact]
    public void EarlierArrivalBeatsMovingForTheWholeHorizon()
    {
        long enterAndRest = SquadArenaPlanner.GoalProgressWeight([0, 0, 0]);
        long orbitAndEnter = SquadArenaPlanner.GoalProgressWeight([1, 1, 0]);
        long neverArrive = SquadArenaPlanner.GoalProgressWeight([1, 1, 1]);

        Assert.True(enterAndRest < orbitAndEnter);
        Assert.True(orbitAndEnter < neverArrive);
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
