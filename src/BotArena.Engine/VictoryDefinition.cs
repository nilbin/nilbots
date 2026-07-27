using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Closed vNext victory union. Mode-specific variants own their early terminal
/// condition while this base owns the ordered timeout-ranking policy.
/// </summary>
public abstract record VictoryDefinition
{
    internal VictoryDefinition(
        ImmutableArray<ScoreRankingDefinition> timeoutRanking)
    {
        if (timeoutRanking.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "Victory timeout ranking must be initialized and non-empty.",
                nameof(timeoutRanking));
        }
        if (timeoutRanking.Any(reference => reference is null))
        {
            throw new ArgumentException(
                "Victory timeout ranking cannot contain null entries.",
                nameof(timeoutRanking));
        }
        if (timeoutRanking
            .Select(reference => reference.Channel)
            .Distinct()
            .Count() != timeoutRanking.Length)
        {
            throw new ArgumentException(
                "Victory timeout-ranking channel references must be unique.",
                nameof(timeoutRanking));
        }

        TimeoutRanking = timeoutRanking;
    }

    public abstract VictoryDefinitionKind Kind { get; }

    /// <summary>
    /// Ordered score comparisons used only when the common match tick limit
    /// ends regulation. Early terminal conditions may produce different
    /// standings from this score order.
    /// </summary>
    public ImmutableArray<ScoreRankingDefinition> TimeoutRanking { get; }

    public enum VictoryDefinitionKind
    {
        Frontline = 0,
        Deathmatch = 1,
    }
}
