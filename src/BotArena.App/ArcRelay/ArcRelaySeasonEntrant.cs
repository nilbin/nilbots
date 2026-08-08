namespace BotArena.App.ArcRelay;

/// <summary>
/// The immutable opening/closing bookends for one entrant in one season.
/// Current rating remains on <see cref="ArcRelayEntrantRating"/> and each
/// settled match remains the append-only trajectory ledger.
/// </summary>
public sealed class ArcRelaySeasonEntrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public Guid EntrantId { get; set; }
    public DateTime EnteredAt { get; set; }
    public bool PresentAtOpening { get; set; }
    public double OpeningRating { get; set; }
    public int? OpeningRank { get; set; }
    public int OpeningRevision { get; set; }
    public required string OpeningPresentationJson { get; set; }
    public double? ClosingRating { get; set; }
    public int? ClosingRank { get; set; }
    public int? ClosingRevision { get; set; }
    public string? ClosingPresentationJson { get; set; }
}
