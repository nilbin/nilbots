namespace BotArena.Sdk;

/// <summary>
/// Contract versions understood by this SDK and guest adapter. A guest must
/// attest these exact values during startup rather than echoing host input.
/// </summary>
public static class ActorContractVersions
{
    public const string RuntimeProtocolVersion = "1.0";
    public const int RuntimeContractVersion = 1;
    public const int MatchStartSchemaVersion = 1;
    public const int ObservationSchemaVersion = 1;
    public const int DecisionSchemaVersion = 1;
    public const int MatchContractSchemaVersion = 1;
}
