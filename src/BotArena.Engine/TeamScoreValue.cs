namespace BotArena.Engine;

/// <summary>
/// One exact terminal score value. Replay and API projections must encode the
/// <see cref="long"/> value losslessly rather than converting it to a floating
/// point number.
/// </summary>
public sealed record TeamScoreValue
{
    public TeamScoreValue(
        ScoreChannelDefinition.ChannelKind channel,
        long value)
    {
        ScoreChannelDefinition.ValueDomain domain =
            ScoreChannelDefinition.ResolveValueDomain(channel);
        if (domain == ScoreChannelDefinition.ValueDomain.NonNegative
            && value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Score channel '{channel}' cannot be negative.");
        }

        Channel = channel;
        Value = value;
    }

    public ScoreChannelDefinition.ChannelKind Channel { get; }
    public long Value { get; }
}
