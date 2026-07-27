using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Closed vNext victory union. Mode-specific variants own their early terminal
/// condition while this base owns the ordered timeout-ranking channels.
/// </summary>
public abstract record VictoryDefinition
{
    internal VictoryDefinition(
        ImmutableArray<ScoreChannelDefinition> rankingChannels)
    {
        if (rankingChannels.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "Victory ranking channels must be initialized and non-empty.",
                nameof(rankingChannels));
        }
        if (rankingChannels.Any(channel => channel is null))
        {
            throw new ArgumentException(
                "Victory ranking channels cannot contain null entries.",
                nameof(rankingChannels));
        }
        if (rankingChannels
            .Select(channel => channel.Channel)
            .Distinct()
            .Count() != rankingChannels.Length)
        {
            throw new ArgumentException(
                "Victory ranking channel kinds must be unique.",
                nameof(rankingChannels));
        }

        RankingChannels = rankingChannels;
    }

    public abstract VictoryDefinitionKind Kind { get; }

    /// <summary>
    /// Ordered, typed comparison channels. Exact equality across every channel
    /// is a tied placement.
    /// </summary>
    public ImmutableArray<ScoreChannelDefinition> RankingChannels { get; }

    public enum VictoryDefinitionKind
    {
        Frontline = 0,
        Deathmatch = 1,
    }
}
