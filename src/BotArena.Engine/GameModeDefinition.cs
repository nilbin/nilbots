using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Closed vNext game-mode union. Participant arrangement is deliberately
/// absent: it belongs to <see cref="MatchFormatDefinition"/> and topology.
/// </summary>
public abstract record GameModeDefinition
{
    internal GameModeDefinition(
        string modeId,
        VictoryDefinition victory,
        ImmutableArray<ScoreChannelDefinition> scoreCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        ArgumentNullException.ThrowIfNull(victory);
        if (scoreCatalog.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "A game mode score catalog must be initialized and non-empty.",
                nameof(scoreCatalog));
        }
        if (scoreCatalog.Any(channel => channel is null))
        {
            throw new ArgumentException(
                "A game mode score catalog cannot contain null entries.",
                nameof(scoreCatalog));
        }
        if (scoreCatalog
            .Select(channel => channel.Channel)
            .Distinct()
            .Count() != scoreCatalog.Length)
        {
            throw new ArgumentException(
                "A game mode score catalog must contain unique channel kinds.",
                nameof(scoreCatalog));
        }

        ImmutableArray<ScoreChannelDefinition> canonicalCatalog = scoreCatalog
            .OrderBy(channel => channel.Channel)
            .ToImmutableArray();
        HashSet<ScoreChannelDefinition.ChannelKind> declaredChannels =
            canonicalCatalog
                .Select(channel => channel.Channel)
                .ToHashSet();
        if (victory.TimeoutRanking.Any(
                reference => !declaredChannels.Contains(reference.Channel)))
        {
            throw new ArgumentException(
                "Every timeout-ranking reference must exist in the game mode score catalog.",
                nameof(scoreCatalog));
        }

        ModeId = modeId;
        Victory = victory;
        ScoreCatalog = canonicalCatalog;
    }

    public abstract GameModeDefinitionKind Kind { get; }
    public string ModeId { get; }
    public VictoryDefinition Victory { get; }

    /// <summary>
    /// Complete public scoreboard schema in canonical channel-kind order.
    /// This is intentionally independent of timeout-ranking priority.
    /// </summary>
    public ImmutableArray<ScoreChannelDefinition> ScoreCatalog { get; }

    protected void ValidatePrimaryTimeoutRanking(
        ScoreChannelDefinition.ChannelKind requiredChannel)
    {
        if (!ScoreCatalog.Any(channel => channel.Channel == requiredChannel))
        {
            throw new ArgumentException(
                $"Game mode score catalog must declare its required '{requiredChannel}' channel.",
                nameof(ScoreCatalog));
        }
        ScoreRankingDefinition primary = Victory.TimeoutRanking[0];
        if (primary.Channel != requiredChannel
            || primary.Direction
                != ScoreRankingDefinition.SortDirection.HigherWins)
        {
            throw new ArgumentException(
                $"Game mode timeout ranking must begin with higher '{requiredChannel}'.",
                nameof(Victory));
        }
    }

    protected void ValidateSupportedScoreCatalog(
        params ScoreChannelDefinition.ChannelKind[] supportedChannels)
    {
        var supported = supportedChannels.ToHashSet();
        ScoreChannelDefinition.ChannelKind[] unsupported = ScoreCatalog
            .Select(channel => channel.Channel)
            .Where(channel => !supported.Contains(channel))
            .ToArray();
        if (unsupported.Length != 0)
        {
            throw new ArgumentException(
                $"Game mode '{ModeId}' cannot produce score channel(s): "
                + string.Join(", ", unsupported),
                nameof(ScoreCatalog));
        }
    }

    public enum GameModeDefinitionKind
    {
        Frontline = 0,
        Deathmatch = 1,
    }
}
