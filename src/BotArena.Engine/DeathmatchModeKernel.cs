using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Pure Deathmatch score and standings kernel. World simulation supplies
/// actual damage results, the active-health snapshot, and match eligibility;
/// this type owns only mode scoring and ranking semantics.
/// </summary>
public sealed class DeathmatchModeKernel
{
    private readonly DeathmatchGameModeDefinition _gameMode;
    private readonly PublicMatchTopology _topology;
    private readonly ImmutableArray<int> _teamIds;
    private readonly HashSet<int> _teamIdSet;

    public DeathmatchModeKernel(
        PublicMatchTopology topology,
        DeathmatchGameModeDefinition gameMode)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(gameMode);
        if (topology.Teams.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "Deathmatch topology teams must be initialized and non-empty.",
                nameof(topology));
        }
        if (topology.Teams.Any(team => team is null))
        {
            throw new ArgumentException(
                "Deathmatch topology teams cannot contain null entries.",
                nameof(topology));
        }

        int[] teamIds = topology.Teams
            .Select(team => team.TeamId)
            .ToArray();
        if (teamIds.Any(teamId => teamId < 0)
            || teamIds.Distinct().Count() != teamIds.Length)
        {
            throw new ArgumentException(
                "Deathmatch topology team IDs must be non-negative and unique.",
                nameof(topology));
        }
        if (teamIds.Length < 2)
        {
            throw new ArgumentException(
                "Deathmatch needs at least two scoring teams.",
                nameof(topology));
        }

        _teamIds = teamIds.Order().ToImmutableArray();
        _teamIdSet = _teamIds.ToHashSet();
        _topology = topology with
        {
            Teams = topology.Teams
                .OrderBy(team => team.TeamId)
                .ToImmutableArray(),
        };
        _gameMode = gameMode;
    }

    public DeathmatchScoreState CreateInitialState() =>
        new(
            _teamIds.Select(teamId =>
                new DeathmatchTeamScore(
                    teamId,
                    kills: 0,
                    deaths: 0,
                    damageDealt: 0))
            .ToArray());

    /// <summary>
    /// Applies every contact before evaluating the optional kill limit. The
    /// active-health and eligibility inputs are post-tick snapshots.
    /// </summary>
    public DeathmatchJointTickResult ApplyJointTick(
        DeathmatchScoreState scoreState,
        IReadOnlyCollection<DeathmatchDamageContact> damageContacts,
        IReadOnlyDictionary<int, long> activeHealthByTeam,
        IReadOnlyCollection<int> eligibleTeamIds)
    {
        ValidateScoreState(scoreState);
        ArgumentNullException.ThrowIfNull(damageContacts);
        DeathmatchDamageContact[] contacts = [.. damageContacts];
        if (contacts.Any(contact => contact is null))
        {
            throw new ArgumentException(
                "Deathmatch damage contacts cannot contain null entries.",
                nameof(damageContacts));
        }
        foreach (DeathmatchDamageContact contact in contacts)
        {
            if (!_teamIdSet.Contains(contact.TargetTeamId)
                || contact.SourceTeamId is int sourceTeamId
                    && !_teamIdSet.Contains(sourceTeamId))
            {
                throw new ArgumentException(
                    "Every attributed damage contact must reference known scoring teams.",
                    nameof(damageContacts));
            }
        }

        Dictionary<int, long> activeHealth =
            SnapshotActiveHealth(activeHealthByTeam);
        HashSet<int> eligibleTeams =
            SnapshotEligibleTeams(eligibleTeamIds);

        DeathmatchTeamScore[] nextScores = scoreState.Teams.ToArray();
        var scoreIndexes = nextScores
            .Select((score, index) => (score.TeamId, Index: index))
            .ToDictionary(entry => entry.TeamId, entry => entry.Index);

        foreach (DeathmatchDamageContact contact in contacts)
        {
            if (contact.SourceTeamId is int sourceTeamId
                && sourceTeamId != contact.TargetTeamId)
            {
                AddScore(
                    nextScores,
                    scoreIndexes,
                    sourceTeamId,
                    kills: contact.CausedDestruction ? 1 : 0,
                    deaths: 0,
                    damageDealt: contact.ActualHealthRemoved);
            }

            if (contact.CausedDestruction)
            {
                AddScore(
                    nextScores,
                    scoreIndexes,
                    contact.TargetTeamId,
                    kills: 0,
                    deaths: 1,
                    damageDealt: 0);
            }
        }

        var nextState = new DeathmatchScoreState(nextScores);
        TeamStandings? killLimitStandings =
            ResolveKillLimitStandings(
                nextState,
                activeHealth,
                eligibleTeams);
        return new DeathmatchJointTickResult(
            nextState,
            killLimitStandings);
    }

    /// <summary>
    /// Resolves max-tick standings with the mode's ordered timeout ranking.
    /// Only eligible teams are compared; ineligible teams are tied below them.
    /// </summary>
    public TeamStandings ResolveTimeoutStandings(
        DeathmatchScoreState scoreState,
        IReadOnlyDictionary<int, long> activeHealthByTeam,
        IReadOnlyCollection<int> eligibleTeamIds)
    {
        ValidateScoreState(scoreState);
        Dictionary<int, long> activeHealth =
            SnapshotActiveHealth(activeHealthByTeam);
        HashSet<int> eligibleTeams =
            SnapshotEligibleTeams(eligibleTeamIds);
        Dictionary<int, DeathmatchTeamScore> scoresByTeam =
            scoreState.Teams.ToDictionary(score => score.TeamId);

        int Compare(int leftTeamId, int rightTeamId)
        {
            foreach (ScoreRankingDefinition ranking
                in _gameMode.DeathmatchVictory.TimeoutRanking)
            {
                long left = ScoreValue(
                    scoresByTeam[leftTeamId],
                    activeHealth[leftTeamId],
                    ranking.Channel);
                long right = ScoreValue(
                    scoresByTeam[rightTeamId],
                    activeHealth[rightTeamId],
                    ranking.Channel);
                int comparison = left.CompareTo(right);
                if (comparison == 0)
                    continue;

                return ranking.Direction
                    == ScoreRankingDefinition.SortDirection.HigherWins
                    ? -comparison
                    : comparison;
            }

            return 0;
        }

        return BuildStandings(
            scoreState,
            activeHealth,
            eligibleTeams,
            Compare);
    }

    private TeamStandings? ResolveKillLimitStandings(
        DeathmatchScoreState scoreState,
        IReadOnlyDictionary<int, long> activeHealth,
        IReadOnlySet<int> eligibleTeams)
    {
        int? killsToWin = _gameMode.DeathmatchVictory.KillsToWin;
        if (killsToWin is null || eligibleTeams.Count == 0)
            return null;

        Dictionary<int, DeathmatchTeamScore> scoresByTeam =
            scoreState.Teams.ToDictionary(score => score.TeamId);
        long highestEligibleKills = eligibleTeams
            .Max(teamId => scoresByTeam[teamId].Kills);
        if (highestEligibleKills < killsToWin.Value)
            return null;

        int Compare(int leftTeamId, int rightTeamId) =>
            -scoresByTeam[leftTeamId]
                .Kills
                .CompareTo(scoresByTeam[rightTeamId].Kills);

        return BuildStandings(
            scoreState,
            activeHealth,
            eligibleTeams,
            Compare);
    }

    private TeamStandings BuildStandings(
        DeathmatchScoreState scoreState,
        IReadOnlyDictionary<int, long> activeHealth,
        IReadOnlySet<int> eligibleTeams,
        Comparison<int> authoritativeComparison)
    {
        int[] rankedEligible = eligibleTeams.ToArray();
        Array.Sort(
            rankedEligible,
            (left, right) =>
            {
                int comparison = authoritativeComparison(left, right);
                return comparison != 0
                    ? comparison
                    : left.CompareTo(right);
            });

        var ranks = new Dictionary<int, int>(_teamIds.Length);
        for (int index = 0; index < rankedEligible.Length; index++)
        {
            int rank = index == 0
                ? 1
                : authoritativeComparison(
                    rankedEligible[index - 1],
                    rankedEligible[index]) == 0
                    ? ranks[rankedEligible[index - 1]]
                    : index + 1;
            ranks.Add(rankedEligible[index], rank);
        }

        int ineligibleRank = rankedEligible.Length + 1;
        foreach (int teamId in _teamIds)
        {
            if (!eligibleTeams.Contains(teamId))
                ranks.Add(teamId, ineligibleRank);
        }

        int topRankCount = ranks.Count(entry => entry.Value == 1);
        bool hasUniqueWinner = topRankCount == 1;
        Dictionary<int, DeathmatchTeamScore> scoresByTeam =
            scoreState.Teams.ToDictionary(score => score.TeamId);
        TeamStanding[] standings = _teamIds
            .Select(teamId =>
            {
                int rank = ranks[teamId];
                TeamStandingOutcome outcome = rank switch
                {
                    1 when hasUniqueWinner => TeamStandingOutcome.Win,
                    1 => TeamStandingOutcome.Draw,
                    _ => TeamStandingOutcome.Loss,
                };
                return new TeamStanding(
                    teamId,
                    rank,
                    outcome,
                    CreatePublicScores(
                        scoresByTeam[teamId],
                        activeHealth[teamId]));
            })
            .ToArray();
        return new TeamStandings(_topology, _gameMode, standings);
    }

    private IReadOnlyCollection<TeamScoreValue> CreatePublicScores(
        DeathmatchTeamScore score,
        long activeHealth) =>
        _gameMode.ScoreCatalog
            .Select(channel =>
                new TeamScoreValue(
                    channel.Channel,
                    ScoreValue(score, activeHealth, channel.Channel)))
            .ToArray();

    private static long ScoreValue(
        DeathmatchTeamScore score,
        long activeHealth,
        ScoreChannelDefinition.ChannelKind channel) =>
        channel switch
        {
            ScoreChannelDefinition.ChannelKind.Kills => score.Kills,
            ScoreChannelDefinition.ChannelKind.Deaths => score.Deaths,
            ScoreChannelDefinition.ChannelKind.DamageDealt =>
                score.DamageDealt,
            ScoreChannelDefinition.ChannelKind.ActiveHealth => activeHealth,
            _ => throw new InvalidOperationException(
                $"Deathmatch cannot produce score channel '{channel}'."),
        };

    private void ValidateScoreState(DeathmatchScoreState scoreState)
    {
        ArgumentNullException.ThrowIfNull(scoreState);
        if (!scoreState.Teams
            .Select(score => score.TeamId)
            .SequenceEqual(_teamIds))
        {
            throw new ArgumentException(
                "Deathmatch score state must cover exactly the kernel topology teams.",
                nameof(scoreState));
        }
    }

    private Dictionary<int, long> SnapshotActiveHealth(
        IReadOnlyDictionary<int, long> activeHealthByTeam)
    {
        ArgumentNullException.ThrowIfNull(activeHealthByTeam);
        var snapshot = activeHealthByTeam.ToDictionary();
        if (snapshot.Count != _teamIds.Length
            || !snapshot.Keys.ToHashSet().SetEquals(_teamIdSet))
        {
            throw new ArgumentException(
                "Active-health snapshot must cover exactly the kernel topology teams.",
                nameof(activeHealthByTeam));
        }
        if (snapshot.Values.Any(value => value < 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeHealthByTeam),
                "Active-health snapshots cannot contain negative values.");
        }

        return snapshot;
    }

    private HashSet<int> SnapshotEligibleTeams(
        IReadOnlyCollection<int> eligibleTeamIds)
    {
        ArgumentNullException.ThrowIfNull(eligibleTeamIds);
        int[] snapshot = [.. eligibleTeamIds];
        if (snapshot.Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Eligible scoring-team IDs must be unique.",
                nameof(eligibleTeamIds));
        }
        if (snapshot.Any(teamId => !_teamIdSet.Contains(teamId)))
        {
            throw new ArgumentException(
                "Eligible scoring teams must belong to the kernel topology.",
                nameof(eligibleTeamIds));
        }

        return snapshot.ToHashSet();
    }

    private static void AddScore(
        DeathmatchTeamScore[] scores,
        IReadOnlyDictionary<int, int> scoreIndexes,
        int teamId,
        long kills,
        long deaths,
        long damageDealt)
    {
        int index = scoreIndexes[teamId];
        DeathmatchTeamScore current = scores[index];
        scores[index] = new DeathmatchTeamScore(
            teamId,
            checked(current.Kills + kills),
            checked(current.Deaths + deaths),
            checked(current.DamageDealt + damageDealt));
    }
}
