using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Canonical, validated team placements and public scoreboard values. This is
/// a reusable result component, not a terminal-result envelope; completion
/// facts and typed mode-specific result units belong to the eventual envelope.
/// </summary>
public sealed class TeamStandings
{
    public TeamStandings(
        PublicMatchTopology topology,
        GameModeDefinition gameMode,
        IReadOnlyCollection<TeamStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(gameMode);
        ArgumentNullException.ThrowIfNull(standings);

        int[] topologyTeamIds = ValidateTopologyTeams(topology);
        TeamStanding[] canonicalStandings = [.. standings];
        ValidateStandingTeams(canonicalStandings, topologyTeamIds);
        ValidateScoreCatalog(canonicalStandings, gameMode);
        Array.Sort(
            canonicalStandings,
            static (left, right) =>
            {
                int rankComparison = left.Rank.CompareTo(right.Rank);
                return rankComparison != 0
                    ? rankComparison
                    : left.TeamId.CompareTo(right.TeamId);
            });
        ValidateCompetitionRanks(canonicalStandings);
        int? winnerTeamId = ValidateOutcomes(canonicalStandings);

        Standings = canonicalStandings.ToImmutableArray();
        WinnerTeamId = winnerTeamId;
    }

    /// <summary>Ordered by rank and then stable team ID.</summary>
    public ImmutableArray<TeamStanding> Standings { get; }

    /// <summary>
    /// Derived compatibility projection. Null means the top rank is tied.
    /// Standings remain the authoritative result component.
    /// </summary>
    public int? WinnerTeamId { get; }

    private static int[] ValidateTopologyTeams(PublicMatchTopology topology)
    {
        if (topology.Teams.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "Standings topology teams must be initialized and non-empty.",
                nameof(topology));
        }
        if (topology.Teams.Any(team => team is null))
        {
            throw new ArgumentException(
                "Standings topology teams cannot contain null entries.",
                nameof(topology));
        }

        int[] teamIds = topology.Teams
            .Select(team => team.TeamId)
            .ToArray();
        if (teamIds.Any(teamId => teamId < 0)
            || teamIds.Distinct().Count() != teamIds.Length)
        {
            throw new ArgumentException(
                "Standings topology team IDs must be non-negative and unique.",
                nameof(topology));
        }
        if (teamIds.Length < 2)
        {
            throw new ArgumentException(
                "Team standings need at least two scoring teams.",
                nameof(topology));
        }

        return teamIds;
    }

    private static void ValidateStandingTeams(
        IReadOnlyList<TeamStanding> standings,
        IReadOnlyCollection<int> topologyTeamIds)
    {
        if (standings.Count != topologyTeamIds.Count)
        {
            throw new ArgumentException(
                "Standings must cover every topology team exactly once.",
                nameof(standings));
        }
        if (standings.Any(standing => standing is null))
        {
            throw new ArgumentException(
                "Standings cannot contain null entries.",
                nameof(standings));
        }

        int[] standingTeamIds = standings
            .Select(standing => standing.TeamId)
            .ToArray();
        if (standingTeamIds.Distinct().Count() != standingTeamIds.Length
            || !standingTeamIds.ToHashSet().SetEquals(topologyTeamIds))
        {
            throw new ArgumentException(
                "Standings must cover exactly the topology's unique scoring teams.",
                nameof(standings));
        }
    }

    private static void ValidateScoreCatalog(
        IReadOnlyList<TeamStanding> standings,
        GameModeDefinition gameMode)
    {
        ScoreChannelDefinition.ChannelKind[] expectedChannels = gameMode
            .ScoreCatalog
            .Select(channel => channel.Channel)
            .ToArray();

        foreach (TeamStanding standing in standings)
        {
            ScoreChannelDefinition.ChannelKind[] actualChannels = standing
                .Scores
                .Select(score => score.Channel)
                .ToArray();
            if (!actualChannels.SequenceEqual(expectedChannels))
            {
                throw new ArgumentException(
                    "Every standing must contain exactly the game mode's declared score channels.",
                    nameof(standings));
            }
        }
    }

    private static void ValidateCompetitionRanks(
        IReadOnlyList<TeamStanding> standings)
    {
        int groupStartIndex = 0;
        while (groupStartIndex < standings.Count)
        {
            int rank = standings[groupStartIndex].Rank;
            int expectedRank = groupStartIndex + 1;
            if (rank != expectedRank)
            {
                throw new ArgumentException(
                    "Standing ranks must use competition ranking (for example 1, 1, 3).",
                    nameof(standings));
            }

            do
            {
                groupStartIndex++;
            }
            while (groupStartIndex < standings.Count
                && standings[groupStartIndex].Rank == rank);
        }
    }

    private static int? ValidateOutcomes(
        IReadOnlyList<TeamStanding> standings)
    {
        int topRankCount = standings.Count(standing => standing.Rank == 1);
        bool hasUniqueWinner = topRankCount == 1;

        foreach (TeamStanding standing in standings)
        {
            TeamStandingOutcome expected = standing.Rank switch
            {
                1 when hasUniqueWinner => TeamStandingOutcome.Win,
                1 => TeamStandingOutcome.Draw,
                _ => TeamStandingOutcome.Loss,
            };
            if (standing.Outcome != expected)
            {
                throw new ArgumentException(
                    "Standing outcomes must identify a unique winner or mark every tied top team as a draw.",
                    nameof(standings));
            }
        }

        return hasUniqueWinner
            ? standings[0].TeamId
            : null;
    }
}
