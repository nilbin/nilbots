using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Typed vNext Deathmatch semantics. FFA and team play reuse this same mode
/// through different formats and topologies.
/// </summary>
public sealed record DeathmatchGameModeDefinition : GameModeDefinition
{
    public const string Id = "deathmatch";

    public DeathmatchGameModeDefinition(
        DeathmatchVictoryDefinition victory,
        ImmutableArray<ScoreChannelDefinition> scoreCatalog,
        DeathmatchScoringDefinition scoring)
        : base(Id, victory, scoreCatalog)
    {
        ArgumentNullException.ThrowIfNull(scoring);
        ValidatePrimaryTimeoutRanking(
            ScoreChannelDefinition.ChannelKind.Kills);
        ValidateSupportedScoreCatalog(
            ScoreChannelDefinition.ChannelKind.Kills,
            ScoreChannelDefinition.ChannelKind.Deaths,
            ScoreChannelDefinition.ChannelKind.DamageDealt,
            ScoreChannelDefinition.ChannelKind.ActiveHealth);

        DeathmatchVictory = victory;
        Scoring = scoring;
    }

    public override GameModeDefinitionKind Kind =>
        GameModeDefinitionKind.Deathmatch;
    public DeathmatchVictoryDefinition DeathmatchVictory { get; }
    public DeathmatchScoringDefinition Scoring { get; }
}
