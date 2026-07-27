namespace BotArena.Engine;

/// <summary>
/// Initial authoritative state and canonical actors returned by a headless
/// Frontline environment reset.
/// </summary>
public sealed record FrontlineResetResult(
    FrontlineMatchState State,
    IReadOnlyList<FrontlineActorId> ActiveActors);
