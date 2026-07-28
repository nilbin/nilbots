using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One scoring team's terminal placement. Rank uses competition ranking, so
/// teams tied by the authoritative completion outcome share a rank and the
/// following rank skips accordingly.
/// </summary>
public sealed class TeamStanding
{
    public TeamStanding(
        int teamId,
        int rank,
        TeamStandingOutcome outcome,
        IReadOnlyCollection<TeamScoreValue> scores)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        if (rank <= 0)
            throw new ArgumentOutOfRangeException(nameof(rank));
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        ArgumentNullException.ThrowIfNull(scores);

        TeamScoreValue[] scoreSnapshot = [.. scores];
        if (scoreSnapshot.Length == 0)
        {
            throw new ArgumentException(
                "A team standing needs at least one score channel.",
                nameof(scores));
        }
        if (scoreSnapshot.Any(score => score is null))
        {
            throw new ArgumentException(
                "Team scores cannot contain null entries.",
                nameof(scores));
        }
        if (scoreSnapshot
            .Select(score => score.Channel)
            .Distinct()
            .Count() != scoreSnapshot.Length)
        {
            throw new ArgumentException(
                "Team score channel kinds must be unique.",
                nameof(scores));
        }

        TeamId = teamId;
        Rank = rank;
        Outcome = outcome;
        Scores = scoreSnapshot
            .OrderBy(score => score.Channel)
            .ToImmutableArray();
    }

    public int TeamId { get; }
    public int Rank { get; }
    public TeamStandingOutcome Outcome { get; }

    /// <summary>
    /// Complete public scoreboard in canonical channel-kind order, validated
    /// against the resolved game mode by <see cref="TeamStandings"/>.
    /// </summary>
    public ImmutableArray<TeamScoreValue> Scores { get; }
}
