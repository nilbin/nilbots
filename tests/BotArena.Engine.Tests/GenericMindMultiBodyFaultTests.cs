namespace BotArena.Engine.Tests;

/// <summary>
/// THE MULTI-BODY MIND FAULT (DECISIONS #192; the pre-friction pass).
/// <para>
/// A trapped mind stops every body it owns, and each of those bodies records a
/// faulted turn. Per-life evidence refuses a faulted turn whose fault names a
/// DIFFERENT actor, so the participant's one fault has to be restated under
/// each stopped body's own identity. Before the fix the whole fan-out carried
/// the canonically-first body's identity, which validated only when the mind
/// happened to own exactly one body: on any larger roster — the shipped legion
/// game, and therefore every real mind match — the abort killed the whole
/// document instead of recording a clean participant disqualification. The null
/// pin hit it with a pre-mind artifact faulting at startup.
/// </para>
/// </summary>
public sealed class GenericMindMultiBodyFaultTests
{
    /// <summary>
    /// The legion roster fields three bodies per participant at tick 0, which
    /// is the smallest contract that reproduces the abort.
    /// </summary>
    private static ActorResolvedMatchDefinition LegionMindDefinition() =>
        GenericMindSessionTestFixture.OnMindProfile(
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                roster: FrontlineLabsRosterArm.Legion));

    [Fact]
    public void AStartupTrapWithManyBodiesCompletesAsADisqualification()
    {
        ActorResolvedMatchDefinition definition = LegionMindDefinition();
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant =>
                    new GenericMindSessionTestFixture.RecordingMindFactory(
                        (_, observation) =>
                            participant.ParticipantId == 0
                                ? throw new InvalidOperationException(
                                    "a pre-mind guest traps on its first tick")
                                : GenericMindSessionTestFixture.ScriptedMind(
                                    definition,
                                    observation)));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_675_309);

        GenericActorMatchResult result = session.Run();

        // The match RESOLVES: the trapped participant is disqualified and the
        // healthy one is the only eligible team, instead of the run aborting.
        Assert.Equal(1, Assert.Single(result.EligibleTeamIds));
        Assert.Equal(1, result.WinnerTeamId);
        Assert.Equal("fault-eligibility", result.CompletionReason);
    }

    [Fact]
    public void EveryStoppedBodyRestatesTheParticipantsOneFault()
    {
        ActorResolvedMatchDefinition definition = LegionMindDefinition();
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant =>
                    new GenericMindSessionTestFixture.RecordingMindFactory(
                        (_, observation) =>
                            participant.ParticipantId == 0
                                ? throw new InvalidOperationException(
                                    "a pre-mind guest traps on its first tick")
                                : GenericMindSessionTestFixture.ScriptedMind(
                                    definition,
                                    observation)));
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
        // The repro shape: more than one own live body under the trap.
        Assert.True(faulted.LiveOwnBodyCount > 1);

        GenericActorMatchActorTurn[] stopped = [
            .. session.Chronology.Ticks
                .SelectMany(frame => frame.ActorTurns)
                .Where(turn =>
                    turn.Tick == faulted.Tick
                    && turn.ParticipantId == faulted.ParticipantId),
        ];
        Assert.Equal(faulted.LiveOwnBodyCount, stopped.Length);
        foreach (GenericActorMatchActorTurn turn in stopped)
        {
            GenericActorRuntimeFault fault =
                turn.ActionResolution.RuntimeFault!;
            // Each body names ITSELF...
            Assert.Equal(turn.ActorId, fault.ActorId);
            // ...while the participant-scoped facts stay the participant's:
            // N restatements are still ONE fault, not N.
            Assert.Equal(faulted.ParticipantId, fault.ParticipantId);
            Assert.Equal(faulted.RuntimeFault!.FaultCode, fault.FaultCode);
            Assert.Equal(
                faulted.RuntimeFault.CumulativeFaultCount,
                fault.CumulativeFaultCount);
            Assert.Equal(
                faulted.RuntimeFault.DisqualificationTriggered,
                fault.DisqualificationTriggered);
        }

        // And exactly one team-private fault EVENT is published, naming the
        // canonically first body — the event projection is unchanged.
        GenericActorAuthoritativeEvent[] faultEvents = [
            .. session.Chronology.Ticks
                .SelectMany(frame => frame.Events)
                .Where(item =>
                    item.Kind
                    == GenericActorRuntimeObservation.EventKind.RuntimeFault),
        ];
        GenericActorAuthoritativeEvent single = Assert.Single(faultEvents);
        var payload = Assert.IsType<
            GenericActorRuntimeObservation.EventPayload.RuntimeFault>(
            single.Payload);
        Assert.Equal(
            faulted.ResolvedBodies.Order().First(),
            payload.Fault.ActorId);
    }

    [Fact]
    public void TheFaultedMatchStillWritesAVerifyingDocument()
    {
        ActorResolvedMatchDefinition definition = LegionMindDefinition();
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant =>
                    new GenericMindSessionTestFixture.RecordingMindFactory(
                        (_, observation) =>
                            participant.ParticipantId == 0
                                ? throw new InvalidOperationException(
                                    "a pre-mind guest traps on its first tick")
                                : GenericMindSessionTestFixture.ScriptedMind(
                                    definition,
                                    observation)));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_675_309);
        session.Run();

        GenericActorReplayDocument document =
            GenericActorReplayDocument.Create(session.Chronology);
        Assert.True(
            GenericActorReplayDocument.VerifyHash(
                document.CanonicalJson,
                out string? failure),
            failure);
    }
}
