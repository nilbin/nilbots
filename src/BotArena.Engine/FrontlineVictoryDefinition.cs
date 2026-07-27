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
        ImmutableArray<ScoreChannelDefinition> rankingChannels)
        : base(rankingChannels)
    {
        if (pushesToBreach <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pushesToBreach),
                "Frontline pushes to breach must be positive.");
        }
        if (rankingChannels[0] is not
            {
                Channel: ScoreChannelDefinition.ChannelKind.TerritorialProgress,
                Direction: ScoreChannelDefinition.SortDirection.HigherWins,
            })
        {
            throw new ArgumentException(
                "Frontline's primary ranking channel must be higher territorial progress.",
                nameof(rankingChannels));
        }

        PushesToBreach = pushesToBreach;
    }

    public override VictoryDefinitionKind Kind =>
        VictoryDefinitionKind.Frontline;

    public int PushesToBreach { get; }
}
