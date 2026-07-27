namespace BotArena.Engine;

/// <summary>
/// One reference in an ordered timeout-ranking policy. The referenced channel
/// must be declared in the containing game mode's public score catalog.
/// </summary>
public sealed record ScoreRankingDefinition
{
    public ScoreRankingDefinition(
        ScoreChannelDefinition.ChannelKind channel,
        SortDirection direction)
    {
        _ = ScoreChannelDefinition.ResolveValueDomain(channel);
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        Channel = channel;
        Direction = direction;
    }

    public ScoreChannelDefinition.ChannelKind Channel { get; }
    public SortDirection Direction { get; }

    public enum SortDirection
    {
        HigherWins = 0,
        LowerWins = 1,
    }
}
