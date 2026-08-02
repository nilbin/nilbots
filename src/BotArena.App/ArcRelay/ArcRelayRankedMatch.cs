namespace BotArena.App.ArcRelay;

/// <summary>
/// Rating settlement record for one ranked Arc Relay match. The gameplay match
/// remains the normal immutable hosted match; this row owns only ladder facts.
/// </summary>
public sealed class ArcRelayRankedMatch
{
    public Guid MatchId { get; set; }
    public Guid LadderId { get; set; }
    public Guid EntrantAId { get; set; }
    public Guid EntrantBId { get; set; }
    public double RatingABefore { get; set; }
    public double RatingBBefore { get; set; }
    public double? RatingChangeA { get; set; }
    public double? RatingChangeB { get; set; }
    public DateTime? SettledAt { get; set; }
}
