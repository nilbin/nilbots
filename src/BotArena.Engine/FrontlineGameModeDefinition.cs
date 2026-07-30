using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Typed vNext Frontline mode values, independent of match format.</summary>
public sealed record FrontlineGameModeDefinition : GameModeDefinition
{
    public const string Id = "frontline";

    /// <summary>Creates the typed Frontline mode.</summary>
    /// <param name="victory">Breach victory and timeout ranking.</param>
    /// <param name="scoreCatalog">Exactly signed territorial progress.</param>
    /// <param name="frontlinePositionCount">Odd chain length.</param>
    /// <param name="capture">Capture, decay, and redeploy arithmetic.</param>
    /// <param name="secondaryControl">
    /// The optional side-objective capability
    /// (<c>docs/DESIGN-SIDE-OBJECTIVES-2026-07-30.md</c> §3). Null — the
    /// historical and default value — means the mode has no side objective at
    /// all, and the canonical writer then emits no bytes for it, so every
    /// ruleset authored before this capability existed keeps its exact
    /// fingerprint.
    /// </param>
    /// <param name="scrapEconomy">
    /// The optional battlefield-economy capability
    /// (<c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> §14, DECISIONS #187).
    /// Null — the historical and default value — means the mode has no
    /// economy at all, and the canonical writer then emits no bytes for it.
    /// It is mutually exclusive with
    /// <paramref name="secondaryControl"/>: both claim the side lanes'
    /// attention, and the measured factor space stays three-valued only if no
    /// cell ever carries both.
    /// </param>
    public FrontlineGameModeDefinition(
        FrontlineVictoryDefinition victory,
        ImmutableArray<ScoreChannelDefinition> scoreCatalog,
        int frontlinePositionCount,
        FrontlineCaptureDefinition capture,
        FrontlineSecondaryControlDefinition? secondaryControl = null,
        FrontlineScrapEconomyDefinition? scrapEconomy = null)
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

        if (secondaryControl is not null && scrapEconomy is not null)
        {
            throw new ArgumentException(
                "A Frontline mode declares a side objective or a scrap "
                + "economy, never both: both claim the side lanes' attention, "
                + "so a cell carrying both could not attribute either.",
                nameof(scrapEconomy));
        }

        FrontlinePositionCount = frontlinePositionCount;
        Capture = capture;
        FrontlineVictory = victory;
        SecondaryControl = secondaryControl;
        ScrapEconomy = scrapEconomy;
    }

    public override GameModeDefinitionKind Kind =>
        GameModeDefinitionKind.Frontline;
    public FrontlineVictoryDefinition FrontlineVictory { get; }
    public int FrontlinePositionCount { get; }
    public FrontlineCaptureDefinition Capture { get; }

    /// <summary>
    /// The declared side objective, or null when the mode has none. Inert
    /// absence is the historical contract and writes no canonical bytes.
    /// </summary>
    public FrontlineSecondaryControlDefinition? SecondaryControl { get; }

    /// <summary>
    /// The declared battlefield economy, or null when the mode has none.
    /// Inert absence is the historical contract and writes no canonical bytes.
    /// </summary>
    public FrontlineScrapEconomyDefinition? ScrapEconomy { get; }
}
