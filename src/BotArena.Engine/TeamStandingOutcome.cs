namespace BotArena.Engine;

/// <summary>
/// Match-level outcome derived from the top placement. A top-rank tie is a
/// draw for the tied teams; teams below that tie still lose.
/// </summary>
public enum TeamStandingOutcome
{
    Win = 0,
    Loss = 1,
    Draw = 2,
}
