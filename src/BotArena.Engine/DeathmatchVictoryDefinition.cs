using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Deathmatch victory: most kills wins at timeout, with an optional kill
/// threshold allowing an earlier terminal tick.
/// </summary>
public sealed record DeathmatchVictoryDefinition : VictoryDefinition
{
    public DeathmatchVictoryDefinition(
        int? killsToWin,
        ImmutableArray<ScoreRankingDefinition> timeoutRanking)
        : base(timeoutRanking)
    {
        if (killsToWin <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(killsToWin),
                "A configured deathmatch kill limit must be positive.");
        }

        KillsToWin = killsToWin;
    }

    public override VictoryDefinitionKind Kind =>
        VictoryDefinitionKind.Deathmatch;

    /// <summary>Null means only the common match tick limit ends regulation.</summary>
    public int? KillsToWin { get; }

    public TerminalTickPrecedenceKind TerminalTickPrecedence =>
        TerminalTickPrecedenceKind
            .KillLimitAfterCompleteJointTickBeforeMaxTickTimeout;

    public enum TerminalTickPrecedenceKind
    {
        /// <summary>
        /// On the final allowed tick, apply the complete joint tick and check
        /// the kill limit first. If it is not reached, resolve the timeout
        /// ranking. This prevents the same state receiving two result rules.
        /// </summary>
        KillLimitAfterCompleteJointTickBeforeMaxTickTimeout = 0,
    }
}
