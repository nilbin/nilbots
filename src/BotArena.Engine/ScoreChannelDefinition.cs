namespace BotArena.Engine;

/// <summary>
/// One typed, ordered terminal-ranking value. Collection order is ranking
/// priority; the channel kind supplies semantics without an open string key.
/// </summary>
public sealed record ScoreChannelDefinition
{
    public ScoreChannelDefinition(
        ChannelKind channel,
        SortDirection direction)
    {
        if (!Enum.IsDefined(channel))
            throw new ArgumentOutOfRangeException(nameof(channel));
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        Channel = channel;
        Direction = direction;
    }

    public ChannelKind Channel { get; }
    public SortDirection Direction { get; }

    public enum ChannelKind
    {
        Kills = 0,
        Deaths = 1,
        DamageDealt = 2,
        ActiveHealth = 3,
        TerritorialProgress = 4,
    }

    public enum SortDirection
    {
        HigherWins = 0,
        LowerWins = 1,
    }
}
