using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericFrontlineMatchSessionTests
{
    [Fact]
    public void TerminalControlRejectsANegativeClaimingTeam()
    {
        var control =
            new GenericActorRuntimeObservation.ModeObservationState.Frontline(
                FrontlineGameModeDefinition.Id,
                activePositionIndex: 1,
                claimingTeamId: -1,
                captureProgress: 1,
                decayTicksElapsed: 0,
                controlResumesAtTick: 0);
        var scores = new FrontlineScoreState(
            [
                new FrontlineTeamScore(0, 1),
                new FrontlineTeamScore(1, -1),
            ]);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchModeResult.Frontline(
                GenericFrontlineEndReason.MaxTicks,
                control,
                scores));
    }

    [Fact]
    public void SoleControlCanBreachBeforeTheMaximumTick()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 5,
            captureThreshold: 1,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)]);
        using GenericActorMatchSession session = Session(
            definition,
            (start, observation) =>
                start.ParticipantId == 10 && observation.Tick == 1
                    ? GenericDeathmatchSessionTestFixture.Move(Direction.East)
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchResult result = session.Run();
        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                result.Mode);

        Assert.Equal(GenericFrontlineEndReason.BaseBreach, frontline.Reason);
        Assert.Equal("base-breach", result.CompletionReason);
        Assert.Equal(1, result.EndTick);
        Assert.Equal(0, result.WinnerTeamId);
        Assert.Equal(2, frontline.Control.ActivePositionIndex);
        Assert.Equal(2, session.Chronology.Ticks.Length);
    }

    [Fact]
    public void BaseBreachOnFinalTickBeatsTimeout()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 2,
            captureThreshold: 1,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)]);
        using GenericActorMatchSession session = Session(
            definition,
            (start, observation) =>
                start.ParticipantId == 10 && observation.Tick == 1
                    ? GenericDeathmatchSessionTestFixture.Move(Direction.East)
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                session.Run().Mode);

        Assert.Equal(GenericFrontlineEndReason.BaseBreach, frontline.Reason);
        Assert.Equal(0, session.Result!.WinnerTeamId);
        Assert.Equal(1, session.Result.EndTick);
    }

    [Fact]
    public void TimeoutRanksTheSignedTerritorialScore()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 1,
            captureThreshold: 3,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)]);
        using GenericActorMatchSession session = Session(definition);

        GenericActorMatchResult result = session.Run();
        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                result.Mode);

        Assert.Equal(GenericFrontlineEndReason.MaxTicks, frontline.Reason);
        Assert.Equal(0, result.WinnerTeamId);
        Assert.Equal(
            1,
            frontline.Scores.Teams.Single(team => team.TeamId == 0)
                .TerritorialProgress);
        Assert.Equal(
            -1,
            frontline.Scores.Teams.Single(team => team.TeamId == 1)
                .TerritorialProgress);
    }

    [Fact]
    public void ObjectiveWeightZeroTurretIsIgnored()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 1,
            captureThreshold: 3,
            teamZero: [(new Position(3, 3), "turret")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)]);
        using GenericActorMatchSession session = Session(definition);

        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                session.Run().Mode);

        Assert.Null(session.Result!.WinnerTeamId);
        Assert.Null(frontline.Control.ClaimingTeamId);
        Assert.Equal(0, frontline.Control.CaptureProgress);
        Assert.All(
            frontline.Scores.Teams,
            team => Assert.Equal(0, team.TerritorialProgress));
    }

    [Fact]
    public void MultipleBodiesProvideBinaryPresenceWithoutStacking()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 1,
            captureThreshold: 3,
            teamZero:
            [
                (new Position(3, 2), "mobile"),
                (new Position(3, 3), "mobile"),
            ],
            teamOne:
            [
                (new Position(6, 2), "mobile"),
                (new Position(6, 3), "mobile"),
            ],
            lowObjective: [new Position(2, 2), new Position(2, 3)],
            centreObjective: [new Position(3, 2), new Position(3, 3)],
            highObjective: [new Position(4, 2), new Position(4, 3)]);
        using GenericActorMatchSession session = Session(definition);

        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                session.Run().Mode);

        Assert.Equal(0, frontline.Control.ClaimingTeamId);
        Assert.Equal(1, frontline.Control.CaptureProgress);
        Assert.Equal(
            1,
            frontline.Scores.Teams.Single(team => team.TeamId == 0)
                .TerritorialProgress);
    }

    [Fact]
    public void LethallyDamagedBodyDoesNotContributePresence()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 1,
            captureThreshold: 3,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(4, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(5, 3)],
            maxHealth: 1);
        using GenericActorMatchSession session = Session(
            definition,
            (start, _) =>
                start.ParticipantId == 20
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchStepResult step = session.Step(
            session.PrepareTick().Observations);
        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                step.Result!.Mode);

        Assert.Contains(
            step.Events,
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.Destruction);
        Assert.DoesNotContain(
            step.PostState.ActiveLives,
            life => life.ActorId.TeamId == 0);
        Assert.Null(frontline.Control.ClaimingTeamId);
        Assert.Equal(0, frontline.Control.CaptureProgress);
    }

    [Fact]
    public void FaultEligibilityShortCircuitsObjectiveUpdate()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 3,
            captureThreshold: 3,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)]);
        using GenericActorMatchSession session = Session(
            definition,
            (start, _) =>
                start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Unknown()
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchStepResult step = session.Step(
            session.PrepareTick().Observations);
        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                step.Result!.Mode);

        Assert.Equal(
            GenericFrontlineEndReason.FaultEligibility,
            frontline.Reason);
        Assert.Equal(1, step.Result.WinnerTeamId);
        Assert.Null(frontline.Control.ClaimingTeamId);
        Assert.Equal(0, frontline.Control.CaptureProgress);
        Assert.DoesNotContain(
            step.Events,
            item => item.Kind is
                GenericActorRuntimeObservation.EventKind.ScoreChanged
                or GenericActorRuntimeObservation.EventKind.ModeChanged);
    }

    [Fact]
    public void ChronologyRejectsTimeoutReplacingFinalTickBreach()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 2,
            captureThreshold: 1,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)]);
        using GenericActorMatchSession session = Session(
            definition,
            (start, observation) =>
                start.ParticipantId == 10 && observation.Tick == 1
                    ? GenericDeathmatchSessionTestFixture.Move(Direction.East)
                    : GenericDeathmatchSessionTestFixture.Wait());
        session.Run();
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchResult result = chronology.Result!;
        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                result.Mode);
        var forged = new GenericActorMatchResult(
            "max-ticks",
            result.EndTick,
            result.Standings,
            result.EligibleTeamIds,
            result.Units,
            new GenericActorMatchModeResult.Frontline(
                GenericFrontlineEndReason.MaxTicks,
                frontline.Control,
                frontline.Scores));

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                chronology.Ticks,
                forged));
    }

    [Theory]
    [InlineData(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
        1)]
    [InlineData(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            .TickStartAfterDuration,
        2)]
    public void FormPhaseUsesSourceAtEndClockAndTargetAtTickStart(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            completion,
        int maxTicks)
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks,
            captureThreshold: 3,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)],
            includeTransform: true,
            transformCompletion: completion);
        using GenericActorMatchSession session = Session(
            definition,
            (start, observation) =>
                start.ParticipantId == 10 && observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Transform("turret")
                    : GenericDeathmatchSessionTestFixture.Wait());

        GenericActorMatchModeResult.Frontline frontline =
            Assert.IsType<GenericActorMatchModeResult.Frontline>(
                session.Run().Mode);

        Assert.Equal(1, frontline.Control.CaptureProgress);
        Assert.Equal(
            "turret",
            session.Chronology.Ticks[^1].PostState.ActiveLives.Single(
                life => life.ActorId.TeamId == 0).FormId);
    }

    [Theory]
    [InlineData(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
        2,
        false)]
    [InlineData(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            .TickStartAfterDuration,
        1,
        true)]
    public void ChronologyRejectsCompletionAtTheWrongConfiguredBoundary(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            completion,
        int durationTicks,
        bool completionStartsAtTickBoundary)
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 2,
            captureThreshold: 3,
            teamZero: [(new Position(2, 5), "mobile")],
            teamOne: [(new Position(6, 5), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)],
            includeTransform: true,
            transformCompletion: completion,
            transformDurationTicks: durationTicks);
        using GenericActorMatchSession session = Session(
            definition,
            (start, observation) =>
                start.ParticipantId == 10 && observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Transform("turret")
                    : GenericDeathmatchSessionTestFixture.Wait());
        session.Run();
        GenericActorMatchChronology chronology = session.Chronology;

        ImmutableArray<GenericActorMatchTickFrame> forgedTicks =
            MoveCompletionToOppositeBoundary(
                chronology,
                tick: 1,
                completionStartsAtTickBoundary);
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                forgedTicks,
                chronology.Result));

        Assert.Contains(
            "configured completion boundary",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ChronologyRejectsCombatFactAfterFrontlineModeFacts()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 1,
            captureThreshold: 3,
            teamZero:
            [
                (new Position(3, 2), "mobile"),
                (new Position(3, 3), "mobile"),
            ],
            teamOne:
            [
                (new Position(4, 3), "mobile"),
                (new Position(6, 2), "mobile"),
            ],
            lowObjective: [new Position(2, 2), new Position(2, 3)],
            centreObjective: [new Position(3, 2), new Position(3, 3)],
            highObjective: [new Position(4, 2), new Position(4, 3)],
            maxHealth: 1);
        using GenericActorMatchSession session = Session(
            definition,
            (start, _) =>
                start.ParticipantId == 20
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        session.Run();
        GenericActorMatchChronology chronology = session.Chronology;

        ImmutableArray<GenericActorMatchTickFrame> forgedTicks =
            SwapResolutionEventOrder(
                chronology,
                tick: 0,
                item => item.Kind
                    == GenericActorRuntimeObservation.EventKind.Destruction,
                item => item.Kind
                    == GenericActorRuntimeObservation.EventKind.ScoreChanged);
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                forgedTicks,
                chronology.Result));

        Assert.Contains(
            "post-combat, mode-update",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ChronologyRejectsEndClockCompletionBeforeFrontlineModeFacts()
    {
        ActorResolvedMatchDefinition definition = Definition(
            maxTicks: 1,
            captureThreshold: 3,
            teamZero: [(new Position(3, 3), "mobile")],
            teamOne: [(new Position(6, 3), "mobile")],
            lowObjective: [new Position(2, 3)],
            centreObjective: [new Position(3, 3)],
            highObjective: [new Position(4, 3)],
            includeTransform: true);
        using GenericActorMatchSession session = Session(
            definition,
            (start, observation) =>
                start.ParticipantId == 10 && observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Transform("turret")
                    : GenericDeathmatchSessionTestFixture.Wait());
        session.Run();
        GenericActorMatchChronology chronology = session.Chronology;

        ImmutableArray<GenericActorMatchTickFrame> forgedTicks =
            SwapResolutionEventOrder(
                chronology,
                tick: 0,
                item => item.Kind
                    == GenericActorRuntimeObservation.EventKind.ModeChanged,
                item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .FormTransitionCompleted);
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                forgedTicks,
                chronology.Result));

        Assert.Contains(
            "post-combat, mode-update",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static ImmutableArray<GenericActorMatchTickFrame>
        MoveCompletionToOppositeBoundary(
            GenericActorMatchChronology chronology,
            int tick,
            bool completionStartsAtTickBoundary)
    {
        GenericActorMatchTickFrame frame = chronology.Ticks[tick];
        GenericActorAuthoritativeEvent completion =
            (completionStartsAtTickBoundary
                ? frame.TickStart.Events
                : frame.Events)
            .Single(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted);
        GenericActorAuthoritativeEvent[] tickStartEvents =
            completionStartsAtTickBoundary
                ? frame.TickStart.Events
                    .Where(item => !ReferenceEquals(item, completion))
                    .ToArray()
                : [.. frame.TickStart.Events, completion];
        GenericActorAuthoritativeEvent[] resolutionEvents =
            completionStartsAtTickBoundary
                ? [.. frame.Events, completion]
                : frame.Events
                    .Where(item => !ReferenceEquals(item, completion))
                    .ToArray();
        var tickStart = new GenericActorMatchTickStart(
            frame.Tick,
            frame.TickStart.State,
            frame.TickStart.ActiveActorIds,
            frame.TickStart.LifeStarts,
            tickStartEvents,
            frame.TickStart.Traversals);
        var forgedFrame = new GenericActorMatchTickFrame(
            tickStart,
            frame.ActorTurns,
            resolutionEvents,
            frame.Traversals,
            frame.PostState);
        GenericActorMatchTickFrame[] ticks = chronology.Ticks.ToArray();
        ticks[tick] = forgedFrame;
        return ticks.ToImmutableArray();
    }

    private static ImmutableArray<GenericActorMatchTickFrame>
        SwapResolutionEventOrder(
            GenericActorMatchChronology chronology,
            int tick,
            Func<GenericActorAuthoritativeEvent, bool> first,
            Func<GenericActorAuthoritativeEvent, bool> second)
    {
        GenericActorMatchTickFrame frame = chronology.Ticks[tick];
        GenericActorAuthoritativeEvent left = frame.Events.Single(first);
        GenericActorAuthoritativeEvent right = frame.Events.First(second);
        GenericActorAuthoritativeEvent[] events = frame.Events
            .Select(item =>
                ReferenceEquals(item, left)
                    ? Reorder(item, right)
                    : ReferenceEquals(item, right)
                        ? Reorder(item, left)
                        : item)
            .ToArray();
        var forgedFrame = new GenericActorMatchTickFrame(
            frame.TickStart,
            frame.ActorTurns,
            events,
            frame.Traversals,
            frame.PostState);
        GenericActorMatchTickFrame[] ticks = chronology.Ticks.ToArray();
        ticks[tick] = forgedFrame;
        return ticks.ToImmutableArray();
    }

    private static GenericActorAuthoritativeEvent Reorder(
        GenericActorAuthoritativeEvent item,
        GenericActorAuthoritativeEvent order) =>
        new(
            item.EventHandle,
            item.Tick,
            order.Ordinal,
            order.SourceOrdinal,
            item.Kind,
            item.Payload,
            item.EventAudience);

    private static GenericActorMatchSession Session(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision>? decide = null)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                decide);
        return new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 9_001);
    }

    private static ActorResolvedMatchDefinition Definition(
        int maxTicks,
        int captureThreshold,
        IReadOnlyList<(Position Position, string FormId)> teamZero,
        IReadOnlyList<(Position Position, string FormId)> teamOne,
        IReadOnlyList<Position> lowObjective,
        IReadOnlyList<Position> centreObjective,
        IReadOnlyList<Position> highObjective,
        int maxHealth = 3,
        bool includeTransform = false,
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            transformCompletion =
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
        int transformDurationTicks = 1)
    {
        if (teamZero.Count != teamOne.Count)
        {
            throw new ArgumentException(
                "The focused Frontline fixture uses equal team sizes.");
        }

        ActorResolvedMatchDefinition baseline =
            GenericActorContractTestFixture.Frontline();
        var mode = new FrontlineGameModeDefinition(
            new FrontlineVictoryDefinition(
                pushesToBreach: 2,
                [
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress),
            ],
            frontlinePositionCount: 3,
            new FrontlineCaptureDefinition(
                captureThreshold,
                gainPerSoleTeamTick: 1,
                decayAmount: 0,
                decayIntervalTicks: 0,
                redeployPauseTicks: 0));
        ActorFormDefinition sourceMobile = baseline.Rules.Forms.Single();
        var move = new ActorActionDefinition(
            "move",
            1,
            ActorActionKind.Movement,
            [ActorActionParameterKind.Direction]);
        var transform = new ActorActionDefinition(
            "transform",
            101,
            ActorActionKind.SameLifeTransition,
            [ActorActionParameterKind.FormTarget]);
        string[] mobileActions = includeTransform
            ? ["wait", "move", "shoot", "transform"]
            : ["wait", "move", "shoot"];
        var mobile = new ActorFormDefinition(
            "mobile",
            maxHealth,
            sourceMobile.MovementProfileId,
            sourceMobile.VisionProfileId,
            sourceMobile.AttackProfileId,
            objectiveWeight: 1,
            mobileActions);
        var turret = new ActorFormDefinition(
            "turret",
            maxHealth: 5,
            sourceMobile.MovementProfileId,
            sourceMobile.VisionProfileId,
            sourceMobile.AttackProfileId,
            objectiveWeight: 0,
            ["wait", "shoot"]);
        ActorSameLifeTransitionDefinition[] transitions = includeTransform
            ?
            [
                new ActorFormTransitionDefinition(
                    "deploy-turret",
                    transform.Id,
                    mobile.Id,
                    turret.Id,
                    new ActorTransitionWindupDefinition(
                        durationTicks: transformDurationTicks,
                        ActorTransitionWindupDefinition.PendingActionKind
                            .WaitOnly,
                        ActorTransitionWindupDefinition.SourceFormKind
                            .RetainSourceForm,
                        ActorTransitionWindupDefinition.TargetabilityKind
                            .TargetableAndOccupiesTile,
                        ActorTransitionWindupDefinition.LethalDamageKind
                            .CancelTransition,
                        transformCompletion,
                        ActorTransitionWindupDefinition.PlacementReferenceKind
                            .QueueTimePose),
                    ActorSameLifeTransitionDefinition.MemoryContinuityKind
                        .PreservePrivateMemory,
                    new ActorSameLifeHealthDefinition(
                        ActorSameLifeHealthDefinition.HealthPolicyKind
                            .PreserveCurrentCappedToTargetMaximum,
                        flatHealthGain: 0),
                    ActorSameLifeCombatStateDefinition
                        .PreserveWithoutRefillV1,
                    new ActorSameLifePlacementDefinition(
                        ActorSameLifePlacementDefinition
                            .PositionContinuityKind.SameOccupiedGroundTile,
                        ActorSameLifePlacementDefinition
                            .LegalityEvaluationKind
                            .QueueAndCompletionTileTags,
                        requiredTileTags: [],
                        forbiddenTileTags: [],
                        ActorSameLifePlacementDefinition
                            .FailedCompletionKind
                            .CancelAndRemainInSourceForm),
                    irreversibleForLife: true),
            ]
            : [];
        var rules = new ActorRulesDefinition(
            "generic-frontline-session-fixture",
            new ActorRulesLimits(
                maxTicks,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            baseline.Rules.SeedMechanics,
            mode,
            baseline.Rules.Lifecycle,
            [mobile, turret],
            baseline.Rules.MovementProfiles,
            baseline.Rules.VisionProfiles,
            baseline.Rules.AttackProfiles,
            includeTransform
                ? [.. baseline.Rules.Actions, move, transform]
                : [.. baseline.Rules.Actions, move],
            fabricationTransitions: [],
            transitions,
            replicationTransitions: [],
            baseline.Rules.TeamPerception,
            baseline.Rules.Collisions,
            baseline.Rules.TickResolution);

        var teams = ImmutableArray.Create(
            new PublicScoringTeam(0),
            new PublicScoringTeam(1));
        var participants = ImmutableArray.CreateBuilder<PublicParticipant>();
        var slots = ImmutableArray.CreateBuilder<PublicUnitSlot>();
        var initialLives = ImmutableArray.CreateBuilder<PublicInitialLife>();
        var spawnAnchors =
            ImmutableArray.CreateBuilder<ActorMapSpawnAnchorDefinition>();
        var spawns = ImmutableArray.CreateBuilder<InitialSpawnDefinition>();
        var deployments =
            ImmutableArray.CreateBuilder<InitialLifeDeployment>();
        var assignments = new List<
            ActorUnitSlotLifecycleAssignmentDefinition>();
        AddTeam(
            0,
            teamZero,
            Direction.East,
            participantBase: 10,
            participants,
            slots,
            initialLives,
            spawnAnchors,
            spawns,
            deployments,
            assignments);
        AddTeam(
            1,
            teamOne,
            Direction.West,
            participantBase: 20,
            participants,
            slots,
            initialLives,
            spawnAnchors,
            spawns,
            deployments,
            assignments);
        var topology = new PublicMatchTopology
        {
            Teams = teams,
            Participants = participants.ToImmutable(),
            UnitSlots = slots.ToImmutable(),
            InitialLives = initialLives.ToImmutable(),
        };
        var map = new ActorMapDefinition(
            "generic-frontline-session-arena",
            version: 1,
            [
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            ],
            spawnAnchors.ToImmutable(),
            [
                new ActorMapRegionDefinition(
                    "low",
                    ActorMapRegionDefinition.RegionKind.Objective,
                    [.. lowObjective]),
                new ActorMapRegionDefinition(
                    "centre",
                    ActorMapRegionDefinition.RegionKind.Objective,
                    [.. centreObjective]),
                new ActorMapRegionDefinition(
                    "high",
                    ActorMapRegionDefinition.RegionKind.Objective,
                    [.. highObjective]),
            ],
            []);
        var binding = new FrontlineActorModeMapBindingDefinition(
            ["low", "centre", "high"],
            [
                new FrontlineTeamAdvanceDefinition(
                    0,
                    FrontlineTeamAdvanceDefinition
                        .ObjectiveAdvanceDirection.TowardHigherIndex),
                new FrontlineTeamAdvanceDefinition(
                    1,
                    FrontlineTeamAdvanceDefinition
                        .ObjectiveAdvanceDirection.TowardLowerIndex),
            ]);
        MatchFormatDefinition format = teamZero.Count == 1
            ? new HeadToHeadMatchFormatDefinition()
            : new TeamsMatchFormatDefinition(2, teamZero.Count);
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            format,
            topology,
            new InitialDeploymentDefinition(
                spawns.ToImmutable(),
                deployments.ToImmutable()),
            assignments,
            participantRegionAssignments: [],
            binding);
    }

    private static void AddTeam(
        int teamId,
        IReadOnlyList<(Position Position, string FormId)> actors,
        Direction facing,
        int participantBase,
        ImmutableArray<PublicParticipant>.Builder participants,
        ImmutableArray<PublicUnitSlot>.Builder slots,
        ImmutableArray<PublicInitialLife>.Builder initialLives,
        ImmutableArray<ActorMapSpawnAnchorDefinition>.Builder spawnAnchors,
        ImmutableArray<InitialSpawnDefinition>.Builder spawns,
        ImmutableArray<InitialLifeDeployment>.Builder deployments,
        ICollection<ActorUnitSlotLifecycleAssignmentDefinition> assignments)
    {
        for (int unitId = 0; unitId < actors.Count; unitId++)
        {
            int participantId = participantBase + unitId;
            (Position position, string formId) = actors[unitId];
            string spawnId = $"team-{teamId}-unit-{unitId}";
            var spawn = new InitialSpawnDefinition(
                spawnId,
                position,
                facing);
            participants.Add(new PublicParticipant(participantId, teamId));
            slots.Add(new PublicUnitSlot(teamId, unitId, participantId));
            initialLives.Add(
                new PublicInitialLife(teamId, unitId, 0, formId));
            spawnAnchors.Add(
                new ActorMapSpawnAnchorDefinition(
                    spawn,
                    [ActorMovementLayer.Ground]));
            spawns.Add(spawn);
            deployments.Add(
                new InitialLifeDeployment(
                    teamId,
                    unitId,
                    lifeId: 0,
                    formId,
                    spawnId));
            assignments.Add(
                new ActorUnitSlotLifecycleAssignmentDefinition(
                    teamId,
                    unitId,
                    "prime-respawn",
                    initialGeneration: 0,
                    allowedFormIds: ["mobile", "turret"],
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.ActiveAtTickZero,
                    unlockTick: null,
                    assignedRespawnSpawnId: spawnId));
        }
    }
}
