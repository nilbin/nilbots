using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Frontline victory: an authored number of pushes ends the match early;
/// timeout placement compares the supplied territorial score channels.
/// </summary>
public sealed record FrontlineVictoryDefinition : VictoryDefinition
{
    public FrontlineVictoryDefinition(
        int pushesToBreach,
        ImmutableArray<ScoreRankingDefinition> timeoutRanking)
        : base(timeoutRanking)
    {
        if (pushesToBreach <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pushesToBreach),
                "Frontline pushes to breach must be positive.");
        }

        PushesToBreach = pushesToBreach;
    }

    public override VictoryDefinitionKind Kind =>
        VictoryDefinitionKind.Frontline;

    public int PushesToBreach { get; }
}
