using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Pure schema-3 Frontline objective, score, and standings kernel. The world
/// simulation supplies only binary team presence in the active objective;
/// forms, combat, lifecycle, replication, and fabrication remain reusable
/// mechanics outside the mode.
/// </summary>
public sealed class FrontlineModeKernel
{
    private readonly PublicMatchTopology _topology;
    private readonly FrontlineGameModeDefinition _gameMode;
    private readonly ImmutableArray<int> _teamIds;
    private readonly HashSet<int> _teamIdSet;
    private readonly Dictionary<int, int> _advanceByTeam;

    public FrontlineModeKernel(
        PublicMatchTopology topology,
        FrontlineGameModeDefinition gameMode,
        FrontlineActorModeMapBindingDefinition mapBinding)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(gameMode);
        ArgumentNullException.ThrowIfNull(mapBinding);

        int[] topologyTeamIds = topology.Teams.IsDefault
            ? []
            : topology.Teams
                .Select(team => team?.TeamId ?? -1)
                .ToArray();
        int[] advanceTeamIds = mapBinding.TeamAdvances
            .Select(advance => advance.TeamId)
            .ToArray();
        if (topologyTeamIds.Length != 2
            || topologyTeamIds.Any(teamId => teamId < 0)
            || topologyTeamIds.Distinct().Count() != 2
            || advanceTeamIds.Length != 2
            || advanceTeamIds.Distinct().Count() != 2
            || !topologyTeamIds.ToHashSet().SetEquals(advanceTeamIds))
        {
            throw new ArgumentException(
                "Frontline requires exactly two topology teams with matching advance bindings.",
                nameof(topology));
        }
        if (mapBinding.OrderedObjectiveRegionIds.Length
            != gameMode.FrontlinePositionCount)
        {
            throw new ArgumentException(
                "Frontline objective-region count must match its position count.",
                nameof(mapBinding));
        }

        _teamIds = topologyTeamIds.Order().ToImmutableArray();
        _teamIdSet = _teamIds.ToHashSet();
        _advanceByTeam = mapBinding.TeamAdvances.ToDictionary(
            advance => advance.TeamId,
            advance => advance.ObjectiveIndexDelta);
        if (_advanceByTeam.Values.Sum() != 0
            || _advanceByTeam.Values.Any(delta => delta is not (-1 or 1)))
        {
            throw new ArgumentException(
                "Frontline teams must advance in opposite unit directions.",
                nameof(mapBinding));
        }

        _topology = topology with
        {
            Teams = _teamIds
                .Select(teamId => new PublicScoringTeam(teamId))
                .ToImmutableArray(),
        };
        _gameMode = gameMode;
    }

    public FrontlineControlState CreateInitialState(int firstTick = 0)
    {
        if (firstTick < 0)
            throw new ArgumentOutOfRangeException(nameof(firstTick));

        return new FrontlineControlState(
            NextTick: firstTick,
            ActivePositionIndex: _gameMode.FrontlinePositionCount / 2,
            ClaimingTeamId: null,
            CaptureProgress: 0,
            DecayTicksElapsed: 0,
            ControlResumesAtTick: firstTick,
            WinnerTeamId: null);
    }

    /// <summary>
    /// Applies one post-combat objective update. Presence is a set of scoring
    /// teams with at least one positive-weight active body in the current
    /// objective; body count never multiplies pressure.
    /// </summary>
    public FrontlineControlStepResult ApplyJointTick(
        FrontlineControlState state,
        int tick,
        IReadOnlyCollection<int> presentTeamIds)
    {
        ValidateState(state);
        ArgumentNullException.ThrowIfNull(presentTeamIds);
        if (state.WinnerTeamId is not null)
        {
            throw new InvalidOperationException(
                "A breached Frontline match cannot step again.");
        }
        if (tick != state.NextTick)
        {
            throw new ArgumentException(
                $"Expected Frontline tick {state.NextTick}, received {tick}.",
                nameof(tick));
        }
        if (tick == int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "Frontline cannot advance beyond the signed 32-bit tick range.");
        }

        int[] presence = [.. presentTeamIds];
        if (presence.Distinct().Count() != presence.Length
            || presence.Any(teamId => !_teamIdSet.Contains(teamId)))
        {
            throw new ArgumentException(
                "Frontline presence must be a unique subset of topology teams.",
                nameof(presentTeamIds));
        }

        if (tick < state.ControlResumesAtTick)
        {
            return new FrontlineControlStepResult(
                state with { NextTick = tick + 1 },
                Transition: null);
        }

        return presence.Length == 1
            ? ApplySoleControl(state, tick, presence[0])
            : ApplyNonSoleControl(state, tick);
    }

    public FrontlineScoreState CreateScoreState(
        FrontlineControlState state)
    {
        ValidateState(state);
        int centre = _gameMode.FrontlinePositionCount / 2;
        return new FrontlineScoreState(
            _teamIds.Select(teamId =>
            {
                long position = checked(
                    (long)_advanceByTeam[teamId]
                    * (state.ActivePositionIndex - centre)
                    * _gameMode.Capture.Threshold);
                long claim = state.ClaimingTeamId switch
                {
                    null => 0,
                    int claimant when claimant == teamId =>
                        state.CaptureProgress,
                    _ => -state.CaptureProgress,
                };
                return new FrontlineTeamScore(
                    teamId,
                    checked(position + claim));
            }).ToArray());
    }

    public TeamStandings ResolveBreachStandings(
        FrontlineControlState state,
        IReadOnlyCollection<int> eligibleTeamIds)
    {
        ValidateState(state);
        if (state.WinnerTeamId is not int winnerTeamId)
        {
            throw new ArgumentException(
                "Breach standings require a breached control state.",
                nameof(state));
        }
        HashSet<int> eligible = SnapshotEligibleTeams(eligibleTeamIds);
        if (!eligible.Contains(winnerTeamId))
        {
            throw new ArgumentException(
                "The breaching team must remain eligible.",
                nameof(eligibleTeamIds));
        }

        return BuildStandings(
            state,
            eligible,
            (leftTeamId, rightTeamId) =>
                leftTeamId == winnerTeamId
                    ? -1
                    : rightTeamId == winnerTeamId
                        ? 1
                        : 0);
    }

    public TeamStandings ResolveTimeoutStandings(
        FrontlineControlState state,
        IReadOnlyCollection<int> eligibleTeamIds)
    {
        ValidateState(state);
        if (state.WinnerTeamId is not null)
        {
            throw new ArgumentException(
                "Timeout standings cannot replace an existing breach.",
                nameof(state));
        }
        HashSet<int> eligible = SnapshotEligibleTeams(eligibleTeamIds);
        Dictionary<int, long> scores = CreateScoreState(state)
            .Teams
            .ToDictionary(
                score => score.TeamId,
                score => score.TerritorialProgress);
        return BuildStandings(
            state,
            eligible,
            (leftTeamId, rightTeamId) =>
                -scores[leftTeamId].CompareTo(scores[rightTeamId]));
    }

    private FrontlineControlStepResult ApplySoleControl(
        FrontlineControlState state,
        int tick,
        int teamId)
    {
        int? claimant = state.ClaimingTeamId;
        int progress = state.CaptureProgress;
        int gain = _gameMode.Capture
            .GainPhaseAtTick(tick)
            .GainPerSoleTeamTick;

        if (claimant is null)
        {
            claimant = teamId;
            progress = gain;
        }
        else if (claimant == teamId)
        {
            long increased = checked((long)progress + gain);
            if (increased >= _gameMode.Capture.Threshold)
                return CompleteCapture(state, tick, teamId);
            progress = (int)increased;
        }
        else
        {
            long remaining = checked((long)progress - gain);
            if (remaining <= 0)
            {
                claimant = null;
                progress = 0;
            }
            else
            {
                progress = (int)remaining;
            }
        }

        if (claimant == teamId
            && progress >= _gameMode.Capture.Threshold)
        {
            return CompleteCapture(state, tick, teamId);
        }

        return new FrontlineControlStepResult(
            state with
            {
                NextTick = tick + 1,
                ClaimingTeamId = claimant,
                CaptureProgress = progress,
                DecayTicksElapsed = 0,
            },
            Transition: null);
    }

    private FrontlineControlStepResult ApplyNonSoleControl(
        FrontlineControlState state,
        int tick)
    {
        FrontlineCaptureDefinition capture = _gameMode.Capture;
        if (state.ClaimingTeamId is null)
        {
            return new FrontlineControlStepResult(
                state with
                {
                    NextTick = tick + 1,
                    DecayTicksElapsed = 0,
                },
                Transition: null);
        }
        if (capture.DecayAmount == 0)
        {
            return new FrontlineControlStepResult(
                state with
                {
                    NextTick = tick + 1,
                    DecayTicksElapsed = 0,
                },
                Transition: null);
        }

        int elapsed = state.DecayTicksElapsed + 1;
        if (elapsed < capture.DecayIntervalTicks)
        {
            return new FrontlineControlStepResult(
                state with
                {
                    NextTick = tick + 1,
                    DecayTicksElapsed = elapsed,
                },
                Transition: null);
        }

        int progress = Math.Max(
            0,
            state.CaptureProgress - capture.DecayAmount);
        return new FrontlineControlStepResult(
            state with
            {
                NextTick = tick + 1,
                ClaimingTeamId =
                    progress == 0 ? null : state.ClaimingTeamId,
                CaptureProgress = progress,
                DecayTicksElapsed = 0,
            },
            Transition: null);
    }

    private FrontlineControlStepResult CompleteCapture(
        FrontlineControlState state,
        int tick,
        int teamId)
    {
        int delta = _advanceByTeam[teamId];
        int breachEdge = delta > 0
            ? _gameMode.FrontlinePositionCount - 1
            : 0;
        if (state.ActivePositionIndex == breachEdge)
        {
            return new FrontlineControlStepResult(
                state with
                {
                    NextTick = tick + 1,
                    ClaimingTeamId = null,
                    CaptureProgress = 0,
                    DecayTicksElapsed = 0,
                    WinnerTeamId = teamId,
                },
                new FrontlineBaseBreached(
                    tick,
                    teamId,
                    state.ActivePositionIndex));
        }

        long resumesAt = checked(
            (long)tick
            + 1
            + _gameMode.Capture.RedeployPauseTicks);
        if (resumesAt > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "Frontline redeploy schedule exceeds the signed 32-bit tick range.");
        }
        int nextPosition = checked(state.ActivePositionIndex + delta);
        if (nextPosition < 0
            || nextPosition >= _gameMode.FrontlinePositionCount)
        {
            throw new InvalidOperationException(
                "Frontline capture advanced outside the objective sequence.");
        }

        return new FrontlineControlStepResult(
            state with
            {
                NextTick = tick + 1,
                ActivePositionIndex = nextPosition,
                ClaimingTeamId = null,
                CaptureProgress = 0,
                DecayTicksElapsed = 0,
                ControlResumesAtTick = (int)resumesAt,
            },
            new FrontlinePositionAdvanced(
                tick,
                teamId,
                state.ActivePositionIndex,
                nextPosition));
    }

    private TeamStandings BuildStandings(
        FrontlineControlState state,
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

        int topCount = ranks.Count(entry => entry.Value == 1);
        bool uniqueWinner = topCount == 1;
        Dictionary<int, long> scores = CreateScoreState(state)
            .Teams
            .ToDictionary(
                score => score.TeamId,
                score => score.TerritorialProgress);
        TeamStanding[] standings = _teamIds
            .Select(teamId =>
            {
                int rank = ranks[teamId];
                TeamStandingOutcome outcome = rank switch
                {
                    1 when uniqueWinner => TeamStandingOutcome.Win,
                    1 => TeamStandingOutcome.Draw,
                    _ => TeamStandingOutcome.Loss,
                };
                return new TeamStanding(
                    teamId,
                    rank,
                    outcome,
                    [
                        new TeamScoreValue(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            scores[teamId]),
                    ]);
            })
            .ToArray();
        return new TeamStandings(_topology, _gameMode, standings);
    }

    private HashSet<int> SnapshotEligibleTeams(
        IReadOnlyCollection<int> eligibleTeamIds)
    {
        ArgumentNullException.ThrowIfNull(eligibleTeamIds);
        int[] snapshot = [.. eligibleTeamIds];
        if (snapshot.Distinct().Count() != snapshot.Length
            || snapshot.Any(teamId => !_teamIdSet.Contains(teamId)))
        {
            throw new ArgumentException(
                "Eligible Frontline teams must be a unique subset of topology teams.",
                nameof(eligibleTeamIds));
        }
        return snapshot.ToHashSet();
    }

    private void ValidateState(FrontlineControlState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        FrontlineCaptureDefinition capture = _gameMode.Capture;
        bool decayDisabled =
            capture.DecayAmount == 0
            && capture.DecayIntervalTicks == 0;
        bool invalid =
            state.NextTick < 0
            || state.ActivePositionIndex < 0
            || state.ActivePositionIndex
                >= _gameMode.FrontlinePositionCount
            || state.ClaimingTeamId is int claimant
                && !_teamIdSet.Contains(claimant)
            || state.WinnerTeamId is int winner
                && !_teamIdSet.Contains(winner)
            || state.CaptureProgress < 0
            || state.CaptureProgress >= capture.Threshold
            || state.DecayTicksElapsed < 0
            || decayDisabled && state.DecayTicksElapsed != 0
            || !decayDisabled
                && state.DecayTicksElapsed >= capture.DecayIntervalTicks
            || state.ControlResumesAtTick < 0
            || state.ClaimingTeamId is null
                && state.CaptureProgress != 0
            || state.ClaimingTeamId is null
                && state.DecayTicksElapsed != 0
            || state.ClaimingTeamId is not null
                && state.CaptureProgress == 0
            || state.NextTick < state.ControlResumesAtTick
                && (state.ClaimingTeamId is not null
                    || state.CaptureProgress != 0
                    || state.DecayTicksElapsed != 0)
            || state.WinnerTeamId is int winnerTeamId
                && (state.ActivePositionIndex
                        != (_advanceByTeam[winnerTeamId] > 0
                            ? _gameMode.FrontlinePositionCount - 1
                            : 0)
                    || state.ClaimingTeamId is not null
                    || state.CaptureProgress != 0
                    || state.DecayTicksElapsed != 0
                    || state.ControlResumesAtTick > state.NextTick);
        if (invalid)
        {
            throw new ArgumentException(
                "Frontline control state violates the resolved mode invariants.",
                nameof(state));
        }
    }
}
