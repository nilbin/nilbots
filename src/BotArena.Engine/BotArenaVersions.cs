namespace BotArena.Engine;

/// <summary>
/// The four independent version axes defined by the plan (§34), plus the engine build version.
/// These are serialized into every replay header and must only change deliberately.
/// </summary>
public static class BotArenaVersions
{
    public const string EngineVersion = "0.1.0";
    // 0.2: seed-spawn variation (DECISIONS #47; GameRules.V0_2).
    public const string GameRulesVersion = "0.2";
    public const string RuntimeProtocolVersion = "0.1";
    public const string RuntimeConfigurationVersion = "0.1";
    public const int ReplayFormatVersion = 1;
}
