namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the FaceMovementDirection movement coupling (DECISIONS #155/#156): a
/// successful step turns the body to the direction it moved and the Movement
/// event is the evidence of that change, while a Blocked step changes
/// nothing. PreserveFacing is included as the control so the tests measure
/// the coupling rather than the arena.
/// </summary>
public sealed class ActorFaceMovementDirectionTests
{
    [Fact]
    public void SuccessfulMoveTurnsTheBodyAndTheEventCarriesTheNewFacing()
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(
                ActorMovementFacingCoupling.FaceMovementDirection);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Move(
                        Direction.North)
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchStepResult step = session.Step(
            session.PrepareTick().Observations);
        GenericActorWorldSnapshot.LifeSnapshot mover =
            MovementFacingCouplingTestContracts.MoverAfter(step);
        GenericActorRuntimeObservation.EventPayload.Movement movement =
            MovementFacingCouplingTestContracts.MovementOf(step, teamId: 0);

        Assert.Equal(new Position(1, 2), mover.Position);
        Assert.Equal(Direction.North, mover.Facing);
        Assert.Equal(new Position(1, 3), movement.From);
        Assert.Equal(new Position(1, 2), movement.To);
        // The event is the facing-change evidence, so it must already carry
        // the post-step facing rather than the pose the life entered with.
        Assert.Equal(Direction.North, movement.Facing);
    }

    [Fact]
    public void BlockedMoveChangesNeitherPositionNorFacing()
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(
                ActorMovementFacingCoupling.FaceMovementDirection);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(
                definition,
                (start, _) => start.ParticipantId == 10
                    // Due west of (1,3) is the arena wall.
                    ? GenericDeathmatchSessionTestFixture.Move(Direction.West)
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchStepResult step = session.Step(
            session.PrepareTick().Observations);
        GenericActorWorldSnapshot.LifeSnapshot mover =
            MovementFacingCouplingTestContracts.MoverAfter(step);
        GenericActorRuntimeObservation.EventPayload.MovementBlocked blocked =
            MovementFacingCouplingTestContracts.BlockedMovementOf(
                step,
                teamId: 0);

        Assert.Equal(new Position(1, 3), mover.Position);
        Assert.Equal(Direction.East, mover.Facing);
        Assert.Equal(new Position(0, 3), blocked.AttemptedTo);
        Assert.Equal(Direction.East, blocked.Facing);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            step.ActionResolutions
                .Single(resolution => resolution.ActorId.TeamId == 0)
                .Resolution.Outcome);
    }

    [Fact]
    public void PreserveFacingLeavesTheBodyPointingWhereItWas()
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(
                ActorMovementFacingCoupling.PreserveFacing);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Move(
                        Direction.North)
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchStepResult step = session.Step(
            session.PrepareTick().Observations);
        GenericActorWorldSnapshot.LifeSnapshot mover =
            MovementFacingCouplingTestContracts.MoverAfter(step);

        Assert.Equal(new Position(1, 2), mover.Position);
        Assert.Equal(Direction.East, mover.Facing);
        Assert.Equal(
            Direction.East,
            MovementFacingCouplingTestContracts
                .MovementOf(step, teamId: 0)
                .Facing);
    }

    [Fact]
    public void CoupledFacingSurvivesTheChronologyCausalityValidator()
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(
                ActorMovementFacingCoupling.FaceMovementDirection);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(
                definition,
                (start, observation) => start.ParticipantId != 10
                    ? GenericDeathmatchSessionTestFixture.Wait()
                    : observation.Tick % 2 == 0
                        ? GenericDeathmatchSessionTestFixture.Move(
                            Direction.North)
                        : GenericDeathmatchSessionTestFixture.Move(
                            Direction.South));

        // The chronology validates every tick's evidence as it is recorded;
        // a facing change with no rotation event would be rejected there if
        // movement coupling had not been taught to it.
        session.Run();

        Assert.Equal(4, session.Chronology.Ticks.Length);
        Assert.DoesNotContain(
            session.Chronology.Ticks.SelectMany(tick => tick.Events),
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.RuntimeFault);
        Assert.DoesNotContain(
            session.Chronology.Ticks.SelectMany(tick => tick.Events),
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.Rotation);
        Assert.Equal(
            Direction.South,
            session.Chronology.Ticks[^1].PostState.ActiveLives
                .Single(life => life.ActorId.TeamId == 0)
                .Facing);
    }
}
