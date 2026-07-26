namespace BotArena.Engine;

/// <summary>
/// Complete engine-side inputs accepted before tick zero. Frontline fields are
/// either both present or both null.
/// </summary>
public sealed record ResolvedMatchDefinition(
    GameRules Rules,
    ArenaMap Map,
    PublicMatchTopology Topology,
    FrontlineRules? FrontlineRules,
    FrontlineMapProfile? FrontlineMapProfile)
{
    public bool IsFrontline => FrontlineRules is not null;
}
