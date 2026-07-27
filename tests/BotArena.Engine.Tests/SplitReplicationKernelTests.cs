namespace BotArena.Engine.Tests;

public sealed class SplitReplicationKernelTests
{
    [Fact]
    public void ReserveAndComplete_UsesSourceSlotThenReadySlotAndCurrentHealth()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));

        SplitReplicationBatchResult batch = kernel.ReserveBatch(
            tick: 4,
            [new(new ActorIdentity(0, 0, 0), "split-mobile", "split-4-0")],
            actors,
            Slots(actors),
            []);

        SplitReplicationReservation reservation =
            Assert.Single(batch.Reservations);
        Assert.Equal(5, reservation.DueTick);
        Assert.Collection(
            reservation.Descendants,
            descendant =>
            {
                Assert.Equal((0, 0), (descendant.TeamId, descendant.UnitId));
                Assert.Equal(new Position(2, 3), descendant.Position);
                Assert.Equal(1, descendant.Generation);
                Assert.Equal("child", descendant.FormId);
            },
            descendant =>
            {
                Assert.Equal((0, 1), (descendant.TeamId, descendant.UnitId));
                Assert.Equal(new Position(4, 3), descendant.Position);
            });

        SplitReplicationCompletion completion = kernel.Complete(
            reservation.DueTick,
            reservation,
            actors[0] with { Health = 3 });

        Assert.Equal(
            SplitReplicationCompletion.SplitCompletionOutcomeKind.Completed,
            completion.Outcome);
        Assert.Null(completion.Reason);
        Assert.All(
            completion.Descendants,
            descendant => Assert.Equal(1, descendant.Health));
    }

    [Fact]
    public void Completion_RechecksHealthAndNeverClampsUpToMinimum()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));
        SplitReplicationReservation reservation = Assert.Single(
            kernel.ReserveBatch(
                0,
                [new(new ActorIdentity(0, 0, 0), "split-mobile", "split-0-0")],
                actors,
                Slots(actors),
                [])
            .Reservations);

        SplitReplicationCompletion completion = kernel.Complete(
            reservation.DueTick,
            reservation,
            actors[0] with { Health = 1 });

        Assert.Equal(
            SplitReplicationCompletion.SplitCompletionOutcomeKind.Cancelled,
            completion.Outcome);
        Assert.Equal(
            SplitReplicationCompletion.SplitCancellationReason
                .InsufficientHealth,
            completion.Reason);
        Assert.Empty(completion.Descendants);
    }

    [Fact]
    public void Completion_OnlyRunsAtTheReservedDueTick()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));
        SplitReplicationReservation reservation = Assert.Single(
            kernel.ReserveBatch(
                3,
                [new(new ActorIdentity(0, 0, 0), "split-mobile", "split-3-0")],
                actors,
                Slots(actors),
                [])
            .Reservations);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => kernel.Complete(
                reservation.DueTick - 1,
                reservation,
                actors[0]));
    }

    [Fact]
    public void Completion_CancelsWhenSameLifeEligibilityChangesAfterQueue()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));
        SplitReplicationReservation reservation = Assert.Single(
            kernel.ReserveBatch(
                0,
                [new(new ActorIdentity(0, 0, 0), "split-mobile", "split-0-0")],
                actors,
                Slots(actors),
                [])
            .Reservations);

        foreach (SplitReplicationActorSnapshot changedSource in new[]
        {
            actors[0] with { HasPriorSameLifeTransition = true },
            actors[0] with { HasPendingSameLifeTransition = true },
        })
        {
            SplitReplicationCompletion completion = kernel.Complete(
                reservation.DueTick,
                reservation,
                changedSource);

            Assert.Equal(
                SplitReplicationCompletion.SplitCompletionOutcomeKind
                    .Cancelled,
                completion.Outcome);
            Assert.Equal(
                SplitReplicationCompletion.SplitCancellationReason
                    .SourceStateChanged,
                completion.Reason);
            Assert.Empty(completion.Descendants);
        }
    }

    [Fact]
    public void Completion_RejectsForgedPlacementOutsideCanonicalOffsets()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));
        SplitReplicationReservation reservation = Assert.Single(
            kernel.ReserveBatch(
                0,
                [new(new ActorIdentity(0, 0, 0), "split-mobile", "split-0-0")],
                actors,
                Slots(actors),
                [])
            .Reservations);
        SplitReplicationReservation teleported = reservation with
        {
            Descendants =
            [
                reservation.Descendants[0] with
                {
                    Position = new Position(5, 5),
                },
                reservation.Descendants[1],
            ],
        };
        SplitReplicationReservation reordered = reservation with
        {
            Descendants =
            [
                reservation.Descendants[0] with
                {
                    Position = reservation.Descendants[1].Position,
                },
                reservation.Descendants[1] with
                {
                    Position = reservation.Descendants[0].Position,
                },
            ],
        };

        Assert.Throws<ArgumentException>(() =>
            kernel.Complete(
                teleported.DueTick,
                teleported,
                actors[0]));
        Assert.Throws<ArgumentException>(() =>
            kernel.Complete(
                reordered.DueTick,
                reordered,
                actors[0]));
    }

    [Fact]
    public void Reservation_RejectsMalformedRequestsBeforeCanonicalSorting()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));

        Assert.Throws<ArgumentException>(() =>
            kernel.ReserveBatch(
                0,
                [null!],
                actors,
                Slots(actors),
                []));
        Assert.Throws<ArgumentException>(() =>
            kernel.ReserveBatch(
                0,
                [new(null!, "split-mobile", "missing-source")],
                actors,
                Slots(actors),
                []));
    }

    [Fact]
    public void IntersectingJointBundles_AllBlockWithoutOrderWinner()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(5, 3));
        SplitReplicationRequest west =
            new(new ActorIdentity(0, 0, 0), "split-mobile", "west");
        SplitReplicationRequest east =
            new(new ActorIdentity(1, 0, 0), "split-mobile", "east");

        SplitReplicationBatchResult forward = kernel.ReserveBatch(
            2,
            [west, east],
            actors,
            Slots(actors),
            []);
        SplitReplicationBatchResult reversed = kernel.ReserveBatch(
            2,
            [east, west],
            actors.Reverse().ToArray(),
            Slots(actors).Reverse().ToArray(),
            []);

        Assert.Equal(
            forward.Outcomes.Select(outcome =>
                (outcome.Request.SourceActorId,
                 outcome.Request.TransitionId,
                 outcome.Request.OperationId,
                 outcome.Outcome,
                 outcome.Reason)),
            reversed.Outcomes.Select(outcome =>
                (outcome.Request.SourceActorId,
                 outcome.Request.TransitionId,
                 outcome.Request.OperationId,
                 outcome.Outcome,
                 outcome.Reason)));
        Assert.Empty(forward.Reservations);
        Assert.All(
            forward.Outcomes,
            outcome =>
            {
                Assert.Equal(
                    SplitReplicationReservationOutcome
                        .SplitReservationOutcomeKind.Blocked,
                    outcome.Outcome);
                Assert.Equal(
                    SplitReplicationReservationOutcome
                        .SplitReservationBlockReason
                        .ConflictingReservation,
                    outcome.Reason);
            });
    }

    [Fact]
    public void Reservation_RequiresEnoughReadySlotsAndUnclaimedTiles()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));
        SplitReplicationRequest request =
            new(new ActorIdentity(0, 0, 0), "split-mobile", "west");

        SplitReplicationSlotSnapshot[] unavailable = Slots(actors)
            .Select(slot => slot.TeamId == 0 && slot.UnitId == 1
                ? slot with
                {
                    State = SplitReplicationSlotSnapshot
                        .SplitSlotState.Unavailable,
                }
                : slot)
            .ToArray();
        SplitReplicationReservationOutcome slotBlocked = Assert.Single(
            kernel.ReserveBatch(1, [request], actors, unavailable, []).Outcomes);
        Assert.Equal(
            SplitReplicationReservationOutcome.SplitReservationBlockReason
                .InsufficientSlots,
            slotBlocked.Reason);

        SplitReplicationReservationOutcome tileBlocked = Assert.Single(
            kernel.ReserveBatch(
                1,
                [request],
                actors,
                Slots(actors),
                [new Position(2, 3)])
            .Outcomes);
        Assert.Equal(
            SplitReplicationReservationOutcome.SplitReservationBlockReason
                .InsufficientPositions,
            tileBlocked.Reason);
    }

    [Fact]
    public void Reservation_NeverUsesPermanentlyReservedReturnSpawn()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(2, 3),
            eastPosition: new Position(7, 3));

        SplitReplicationReservationOutcome outcome = Assert.Single(
            kernel.ReserveBatch(
                1,
                [new(new ActorIdentity(0, 0, 0), "split-mobile", "west")],
                actors,
                Slots(actors),
                [])
            .Outcomes);

        Assert.Equal(
            SplitReplicationReservationOutcome.SplitReservationBlockReason
                .InsufficientPositions,
            outcome.Reason);
    }

    [Fact]
    public void Eligibility_RejectsGenerationTransformAndPendingState()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationRequest request =
            new(new ActorIdentity(0, 0, 0), "split-mobile", "west");
        SplitReplicationActorSnapshot[] baseline = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));

        foreach (SplitReplicationActorSnapshot ineligible in new[]
        {
            baseline[0] with { Generation = 1 },
            baseline[0] with { HasPriorSameLifeTransition = true },
            baseline[0] with { HasPendingSameLifeTransition = true },
        })
        {
            SplitReplicationActorSnapshot[] actors =
                [ineligible, baseline[1]];
            SplitReplicationReservationOutcome outcome = Assert.Single(
                kernel.ReserveBatch(
                    1,
                    [request],
                    actors,
                    Slots(actors),
                    [])
                .Outcomes);
            Assert.Equal(
                SplitReplicationReservationOutcome.SplitReservationBlockReason
                    .SourceNotEligible,
                outcome.Reason);
        }
    }

    [Fact]
    public void Facing_RotatesDeclaredOffsetsInSourceLocalCoordinates()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        var kernel = new SplitReplicationKernel(definition);
        SplitReplicationActorSnapshot[] actors = Actors(
            westPosition: new Position(3, 3),
            eastPosition: new Position(7, 3));
        actors[0] = actors[0] with { Facing = Direction.East };

        SplitReplicationReservation reservation = Assert.Single(
            kernel.ReserveBatch(
                0,
                [new(new ActorIdentity(0, 0, 0), "split-mobile", "east-facing")],
                actors,
                Slots(actors),
                [])
            .Reservations);

        Assert.Equal(
            [new Position(3, 2), new Position(3, 4)],
            reservation.Descendants.Select(descendant => descendant.Position));
    }

    private static SplitReplicationActorSnapshot[] Actors(
        Position westPosition,
        Position eastPosition) =>
        [
            new(
                new ActorIdentity(0, 0, 0),
                ParticipantId: 10,
                Generation: 0,
                FormId: "mobile",
                Health: 3,
                westPosition,
                Direction.North,
                HasPriorSameLifeTransition: false,
                HasPendingSameLifeTransition: false),
            new(
                new ActorIdentity(1, 0, 0),
                ParticipantId: 20,
                Generation: 0,
                FormId: "mobile",
                Health: 3,
                eastPosition,
                Direction.North,
                HasPriorSameLifeTransition: false,
                HasPendingSameLifeTransition: false),
        ];

    private static SplitReplicationSlotSnapshot[] Slots(
        IReadOnlyCollection<SplitReplicationActorSnapshot> actors)
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

    private static SplitReplicationSlotSnapshot ActiveOrReady(
        int teamId,
        int unitId,
        IReadOnlyDictionary<(int TeamId, int UnitId), ActorIdentity> active) =>
        active.TryGetValue((teamId, unitId), out ActorIdentity? actorId)
            ? new(
                teamId,
                unitId,
                SplitReplicationSlotSnapshot.SplitSlotState.Active,
                actorId)
            : new(
                teamId,
                unitId,
                SplitReplicationSlotSnapshot.SplitSlotState.Ready,
                ActiveActorId: null);
}
