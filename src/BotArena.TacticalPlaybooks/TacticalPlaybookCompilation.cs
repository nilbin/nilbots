namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Canonical, bounded runtime package produced from one editable tactical
/// playbook and its separately versioned map layout. When the source was a
/// Tactics document, the compilation additionally carries the tactics hash and
/// the deterministically materialized Orders v2 source pair.
/// </summary>
public sealed record TacticalPlaybookCompilation(
    string PlaybookPath,
    string PlaybookSha256,
    string LayoutPath,
    string LayoutSha256,
    string[] Composition,
    byte[] NormalizedPlaybook,
    byte[] NormalizedLayout,
    byte[] LinkedData,
    bool UsesOrderTree,
    string? TacticsSha256 = null,
    byte[]? MaterializedOrders = null,
    byte[]? MaterializedMapPlan = null);
