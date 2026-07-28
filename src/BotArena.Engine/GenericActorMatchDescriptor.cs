using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Immutable match identity and participant provenance shared by authoritative
/// chronology and replay projection. Execution-only runtime factories are
/// deliberately absent.
/// </summary>
public sealed record GenericActorMatchDescriptor
{
    public GenericActorMatchDescriptor(
        ActorResolvedMatchDefinition definition,
        ulong matchSeed,
        string engineVersion,
        string actorRuntimeProtocolVersion,
        string actorRuntimeConfigurationVersion,
        IEnumerable<GenericActorParticipantProvenance> participants)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            actorRuntimeProtocolVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            actorRuntimeConfigurationVersion);
        ArgumentNullException.ThrowIfNull(participants);

        if (!string.Equals(
                actorRuntimeProtocolVersion,
                definition.CapabilityVersions.RuntimeProtocolVersion,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Actor runtime protocol version must match the resolved definition.",
                nameof(actorRuntimeProtocolVersion));
        }
        if (!string.Equals(
                actorRuntimeConfigurationVersion,
                definition.CapabilityVersions.RuntimeConfigurationVersion,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Actor runtime configuration version must match the resolved definition.",
                nameof(actorRuntimeConfigurationVersion));
        }

        Definition = definition;
        MatchContractFingerprint =
            ActorContractFingerprint.ComputeMatch(definition);
        MatchSeed = matchSeed;
        EngineVersion = engineVersion;
        ActorRuntimeProtocolVersion = actorRuntimeProtocolVersion;
        ActorRuntimeConfigurationVersion =
            actorRuntimeConfigurationVersion;
        Participants =
            GenericActorParticipantProvenance.CanonicalizeExact(
                definition,
                participants,
                nameof(participants));
    }

    /// <summary>
    /// Constructs a descriptor with the process's current engine version and
    /// the runtime protocol/configuration captured by the resolved definition.
    /// </summary>
    public GenericActorMatchDescriptor(
        ActorResolvedMatchDefinition definition,
        ulong matchSeed,
        IEnumerable<GenericActorParticipantProvenance> participants)
        : this(
            definition,
            matchSeed,
            BotArenaVersions.GenericActorEngineVersion,
            definition?.CapabilityVersions.RuntimeProtocolVersion
                ?? throw new ArgumentNullException(nameof(definition)),
            definition.CapabilityVersions.RuntimeConfigurationVersion,
            participants)
    {
    }

    public ActorResolvedMatchDefinition Definition { get; }
    public string MatchContractFingerprint { get; }
    public ulong MatchSeed { get; }
    public string EngineVersion { get; }
    public string ActorRuntimeProtocolVersion { get; }
    public string ActorRuntimeConfigurationVersion { get; }
    public ImmutableArray<GenericActorParticipantProvenance> Participants
    {
        get;
    }

    /// <summary>
    /// Copies factory-backed execution configurations into a descriptor that
    /// retains only canonical, factory-free provenance.
    /// </summary>
    public static GenericActorMatchDescriptor Create(
        ActorResolvedMatchDefinition definition,
        ulong matchSeed,
        IEnumerable<GenericActorParticipantConfiguration> configurations) =>
        new(
            definition,
            matchSeed,
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                configurations));
}
