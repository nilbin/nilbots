using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Typed vNext Frontline mode values, independent of match format.</summary>
public sealed record FrontlineGameModeDefinition : GameModeDefinition
{
    public const string Id = "frontline";

    public FrontlineGameModeDefinition(
        FrontlineVictoryDefinition victory,
        ImmutableArray<ScoreChannelDefinition> scoreCatalog,
        int frontlinePositionCount,
        FrontlineCaptureDefinition capture)
        : base(Id, victory, scoreCatalog)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (frontlinePositionCount < 3 || frontlinePositionCount % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frontlinePositionCount),
                "Frontline position count must be odd and at least three.");
        }
        if ((long)victory.PushesToBreach * 2 - 1
            != frontlinePositionCount)
        {
            throw new ArgumentException(
                "Frontline position count must match the number of pushes to breach.",
                nameof(frontlinePositionCount));
        }
        if (victory.TimeoutRanking.Length != 1)
        {
            throw new ArgumentException(
                "Frontline timeout ranking must contain exactly one channel.",
                nameof(victory));
        }
        ValidatePrimaryTimeoutRanking(
            ScoreChannelDefinition.ChannelKind.TerritorialProgress);
        if (ScoreCatalog.Length != 1
            || ScoreCatalog[0].Channel
                != ScoreChannelDefinition.ChannelKind.TerritorialProgress
            || ScoreCatalog[0].Domain
                != ScoreChannelDefinition.ValueDomain.Signed)
        {
            throw new ArgumentException(
                "Frontline score catalog must contain exactly signed TerritorialProgress.",
                nameof(scoreCatalog));
        }

        FrontlinePositionCount = frontlinePositionCount;
        Capture = capture;
        FrontlineVictory = victory;
    }

    public override GameModeDefinitionKind Kind =>
        GameModeDefinitionKind.Frontline;
    public FrontlineVictoryDefinition FrontlineVictory { get; }
    public int FrontlinePositionCount { get; }
    public FrontlineCaptureDefinition Capture { get; }
}
