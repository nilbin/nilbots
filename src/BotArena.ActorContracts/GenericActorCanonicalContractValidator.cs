namespace BotArena.ActorContracts;

/// <summary>
/// Dependency-neutral admission boundary for a complete canonical generic
/// actor match contract.
/// </summary>
public static class GenericActorCanonicalContractValidator
{
    /// <summary>
    /// Validates exact canonical syntax, the negotiated profile, every typed
    /// field and union, structural consistency, and all component and aggregate
    /// fingerprints.
    /// </summary>
    public static GenericActorCanonicalContractValidation Validate(
        string canonicalJson)
    {
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonicalJson);
        return new GenericActorCanonicalContractValidation(
            contract.SchemaVersion,
            contract.MatchContractFingerprint,
            contract.CapabilityVersions.ContractProfileId,
            contract.Rules.RulesetId);
    }
}

/// <summary>Identity axes proven by strict canonical-contract validation.</summary>
public sealed record GenericActorCanonicalContractValidation(
    int SchemaVersion,
    string MatchContractFingerprint,
    string ContractProfileId,
    string RulesetId);
