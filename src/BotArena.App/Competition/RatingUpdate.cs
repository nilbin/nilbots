namespace BotArena.App.Competition;

/// <summary>
/// One entrant's complete rating transition returned by a policy.
/// </summary>
public sealed record RatingUpdate(
    Guid EntrantId,
    double RatingBefore,
    double RatingAfter,
    double RatingChange,
    string? PolicyState);
