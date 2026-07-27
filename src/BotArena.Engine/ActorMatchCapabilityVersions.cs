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
}
