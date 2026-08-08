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
        GenericActorContractVersions.ContractProfileId,
        GenericActorContractVersions.RuntimeContractVersion,
        GenericActorContractVersions.MatchStartSchemaVersion,
        GenericActorContractVersions.ObservationSchemaVersion,
        GenericActorContractVersions.DecisionSchemaVersion,
        GenericActorContractVersions.MatchContractSchemaVersion);

    /// <summary>
    /// The participant-scoped MIND profile (DECISIONS #190/#191). It sits
    /// BESIDE <see cref="GenericV2"/>, never in sequence with it: three live
    /// assets depend on the per-life generation staying byte-exact. The
    /// resolved match-contract schema is CARRIED at 2 because the game is
    /// unchanged — only who is driving it changes — and that carry is what
    /// makes the null pin meaningful.
    /// <para>P0 lands it inert: no host negotiates it and no guest attests it
    /// until P2.</para>
    /// </summary>
    public static ActorContractProfile MindV1 { get; } = new(
        GenericMindContractVersions.ContractProfileId,
        GenericMindContractVersions.RuntimeContractVersion,
        GenericMindContractVersions.MatchStartSchemaVersion,
        GenericMindContractVersions.ObservationSchemaVersion,
        GenericMindContractVersions.DecisionSchemaVersion,
        GenericMindContractVersions.MatchContractSchemaVersion);
}
