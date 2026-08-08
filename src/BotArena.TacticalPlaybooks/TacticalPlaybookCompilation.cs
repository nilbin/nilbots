namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Canonical, bounded runtime package produced from one editable tactical
/// playbook and its separately versioned map layout.
/// </summary>
public sealed record TacticalPlaybookCompilation(
    string PlaybookPath,
    string PlaybookSha256,
    string LayoutPath,
    string LayoutSha256,
    string[] Composition,
    byte[] NormalizedPlaybook,
    byte[] NormalizedLayout,
    byte[] LinkedData);
