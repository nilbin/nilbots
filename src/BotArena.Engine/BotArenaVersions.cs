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
    public const int ReplayFormatVersion = 1;
    public const int PublicRulesManifestSchemaVersion = 2;
    public const int PublicMapManifestSchemaVersion = 1;
    public const int PublicMatchContractSchemaVersion = 1;
    /// <summary>
    /// Logical actor-runtime contract used by the experimental in-process
    /// Frontline slice. This is not legacy line protocol 0.1 and does not
    /// claim that a WASM transport exists yet.
    /// </summary>
    public const int ActorRuntimeContractVersion = 1;
    public const int ActorMatchStartSchemaVersion = 1;
    public const int ActorObservationSchemaVersion = 1;
    public const int ActorDecisionSchemaVersion = 1;
    public const int ActorHostFaultSchemaVersion = 1;
    /// <summary>
    /// Additive entity replay contract. Legacy <see cref="ReplayFormatVersion"/>
    /// remains 1 and is still the only publicly emitted replay.
    /// </summary>
    public const int EntityReplayFormatVersion = 2;
}
