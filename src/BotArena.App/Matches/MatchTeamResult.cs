namespace BotArena.App.Matches;

/// <summary>
/// One scoring team's authoritative match placement. Competition ranking is
/// used, so tied teams share a placement and the next placement skips.
/// </summary>
public sealed class MatchTeamResult
{
    public Guid MatchId { get; set; }
    public int TeamId { get; set; }
    public int Placement { get; set; }
    public MatchTeamOutcome Outcome { get; set; }
    public List<MatchTeamScore> Scores { get; set; } = [];
}
