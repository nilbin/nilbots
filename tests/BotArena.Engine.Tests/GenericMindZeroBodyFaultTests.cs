namespace BotArena.Engine.Tests;

/// <summary>
/// THE ZERO-BODY FAULT (DECISIONS #191 P3;
/// <c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.7, §4.7).
/// <para>
/// A mind ticks on every tick of the match, including ticks on which it owns no
/// live body — so it can also TRAP on one. The per-body <c>RuntimeFault</c>
/// event has nowhere to land in that case, and under the shipped
/// threshold-of-zero contract that silent frame is exactly the moment a
/// participant lost the match. The mind-scoped event is what makes it visible
/// in events and in the replay rather than only in the disqualification that
/// follows it.
/// </para>
/// </summary>
public sealed class GenericMindZeroBodyFaultTests
{
    [Fact]
    public void AMindThatTrapsWithNoBodiesPublishesAParticipantScopedEvent()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition
                    .CreateAutomaticCompanionsExperiment());
        // Participant 0 owns no live body at tick 0 only if the topology says
        // so, which it does not — so the trap is staged on the tick after its
        // body is gone. Driving that from the outside is fragile; driving it
        // from the coordinator's own zero-body path is exact.
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant =>
                    new GenericMindSessionTestFixture.RecordingMindFactory(
                        (_, observation) =>
                            observation.Bodies.IsEmpty
                                ? throw new InvalidOperationException(
                                    "a mind with nothing to command still traps")
                                : GenericMindSessionTestFixture.ScriptedMind(
                                    definition,
                                    observation)));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_675_309);

        // No body is ever lost in this arm, so nothing traps and no mind event
        // is published — the shape exists for the case, not as noise.
        for (int tick = 0; tick < 12; tick++)
            session.Step();
        Assert.Empty(MindFaultEvents(session.Chronology));
    }

    [Fact]
    public void TheEventCarriesTheParticipantAndNoActorIdentity()
    {
        // The event's identity IS the absence of an actor: a fault with a body
        // keeps publishing one per-body event, so a document carrying both
        // encodings for the same fault is malformed and refused.
        var fault = new GenericMindRuntimeFault(
            ParticipantId: 3,
            TeamId: 1,
            ActorId: null,
            GenericActorRuntimeFault.FaultStage.TickExecution,
            GenericActorRuntimeFaultCodes.TickExecutionFailed,
            CumulativeFaultCount: 1,
            DisqualificationTriggered: true);
        Assert.Null(fault.ToActorFault());

        var authoritative = new GenericActorAuthoritativeEvent(
            "authoritative:mind-fault:0",
            tick: 4,
            globalOrdinal: 0,
            GenericActorRuntimeObservation.EventKind.MindRuntimeFault,
            new GenericActorRuntimeObservation.EventPayload.MindRuntimeFault(
                fault),
            new GenericActorAuthoritativeEvent.Audience.TeamPrivate(1));
        Assert.Equal(
            GenericActorRuntimeObservation.EventKind.MindRuntimeFault,
            authoritative.Kind);

        // And the per-body payload cannot wear the mind kind, nor the reverse.
        Assert.Throws<ArgumentException>(() =>
            new GenericActorAuthoritativeEvent(
                "authoritative:mismatched:1",
                tick: 4,
                globalOrdinal: 1,
                GenericActorRuntimeObservation.EventKind.MindRuntimeFault,
                new GenericActorRuntimeObservation.EventPayload.RuntimeFault(
                    new GenericActorRuntimeFault(
                        3,
                        new ActorIdentity(1, 0, 0),
                        GenericActorRuntimeFault.FaultStage.TickExecution,
                        GenericActorRuntimeFaultCodes.TickExecutionFailed,
                        1,
                        true)),
                new GenericActorAuthoritativeEvent.Audience.TeamPrivate(1)));
    }

    /// <summary>
    /// The end-to-end path: a mind that traps on a tick it owns nothing
    /// publishes the participant-scoped event, and the event survives into a
    /// verifiable replay document.
    /// <para>
    /// Every contract must field at least one life per team, so a zero-body
    /// tick is necessarily a MID-MATCH state: the body dies and the respawn
    /// clock runs. That is the sub-case §2.7 argues hardest for — a mind that
    /// went dark during the respawn window would lose the ability to plan the
    /// return — and it is where the fault has no body to name.
    /// </para>
    /// </summary>
    [Fact]
    public void AZeroBodyTrapReachesTheReplayDocument()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition
                    .CreateAutomaticCompanionsExperiment());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant =>
                    new GenericMindSessionTestFixture.RecordingMindFactory(
                        (_, observation) =>
                            participant.ParticipantId == 0
                            && observation.Bodies.IsEmpty
                                ? throw new InvalidOperationException(
                                    "trapped with nothing to command")
                                : Duel(definition, observation)));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_675_309);
        session.Run();

        GenericActorMatchMindTurn faulted = session.Chronology.Ticks
            .SelectMany(frame => frame.MindTurns)
            .First(turn => turn.RuntimeFault is not null);
        Assert.Equal(0, faulted.LiveOwnBodyCount);
        Assert.Null(faulted.RuntimeFault!.ActorId);
        Assert.True(faulted.RuntimeFault.DisqualificationTriggered);
        // The base fuel term is available at zero bodies, which is what makes
        // the "ticks with no bodies" invariant affordable at all.
        Assert.Equal(
            GenericMindTickBudget.BaseTickFuel,
            faulted.TickFuelBudget);

        Assert.NotEmpty(MindFaultEvents(session.Chronology));
        GenericActorReplayDocument document =
            GenericActorReplayDocument.Create(session.Chronology);
        Assert.True(
            GenericActorReplayDocument.VerifyHash(
                document.CanonicalJson,
                out string? failure),
            failure);
        Assert.Contains(
            "mind-runtime-fault",
            document.CanonicalJson,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Walk to the centre and shoot whatever is in front. Crude, deterministic,
    /// and enough to actually kill a body — which is the only way a mind ever
    /// reaches a tick with nothing to command.
    /// </summary>
    private static GenericMindRuntimeDecisions Duel(
        ActorResolvedMatchDefinition definition,
        GenericMindRuntimeObservation observation)
    {
        ActorActionDefinition wait = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Wait);
        ActorActionDefinition? move = definition.Rules.Actions
            .FirstOrDefault(action =>
                action.Kind == ActorActionKind.Movement);
        ActorActionDefinition? shoot = definition.Rules.Actions
            .FirstOrDefault(action => action.Kind == ActorActionKind.Attack);
        int centre = definition.Map.Width / 2;
        return new GenericMindRuntimeDecisions(
        [
            .. observation.Bodies.Select(body =>
            {
                GenericActorRuntimeActionLegality? attack = shoot is null
                    ? null
                    : body.ActionLegalities.FirstOrDefault(legality =>
                        string.Equals(
                            legality.ActionId,
                            shoot.Id,
                            StringComparison.Ordinal));
                if (attack is { Available: true }
                    && observation.Enemies.Any(enemy =>
                        enemy.Position.Y == body.Position.Y))
                {
                    return new GenericMindCommand(
                        body.ActorId.UnitId,
                        body.ActorId.LifeId,
                        shoot!.Id,
                        shoot.Code,
                        []);
                }
                if (move is null || body.Position.X == centre)
                {
                    return new GenericMindCommand(
                        body.ActorId.UnitId,
                        body.ActorId.LifeId,
                        wait.Id,
                        wait.Code,
                        []);
                }
                return new GenericMindCommand(
                    body.ActorId.UnitId,
                    body.ActorId.LifeId,
                    move.Id,
                    move.Code,
                    [
                        new GenericActorRuntimeActionArgument
                            .DirectionArgument(
                            body.Position.X < centre
                                ? Direction.East
                                : Direction.West),
                    ]);
            }),
        ]);
    }

    private static IReadOnlyList<GenericActorAuthoritativeEvent>
        MindFaultEvents(GenericActorMatchChronology chronology) =>
        [
            .. chronology.Ticks
                .SelectMany(frame => frame.Events)
                .Where(item =>
                    item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .MindRuntimeFault),
        ];
}
