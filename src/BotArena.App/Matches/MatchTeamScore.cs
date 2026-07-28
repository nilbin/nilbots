namespace BotArena.App.Matches;

/// <summary>
/// One exact signed score-channel value for a scoring team's terminal result.
/// </summary>
public sealed class MatchTeamScore
{
    public Guid MatchId { get; set; }
    public int TeamId { get; set; }
    public required string ScoreChannelId { get; set; }
    public long Value { get; set; }
}
