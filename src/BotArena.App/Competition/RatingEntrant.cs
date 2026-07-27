namespace BotArena.App.Competition;

/// <summary>
/// One submitted competitor entering a rating calculation. Several entrants may
/// share a scoring team; an FFA normally has one entrant per team.
/// </summary>
public sealed record RatingEntrant(
    Guid EntrantId,
    int TeamId,
    double Rating,
    string? PolicyState = null);
