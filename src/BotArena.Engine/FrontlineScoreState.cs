using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Exact public Frontline score for every scoring team. The value is signed:
/// higher always means farther toward that team's opposing base.
/// </summary>
public sealed record FrontlineScoreState
{
    public FrontlineScoreState(
        IReadOnlyCollection<FrontlineTeamScore> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        FrontlineTeamScore[] snapshot = [.. teams];
        if (snapshot.Length != 2
            || snapshot.Any(team => team is null)
            || snapshot.Select(team => team.TeamId).Distinct().Count()
                != snapshot.Length)
        {
            throw new ArgumentException(
                "Frontline scores must contain exactly two unique teams.",
                nameof(teams));
        }

        Teams = snapshot
            .OrderBy(team => team.TeamId)
            .ToImmutableArray();
    }

    public ImmutableArray<FrontlineTeamScore> Teams { get; }
}

public sealed record FrontlineTeamScore
{
    public FrontlineTeamScore(
        int teamId,
        long territorialProgress)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));

        TeamId = teamId;
        TerritorialProgress = territorialProgress;
    }

    public int TeamId { get; }
    public long TerritorialProgress { get; }
}
