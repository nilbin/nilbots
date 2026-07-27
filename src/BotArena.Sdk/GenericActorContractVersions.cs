namespace BotArena.Sdk;

/// <summary>
/// Contract versions for the generic actor-match programming model. The
/// framing protocol remains actor protocol 1.0; negotiation selects this
/// complete tuple before the host sends a version-specific MatchStart.
/// </summary>
public static class GenericActorContractVersions
{
    public const string ContractProfileId = "generic-actor-match-2";
    public const string RuntimeProtocolVersion = "1.0";
    public const int RuntimeContractVersion = 2;
    public const int MatchStartSchemaVersion = 2;
    public const int ObservationSchemaVersion = 2;
    public const int DecisionSchemaVersion = 2;
    public const int MatchContractSchemaVersion = 2;
    public const int ReplayFormatVersion = 3;
}
