namespace BotArena.Engine;

/// <summary>Typed terminal envelope for a generic Deathmatch run.</summary>
public sealed record GenericDeathmatchResult(
    GenericDeathmatchEndReason Reason,
    int EndTick,
    DeathmatchScoreState Scores,
    TeamStandings Standings);
