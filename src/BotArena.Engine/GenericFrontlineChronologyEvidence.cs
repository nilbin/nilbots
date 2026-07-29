using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Replays Frontline's pure objective kernel over recorded generic world
/// boundaries. This validates mode phase order without re-simulating combat:
/// presence comes from post-combat survivors, while an end-clock form
/// completion contributes with its source form on the started tick.
/// </summary>
internal static class GenericFrontlineChronologyEvidence
{
    private const string TerritorialProgressChannel =
        "territorial-progress";

    public static void Validate(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyList<GenericActorMatchTickFrame> ticks,
        GenericActorMatchResult? result)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(initialFrame);
        ArgumentNullException.ThrowIfNull(ticks);
        if (definition.Rules.GameMode
                is not FrontlineGameModeDefinition gameMode
            || definition.ModeMapBinding
                is not FrontlineActorModeMapBindingDefinition binding)
        {
            throw new ArgumentException(
                "Frontline chronology evidence requires an exact Frontline mode and map-binding pair.",
                nameof(definition));
        }

        var kernel = new FrontlineModeKernel(
            definition.Topology,
            gameMode,
            binding);
        FrontlineControlState control = kernel.CreateInitialState();
        Dictionary<string, int> objectiveWeights =
            definition.Rules.Forms.ToDictionary(
                form => form.Id,
                form => form.ObjectiveWeight,
                StringComparer.Ordinal);
        Dictionary<string, ImmutableHashSet<Position>> regionTiles =
            definition.Map.Regions.ToDictionary(
                region => region.RegionId,
                region => region.Tiles.ToImmutableHashSet(),
                StringComparer.Ordinal);

        ValidateBoundary(
            initialFrame.State,
            control,
            kernel,
            gameMode,
            "initialFrame");
        if (initialFrame.Events.Any(IsModeOwnedEvent))
        {
            throw Invalid(
                "initial facts cannot claim a score or control transition");
        }

        GenericFrontlineEndReason? terminalReason = null;
        for (int index = 0; index < ticks.Count; index++)
        {
            GenericActorMatchTickFrame frame = ticks[index];
            ValidateBoundary(
                frame.TickStart.State,
                control,
                kernel,
                gameMode,
                "ticks");
            ValidateObservedModeBoundary(frame);

            ImmutableArray<int> eligible = frame.PostState.Scoreboard.Teams
                .Where(team => team.Eligible)
                .Select(team => team.TeamId)
                .Order()
                .ToImmutableArray();
            FrontlineControlState previous = control;
            if (eligible.Length <= 1)
            {
                terminalReason = GenericFrontlineEndReason.FaultEligibility;
            }
            else
            {
                ImmutableHashSet<Position> activeTiles = regionTiles[
                    binding.OrderedObjectiveRegionIds[
                        previous.ActivePositionIndex]];
                ImmutableDictionary<int, int> objectiveWeightByTeam =
                    ObjectiveWeightAtModePhase(
                    definition,
                    frame,
                    objectiveWeights,
                    activeTiles);
                control = kernel.ApplyJointTick(
                        previous,
                        frame.Tick,
                        objectiveWeightByTeam)
                    .State;
                if (control.WinnerTeamId is not null)
                {
                    terminalReason = GenericFrontlineEndReason.BaseBreach;
                }
                else if (frame.Tick + 1
                         >= definition.Rules.Limits.MaxTicks)
                {
                    terminalReason = GenericFrontlineEndReason.MaxTicks;
                }
            }

            ValidateBoundary(
                frame.PostState,
                control,
                kernel,
                gameMode,
                "ticks");
            ValidateModeOwnedEvents(
                definition,
                frame,
                previous,
                control,
                kernel,
                gameMode,
                eligible.Length <= 1);

            if (terminalReason is not null && index != ticks.Count - 1)
            {
                throw Invalid(
                    "no frame may follow fault eligibility, base breach, or the maximum-tick boundary");
            }
        }

        if (result is null)
        {
            if (terminalReason is not null)
            {
                throw Invalid(
                    "a chronology that reached a Frontline terminal phase must carry terminal facts");
            }
            return;
        }
        if (ticks.Count == 0 || terminalReason is null)
        {
            throw Invalid(
                "Frontline cannot complete before an executed terminal joint tick");
        }
        if (result.Mode
                is not GenericActorMatchModeResult.Frontline frontline
            || frontline.Reason != terminalReason.Value)
        {
            throw Invalid(
                "typed Frontline terminal reason does not match the first reached terminal phase");
        }

        GenericActorWorldSnapshot finalState = ticks[^1].PostState;
        GenericActorRuntimeObservation.ModeObservationState.Frontline
            expectedPublicControl = ProjectControl(gameMode, control);
        if (!Equals(frontline.Control, expectedPublicControl)
            || !ScoresEqual(
                frontline.Scores,
                kernel.CreateScoreState(control)))
        {
            throw Invalid(
                "terminal Frontline control and score facts must exactly match the final objective state");
        }

        TeamStandings expectedStandings = terminalReason.Value switch
        {
            GenericFrontlineEndReason.FaultEligibility =>
                kernel.ResolveTimeoutStandings(
                    control,
                    result.EligibleTeamIds),
            GenericFrontlineEndReason.BaseBreach =>
                kernel.ResolveBreachStandings(
                    control,
                    result.EligibleTeamIds),
            GenericFrontlineEndReason.MaxTicks =>
                kernel.ResolveTimeoutStandings(
                    control,
                    result.EligibleTeamIds),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        if (!StandingsEqual(expectedStandings, result.Standings))
        {
            throw Invalid(
                "terminal standings do not follow the resolved Frontline victory policy");
        }

        string expectedCompletionReason = terminalReason.Value switch
        {
            GenericFrontlineEndReason.FaultEligibility =>
                "fault-eligibility",
            GenericFrontlineEndReason.BaseBreach => "base-breach",
            GenericFrontlineEndReason.MaxTicks => "max-ticks",
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        if (!string.Equals(
                result.CompletionReason,
                expectedCompletionReason,
                StringComparison.Ordinal)
            || !ScoreboardMatches(
                finalState.Scoreboard,
                frontline.Scores))
        {
            throw Invalid(
                "terminal completion ID and territorial scores must match the final public state");
        }
    }

    private static ImmutableDictionary<int, int>
        ObjectiveWeightAtModePhase(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickFrame frame,
        IReadOnlyDictionary<string, int> objectiveWeights,
        IReadOnlySet<Position> activeTiles)
    {
        Dictionary<ActorIdentity, string> endClockSourceForms = frame.Events
            .Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .FormTransitionCompleted)
            .Select(item =>
                (GenericActorRuntimeObservation.EventPayload.FormTransition)
                    item.Payload)
            .Where(payload =>
            {
                ActorSameLifeTransitionDefinition transition =
                    definition.Rules.SameLifeTransitions.Single(value =>
                        string.Equals(
                            value.TransitionId,
                            payload.TransitionId,
                            StringComparison.Ordinal));
                return transition.Windup.Completion
                    == ActorTransitionWindupDefinition
                        .ActorTransitionCompletionKind
                        .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate;
            })
            .ToDictionary(
                payload => payload.ActorId,
                payload => payload.FromFormId);

        return frame.PostState.ActiveLives
            .Select(life =>
            {
                string modePhaseForm = endClockSourceForms.GetValueOrDefault(
                    life.ActorId,
                    life.FormId);
                return (
                    Life: life,
                    Weight: objectiveWeights[modePhaseForm]);
            })
            .Where(item =>
                item.Weight > 0
                && activeTiles.Contains(item.Life.Position))
            .GroupBy(item => item.Life.ActorId.TeamId)
            .OrderBy(group => group.Key)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.Sum(item => item.Weight));
    }

    private static void ValidateBoundary(
        GenericActorWorldSnapshot state,
        FrontlineControlState control,
        FrontlineModeKernel kernel,
        FrontlineGameModeDefinition gameMode,
        string parameterName)
    {
        if (state.Mode
                is not GenericActorRuntimeObservation.ModeObservationState
                    .Frontline actualMode
            || !Equals(actualMode, ProjectControl(gameMode, control))
            || !ScoreboardMatches(
                state.Scoreboard,
                kernel.CreateScoreState(control)))
        {
            throw new ArgumentException(
                "Frontline world boundary does not match the authoritative objective kernel.",
                parameterName);
        }
    }

    private static void ValidateObservedModeBoundary(
        GenericActorMatchTickFrame frame)
    {
        foreach (GenericActorMatchActorTurn turn in frame.ActorTurns)
        {
            if (!Equals(
                    turn.Observation.Mode,
                    frame.TickStart.State.Mode)
                || !ScoreboardsEqual(
                    turn.Observation.Scoreboard,
                    frame.TickStart.State.Scoreboard))
            {
                throw Invalid(
                    "every actor must receive the exact frozen public Frontline mode and scoreboard boundary");
            }
        }
    }

    private static void ValidateModeOwnedEvents(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickFrame frame,
        FrontlineControlState previous,
        FrontlineControlState current,
        FrontlineModeKernel kernel,
        FrontlineGameModeDefinition gameMode,
        bool modePhaseSkipped)
    {
        FrontlineScoreState beforeScores = kernel.CreateScoreState(previous);
        FrontlineScoreState afterScores = kernel.CreateScoreState(current);
        var expectedScores = afterScores.Teams
            .Where(score =>
                beforeScores.Teams.Single(
                        value => value.TeamId == score.TeamId)
                    .TerritorialProgress
                != score.TerritorialProgress)
            .ToArray();
        GenericActorRuntimeObservation.ModeObservationState.Frontline
            beforeMode = ProjectControl(gameMode, previous);
        GenericActorRuntimeObservation.ModeObservationState.Frontline
            afterMode = ProjectControl(gameMode, current);
        bool expectedModeChange = !Equals(beforeMode, afterMode);
        GenericActorAuthoritativeEvent[] facts = frame.Events
            .Where(IsModeOwnedEvent)
            .OrderBy(item => item.Ordinal)
            .ToArray();
        ValidateModeOwnedEventPhaseOrder(
            definition,
            frame,
            facts);
        int expectedCount =
            expectedScores.Length + (expectedModeChange ? 1 : 0);
        if (modePhaseSkipped && expectedCount != 0
            || facts.Length != expectedCount
            || facts.Any(item =>
                item.EventAudience
                    is not GenericActorAuthoritativeEvent.Audience.Public))
        {
            throw Invalid(
                "Frontline mode events must exactly describe the non-skipped score and control changes");
        }

        for (int index = 0; index < expectedScores.Length; index++)
        {
            FrontlineTeamScore expected = expectedScores[index];
            if (facts[index].Payload is not
                    GenericActorRuntimeObservation.EventPayload.ScoreChanged
                        payload
                || payload.TeamId != expected.TeamId
                || !string.Equals(
                    payload.Channel,
                    TerritorialProgressChannel,
                    StringComparison.Ordinal)
                || payload.NewValue != expected.TerritorialProgress)
            {
                throw Invalid(
                    "Frontline score-change evidence is incomplete, reordered, or has the wrong value");
            }
        }
        if (expectedModeChange
            && (facts[^1].Payload is not
                    GenericActorRuntimeObservation.EventPayload.ModeChanged
            {
                State:
                        GenericActorRuntimeObservation.ModeObservationState
                            .Frontline,
            }
                || !Equals(
                    ((GenericActorRuntimeObservation.EventPayload.ModeChanged)
                        facts[^1].Payload).State,
                    afterMode)))
        {
            throw Invalid(
                "Frontline mode-change evidence must carry the exact post-update public control state");
        }
    }

    private static void ValidateModeOwnedEventPhaseOrder(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickFrame frame,
        IReadOnlyList<GenericActorAuthoritativeEvent> modeFacts)
    {
        if (modeFacts.Count == 0)
            return;

        long firstModeOrdinal = modeFacts[0].Ordinal;
        long lastModeOrdinal = modeFacts[^1].Ordinal;
        bool preModeFactAfterMode =
            frame.Traversals.Any(item =>
                item.Ordinal >= firstModeOrdinal)
            || frame.Events.Any(item =>
                IsPreModeResolutionEvent(item)
                && item.Ordinal >= firstModeOrdinal);
        bool endClockCompletionBeforeMode =
            frame.Events.Any(item =>
            {
                if (item.Kind
                        != GenericActorRuntimeObservation.EventKind
                            .FormTransitionCompleted
                    || item.Payload is not
                        GenericActorRuntimeObservation.EventPayload
                            .FormTransition payload)
                {
                    return false;
                }

                ActorFormTransitionDefinition? transition = definition
                    .Rules.SameLifeTransitions
                    .OfType<ActorFormTransitionDefinition>()
                    .SingleOrDefault(value => string.Equals(
                        value.TransitionId,
                        payload.TransitionId,
                        StringComparison.Ordinal));
                return transition?.Windup.Completion
                        == ActorTransitionWindupDefinition
                            .ActorTransitionCompletionKind
                            .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate
                    && item.Ordinal <= lastModeOrdinal;
            });
        if (preModeFactAfterMode || endClockCompletionBeforeMode)
        {
            throw Invalid(
                "authoritative facts must preserve the post-combat, mode-update, then end-clock completion order");
        }
    }

    private static bool IsPreModeResolutionEvent(
        GenericActorAuthoritativeEvent item) =>
        item.Kind is
            GenericActorRuntimeObservation.EventKind.Rotation
            or GenericActorRuntimeObservation.EventKind.Movement
            or GenericActorRuntimeObservation.EventKind.MovementBlocked
            or GenericActorRuntimeObservation.EventKind.Attack
            or GenericActorRuntimeObservation.EventKind.ProjectileDeflected
            or GenericActorRuntimeObservation.EventKind.Damage
            or GenericActorRuntimeObservation.EventKind.Destruction
            or GenericActorRuntimeObservation.EventKind.LifeSpawned
            or GenericActorRuntimeObservation.EventKind.LifeRetired
            or GenericActorRuntimeObservation.EventKind.RuntimeFault
            or GenericActorRuntimeObservation.EventKind
                .ParticipantDisqualified
            or GenericActorRuntimeObservation.EventKind.LifecycleQueued
            or GenericActorRuntimeObservation.EventKind.LifecycleCancelled
            or GenericActorRuntimeObservation.EventKind.LifecycleCompleted
            or GenericActorRuntimeObservation.EventKind
                .FormTransitionStarted
            or GenericActorRuntimeObservation.EventKind
                .LifecycleClockCancelled;

    private static GenericActorRuntimeObservation.ModeObservationState
        .Frontline ProjectControl(
            FrontlineGameModeDefinition gameMode,
            FrontlineControlState control)
    {
        FrontlineRatchetHold? activeHold =
            control.RatchetHold is { } hold
            && hold.HoldsThroughTick >= control.NextTick
                ? hold
                : null;
        return new(
            gameMode.ModeId,
            control.ActivePositionIndex,
            control.ClaimingTeamId,
            control.CaptureProgress,
            control.DecayTicksElapsed,
            control.ControlResumesAtTick,
            activeHold?.TeamId,
            activeHold is null
                ? 0
                : checked(
                    activeHold.HoldsThroughTick
                    - control.NextTick
                    + 1));
    }

    private static bool ScoreboardMatches(
        GenericActorRuntimeObservation.ScoreboardState scoreboard,
        FrontlineScoreState scores)
    {
        if (scoreboard.Teams.Length != scores.Teams.Length)
            return false;
        Dictionary<int, long> expected = scores.Teams.ToDictionary(
            score => score.TeamId,
            score => score.TerritorialProgress);
        return scoreboard.Teams.All(team =>
            expected.TryGetValue(team.TeamId, out long value)
            && team.Scores.Length == 1
            && string.Equals(
                team.Scores[0].Channel,
                TerritorialProgressChannel,
                StringComparison.Ordinal)
            && team.Scores[0].Value == value);
    }

    private static bool ScoresEqual(
        FrontlineScoreState left,
        FrontlineScoreState right) =>
        left.Teams.Length == right.Teams.Length
        && left.Teams.Zip(right.Teams).All(pair =>
            pair.First.TeamId == pair.Second.TeamId
            && pair.First.TerritorialProgress
                == pair.Second.TerritorialProgress);

    private static bool ScoreboardsEqual(
        GenericActorRuntimeObservation.ScoreboardState left,
        GenericActorRuntimeObservation.ScoreboardState right) =>
        left.Teams.Length == right.Teams.Length
        && left.Teams.Zip(right.Teams).All(pair =>
            pair.First.TeamId == pair.Second.TeamId
            && pair.First.Eligible == pair.Second.Eligible
            && pair.First.Scores.Length == pair.Second.Scores.Length
            && pair.First.Scores.Zip(pair.Second.Scores).All(scores =>
                string.Equals(
                    scores.First.Channel,
                    scores.Second.Channel,
                    StringComparison.Ordinal)
                && scores.First.Value == scores.Second.Value));

    private static bool StandingsEqual(
        TeamStandings left,
        TeamStandings right) =>
        left.WinnerTeamId == right.WinnerTeamId
        && left.Standings.Length == right.Standings.Length
        && left.Standings.Zip(right.Standings).All(pair =>
            pair.First.TeamId == pair.Second.TeamId
            && pair.First.Rank == pair.Second.Rank
            && pair.First.Outcome == pair.Second.Outcome
            && pair.First.Scores.SequenceEqual(pair.Second.Scores));

    private static bool IsModeOwnedEvent(
        GenericActorAuthoritativeEvent item) =>
        item.Kind is GenericActorRuntimeObservation.EventKind.ScoreChanged
            or GenericActorRuntimeObservation.EventKind.ModeChanged;

    private static ArgumentException Invalid(string reason) =>
        new($"Frontline chronology evidence is invalid: {reason}.", "result");
}
