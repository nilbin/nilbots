namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the FacingLocked movement coupling (DECISIONS #155/#156): the
/// published Direction domain for a movement action is exactly the mover's
/// current facing, rotation keeps all four cardinals so turning stays a
/// separate decision, and resolution defensively blocks an off-facing
/// movement instead of displacing the life.
/// </summary>
public sealed class ActorFacingLockedLegalityTests
{
    private static readonly Direction[] AllCardinals =
        Enum.GetValues<Direction>();

    [Fact]
    public void MovementOffersOnlyTheFacingWhileRotationOffersAllFour()
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(
                ActorMovementFacingCoupling.FacingLocked);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(definition);

        GenericActorMatchPreparedTick prepared = session.PrepareTick();

        Assert.Equal(2, prepared.Observations.Length);
        foreach (GenericActorRuntimeObservation observation
                 in prepared.Observations)
        {
            Assert.Equal(
                [observation.Self.Facing],
                MovementFacingCouplingTestContracts.AllowedDirections(
                    observation,
                    "move"));
            Assert.Equal(
                AllCardinals,
                MovementFacingCouplingTestContracts.AllowedDirections(
                    observation,
                    "rotate"));
        }
    }

    [Theory]
    [InlineData(ActorMovementFacingCoupling.PreserveFacing)]
    [InlineData(ActorMovementFacingCoupling.FaceMovementDirection)]
    public void EveryOtherCouplingKeepsAllFourMovementDirections(
        ActorMovementFacingCoupling coupling)
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(coupling);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(definition);

        GenericActorMatchPreparedTick prepared = session.PrepareTick();

        Assert.All(
            prepared.Observations,
            observation => Assert.Equal(
                AllCardinals,
                MovementFacingCouplingTestContracts.AllowedDirections(
                    observation,
                    "move")));
    }

    [Fact]
    public void RotatingFirstMovesTheMaskWithTheBody()
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(
                ActorMovementFacingCoupling.FacingLocked);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(
                definition,
                (start, observation) => start.ParticipantId != 10
                    ? GenericDeathmatchSessionTestFixture.Wait()
                    : observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Rotate(
                            Direction.North)
                        : GenericDeathmatchSessionTestFixture.Move(
                            Direction.North));

        session.Step(session.PrepareTick().Observations);
        GenericActorMatchPreparedTick second = session.PrepareTick();
        GenericActorRuntimeObservation mover = second.Observations
            .Single(observation => observation.Self.ActorId.TeamId == 0);

        Assert.Equal(Direction.North, mover.Self.Facing);
        Assert.Equal(
            [Direction.North],
            MovementFacingCouplingTestContracts.AllowedDirections(
                mover,
                "move"));

        GenericActorMatchStepResult step = session.Step(second.Observations);

        Assert.Equal(
            new Position(1, 2),
            MovementFacingCouplingTestContracts.MoverAfter(step).Position);
        // FacingLocked commits movement to the facing; it never rewrites it.
        Assert.Equal(
            Direction.North,
            MovementFacingCouplingTestContracts.MoverAfter(step).Facing);
    }

    [Fact]
    public void OffFacingMovementFaultsAndNeverDisplacesTheLife()
    {
        ActorResolvedMatchDefinition definition =
            MovementFacingCouplingTestContracts.Deathmatch(
                ActorMovementFacingCoupling.FacingLocked,
                faultsAllowedBeforeDisqualification: 2);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(
                definition,
                (start, _) => start.ParticipantId == 10
                    // The life faces East; North is outside its mask.
                    ? GenericDeathmatchSessionTestFixture.Move(
                        Direction.North)
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchStepResult step = session.Step(
            session.PrepareTick().Observations);
        GenericActorWorldSnapshot.LifeSnapshot mover =
            MovementFacingCouplingTestContracts.MoverAfter(step);

        Assert.Equal(new Position(1, 3), mover.Position);
        Assert.Equal(Direction.East, mover.Facing);
        Assert.Contains(
            step.Events,
            item => item.Payload is
                GenericActorRuntimeObservation.EventPayload.RuntimeFault
                    fault
                && fault.Fault.FaultCode
                    == GenericActorRuntimeFaultCodes.ArgumentOutOfDomain);
    }
}
