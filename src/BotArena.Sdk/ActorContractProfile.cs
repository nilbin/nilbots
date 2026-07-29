namespace BotArena.Sdk;

/// <summary>
/// One indivisible actor contract profile offered during protocol
/// negotiation. Guests select the exact tuple rather than independently
/// mixing schema versions that were never tested together.
/// </summary>
public sealed record ActorContractProfile(
    string ProfileId,
    int RuntimeContractVersion,
    int MatchStartSchemaVersion,
    int ObservationSchemaVersion,
    int DecisionSchemaVersion,
    int MatchContractSchemaVersion)
{
    public static ActorContractProfile GenericV2 { get; } = new(
        GenericActorContractVersions.GenericV2ContractProfileId,
        GenericActorContractVersions.GenericV2RuntimeContractVersion,
        GenericActorContractVersions.GenericV2MatchStartSchemaVersion,
        GenericActorContractVersions.GenericV2ObservationSchemaVersion,
        GenericActorContractVersions.GenericV2DecisionSchemaVersion,
        GenericActorContractVersions.GenericV2MatchContractSchemaVersion);

    public static ActorContractProfile GenericV3 { get; } = new(
        GenericActorContractVersions.ContractProfileId,
        GenericActorContractVersions.RuntimeContractVersion,
        GenericActorContractVersions.MatchStartSchemaVersion,
        GenericActorContractVersions.ObservationSchemaVersion,
        GenericActorContractVersions.DecisionSchemaVersion,
        GenericActorContractVersions.MatchContractSchemaVersion);
}
