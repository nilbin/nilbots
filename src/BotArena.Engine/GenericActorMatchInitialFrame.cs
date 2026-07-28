using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Authoritative state and life-start chronology that exist before tick zero.
/// This frame is mandatory even when a replay contains no executed ticks.
/// </summary>
public sealed record GenericActorMatchInitialFrame
{
    public GenericActorMatchInitialFrame(
        GenericActorWorldSnapshot state,
        IReadOnlyCollection<GenericActorLifeStart> lifeStarts,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(lifeStarts);
        ArgumentNullException.ThrowIfNull(events);
        if (state.NextTick != 0)
        {
            throw new ArgumentException(
                "Initial world state must precede tick zero.",
                nameof(state));
        }

        GenericActorLifeStart[] startSnapshot = [.. lifeStarts];
        GenericActorAuthoritativeEvent[] eventSnapshot = [.. events];
        if (startSnapshot.Any(start => start is null)
            || startSnapshot
                .Select(start => start.ActorId)
                .Distinct()
                .Count() != startSnapshot.Length)
        {
            throw new ArgumentException(
                "Initial life starts must be non-null and actor-unique.",
                nameof(lifeStarts));
        }

        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            activeLives = state.ActiveLives.ToDictionary(
                life => life.ActorId);
        if (startSnapshot.Length != activeLives.Count
            || startSnapshot.Any(start =>
                !activeLives.TryGetValue(
                    start.ActorId,
                    out GenericActorWorldSnapshot.LifeSnapshot? life)
                || life.ParticipantId != start.ParticipantId
                || life.SpawnedAtTick != 0
                || life.Generation != start.Origin.Generation
                || life.SpawnReason != start.Origin.Reason
                || life.ParentActorId != start.Origin.ParentActorId
                || !string.Equals(
                    life.SourceTransitionId,
                    start.Origin.SourceTransitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    life.SourceOperationId,
                    start.Origin.SourceOperationId,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Initial life starts must exactly describe every initially active life.",
                nameof(lifeStarts));
        }

        ValidateEvents(eventSnapshot, nameof(events));

        State = state;
        LifeStarts = startSnapshot
            .OrderBy(start => start.ActorId)
            .ToImmutableArray();
        Events = eventSnapshot
            .OrderBy(item => item.Ordinal)
            .ToImmutableArray();
    }

    public GenericActorWorldSnapshot State { get; }
    public ImmutableArray<GenericActorLifeStart> LifeStarts { get; }
    public ImmutableArray<GenericActorAuthoritativeEvent> Events { get; }

    internal void ValidateAgainst(
        GenericActorMatchDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ActorResolvedMatchDefinition definition = descriptor.Definition;
        if (!string.Equals(
                State.MatchContractFingerprint,
                descriptor.MatchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Initial world state does not reference the descriptor's exact match contract.",
                nameof(descriptor));
        }

        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            lives = State.ActiveLives.ToDictionary(life => life.ActorId);
        Dictionary<ActorIdentity, GenericActorLifeStart> starts =
            LifeStarts.ToDictionary(start => start.ActorId);
        Dictionary<string, InitialSpawnDefinition> spawns =
            definition.InitialDeployment.Spawns.ToDictionary(
                spawn => spawn.SpawnId,
                StringComparer.Ordinal);
        Dictionary<(int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            definition.LifecycleAssignments.ToDictionary(
                assignment => (assignment.TeamId, assignment.UnitId));
        Dictionary<(int TeamId, int UnitId), PublicUnitSlot> slots =
            definition.Topology.UnitSlots.ToDictionary(
                slot => (slot.TeamId, slot.UnitId));
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(
                form => form.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorAttackProfileDefinition> attackProfiles =
            definition.Rules.AttackProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);

        if (lives.Count != definition.InitialDeployment.Lives.Length
            || starts.Count != definition.InitialDeployment.Lives.Length)
        {
            throw InvalidInitialDeployment();
        }
        foreach (InitialLifeDeployment deployment in
                 definition.InitialDeployment.Lives)
        {
            var actorId = new ActorIdentity(
                deployment.TeamId,
                deployment.UnitId,
                deployment.LifeId);
            if (!lives.TryGetValue(
                    actorId,
                    out GenericActorWorldSnapshot.LifeSnapshot? life)
                || !starts.TryGetValue(
                    actorId,
                    out GenericActorLifeStart? start))
            {
                throw InvalidInitialDeployment();
            }

            InitialSpawnDefinition spawn = spawns[deployment.SpawnId];
            ActorFormDefinition form = forms[deployment.FormId];
            ActorUnitSlotLifecycleAssignmentDefinition assignment =
                assignments[(deployment.TeamId, deployment.UnitId)];
            int participantId = slots[
                (deployment.TeamId, deployment.UnitId)]
                .ControllerParticipantId;
            int generation = assignment.InitialGeneration
                ?? throw InvalidInitialDeployment();
            int? initialEnergy =
                form.AttackProfileId is string attackProfileId
                && attackProfiles[attackProfileId].MaxEnergy > 0
                    ? attackProfiles[attackProfileId].MaxEnergy
                    : null;
            if (life.ParticipantId != participantId
                || start.ParticipantId != participantId
                || !string.Equals(
                    life.FormId,
                    deployment.FormId,
                    StringComparison.Ordinal)
                || life.Position != spawn.Position
                || life.Facing != spawn.Facing
                || life.Generation != generation
                || start.Origin.Generation != generation
                || life.Health != form.MaxHealth
                || life.Cooldown != 0
                || life.Energy != initialEnergy
                || life.SpawnedAtTick != 0
                || life.PreviousActionResolution is not null
                || life.PendingSameLifeTransition is not null
                || life.SpawnReason
                    != GenericActorRuntimeStart.SpawnReason.Initial
                || start.Origin.Reason
                    != GenericActorRuntimeStart.SpawnReason.Initial
                || life.ParentActorId is not null
                || start.Origin.ParentActorId is not null
                || life.SourceTransitionId is not null
                || life.SourceOperationId is not null
                || start.Origin.SourceTransitionId is not null
                || start.Origin.SourceOperationId is not null)
            {
                throw InvalidInitialDeployment();
            }
        }
    }

    private static void ValidateEvents(
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        if (events.Any(item => item is null || item.Tick != 0)
            || events.Select(item => item.Ordinal).Distinct().Count()
                != events.Count)
        {
            throw new ArgumentException(
                "Initial events must be non-null, belong to tick zero, and have unique ordinals.",
                parameterName);
        }
    }

    private static ArgumentException InvalidInitialDeployment() =>
        new(
            "Initial world and life-start facts must exactly match the resolved initial deployment.");
}
