using System.Collections.Immutable;
using BotArena.Runtime.Wasm;
using Engine = BotArena.Engine;

namespace BotArena.Runtime.Wasm.Tests;

/// <summary>
/// A hand-written WASM mind driving a REAL match session, sandbox and all.
/// Everything else in this suite exercises the runtime seam directly; this
/// exercises the whole path — session, coordinator, mapper, codec, protocol,
/// guest — with fuel and memory actually enforced.
/// </summary>
public sealed class WasmGenericMindSessionTests
{
    [Fact]
    public void AWasmMindDrivesAMatchWithOneRuntimeForTheWholeMatch()
    {
        Engine.ActorResolvedMatchDefinition definition =
            OnMindProfile(Engine.FrontlineLabsDefinition.Create());
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.Happy();
        using var sandboxed = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                TickTimeoutMs = 10_000,
            });

        int sandboxedParticipant =
            definition.Topology.Participants[0].ParticipantId;
        using var session = new Engine.GenericActorMatchSession(
            definition,
            Configurations(definition, sandboxedParticipant, sandboxed),
            matchSeed: 4_242);

        var budgets = new List<(int Bodies, long Fuel)>();
        for (int tick = 0; tick < 24 && !session.IsCompleted; tick++)
        {
            Engine.GenericActorMatchStepResult step = session.Step();
            Engine.GenericMindRuntimeTurn turn = step.MindTurns.Single(
                mind => mind.ParticipantId == sandboxedParticipant);
            budgets.Add((turn.LiveOwnBodyCount, turn.TickFuelBudget));
        }

        // Every tick's budget is exactly the formula over that tick's
        // authoritative live-body count — a pure function of replayable state,
        // so two hosts compute the same number.
        Assert.All(
            budgets,
            entry => Assert.Equal(
                250_000_000L + (200_000_000L * entry.Bodies),
                entry.Fuel));
        Assert.Contains(budgets, entry => entry.Bodies > 0);

        // The sandboxed mind survived the whole run: no fault, no
        // disqualification, one Store from tick 0 to the last tick.
        Engine.GenericActorRuntimeObservation.ObservedParticipantStatus status =
            session.PrepareTick().MindObservations
                .SelectMany(observation => observation.Team.Participants)
                .First(participant =>
                    participant.ParticipantId == sandboxedParticipant);
        Assert.Equal(0, status.RuntimeFaultCount);
        Assert.False(status.Disqualified);
    }

    [Fact]
    public void AMindThatBurnsItsFuelDisqualifiesItsParticipant()
    {
        Engine.ActorResolvedMatchDefinition definition =
            OnMindProfile(Engine.FrontlineLabsDefinition.Create());
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.FuelHog();
        using var sandboxed = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                BaseTickFuel = 1_000_000,
                PerBodyTickFuel = 0,
                TickTimeoutMs = 10_000,
            });

        int sandboxedParticipant =
            definition.Topology.Participants[0].ParticipantId;
        using var session = new Engine.GenericActorMatchSession(
            definition,
            Configurations(definition, sandboxedParticipant, sandboxed),
            matchSeed: 7);

        Engine.GenericActorMatchStepResult step = session.Step();

        // The fault is participant-scoped, and under the shipped allowance of
        // zero the FIRST one disqualifies. A per-life trap already cost the
        // whole participant its match, so this is the existing policy applied
        // to a coarser unit rather than a new penalty.
        Engine.GenericMindRuntimeFault fault = Assert.Single(
            step.RuntimeTick.Faults is { Length: > 0 }
                ? step.MindTurns
                    .Where(turn => turn.RuntimeFault is not null)
                    .Select(turn => turn.RuntimeFault!)
                : []);
        Assert.Equal(sandboxedParticipant, fault.ParticipantId);
        Assert.Equal(
            Engine.GenericActorRuntimeFault.FaultStage.TickExecution,
            fault.Stage);
        Assert.True(fault.DisqualificationTriggered);
        Assert.Contains(
            sandboxedParticipant,
            step.RuntimeTick.NewlyDisqualifiedParticipantIds);
    }

    private static Engine.ActorResolvedMatchDefinition OnMindProfile(
        Engine.ActorResolvedMatchDefinition source) =>
        new(
            source.Rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            Engine.ActorMatchCapabilityVersions.Mind);

    /// <summary>
    /// The sandboxed mind on one side and a do-nothing mind on the other, so
    /// the match is a real two-participant session rather than a solo probe.
    /// </summary>
    private static ImmutableArray<Engine.GenericActorParticipantConfiguration>
        Configurations(
            Engine.ActorResolvedMatchDefinition definition,
            int sandboxedParticipant,
            Engine.IGenericMindRuntimeFactory sandboxed) =>
        [
            .. definition.Topology.Participants.Select(participant =>
                new Engine.GenericActorParticipantConfiguration
                {
                    ParticipantId = participant.ParticipantId,
                    TeamId = participant.TeamId,
                    Name = $"mind-{participant.ParticipantId}",
                    MindRuntimeFactory =
                        participant.ParticipantId == sandboxedParticipant
                            ? sandboxed
                            : new IdleMindFactory(),
                    RuntimeKind =
                        participant.ParticipantId == sandboxedParticipant
                            ? "wasm-generic-mind"
                            : "in-process-generic-mind",
                    ArtifactHash = $"mind-{participant.ParticipantId}",
                }),
        ];

    private sealed class IdleMindFactory : Engine.IGenericMindRuntimeFactory
    {
        public Engine.IGenericMindRuntime CreateRuntime() => new IdleMind();

        private sealed class IdleMind : Engine.IGenericMindRuntime
        {
            public void StartMatch(Engine.GenericMindRuntimeStart start)
            {
            }

            public Engine.GenericMindRuntimeDecisions ExecuteTick(
                Engine.GenericMindRuntimeObservation observation) =>
                Engine.GenericMindRuntimeDecisions.Empty;
        }
    }
}
