using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Builds the mind-profile twin of an existing resolved definition and hosts a
/// minimal in-process mind. The twin differs from its per-life original in
/// EXACTLY one field — the capability tuple — which is the shape the null pin
/// needs: same rules, same map, same format, same topology, same mode, a
/// different driver.
/// </summary>
internal static class GenericMindSessionTestFixture
{
    /// <summary>
    /// Re-resolves <paramref name="source"/> on
    /// <see cref="ActorMatchCapabilityVersions.Mind"/>. Everything else is the
    /// same object graph, so a difference in outcome between the two can only
    /// come from the controller.
    /// </summary>
    public static ActorResolvedMatchDefinition OnMindProfile(
        ActorResolvedMatchDefinition source) =>
        new(
            source.Rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            ActorMatchCapabilityVersions.Mind);

    public static Dictionary<int, RecordingMindFactory> Factories(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericMindRuntimeStart,
            GenericMindRuntimeObservation,
            GenericMindRuntimeDecisions>? think = null) =>
        definition.Topology.Participants.ToDictionary(
            participant => participant.ParticipantId,
            participant => new RecordingMindFactory(
                think ?? ((_, _) => GenericMindRuntimeDecisions.Empty)));

    public static ImmutableArray<GenericActorParticipantConfiguration>
        Configurations(
            ActorResolvedMatchDefinition definition,
            IReadOnlyDictionary<int, RecordingMindFactory> factories) =>
        definition.Topology.Participants
            .Select(participant =>
                new GenericActorParticipantConfiguration
                {
                    ParticipantId = participant.ParticipantId,
                    TeamId = participant.TeamId,
                    Name = $"mind-{participant.ParticipantId}",
                    MindRuntimeFactory =
                        factories[participant.ParticipantId],
                    RuntimeKind = "in-process-generic-mind",
                    ArtifactHash = $"fixture-mind-{participant.ParticipantId}",
                })
            .ToImmutableArray();

    /// <summary>
    /// The scripted doctrine both profiles run, as a pure function of
    /// <c>(actorId, tick)</c> and the contract. Being a pure function is the
    /// whole point: it lets one mind and N per-life bots issue provably
    /// identical actions, so any divergence between the two runs is the
    /// controller's and not the doctrine's.
    /// <para>
    /// Shaped like the escorted channel (§2.5a): the lowest-numbered body holds
    /// its ground as the channeler, and the others step toward it as screens.
    /// </para>
    /// </summary>
    public static GenericActorRuntimeDecision Script(
        ActorResolvedMatchDefinition definition,
        ActorIdentity actorId,
        int tick)
    {
        ActorActionDefinition wait = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Wait);
        ActorActionDefinition? move = definition.Rules.Actions
            .FirstOrDefault(action =>
                action.Kind == ActorActionKind.Movement);
        if (move is null || actorId.UnitId == 0)
        {
            // The channeler claims the point by standing still. Stationary
            // claim is the most expensive tick this doctrine ever prices, and
            // it is exactly the decision the per-life cohort had to agree on
            // without a channel.
            return new GenericActorRuntimeDecision(wait.Id, wait.Code, [], null);
        }
        if ((tick + actorId.UnitId) % 3 != 0)
            return new GenericActorRuntimeDecision(wait.Id, wait.Code, [], null);

        Direction axis = actorId.UnitId % 2 == 1
            ? Direction.North
            : Direction.South;
        return new GenericActorRuntimeDecision(
            move.Id,
            move.Code,
            [new GenericActorRuntimeActionArgument.DirectionArgument(axis)],
            null);
    }

    /// <summary>The mind form of <see cref="Script"/>: one map, all bodies.</summary>
    public static GenericMindRuntimeDecisions ScriptedMind(
        ActorResolvedMatchDefinition definition,
        GenericMindRuntimeObservation observation) =>
        new(
        [
            .. observation.Bodies.Select(body =>
            {
                GenericActorRuntimeDecision decision = Script(
                    definition,
                    body.ActorId,
                    observation.Tick);
                return new GenericMindCommand(
                    body.ActorId.UnitId,
                    body.ActorId.LifeId,
                    decision.ActionId,
                    decision.ActionCode,
                    decision.Arguments);
            }),
        ]);

    public sealed class RecordingMindFactory : IGenericMindRuntimeFactory
    {
        private readonly Func<
            GenericMindRuntimeStart,
            GenericMindRuntimeObservation,
            GenericMindRuntimeDecisions> _think;

        public RecordingMindFactory(
            Func<
                GenericMindRuntimeStart,
                GenericMindRuntimeObservation,
                GenericMindRuntimeDecisions> think)
        {
            _think = think;
        }

        public List<GenericMindRuntimeStart> Starts { get; } = [];
        public List<GenericMindRuntimeObservation> Observations { get; } = [];
        public int CreateCount { get; private set; }
        public int ThinkCount { get; private set; }
        public int DisposedRuntimeCount { get; private set; }

        public IGenericMindRuntime CreateRuntime()
        {
            CreateCount++;
            return new Runtime(this);
        }

        private sealed class Runtime : IGenericMindRuntime
        {
            private readonly RecordingMindFactory _owner;
            private GenericMindRuntimeStart? _start;
            private bool _disposed;

            public Runtime(RecordingMindFactory owner)
            {
                _owner = owner;
            }

            public void StartMatch(GenericMindRuntimeStart start)
            {
                _start = start;
                _owner.Starts.Add(start);
            }

            public GenericMindRuntimeDecisions ExecuteTick(
                GenericMindRuntimeObservation observation)
            {
                _owner.ThinkCount++;
                _owner.Observations.Add(observation);
                return _owner._think(
                    _start
                    ?? throw new InvalidOperationException(
                        "Mind was not started."),
                    observation);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    throw new InvalidOperationException(
                        "Double-disposed runtime.");
                }
                _disposed = true;
                _owner.DisposedRuntimeCount++;
            }
        }
    }
}
