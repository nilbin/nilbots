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
        "generic-actor-match-2";
    public const string GenericActorRuntimeProtocolVersion = "1.0";
    public const string GenericActorRuntimeConfigurationVersion = "1.0";
    /// <summary>
    /// Semantic implementation version for generation-3 actor matches. This is
    /// deliberately separate from <see cref="EngineVersion"/>, which remains
    /// the historical Duel/replay-v1 engine identity.
    /// </summary>
    public const string GenericActorEngineVersion = "1.0.0";
    public const int GenericActorRuntimeContractVersion = 2;
    public const int GenericActorMatchStartSchemaVersion = 2;
    public const int GenericActorObservationSchemaVersion = 2;
    public const int GenericActorDecisionSchemaVersion = 2;
    public const int GenericActorMatchContractSchemaVersion = 2;
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

    /// <summary>
    /// Negotiated contract profile for the participant-scoped MIND generation
    /// (DECISIONS #190/#191). It sits BESIDE the generic actor block above
    /// rather than superseding it: the per-life generation stays byte-exact so
    /// the hosted playlist, the eight measured lineages, and every frozen
    /// cohort's evidence remain valid and comparable.
    /// </summary>
    public const string GenericMindContractProfileId =
        "generic-mind-match-1";

    /// <summary>Framing protocol, carried unchanged from the actor line.</summary>
    public const string GenericMindRuntimeProtocolVersion = "1.0";

    /// <summary>
    /// Runtime CONFIGURATION 2.0: the fuel formula, the linear-memory ceiling,
    /// and the instance topology all change. Configuration 1.0 stays exactly
    /// as pinned for the per-life generation.
    /// </summary>
    public const string GenericMindRuntimeConfigurationVersion = "2.0";

    /// <summary>
    /// Semantic implementation version for mind matches, separate from
    /// <see cref="GenericActorEngineVersion"/> so introducing the new path
    /// does not relabel per-life matches.
    /// </summary>
    // 1.0.1: Arc Toss legality no longer offers targets whose wall-clipped
    // landing degenerates to the carrier's source tile.
    public const string GenericMindEngineVersion = "1.0.1";

    public const int GenericMindRuntimeContractVersion = 1;
    public const int GenericMindMatchStartSchemaVersion = 1;
    public const int GenericMindObservationSchemaVersion = 1;
    public const int GenericMindDecisionSchemaVersion = 1;

    /// <summary>
    /// CARRIED OVER from the actor generation and load-bearing: the mind plays
    /// the same game, so the resolved static contract is the same schema. The
    /// null pin depends on this.
    /// </summary>
    public const int GenericMindMatchContractSchemaVersion =
        GenericActorMatchContractSchemaVersion;

    /// <summary>
    /// Replay 3, carried: the document grows a <c>mindTurns</c> alternative to
    /// <c>actorTurns</c>, keyed by the contract profile in its header.
    /// </summary>
    public const int GenericMindReplayFormatVersion =
        GenericActorReplayFormatVersion;
}
