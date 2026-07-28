namespace BotArena.Engine.Tests;

public sealed class BoundedChildFabricationKernelTests
{
    [Fact]
    public void Reserve_CapturesLineageDeclaredOffsetAndAssignedFacing()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                fabricationCandidateOffsets:
                [
                    new(int.MaxValue, 0),
                    new(1, 0),
                ]);
        var kernel = new BoundedChildFabricationKernel(definition);
        BoundedChildFabricationActorSnapshot[] actors = Actors();

        BoundedChildFabricationProvisionalReservation reservation =
            Assert.Single(kernel.ReserveBatch(
                tick: 4,
                [Request(0, 10, "west")],
                actors,
                Slots(actors),
                []).Reservations);

        Assert.Equal(new ActorIdentity(0, 0, 0), reservation.SourceActorId);
        Assert.Equal(10, reservation.ParticipantId);
        Assert.Equal(0, reservation.SourceGeneration);
        Assert.Equal("mobile", reservation.SourceFormId);
        Assert.Equal(new Position(2, 3), reservation.SourcePosition);
        Assert.Equal(Direction.East, reservation.SourceFacing);
        Assert.Equal((0, 1), (
            reservation.TargetTeamId,
            reservation.TargetUnitId));
        Assert.Equal("child", reservation.TargetFormId);
        Assert.Equal(1, reservation.TargetGeneration);
        Assert.Equal(new Position(3, 3), reservation.ReservedPosition);
        Assert.Equal(Direction.East, reservation.OutputFacing);
        Assert.Equal(5, reservation.DueTick);
    }

    [Fact]
    public void Reserve_RotatesOffsetsAndUsesEachParticipantsOutputFacing()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new BoundedChildFabricationKernel(definition);
        BoundedChildFabricationActorSnapshot[] actors = Actors();

        BoundedChildFabricationProvisionalReservation reservation =
            Assert.Single(kernel.ReserveBatch(
                0,
                [Request(1, 20, "east")],
                actors.Reverse().ToArray(),
                Slots(actors).Reverse().ToArray(),
                []).Reservations);

        Assert.Equal(new Position(5, 3), reservation.ReservedPosition);
        Assert.Equal(Direction.West, reservation.OutputFacing);
    }

    [Fact]
    public void Reserve_ValidatesSourceRegionGenerationAndExplicitTarget()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new BoundedChildFabricationKernel(definition);
        BoundedChildFabricationActorSnapshot[] baseline = Actors();

        foreach ((BoundedChildFabricationActorSnapshot source,
                  BoundedChildFabricationReservationOutcome
                      .FabricationReservationBlockReason reason) in new[]
        {
            (
                baseline[0] with { Position = new Position(3, 3) },
                BoundedChildFabricationReservationOutcome
                    .FabricationReservationBlockReason.SourceNotEligible),
            (
                baseline[0] with { Generation = int.MaxValue },
                BoundedChildFabricationReservationOutcome
                    .FabricationReservationBlockReason.SourceNotEligible),
        })
        {
            BoundedChildFabricationActorSnapshot[] actors =
                [source, baseline[1]];
            BoundedChildFabricationReservationOutcome outcome =
                Assert.Single(kernel.ReserveBatch(
                    0,
                    [Request(0, 10, "west")],
                    actors,
                    Slots(actors),
                    []).Outcomes);
            Assert.Equal(reason, outcome.Reason);
        }

        BoundedChildFabricationReservationOutcome target = Assert.Single(
            kernel.ReserveBatch(
                0,
                [
                    new BoundedChildFabricationRequest(
                        new ActorIdentity(0, 0, 0),
                        "fabricate-child",
                        "source-target",
                        TargetTeamId: 0,
                        TargetUnitId: 0),
                ],
                baseline,
                Slots(baseline),
                []).Outcomes);
        Assert.Equal(
            BoundedChildFabricationReservationOutcome
                .FabricationReservationBlockReason.TargetUnavailable,
            target.Reason);
    }

    [Fact]
    public void Reserve_UsesTypedUnavailableResultAndCheckedDueTick()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                fabricationUnavailableResult:
                    ActorActionRejectionResult.Rejected);
        var kernel = new BoundedChildFabricationKernel(definition);
        BoundedChildFabricationActorSnapshot[] actors = Actors();

        BoundedChildFabricationReservationOutcome unavailable =
            Assert.Single(kernel.ReserveBatch(
                0,
                [Request(0, 10, "west")],
                actors,
                Slots(actors),
                [new Position(3, 3)]).Outcomes);

        Assert.Equal(
            BoundedChildFabricationReservationOutcome
                .FabricationReservationOutcomeKind.Rejected,
            unavailable.Outcome);
        Assert.Equal(
            BoundedChildFabricationReservationOutcome
                .FabricationReservationBlockReason.InsufficientPositions,
            unavailable.Reason);
        Assert.Throws<OverflowException>(() =>
            kernel.ReserveBatch(
                int.MaxValue,
                [Request(0, 10, "overflow")],
                actors,
                Slots(actors),
                []));
    }

    [Fact]
    public void SharedArbiter_BlocksEntireCrossFamilyConflictChain()
    {
        ActorLifecycleReservationClaim fabrication = Claim(
            "fabrication",
            ActorLifecycleReservationFamily.Fabrication,
            [(0, 1)],
            [new Position(3, 3)]);
        ActorLifecycleReservationClaim splitOne = Claim(
            "split-one",
            ActorLifecycleReservationFamily.Replication,
            [(0, 1), (0, 2)],
            [new Position(4, 3), new Position(4, 4)]);
        ActorLifecycleReservationClaim splitTwo = Claim(
            "split-two",
            ActorLifecycleReservationFamily.Replication,
            [(1, 0), (1, 1)],
            [new Position(4, 4), new Position(5, 4)]);

        string[] forward = ActorLifecycleReservationArbiter
            .BlockedOperationIds(
                [fabrication, splitOne, splitTwo])
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] reversed = ActorLifecycleReservationArbiter
            .BlockedOperationIds(
                [splitTwo, splitOne, fabrication])
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["fabrication", "split-one", "split-two"],
            forward);
        Assert.Equal(forward, reversed);
    }

    private static ActorLifecycleReservationClaim Claim(
        string operationId,
        ActorLifecycleReservationFamily family,
        IEnumerable<(int TeamId, int UnitId)> slots,
        IEnumerable<Position> tiles) =>
        new(
            operationId,
            family,
            slots.Select(slot =>
                new ActorLifecycleSlotClaim(
                    slot.TeamId,
                    slot.UnitId)),
            tiles);

    private static BoundedChildFabricationRequest Request(
        int teamId,
        int participantId,
        string operationId)
    {
        _ = participantId;
        return new BoundedChildFabricationRequest(
            new ActorIdentity(teamId, 0, 0),
            "fabricate-child",
            operationId,
            teamId,
            TargetUnitId: 1);
    }

    private static BoundedChildFabricationActorSnapshot[] Actors() =>
    [
        new(
            new ActorIdentity(0, 0, 0),
            ParticipantId: 10,
            Generation: 0,
            FormId: "mobile",
            Position: new Position(2, 3),
            Facing: Direction.East),
        new(
            new ActorIdentity(1, 0, 0),
            ParticipantId: 20,
            Generation: 0,
            FormId: "mobile",
            Position: new Position(6, 3),
            Facing: Direction.West),
    ];

    private static BoundedChildFabricationSlotSnapshot[] Slots(
        IReadOnlyCollection<BoundedChildFabricationActorSnapshot> actors)
    {
        Dictionary<(int TeamId, int UnitId), ActorIdentity> active =
            actors.ToDictionary(
                actor => (actor.ActorId.TeamId, actor.ActorId.UnitId),
                actor => actor.ActorId);
        return
        [
            ActiveOrReady(0, 0, active),
            ActiveOrReady(0, 1, active),
            ActiveOrReady(1, 0, active),
            ActiveOrReady(1, 1, active),
        ];
    }

    private static BoundedChildFabricationSlotSnapshot ActiveOrReady(
        int teamId,
        int unitId,
        IReadOnlyDictionary<(int TeamId, int UnitId), ActorIdentity> active) =>
        active.TryGetValue((teamId, unitId), out ActorIdentity? actorId)
            ? new(
                teamId,
                unitId,
                BoundedChildFabricationSlotSnapshot
                    .FabricationSlotState.Active,
                actorId)
            : new(
                teamId,
                unitId,
                BoundedChildFabricationSlotSnapshot
                    .FabricationSlotState.Ready,
                ActiveActorId: null);
}
