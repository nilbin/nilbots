using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Arc Relay victory: each Pulse removes one opposing reactor segment and the
/// configured final Pulse ends regulation. Timeout ranks only banked Pulses,
/// then the current charge pips.
/// </summary>
public sealed record ArcRelayVictoryDefinition : VictoryDefinition
{
    public ArcRelayVictoryDefinition(
        int pulsesToDestroyReactor,
        ImmutableArray<ScoreRankingDefinition> timeoutRanking)
        : base(timeoutRanking)
    {
        if (pulsesToDestroyReactor <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pulsesToDestroyReactor));
        }

        PulsesToDestroyReactor = pulsesToDestroyReactor;
    }

    public override VictoryDefinitionKind Kind =>
        VictoryDefinitionKind.ArcRelay;

    public int PulsesToDestroyReactor { get; }
}
