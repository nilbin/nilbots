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
        ImmutableArray<ScoreChannelDefinition> rankingChannels)
        : base(rankingChannels)
    {
        if (killsToWin <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(killsToWin),
                "A configured deathmatch kill limit must be positive.");
        }
        if (rankingChannels[0] is not
            {
                Channel: ScoreChannelDefinition.ChannelKind.Kills,
                Direction: ScoreChannelDefinition.SortDirection.HigherWins,
            })
        {
            throw new ArgumentException(
                "Deathmatch's primary ranking channel must be higher kill count.",
                nameof(rankingChannels));
        }

        KillsToWin = killsToWin;
    }

    public override VictoryDefinitionKind Kind =>
        VictoryDefinitionKind.Deathmatch;

    /// <summary>Null means only the common match tick limit ends regulation.</summary>
    public int? KillsToWin { get; }
}
