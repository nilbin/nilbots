namespace BotArena.Engine;

/// <summary>
/// Independent engine, rules, wire, replay, and public-contract version axes.
/// Each is serialized with its respective contract and must only change deliberately.
/// </summary>
public static class BotArenaVersions
{
    public const string EngineVersion = "0.1.0";
    // 0.2: seed-spawn variation (DECISIONS #47). 0.3: shot range cap 8 + lane-safe
    // spawns (DECISIONS #49; GameRules.V0_3). 0.4: exclusive zone control
    // (DECISIONS #53). 0.5: territorial pressure + cone/hearing + programmed
    // speed-two projectiles (DECISIONS #75; GameRules.V0_5).
    public const string GameRulesVersion = "0.5";
    public const string RuntimeProtocolVersion = "0.1";
    public const string RuntimeConfigurationVersion = "0.1";
    /// <summary>
    /// Negotiated framed protocol for independently instantiated entity lives.
    /// The historical <see cref="RuntimeProtocolVersion"/> remains the exact
    /// replay-v1/duel protocol and is selected separately.
    /// </summary>
    public const string ActorRuntimeProtocolVersion = "1.0";
    /// <summary>
    /// Actor-path frame, memory, fuel, and timeout limit profile. Kept
    /// separate so introducing the new path does not relabel legacy matches.
    /// </summary>
    public const string ActorRuntimeConfigurationVersion = "1.0";
    public const int ReplayFormatVersion = 1;
    public const int PublicRulesManifestSchemaVersion = 2;
    public const int PublicMapManifestSchemaVersion = 1;
    public const int PublicMatchContractSchemaVersion = 1;
    /// <summary>
    /// Logical actor-runtime model shared by the experimental in-process and
    /// WASM Frontline paths. This is independent of legacy line protocol 0.1.
    /// </summary>
    public const int ActorRuntimeContractVersion = 1;
    public const int ActorMatchStartSchemaVersion = 1;
    public const int ActorObservationSchemaVersion = 1;
    public const int ActorDecisionSchemaVersion = 1;
    public const int ActorHostFaultSchemaVersion = 1;
    /// <summary>
    /// Negotiated contract profile for generation-3 generic actor matches.
    /// Actor protocol framing/configuration 1.0 remain unchanged.
    /// </summary>
    public const string GenericActorContractProfileId =
        "generic-actor-match-3";
    public const string GenericActorRuntimeProtocolVersion = "1.0";
    public const string GenericActorRuntimeConfigurationVersion = "1.0";
    /// <summary>
    /// Semantic implementation version for generation-3 actor matches. This is
    /// deliberately separate from <see cref="EngineVersion"/>, which remains
    /// the historical Duel/replay-v1 engine identity.
    /// </summary>
    public const string GenericActorEngineVersion = "1.0.0";
    public const int GenericActorRuntimeContractVersion = 3;
    public const int GenericActorMatchStartSchemaVersion = 3;
    public const int GenericActorObservationSchemaVersion = 3;
    public const int GenericActorDecisionSchemaVersion = 2;
    public const int GenericActorMatchContractSchemaVersion = 3;
    public const int GenericActorReplayFormatVersion = 3;
    /// <summary>
    /// Canonical static-contract budget after reserving one KiB for the
    /// enclosing one-MiB MatchStart frame.
    /// </summary>
    public const int GenericActorMaxCanonicalContractBytes =
        1024 * 1024 - 1024;
    /// <summary>
    /// Maximum JSON values admitted into one generic static contract. This
    /// bounds the guest's immutable DOM independently of textual density.
    /// </summary>
    public const int GenericActorMaxCanonicalContractNodes = 65_536;
    /// <summary>
    /// Maximum direct properties or items in any one canonical JSON
    /// container.
    /// </summary>
    public const int GenericActorMaxCanonicalCollectionCount = 4_096;
    /// <summary>
    /// Additive frozen-alpha entity replay contract. Legacy public Duel stays
    /// on <see cref="ReplayFormatVersion"/> 1; hosted generic Labs uses the
    /// separate <see cref="GenericActorReplayFormatVersion"/> 3 contract.
    /// </summary>
    public const int EntityReplayFormatVersion = 2;
}
