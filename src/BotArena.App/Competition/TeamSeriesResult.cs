namespace BotArena.App.Competition;

/// <summary>
/// One scoring team's terminal series result. Placement uses competition ranking:
/// tied teams share a place and the next place skips accordingly (1, 1, 3).
/// </summary>
public sealed record TeamSeriesResult(
    int TeamId,
    int Placement,
    double SeriesPoints);
