namespace BotArena.Engine.Tests;

public sealed class ActorMovementCadenceTests
{
    [Fact]
    public void StableUnitIdStaggersPublicMoveOpportunities()
    {
        var profile = new ActorMovementProfileDefinition(
            "paced-ground",
            ActorMovementLayer.Ground,
            moveOpportunitiesPerCycle: 2,
            moveCycleTicks: 3);

        Assert.Equal(
            [true, true, false, true, true, false],
            Enumerable.Range(0, 6)
                .Select(tick => ActorMovementCadence.IsMoveOpportunity(
                    profile,
                    new ActorIdentity(0, 0, 0),
                    tick))
                .ToArray());
        Assert.Equal(
            [true, false, true, true, false, true],
            Enumerable.Range(0, 6)
                .Select(tick => ActorMovementCadence.IsMoveOpportunity(
                    profile,
                    new ActorIdentity(0, 1, 0),
                    tick))
                .ToArray());
    }

    [Fact]
    public void ActionMaskAndResolutionUseTheSameCadence()
    {
        ActorResolvedMatchDefinition definition = WithCadence(
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 6,
                }),
            opportunities: 2,
            cycleTicks: 3);
        var availability = new List<bool>();
        Dictionary<int, GenericDeathmatchSessionTestFixture.RecordingFactory>
            factories = GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                {
                    GenericActorRuntimeActionLegality move =
                        observation.ActionLegalities.Single(action =>
                            action.ActionId == "move");
                    if (observation.Self.ActorId.TeamId != 0)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    availability.Add(move.Available);
                    return move.Available
                        ? GenericDeathmatchSessionTestFixture.Move(Direction.East)
                        : GenericDeathmatchSessionTestFixture.Wait();
                });
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition, factories),
            matchSeed: 9_001);

        GenericActorMatchStepResult? step = null;
        while (!session.IsCompleted)
            step = session.Step();

        Assert.Equal(
            [true, true, false, true, true, false],
            availability);
        Assert.Equal(
            new Position(5, 3),
            step!.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0).Position);
    }

    [Fact]
    public void CommandOnAClosedMoveTickIsDefensivelyBlocked()
    {
        ActorResolvedMatchDefinition definition = WithCadence(
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                }),
            opportunities: 2,
            cycleTicks: 3);
        Dictionary<int, GenericDeathmatchSessionTestFixture.RecordingFactory>
            factories = GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                    observation.Self.ActorId.TeamId == 0
                    && observation.Tick == 2
                        ? GenericDeathmatchSessionTestFixture.Move(Direction.East)
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition, factories),
            matchSeed: 9_002);

        session.Step();
        session.Step();
        GenericActorMatchStepResult closedTick = session.Step();

        GenericActorMatchActorResolution resolution =
            closedTick.ActionResolutions.Single(value =>
                value.ActorId.TeamId == 0);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            resolution.Resolution.Outcome);
        Assert.DoesNotContain(
            closedTick.Events,
            item => item.Payload is GenericActorRuntimeObservation.EventPayload
                .Movement movement
                && movement.ActorId.TeamId == 0);
        Assert.Equal(
            new Position(1, 3),
            closedTick.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0).Position);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    public void InvalidCadenceIsRejected(int opportunities, int cycleTicks)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ActorMovementProfileDefinition(
                "invalid",
                ActorMovementLayer.Ground,
                moveOpportunitiesPerCycle: opportunities,
                moveCycleTicks: cycleTicks));
    }

    private static ActorResolvedMatchDefinition WithCadence(
        ActorResolvedMatchDefinition source,
        int opportunities,
        int cycleTicks)
    {
        ActorRulesDefinition rules = source.Rules;
        ActorRulesDefinition paced = new(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles.Select(profile =>
                new ActorMovementProfileDefinition(
                    profile.Id,
                    profile.MovementLayer,
                    profile.FacingCoupling,
                    opportunities,
                    cycleTicks)),
            rules.VisionProfiles,
            rules.AttackProfiles,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            paced,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }
}
