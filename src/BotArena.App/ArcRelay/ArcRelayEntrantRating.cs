using BotArena.App.Bots;

namespace BotArena.App.ArcRelay;

/// <summary>One persistent Elo row per entrant and Arc Relay ladder.</summary>
public sealed class ArcRelayEntrantRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntrantId { get; set; }
    public Guid LadderId { get; set; }
    public double Rating { get; set; } = BotRating.DefaultRating;
    public int RankedMatches { get; set; }
}
