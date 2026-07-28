namespace BotArena.Engine;

/// <summary>
/// Factory-free, replay-safe copy of the exact start registered for one
/// independently instantiated runtime life.
/// </summary>
public sealed record GenericActorLifeStart
{
    public GenericActorLifeStart(
        int schemaVersion,
        int runtimeContractVersion,
        ActorIdentity actorId,
        int participantId,
        ulong actorRandomSeed,
        GenericActorRuntimeStart.LifeOrigin origin,
        string matchContractFingerprint)
    {
        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (runtimeContractVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeContractVersion));
        }
        if (participantId < 0)
            throw new ArgumentOutOfRangeException(nameof(participantId));
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(origin);
        if (!IsValidLineage(actorId, origin))
        {
            throw new ArgumentException(
                "Life-start origin is malformed.",
                nameof(origin));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(
            matchContractFingerprint);

        SchemaVersion = schemaVersion;
        RuntimeContractVersion = runtimeContractVersion;
        ActorId = actorId;
        ParticipantId = participantId;
        ActorRandomSeed = actorRandomSeed;
        Origin = origin;
        MatchContractFingerprint = matchContractFingerprint;
    }

    public int SchemaVersion { get; }
    public int RuntimeContractVersion { get; }
    public ActorIdentity ActorId { get; }
    public int ParticipantId { get; }
    public ulong ActorRandomSeed { get; }
    public GenericActorRuntimeStart.LifeOrigin Origin { get; }
    public string MatchContractFingerprint { get; }

    internal void ValidateAgainst(
        GenericActorMatchDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ActorResolvedMatchDefinition definition = descriptor.Definition;
        if (SchemaVersion
                != definition.CapabilityVersions.MatchStartSchemaVersion
            || RuntimeContractVersion
                != definition.CapabilityVersions.RuntimeContractVersion)
        {
            throw new ArgumentException(
                "Life-start capability versions do not match the resolved match descriptor.",
                nameof(descriptor));
        }
        if (!string.Equals(
                MatchContractFingerprint,
                descriptor.MatchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Life start does not reference the descriptor's exact match contract.",
                nameof(descriptor));
        }

        PublicUnitSlot? slot = definition.Topology.UnitSlots.SingleOrDefault(
            candidate =>
                candidate.TeamId == ActorId.TeamId
                && candidate.UnitId == ActorId.UnitId);
        PublicParticipant? participant =
            definition.Topology.Participants.SingleOrDefault(candidate =>
                candidate.ParticipantId == ParticipantId);
        if (slot is null
            || participant is null
            || slot.ControllerParticipantId != ParticipantId
            || participant.TeamId != ActorId.TeamId)
        {
            throw new ArgumentException(
                "Life-start actor, participant, and stable-slot ownership do not match the resolved topology.",
                nameof(descriptor));
        }
        if (Origin.Reason == GenericActorRuntimeStart.SpawnReason.Initial
            && !definition.Topology.InitialLives.Any(life =>
                life.TeamId == ActorId.TeamId
                && life.UnitId == ActorId.UnitId
                && life.LifeId == ActorId.LifeId))
        {
            throw new ArgumentException(
                "An initial life start must identify one exact topology initial life.",
                nameof(descriptor));
        }

        ulong expectedSeed = SeedDerivation.DeriveActorSeed(
            descriptor.MatchSeed,
            ActorId,
            definition.Rules.SeedMechanics.SeedProfileId);
        if (ActorRandomSeed != expectedSeed)
        {
            throw new ArgumentException(
                "Life-start actor seed does not match deterministic derivation.",
                nameof(descriptor));
        }
    }

    internal void ValidateDynamicLineage(
        GenericActorLifeStart? issuedParent)
    {
        if (Origin.Reason == GenericActorRuntimeStart.SpawnReason.Initial)
        {
            if (issuedParent is not null)
            {
                throw new ArgumentException(
                    "An initial life cannot have issued parent metadata.",
                    nameof(issuedParent));
            }
            return;
        }

        if (Origin.ParentActorId is not ActorIdentity parentActorId
            || issuedParent is null
            || issuedParent.ActorId != parentActorId
            || issuedParent.ParticipantId != ParticipantId)
        {
            throw new ArgumentException(
                "A dynamic life must reference an already-issued parent controlled by the same participant.",
                nameof(issuedParent));
        }

        bool validGeneration = Origin.Reason switch
        {
            GenericActorRuntimeStart.SpawnReason.AutomaticReturn =>
                ActorId.TeamId == parentActorId.TeamId
                && ActorId.UnitId == parentActorId.UnitId
                && Origin.Generation == issuedParent.Origin.Generation,
            GenericActorRuntimeStart.SpawnReason.Fabrication
                or GenericActorRuntimeStart.SpawnReason.Replication =>
                issuedParent.Origin.Generation < int.MaxValue
                && Origin.Generation
                    == issuedParent.Origin.Generation + 1,
            _ => false,
        };
        if (!validGeneration)
        {
            throw new ArgumentException(
                "Dynamic life generation does not match its issued parent and spawn reason.",
                nameof(issuedParent));
        }
    }

    public static GenericActorLifeStart FromRuntimeStart(
        GenericActorRuntimeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(start.Contract);
        return new GenericActorLifeStart(
            start.SchemaVersion,
            start.RuntimeContractVersion,
            start.ActorId,
            start.ParticipantId,
            start.ActorRandomSeed,
            start.Origin,
            ActorContractFingerprint.ComputeMatch(start.Contract));
    }

    private static bool IsValidLineage(
        ActorIdentity actorId,
        GenericActorRuntimeStart.LifeOrigin origin)
    {
        if (!Enum.IsDefined(origin.Reason)
            || origin.Generation < 0
            || origin.ParentActorId == actorId
            || (origin.SourceTransitionId is null)
                != (origin.SourceOperationId is null)
            || origin.SourceTransitionId is not null
                && string.IsNullOrWhiteSpace(origin.SourceTransitionId)
            || origin.SourceOperationId is not null
                && string.IsNullOrWhiteSpace(origin.SourceOperationId))
        {
            return false;
        }

        bool hasTransition = origin.SourceTransitionId is not null;
        return origin.Reason switch
        {
            GenericActorRuntimeStart.SpawnReason.Initial =>
                origin.ParentActorId is null && !hasTransition,
            GenericActorRuntimeStart.SpawnReason.AutomaticReturn =>
                origin.ParentActorId is ActorIdentity parent
                && parent.TeamId == actorId.TeamId
                && parent.UnitId == actorId.UnitId
                && parent.LifeId < actorId.LifeId
                && !hasTransition,
            GenericActorRuntimeStart.SpawnReason.Fabrication
                or GenericActorRuntimeStart.SpawnReason.Replication =>
                origin.ParentActorId is ActorIdentity parent
                && parent.TeamId == actorId.TeamId
                && hasTransition,
            _ => false,
        };
    }
}
