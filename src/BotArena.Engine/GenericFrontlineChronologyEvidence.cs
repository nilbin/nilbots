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
        // The economy is re-derived from the same recorded document as the
        // front, through the SAME kernel the live driver ran: deposits from
        // the declared schedule, wreckage from the recorded destructions,
        // pickup and banking from the recorded post-combat bodies, and
        // purchases from the recorded action resolutions. Nothing is taken
        // from anything the document asserts directly, which is what makes a
        // forged bank, a forged pile, an unaffordable purchase, an
        // over-capped tier, or a pile that outlived its expiry
        // unreconcilable: each produces a different projection than the
        // boundary the document published.
        FrontlineScrapKernel? scrapKernel = gameMode.ScrapEconomy is null
            ? null
            : new FrontlineScrapKernel(
                definition.Topology,
                definition.Map,
                definition.Rules.Forms,
                definition.LifecycleAssignments,
                gameMode.ScrapEconomy);
        FrontlineScrapState? scrap = scrapKernel?.CreateInitialState();
        Dictionary<string, ActorActionKind> actionKinds =
            definition.Rules.Actions.ToDictionary(
                action => action.Id,
                action => action.Kind,
                StringComparer.Ordinal);
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
        // One site, however many regions declare it.
        ImmutableHashSet<Position> secondarySiteTiles =
            gameMode.SecondaryControl is null
                ? []
                : gameMode.SecondaryControl.RegionIds
                    .SelectMany(regionId => regionTiles[regionId])
                    .ToImmutableHashSet();

        ValidateBoundary(
            initialFrame.State,
            control,
            kernel,
            gameMode,
            "initialFrame",
            scrapKernel,
            scrap);
        if (scrapKernel is not null && scrap is not null)
        {
            ValidateSpawnHealth(
                definition,
                initialFrame.Events,
                scrapKernel,
                scrap);
        }
        if (initialFrame.Events.Any(IsModeOwnedEvent))
        {
            throw Invalid(
                "initial facts cannot claim a score or control transition");
        }

        GenericFrontlineEndReason? terminalReason = null;
        // The stillness reading is re-derived from the recorded bodies, not
        // trusted: the previous boundary's positions are the only thing a
        // "did not change tile" claim can mean, so a document that credits
        // gain to a body it also recorded moving cannot reconcile.
        ImmutableDictionary<ActorIdentity, Position> previousPositions =
            PositionsOf(initialFrame.State);
        for (int index = 0; index < ticks.Count; index++)
        {
            GenericActorMatchTickFrame frame = ticks[index];
            ValidateBoundary(
                frame.TickStart.State,
                control,
                kernel,
                gameMode,
                "ticks",
                scrapKernel,
                scrap);
            ValidateObservedModeBoundary(frame);
            FrontlineScrapState? previousScrap = scrap;
            // Lives created during this tick's preparation arrived with the
            // economy as it stood at the END of the previous tick, so the
            // spawn health they claim is checked against exactly that.
            if (scrapKernel is not null && scrap is not null)
            {
                ValidateSpawnHealth(
                    definition,
                    frame.TickStart.Events,
                    scrapKernel,
                    scrap);
            }

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
                if (scrapKernel is not null && scrap is not null)
                {
                    scrap = ApplyRecordedInvestments(
                        scrapKernel,
                        scrap,
                        frame,
                        actionKinds);
                }
                ImmutableHashSet<Position> activeTiles = regionTiles[
                    binding.OrderedObjectiveRegionIds[
                        previous.ActivePositionIndex]];
                control = kernel.ApplyJointTick(
                        previous,
                        frame.Tick,
                        Presence(
                            definition,
                            gameMode,
                            frame,
                            objectiveWeights,
                            activeTiles,
                            previousPositions),
                        // Re-derived from the same recorded bodies as the
                        // front, so a replay cannot claim a latch the rules
                        // would not have produced.
                        secondarySiteTiles.IsEmpty
                            ? ImmutableDictionary<int, int>.Empty
                            : ObjectiveWeightAtModePhase(
                                definition,
                                frame,
                                objectiveWeights,
                                secondarySiteTiles))
                    .State;
                if (scrapKernel is not null && scrap is not null)
                {
                    scrap = scrapKernel.ApplyJointTick(
                        scrap,
                        frame.Tick,
                        ScrapBodiesAtModePhase(definition, frame),
                        RecordedDestructions(frame));
                    if (!scrap.IsConserved())
                    {
                        throw Invalid(
                            "the recorded economy does not conserve scrap: "
                            + "every unit spawned must be banked, spent, "
                            + "carried, on a pile, or evaporated");
                    }
                }
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
                "ticks",
                scrapKernel,
                scrap);
            ValidateModeOwnedEvents(
                definition,
                frame,
                previous,
                control,
                kernel,
                gameMode,
                eligible.Length <= 1,
                scrapKernel,
                previousScrap,
                scrap);

            if (terminalReason is not null && index != ticks.Count - 1)
            {
                throw Invalid(
                    "no frame may follow fault eligibility, base breach, or the maximum-tick boundary");
            }
            previousPositions = PositionsOf(frame.PostState);
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
            expectedPublicControl = ProjectControl(
                gameMode,
                control,
                scrapKernel,
                scrap);
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

    /// <summary>
    /// This tick's objective reading, re-derived from the recorded document.
    /// A non-channel ruleset reads the post-combat objective weight exactly
    /// as it always has. A channeled one additionally re-derives which of
    /// those bodies held their tile — by comparing the previous authoritative
    /// boundary's positions to this one's — and how much hostile damage the
    /// recorded Damage facts put on the active region. Neither is taken from
    /// anything the document could assert directly, which is what makes an
    /// impossible channel history unreconcilable: gain credited to a body the
    /// same document recorded moving, a revert with no damage fact behind it,
    /// or a multiplier past the declared cap all produce a different control
    /// state than the boundary the document published.
    /// </summary>
    private static FrontlineObjectivePresence Presence(
        ActorResolvedMatchDefinition definition,
        FrontlineGameModeDefinition gameMode,
        GenericActorMatchTickFrame frame,
        IReadOnlyDictionary<string, int> objectiveWeights,
        IReadOnlySet<Position> activeTiles,
        IReadOnlyDictionary<ActorIdentity, Position> previousPositions)
    {
        ImmutableDictionary<int, int> denial = ObjectiveWeightAtModePhase(
            definition,
            frame,
            objectiveWeights,
            activeTiles);
        if (gameMode.Capture.ControlPolicy
            != FrontlineCaptureDefinition.ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds)
        {
            return new FrontlineObjectivePresence(denial);
        }

        return new FrontlineObjectivePresence(
            denial,
            ObjectiveWeightAtModePhase(
                definition,
                frame,
                objectiveWeights,
                activeTiles,
                previousPositions),
            frame.Events
                .Where(item =>
                    item.Kind
                    == GenericActorRuntimeObservation.EventKind.Damage)
                .Select(item =>
                    (GenericActorRuntimeObservation.EventPayload.Damage)
                        item.Payload)
                .Where(payload =>
                    payload.Amount > 0
                    && payload.SourceTeamId
                        != payload.TargetActorId.TeamId
                    && activeTiles.Contains(payload.Position))
                .GroupBy(payload => payload.TargetActorId.TeamId)
                .OrderBy(group => group.Key)
                .ToImmutableDictionary(
                    group => group.Key,
                    group => group.Sum(payload => (long)payload.Amount)));
    }

    /// <summary>
    /// Where each recorded life stands at one authoritative boundary.
    /// </summary>
    private static ImmutableDictionary<ActorIdentity, Position> PositionsOf(
        GenericActorWorldSnapshot snapshot) =>
        snapshot.ActiveLives.ToImmutableDictionary(
            life => life.ActorId,
            life => life.Position);

    private static ImmutableDictionary<int, int>
        ObjectiveWeightAtModePhase(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickFrame frame,
        IReadOnlyDictionary<string, int> objectiveWeights,
        IReadOnlySet<Position> activeTiles,
        IReadOnlyDictionary<ActorIdentity, Position>? stationaryAgainst =
            null)
    {
        Dictionary<ActorIdentity, string> endClockSourceForms =
            EndClockSourceForms(definition, frame);

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
                && activeTiles.Contains(item.Life.Position)
                && (stationaryAgainst is null
                    || !stationaryAgainst.TryGetValue(
                        item.Life.ActorId,
                        out Position previous)
                    || previous == item.Life.Position))
            .GroupBy(item => item.Life.ActorId.TeamId)
            .OrderBy(group => group.Key)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.Sum(item => item.Weight));
    }

    /// <summary>
    /// The form each life wore at the MODE phase, where that differs from the
    /// form the post-tick boundary records: an end-clock same-life completion
    /// lands after the mode update, so its life contributed with the source
    /// form.
    /// </summary>
    private static Dictionary<ActorIdentity, string> EndClockSourceForms(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickFrame frame) =>
        frame.Events
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

    private static void ValidateBoundary(
        GenericActorWorldSnapshot state,
        FrontlineControlState control,
        FrontlineModeKernel kernel,
        FrontlineGameModeDefinition gameMode,
        string parameterName,
        FrontlineScrapKernel? scrapKernel,
        FrontlineScrapState? scrap)
    {
        if (state.Mode
                is not GenericActorRuntimeObservation.ModeObservationState
                    .Frontline actualMode
            || !Equals(
                actualMode,
                ProjectControl(gameMode, control, scrapKernel, scrap))
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
        bool modePhaseSkipped,
        FrontlineScrapKernel? scrapKernel = null,
        FrontlineScrapState? previousScrap = null,
        FrontlineScrapState? currentScrap = null)
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
            beforeMode = ProjectControl(
                gameMode,
                previous,
                scrapKernel,
                previousScrap);
        GenericActorRuntimeObservation.ModeObservationState.Frontline
            afterMode = ProjectControl(
                gameMode,
                current,
                scrapKernel,
                currentScrap);
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
            or GenericActorRuntimeObservation.EventKind.MindRuntimeFault
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
            FrontlineControlState control,
            FrontlineScrapKernel? scrapKernel = null,
            FrontlineScrapState? scrap = null) =>
        FrontlineControlProjection.Project(
            gameMode.ModeId,
            control,
            scrapKernel is null || scrap is null
                ? null
                : (scrapKernel, scrap));

    /// <summary>
    /// Re-applies this tick's recorded purchases in canonical order. A
    /// document that records a SUCCESSFUL purchase the ladder would have
    /// refused — unaffordable, capped, or naming a track that does not exist
    /// — cannot reconcile, and neither can one that records a purchase the
    /// bank never paid for, because the re-derived bank is the one the
    /// boundary is compared against.
    /// </summary>
    private static FrontlineScrapState ApplyRecordedInvestments(
        FrontlineScrapKernel scrapKernel,
        FrontlineScrapState scrap,
        GenericActorMatchTickFrame frame,
        IReadOnlyDictionary<string, ActorActionKind> actionKinds)
    {
        FrontlineScrapState current = scrap;
        foreach (GenericActorMatchActorTurn turn in frame.ActorTurns
                     .OrderBy(item => item.ActorId))
        {
            if (turn.ActionResolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome
                        .Success
                || !actionKinds.TryGetValue(
                    turn.ActionResolution.ValidatedAction.ActionId,
                    out ActorActionKind kind)
                || kind != ActorActionKind.ModeInvestment)
            {
                continue;
            }

            string? trackId = turn.ActionResolution.ValidatedAction.Arguments
                .OfType<GenericActorRuntimeActionArgument
                    .UpgradeTrackArgument>()
                .SingleOrDefault()
                ?.TrackId;
            if (trackId is null
                || !scrapKernel.TryInvest(
                    current,
                    turn.ActorId.TeamId,
                    trackId,
                    out FrontlineScrapState bought))
            {
                throw Invalid(
                    "a recorded successful investment names a track the "
                    + "declared ladder would have refused");
            }
            current = bought;
        }
        return current;
    }

    /// <summary>
    /// The bodies the economy phase saw, re-derived from the recorded
    /// document with the same mode-phase form resolution the objective uses:
    /// an end-clock completion contributes with its SOURCE form, because the
    /// completion lands after the mode update.
    /// </summary>
    private static ImmutableArray<FrontlineScrapBody> ScrapBodiesAtModePhase(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickFrame frame)
    {
        Dictionary<ActorIdentity, string> endClockSourceForms =
            EndClockSourceForms(definition, frame);
        return frame.PostState.ActiveLives
            .Select(life => new FrontlineScrapBody(
                life.ActorId,
                endClockSourceForms.GetValueOrDefault(
                    life.ActorId,
                    life.FormId),
                life.Position))
            .ToImmutableArray();
    }

    /// <summary>
    /// This tick's destructions, read from the recorded Destruction facts
    /// rather than from anything the economy state asserts. The death TILE is
    /// the wreck's address, which is why the destruction rather than the
    /// damage contact is the input.
    /// </summary>
    /// <summary>
    /// Every life created at this boundary arrives at its form's declared
    /// maximum health plus EXACTLY the health tier its team held at that
    /// moment — re-derived from the recorded economy rather than read off the
    /// document. The generic chronology checks only the contract-declared
    /// band, because the tier vector is mode state; this is where the exact
    /// number is proven, and it is proven against a bank that was itself
    /// re-derived from recorded pickups, deposits, and purchases.
    /// </summary>
    private static void ValidateSpawnHealth(
        ActorResolvedMatchDefinition definition,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        FrontlineScrapKernel scrapKernel,
        FrontlineScrapState scrap)
    {
        foreach (GenericActorAuthoritativeEvent item in events)
        {
            if (item.Kind
                    != GenericActorRuntimeObservation.EventKind.LifeSpawned
                || item.Payload is not GenericActorRuntimeObservation
                    .EventPayload.LifeSpawned spawned)
            {
                continue;
            }
            ActorFormDefinition form = definition.Rules.Forms.Single(value =>
                string.Equals(
                    value.Id,
                    spawned.FormId,
                    StringComparison.Ordinal));
            int expected = checked(
                form.MaxHealth
                + scrapKernel
                    .ModifiersFor(scrap, spawned.ActorId)
                    .MaxHealthDelta);
            if (spawned.Health != expected)
            {
                throw Invalid(
                    "a life spawned with health the declared form and the "
                    + "re-derived upgrade ladder do not produce");
            }
        }
    }

    private static ImmutableArray<FrontlineScrapDestruction>
        RecordedDestructions(GenericActorMatchTickFrame frame) =>
        frame.Events
            .Where(item =>
                item.Kind
                == GenericActorRuntimeObservation.EventKind.Destruction)
            .Select(item =>
                (GenericActorRuntimeObservation.EventPayload.Destruction)
                    item.Payload)
            .Select(payload => new FrontlineScrapDestruction(
                payload.ActorId,
                payload.Position))
            .OrderBy(item => item.ActorId)
            .ToImmutableArray();

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
