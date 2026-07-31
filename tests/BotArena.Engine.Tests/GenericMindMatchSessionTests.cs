using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// P1's behavioural contract for the participant-scoped coordinator
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.4, §2.7, §4.2, §4.7).
/// </summary>
public sealed class GenericMindMatchSessionTests
{
    [Fact]
    public void AMindProfileSessionDrivesEveryBodyThroughOneRuntime()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition
                    .CreateAutomaticCompanionsExperiment());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                    GenericMindSessionTestFixture.ScriptedMind(
                        definition,
                        observation));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 4_242);

        // One observation per participant, and the complete slot table on
        // every one of them from the very first tick (§13.2).
        GenericActorMatchPreparedTick prepared = session.PrepareTick();
        Assert.Equal(2, prepared.MindObservations.Length);
        Assert.All(
            prepared.MindObservations,
            observation => Assert.Equal(3, observation.Slots.Length));

        const int ticks = 265;
        for (int tick = 0; tick < ticks && !session.IsCompleted; tick++)
            session.Step();

        // ONE runtime, ONE StartMatch, and one Think per tick — for three
        // bodies. Under the per-life profile the same match would have
        // created a Store per life and paid startup fuel every time a
        // companion arrived.
        Assert.All(
            factories.Values,
            factory => Assert.Equal(1, factory.CreateCount));
        Assert.All(
            factories.Values,
            factory => Assert.Single(factory.Starts));
        Assert.All(
            factories.Values,
            factory => Assert.Equal(ticks, factory.ThinkCount));
        // The companions arrive mid-match and the SAME mind commands them:
        // no new instance, no lost memory, no re-derivation.
        Assert.All(
            factories.Values,
            factory => Assert.Equal(
                3,
                factory.Observations.Max(
                    observation => observation.Bodies.Length)));
        Assert.All(
            factories.Values,
            factory => Assert.All(
                factory.Observations,
                observation => Assert.Equal(
                    observation.Bodies.Length,
                    observation.Bodies
                        .Select(body => body.ActorId)
                        .Distinct()
                        .Count())));
    }

    [Fact]
    public void TheMindReceivesItsMatchStartOnceAndItsBodiesEveryTick()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 11);

        for (int tick = 0; tick < 5; tick++)
            session.Step();

        GenericMindSessionTestFixture.RecordingMindFactory first =
            factories.Values.First();
        GenericMindRuntimeStart start = Assert.Single(first.Starts);
        Assert.Equal(
            definition.CapabilityVersions.MatchStartSchemaVersion,
            start.SchemaVersion);
        Assert.Equal(
            definition.CapabilityVersions.RuntimeContractVersion,
            start.RuntimeContractVersion);
        // Head-to-head: one participant per scoring team, so there are no
        // allied minds. The collection exists anyway — it is the 2v2 hook.
        Assert.Empty(start.AlliedParticipantIds);
        Assert.Equal(
            SeedDerivation.DeriveTeamSeed(
                11,
                start.TeamId,
                definition.Rules.SeedMechanics.SeedProfileId),
            start.TeamRandomSeed);
        Assert.NotEqual(start.TeamRandomSeed, start.MindRandomSeed);
        Assert.Equal(5, first.Observations.Count);
        Assert.Equal(
            [0, 1, 2, 3, 4],
            first.Observations.Select(observation => observation.Tick));
    }

    [Fact]
    public void EveryLiveBodyIsPreFilledWithWaitWhenTheMindCommandsNothing()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 5);

        GenericActorMatchStepResult step = session.Step();

        // Forgetting a body costs that body one tick, visibly — not the match.
        Assert.Equal(2, step.ActionResolutions.Length);
        Assert.All(
            step.ActionResolutions,
            resolution => Assert.Equal(
                GenericActorRuntimeActionResolution.ActionOutcome.Success,
                resolution.Resolution.Outcome));
        Assert.All(
            step.ActionResolutions,
            resolution => Assert.Equal(
                ActorActionKind.Wait,
                definition.Rules.Actions
                    .Single(action =>
                        action.Id
                            == resolution.Resolution.AcceptedAction.ActionId)
                    .Kind));
        Assert.All(
            step.MindTurns,
            turn => Assert.Empty(turn.Commands));
        Assert.All(
            step.MindTurns,
            turn => Assert.Single(turn.ResolvedBodies));
    }

    [Fact]
    public void CommandingADeadBodyIsRejectedAndNeverFaults()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        ActorActionDefinition wait = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Wait);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, _) => new GenericMindRuntimeDecisions(
                [
                    // A body that never existed on this participant: a plan
                    // outliving its executor is the normal case under
                    // persistent memory, so this must be forgivable.
                    new GenericMindCommand(
                        UnitId: 7,
                        LifeId: 3,
                        wait.Id,
                        wait.Code,
                        []),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 6);

        GenericActorMatchStepResult step = session.Step();

        Assert.All(
            step.MindTurns,
            turn => Assert.Null(turn.RuntimeFault));
        Assert.All(
            step.MindTurns,
            turn => Assert.Equal(
                GenericMindCommandOutcome.Rejected,
                Assert.Single(turn.Commands).Outcome));
        Assert.Empty(step.RuntimeTick.Faults);
        Assert.All(
            step.RuntimeTick.NewlyDisqualifiedParticipantIds,
            _ => Assert.Fail("A rejected command must not disqualify."));
        // The bodies it DID own still acted: a rejected key costs nothing.
        Assert.All(
            step.ActionResolutions,
            resolution => Assert.Equal(
                GenericActorRuntimeActionResolution.ActionOutcome.Success,
                resolution.Resolution.Outcome));
    }

    [Fact]
    public void TwoCommandsForOneBodyFaultTheParticipant()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        ActorActionDefinition wait = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Wait);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId
                    == definition.Topology.Participants[0].ParticipantId
                    ? new GenericMindRuntimeDecisions(
                    [
                        new GenericMindCommand(0, 0, wait.Id, wait.Code, []),
                        new GenericMindCommand(0, 0, wait.Id, wait.Code, []),
                    ])
                    : GenericMindRuntimeDecisions.Empty);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 7);

        GenericActorMatchStepResult step = session.Step();

        GenericMindRuntimeTurn faulted = Assert.Single(
            step.MindTurns.Where(turn => turn.RuntimeFault is not null));
        Assert.Equal(
            GenericActorRuntimeFaultCodes.DuplicateBodyCommand,
            faulted.RuntimeFault!.FaultCode);
        // The shipped allowance is zero, so the first fault of any kind
        // disqualifies the participant and every slot it controls — exactly
        // what a per-life trap costs today.
        Assert.True(faulted.RuntimeFault.DisqualificationTriggered);
        Assert.Contains(
            faulted.ParticipantId,
            step.RuntimeTick.NewlyDisqualifiedParticipantIds);
        Assert.True(step.IsCompleted);
    }

    [Fact]
    public void AMalformedActionFaultsAndEveryOwnBodyWaits()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        int faultingParticipant =
            definition.Topology.Participants[0].ParticipantId;
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == faultingParticipant
                    ? new GenericMindRuntimeDecisions(
                    [
                        new GenericMindCommand(
                            0,
                            0,
                            "not-an-action",
                            9_999,
                            []),
                    ])
                    : GenericMindRuntimeDecisions.Empty);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8);

        GenericActorMatchStepResult step = session.Step();

        GenericMindRuntimeTurn faulted = Assert.Single(
            step.MindTurns.Where(turn => turn.RuntimeFault is not null));
        Assert.Equal(
            GenericActorRuntimeFaultCodes.UnknownAction,
            faulted.RuntimeFault!.FaultCode);
        Assert.All(
            step.ActionResolutions
                .Where(resolution =>
                    resolution.ParticipantId == faultingParticipant),
            resolution => Assert.Equal(
                GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
                resolution.Resolution.Outcome));
    }

    [Fact]
    public void TheMindTicksOnATickItOwnsNoBody()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                GenericDeathmatchSessionTestFixture.Definition(
                    "head-to-head",
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 10,
                        MaxHealth = 1,
                        DamagePerHit = 1,
                        RespawnDelayTicks = 6,
                    }));
        ActorActionDefinition shoot = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Attack);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10 && observation.Tick == 0
                        ? new GenericMindRuntimeDecisions(
                        [
                            .. observation.Bodies.Select(body =>
                                new GenericMindCommand(
                                    body.ActorId.UnitId,
                                    body.ActorId.LifeId,
                                    shoot.Id,
                                    shoot.Code,
                                    [
                                        new GenericActorRuntimeActionArgument
                                            .ShotProgramArgument(
                                                ShotProgram.Straight),
                                    ])),
                        ])
                        : GenericMindRuntimeDecisions.Empty);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 42);

        int ticks = 0;
        while (!session.IsCompleted)
        {
            session.Step();
            ticks++;
        }

        GenericMindSessionTestFixture.RecordingMindFactory victim =
            factories[20];
        // The body died and its replacement is six ticks away. The mind still
        // ticked through the gap: going dark there would lose the ability to
        // plan the return, and would blind it during exactly the window its
        // enemy beliefs decay fastest (§2.7).
        Assert.Contains(
            victim.Observations,
            observation => observation.Bodies.IsEmpty);
        Assert.Equal(ticks, victim.ThinkCount);
        Assert.Equal(ticks, factories[10].ThinkCount);
        Assert.Equal(1, victim.CreateCount);
        // And on a bodyless tick the slot table still says when it returns,
        // and the world still moves.
        GenericMindRuntimeObservation bodyless = victim.Observations
            .First(observation => observation.Bodies.IsEmpty);
        Assert.NotEmpty(bodyless.Slots);
        Assert.NotEmpty(bodyless.Team.Participants);
        Assert.Contains(
            bodyless.Slots,
            slot => slot.State
                is GenericActorRuntimeObservation.UnitSlotState
                    .AutomaticReturnPending
                or GenericActorRuntimeObservation.UnitSlotState
                    .AvailabilityPending);
        // Base fuel is available at zero bodies, which is what makes the
        // invariant affordable.
        Assert.Equal(
            GenericMindTickBudget.BaseTickFuel,
            GenericMindTickBudget.TickFuel(0));
    }

    [Fact]
    public void TheTickFuelBudgetTracksTheLiveBodyCount()
    {
        Assert.Equal(250_000_000, GenericMindTickBudget.TickFuel(0));
        Assert.Equal(450_000_000, GenericMindTickBudget.TickFuel(1));
        Assert.Equal(2_050_000_000, GenericMindTickBudget.TickFuel(9));
        // The per-body term is EXACTLY today's per-life budget, which is what
        // keeps the null pin from being confounded by a compute difference.
        Assert.Equal(
            200_000_000,
            GenericMindTickBudget.TickFuel(2)
                - GenericMindTickBudget.TickFuel(1));

        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 10);

        GenericActorMatchStepResult step = session.Step();

        Assert.All(
            step.MindTurns,
            turn => Assert.Equal(
                GenericMindTickBudget.TickFuel(turn.LiveOwnBodyCount),
                turn.TickFuelBudget));
        Assert.All(
            step.MindTurns,
            turn => Assert.Equal(450_000_000, turn.TickFuelBudget));
    }

    [Fact]
    public void ReservedIntentsAreRejectedRatherThanFaulted()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, _) => new GenericMindRuntimeDecisions(
                    [],
                    [new GenericMindDeclaredIntent("push-centre", 3)]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 12);

        GenericActorMatchStepResult step = session.Step();

        Assert.All(
            step.MindTurns,
            turn => Assert.Null(turn.RuntimeFault));
        Assert.All(
            step.MindTurns,
            turn => Assert.Equal(
                "push-centre",
                Assert.Single(turn.RejectedIntents).TagId));
        // And nothing is ever delivered: the reservation ships an empty
        // collection, not a channel.
        Assert.All(
            factories.Values,
            factory => Assert.All(
                factory.Observations,
                observation => Assert.Empty(observation.AlliedIntents)));
    }

    [Fact]
    public void AlliesAreEmptyInHeadToHeadAndBodiesCarryTheirOwnOrigin()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition.Create());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 13);

        GenericActorMatchPreparedTick prepared = session.PrepareTick();

        foreach (GenericMindRuntimeObservation observation in
                 prepared.MindObservations)
        {
            // One participant per scoring team, so "allied bodies I do not
            // command" is empty by construction — the boundary made
            // structural rather than documented.
            Assert.Empty(observation.Allies);
            Assert.All(
                observation.Bodies,
                body => Assert.Equal(
                    GenericActorRuntimeStart.SpawnReason.Initial,
                    body.Origin.Reason));
            Assert.All(
                observation.Bodies,
                body => Assert.Equal(0, body.LifeStartedTick));
            // A life's first tick has no previous position, so it has not
            // moved — the rule an author had to derive by hand and could get
            // silently wrong.
            Assert.All(
                observation.Bodies,
                body => Assert.False(body.MovedLastTick));
            Assert.All(
                observation.Bodies,
                body => Assert.Null(body.PreviousPosition));
            Assert.All(observation.Bodies, body => Assert.Null(body.RoleTag));
        }
    }

    [Fact]
    public void MovedLastTickFollowsTheBodyTheMindActuallyMoved()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                GenericDeathmatchSessionTestFixture.Definition(
                    "head-to-head",
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 6,
                    }));
        ActorActionDefinition move = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Movement);
        int mover = definition.Topology.Participants[0].ParticipantId;
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (start, observation) => start.ParticipantId == mover
                    && observation.Tick % 2 == 0
                    ? new GenericMindRuntimeDecisions(
                    [
                        .. observation.Bodies.Select(body =>
                            new GenericMindCommand(
                                body.ActorId.UnitId,
                                body.ActorId.LifeId,
                                move.Id,
                                move.Code,
                                [
                                    new GenericActorRuntimeActionArgument
                                        .DirectionArgument(Direction.North),
                                ])),
                    ])
                    : GenericMindRuntimeDecisions.Empty);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 14);

        while (!session.IsCompleted)
            session.Step();

        // The fact is published, not reconstructed: 8 of 8 wave-8 authors
        // asked for it, and every one of them had to hand-roll nine lines
        // with a documented footgun.
        Assert.Contains(
            factories[mover].Observations.SelectMany(
                observation => observation.Bodies),
            body => body.MovedLastTick);
        // The body that never moved always says so.
        Assert.All(
            factories.Values.Last().Observations
                .SelectMany(observation => observation.Bodies),
            body => Assert.False(body.MovedLastTick));
        // And the published fact always agrees with the published positions.
        Assert.All(
            factories.Values.SelectMany(factory => factory.Observations)
                .SelectMany(observation => observation.Bodies),
            body => Assert.Equal(
                body.PreviousPosition is Position previous
                    && previous != body.Position,
                body.MovedLastTick));
    }

    private static GenericMindRuntimeDecisions Shoot(
        ActorResolvedMatchDefinition definition,
        GenericMindRuntimeObservation observation)
    {
        ActorActionDefinition shoot = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Attack);
        return new GenericMindRuntimeDecisions(
        [
            .. observation.Bodies.Select(body =>
                new GenericMindCommand(
                    body.ActorId.UnitId,
                    body.ActorId.LifeId,
                    shoot.Id,
                    shoot.Code,
                    [
                        new GenericActorRuntimeActionArgument
                            .ShotProgramArgument(ShotProgram.Straight),
                    ])),
        ]);
    }
}
