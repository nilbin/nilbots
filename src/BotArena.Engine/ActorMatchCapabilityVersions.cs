namespace BotArena.Engine;

/// <summary>
/// Immutable actor-host capability tuple resolved for one generation-3 match.
/// Canonical writers read these captured values and never consult process
/// "current" state while fingerprinting a match.
/// </summary>
public sealed record ActorMatchCapabilityVersions
{
    public ActorMatchCapabilityVersions(
        string runtimeProtocolVersion,
        string runtimeConfigurationVersion,
        int runtimeContractVersion,
        int matchStartSchemaVersion,
        int observationSchemaVersion,
        int decisionSchemaVersion)
    {
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

        RuntimeProtocolVersion = runtimeProtocolVersion;
        RuntimeConfigurationVersion = runtimeConfigurationVersion;
        RuntimeContractVersion = runtimeContractVersion;
        MatchStartSchemaVersion = matchStartSchemaVersion;
        ObservationSchemaVersion = observationSchemaVersion;
        DecisionSchemaVersion = decisionSchemaVersion;
    }

    public string RuntimeProtocolVersion { get; }
    public string RuntimeConfigurationVersion { get; }
    public int RuntimeContractVersion { get; }
    public int MatchStartSchemaVersion { get; }
    public int ObservationSchemaVersion { get; }
    public int DecisionSchemaVersion { get; }

    public static ActorMatchCapabilityVersions Current { get; } = new(
        BotArenaVersions.ActorRuntimeProtocolVersion,
        BotArenaVersions.ActorRuntimeConfigurationVersion,
        BotArenaVersions.ActorRuntimeContractVersion,
        BotArenaVersions.ActorMatchStartSchemaVersion,
        BotArenaVersions.ActorObservationSchemaVersion,
        BotArenaVersions.ActorDecisionSchemaVersion);
}
