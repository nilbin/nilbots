namespace BotArena.Engine;

/// <summary>
/// Immutable actor-host capability tuple resolved for one generation-3 match.
/// Canonical writers read these captured values and never consult process
/// "current" state while fingerprinting a match.
/// </summary>
public sealed record ActorMatchCapabilityVersions
{
    public ActorMatchCapabilityVersions(
        string contractProfileId,
        string runtimeProtocolVersion,
        string runtimeConfigurationVersion,
        int runtimeContractVersion,
        int matchStartSchemaVersion,
        int observationSchemaVersion,
        int decisionSchemaVersion,
        int matchContractSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeProtocolVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            runtimeConfigurationVersion);
        if (runtimeContractVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeContractVersion));
        }
        if (matchStartSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchStartSchemaVersion));
        }
        if (observationSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observationSchemaVersion));
        }
        if (decisionSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decisionSchemaVersion));
        }
        if (matchContractSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchContractSchemaVersion));
        }

        ContractProfileId = contractProfileId;
        RuntimeProtocolVersion = runtimeProtocolVersion;
        RuntimeConfigurationVersion = runtimeConfigurationVersion;
        RuntimeContractVersion = runtimeContractVersion;
        MatchStartSchemaVersion = matchStartSchemaVersion;
        ObservationSchemaVersion = observationSchemaVersion;
        DecisionSchemaVersion = decisionSchemaVersion;
        MatchContractSchemaVersion = matchContractSchemaVersion;
    }

    public string ContractProfileId { get; }
    public string RuntimeProtocolVersion { get; }
    public string RuntimeConfigurationVersion { get; }
    public int RuntimeContractVersion { get; }
    public int MatchStartSchemaVersion { get; }
    public int ObservationSchemaVersion { get; }
    public int DecisionSchemaVersion { get; }
    public int MatchContractSchemaVersion { get; }

    public static ActorMatchCapabilityVersions Current { get; } = new(
        BotArenaVersions.GenericActorContractProfileId,
        BotArenaVersions.GenericActorRuntimeProtocolVersion,
        BotArenaVersions.GenericActorRuntimeConfigurationVersion,
        BotArenaVersions.GenericActorRuntimeContractVersion,
        BotArenaVersions.GenericActorMatchStartSchemaVersion,
        BotArenaVersions.GenericActorObservationSchemaVersion,
        BotArenaVersions.GenericActorDecisionSchemaVersion,
        BotArenaVersions.GenericActorMatchContractSchemaVersion);

    /// <summary>
    /// The participant-scoped MIND tuple (DECISIONS #191). Selecting it is
    /// what makes a resolved definition run through the per-participant
    /// coordinator instead of the per-life one; the rules, map, format,
    /// topology and mode it describes are untouched, which is exactly why the
    /// match-contract schema is carried rather than minted.
    /// </summary>
    public static ActorMatchCapabilityVersions Mind { get; } = new(
        BotArenaVersions.GenericMindContractProfileId,
        BotArenaVersions.GenericMindRuntimeProtocolVersion,
        BotArenaVersions.GenericMindRuntimeConfigurationVersion,
        BotArenaVersions.GenericMindRuntimeContractVersion,
        BotArenaVersions.GenericMindMatchStartSchemaVersion,
        BotArenaVersions.GenericMindObservationSchemaVersion,
        BotArenaVersions.GenericMindDecisionSchemaVersion,
        BotArenaVersions.GenericMindMatchContractSchemaVersion);

    /// <summary>
    /// True when this tuple selects the participant-scoped mind generation.
    /// One profile ID decides the whole execution shape: one runtime per
    /// participant, a union-once observation, and a decision MAP.
    /// </summary>
    public bool IsMindProfile =>
        string.Equals(
            ContractProfileId,
            BotArenaVersions.GenericMindContractProfileId,
            StringComparison.Ordinal);
}
