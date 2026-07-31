namespace BotArena.Engine;

/// <summary>
/// One declared public scoreboard channel. Its kind fixes the value domain;
/// timeout comparison order and direction live in
/// <see cref="ScoreRankingDefinition"/>.
/// </summary>
public sealed record ScoreChannelDefinition
{
    public ScoreChannelDefinition(ChannelKind channel)
    {
        Channel = channel;
        Domain = ResolveValueDomain(channel);
    }

    public ChannelKind Channel { get; }
    public ValueDomain Domain { get; }

    internal static ValueDomain ResolveValueDomain(ChannelKind channel) =>
        channel switch
        {
            ChannelKind.Kills => ValueDomain.NonNegative,
            ChannelKind.Deaths => ValueDomain.NonNegative,
            ChannelKind.DamageDealt => ValueDomain.NonNegative,
            ChannelKind.ActiveHealth => ValueDomain.NonNegative,
            ChannelKind.TerritorialProgress => ValueDomain.Signed,
            ChannelKind.Pulses => ValueDomain.NonNegative,
            ChannelKind.ReactorCharge => ValueDomain.NonNegative,
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };

    public enum ChannelKind
    {
        Kills = 0,
        Deaths = 1,
        DamageDealt = 2,
        ActiveHealth = 3,
        TerritorialProgress = 4,
        Pulses = 5,
        ReactorCharge = 6,
    }

    public enum ValueDomain
    {
        NonNegative = 0,
        Signed = 1,
    }
}
