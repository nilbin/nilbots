namespace BotArena.App.Competition;

/// <summary>
/// Stable host-execution routes pinned by immutable playlist versions.
/// Admission profiles answer which artifacts may enter; these identifiers
/// independently answer which hosted engine executes the admitted match.
/// </summary>
public static class PlaylistExecutionPolicyIds
{
    public const string LegacyDuel = "legacy-duel-v1";
    public const string GenericActor = "generic-actor-v1";
}
