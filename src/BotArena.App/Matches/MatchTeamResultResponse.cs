namespace BotArena.App.Matches;

public sealed record MatchTeamResultResponse(
    int TeamId,
    int Placement,
    string Outcome,
    IReadOnlyList<MatchTeamScoreResponse> Scores);
