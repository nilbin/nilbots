using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorChronologyRecorderTests
{
    [Fact]
    public void RecorderSnapshotsCanonicalOneTickChronologyAndCompletion()
    {
        Fixture fixture = CreateTerminalFixture();
        var recorder = new InMemoryGenericActorMatchChronologyRecorder();
        recorder.RecordInitial(fixture.Descriptor, fixture.InitialFrame);

        GenericActorMatchChronology partial = recorder.Snapshot;
        Assert.True(partial.Partial);
        Assert.Equal(
            [new ActorIdentity(0, 0, 0), new ActorIdentity(1, 0, 0)],
            partial.InitialFrame.LifeStarts
                .Select(start => start.ActorId)
                .ToArray());
        Assert.Equal(
            [0L, 1L],
            partial.InitialFrame.Events
                .Select(item => item.Ordinal)
                .ToArray());

        GenericActorMatchTickFrame frame = CreateFrame(fixture, tick: 0);
        recorder.RecordResolvedTick(frame);
        Assert.Equal(
            [new ActorIdentity(0, 0, 0), new ActorIdentity(1, 0, 0)],
            recorder.Snapshot.Ticks[0].ActorTurns
                .Select(turn => turn.ActorId)
                .ToArray());
        Assert.Same(
            frame.ActorTurns[0].Observation,
            recorder.Snapshot.Ticks[0].ActorTurns[0].Observation);

        GenericActorMatchResult result =
            CreateResult(fixture, frame.PostState, endTick: 0);
        recorder.RecordCompleted(result);

        GenericActorMatchChronology complete = recorder.Snapshot;
        Assert.False(complete.Partial);
        Assert.Same(result, complete.Result);
        Assert.Equal([0, 1], result.EligibleTeamIds.ToArray());
        Assert.Equal(
            [(0, 0), (1, 0)],
            result.Units.Select(unit =>
                (unit.TeamId, unit.UnitId)).ToArray());
    }

    [Fact]
    public void RecorderEnforcesInitialContiguousTicksAndSingleCompletion()
    {
        Fixture fixture = CreateTerminalFixture();
        var recorder = new InMemoryGenericActorMatchChronologyRecorder();

        Assert.Throws<InvalidOperationException>(() =>
            recorder.RecordResolvedTick(CreateFrame(fixture, tick: 0)));
        Assert.Throws<InvalidOperationException>(() =>
            recorder.RecordCompleted(
                CreateResult(
                    fixture,
                    fixture.InitialFrame.State,
                    endTick: null)));

        recorder.RecordInitial(fixture.Descriptor, fixture.InitialFrame);
        Assert.Throws<InvalidOperationException>(() =>
            recorder.RecordInitial(
                fixture.Descriptor,
                fixture.InitialFrame));
        Assert.Throws<InvalidOperationException>(() =>
            recorder.RecordResolvedTick(CreateFrame(fixture, tick: 1)));

        GenericActorMatchTickFrame tickZero =
            CreateFrame(fixture, tick: 0);
        recorder.RecordResolvedTick(tickZero);
        Assert.Throws<ArgumentException>(() =>
            recorder.RecordCompleted(
                CreateResult(fixture, tickZero.PostState, endTick: null)));

        GenericActorMatchResult result =
            CreateResult(fixture, tickZero.PostState, endTick: 0);
        recorder.RecordCompleted(result);
        Assert.Throws<InvalidOperationException>(() =>
            recorder.RecordCompleted(result));
        Assert.Throws<InvalidOperationException>(() =>
            recorder.RecordResolvedTick(CreateFrame(fixture, tick: 1)));
    }

    [Fact]
    public void ZeroTickDeathmatchCompletionRequiresLegalTerminalEvidence()
    {
        Fixture fixture = CreateFixture();
        var recorder = new InMemoryGenericActorMatchChronologyRecorder();
        recorder.RecordInitial(fixture.Descriptor, fixture.InitialFrame);

        GenericActorMatchResult result = CreateResult(
            fixture,
            fixture.InitialFrame.State,
            endTick: null);

        Assert.Null(result.EndTick);
        Assert.Throws<ArgumentException>(() =>
            recorder.RecordCompleted(result));
        Assert.Null(recorder.Snapshot.Result);
        Assert.Empty(recorder.Snapshot.Ticks);
        Assert.True(recorder.Snapshot.Partial);
    }

    [Fact]
    public void TickContainersKeepTickStartPurgeAndZeroPathMovementContact()
    {
        Fixture fixture = CreateFixture();
        GenericActorWorldSnapshot state = fixture.InitialFrame.State;
        GenericActorWorldSnapshot.LifeSnapshot owner =
            state.ActiveLives[0];
        var purge = new GenericActorProjectileTraversal(
            tick: 0,
            ordinal: 0,
            GenericActorProjectileTraversal.TraversalPhase.TickStart,
            GenericActorProjectileTraversal.TraversalTrigger
                .LifecyclePlacement,
            projectileId: 4,
            owner.ParticipantId,
            owner.ActorId.TeamId,
            owner.ActorId,
            "mobile-bolt",
            owner.Position,
            path: [],
            ProjectileHeading.East,
            ProjectileHeading.East,
            shotProgram: null,
            new GenericActorProjectileTraversal.TerminalDisposition
                .LifecyclePlacementPurge(owner.Position));
        var movementContact = new GenericActorProjectileTraversal(
            tick: 0,
            ordinal: 1,
            GenericActorProjectileTraversal.TraversalPhase.Resolution,
            GenericActorProjectileTraversal.TraversalTrigger.MovementContact,
            projectileId: 5,
            owner.ParticipantId,
            owner.ActorId.TeamId,
            owner.ActorId,
            "mobile-bolt",
            owner.Position,
            path: [],
            ProjectileHeading.East,
            ProjectileHeading.East,
            shotProgram: null,
            new GenericActorProjectileTraversal.TerminalDisposition
                .MovementContact(state.ActiveLives[1].ActorId, true));
        var tickStart = new GenericActorMatchTickStart(
            0,
            state,
            state.ActiveLives.Select(life => life.ActorId).ToArray(),
            lifeStarts: [],
            events: [],
            traversals: [purge]);
        GenericActorMatchActorTurn[] turns =
            CreateTurns(fixture, state, tick: 0);
        var frame = new GenericActorMatchTickFrame(
            tickStart,
            turns,
            events: [],
            traversals: [movementContact],
            World(fixture.Definition, nextTick: 1));

        Assert.IsType<
            GenericActorProjectileTraversal.TerminalDisposition
                .LifecyclePlacementPurge>(
            Assert.Single(frame.TickStart.Traversals).Terminal);
        Assert.Empty(Assert.Single(frame.Traversals).Path);
        Assert.IsType<
            GenericActorProjectileTraversal.TerminalDisposition
                .MovementContact>(
            Assert.Single(frame.Traversals).Terminal);
    }

    [Fact]
    public void ActorTurnRejectsMismatchedObservationIdentityOrTick()
    {
        Fixture fixture = CreateFixture();
        GenericActorWorldSnapshot state = fixture.InitialFrame.State;
        GenericActorWorldSnapshot.LifeSnapshot life =
            state.ActiveLives[0];
        GenericActorRuntimeObservation observation =
            Observation(fixture, state, life, tick: 0);
        GenericActorRuntimeActionResolution resolution = WaitResolution(
            fixture.Definition);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchActorTurn(
                tick: 1,
                life.ParticipantId,
                life.ActorId,
                observation,
                submittedDecision: null,
                resolution));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchActorTurn(
                tick: 0,
                life.ParticipantId,
                state.ActiveLives[1].ActorId,
                observation,
                submittedDecision: null,
                resolution));
    }

    [Fact]
    public void ChronologyValidatesLifeStartContractSeedOwnershipAndLineage()
    {
        Fixture fixture = CreateFixture();
        GenericActorLifeStart valid =
            fixture.InitialFrame.LifeStarts[0];
        ActorIdentity otherActor =
            fixture.InitialFrame.LifeStarts[1].ActorId;

        Assert.Throws<ArgumentException>(() =>
            new GenericActorLifeStart(
                valid.SchemaVersion,
                valid.RuntimeContractVersion,
                valid.ActorId,
                valid.ParticipantId,
                valid.ActorRandomSeed,
                new GenericActorRuntimeStart.LifeOrigin(
                    GenericActorRuntimeStart.SpawnReason.Initial,
                    valid.Origin.Generation,
                    otherActor,
                    SourceTransitionId: null,
                    SourceOperationId: null),
                valid.MatchContractFingerprint));

        GenericActorLifeStart wrongOwner = CopyLifeStart(
            valid,
            participantId:
                fixture.InitialFrame.LifeStarts[1].ParticipantId);
        Assert.Throws<ArgumentException>(() =>
            wrongOwner.ValidateAgainst(fixture.Descriptor));

        GenericActorLifeStart[] invalidStarts =
        [
            CopyLifeStart(valid, schemaVersion: valid.SchemaVersion + 1),
            CopyLifeStart(
                valid,
                runtimeContractVersion:
                    valid.RuntimeContractVersion + 1),
            CopyLifeStart(
                valid,
                actorRandomSeed: valid.ActorRandomSeed ^ 1UL),
            CopyLifeStart(
                valid,
                matchContractFingerprint: "not-the-match-contract"),
        ];
        foreach (GenericActorLifeStart invalid in invalidStarts)
        {
            GenericActorLifeStart[] starts = fixture.InitialFrame.LifeStarts
                .Select(start =>
                    start.ActorId == invalid.ActorId ? invalid : start)
                .ToArray();
            var initial = new GenericActorMatchInitialFrame(
                fixture.InitialFrame.State,
                starts,
                fixture.InitialFrame.Events);

            Assert.Throws<ArgumentException>(() =>
                new GenericActorMatchChronology(
                    fixture.Descriptor,
                    initial,
                    ticks: [],
                    result: null));
        }
    }

    [Fact]
    public void ChronologyValidatesSubmittedSelectorsAndFaultEvidence()
    {
        Fixture fixture = CreateFixture();
        GenericActorMatchTickFrame frame = CreateFrame(fixture, tick: 0);
        GenericActorMatchActorTurn original = frame.ActorTurns[0];

        GenericActorRuntimeActionResolution missingProjection =
            original.ActionResolution with
            {
                SubmittedAction = null,
            };
        GenericActorMatchActorTurn mismatchedSubmission = CopyTurn(
            original,
            actionResolution: missingProjection);
        Assert.Throws<ArgumentException>(() =>
            ChronologyWithFrame(
                fixture,
                ReplaceTurn(frame, mismatchedSubmission)));

        var unknownAction =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                "not-in-catalog",
                9_999,
                []);
        GenericActorMatchActorTurn unknownAccepted = CopyTurn(
            original,
            actionResolution: original.ActionResolution with
            {
                AcceptedAction = unknownAction,
            });
        Assert.Throws<ArgumentException>(() =>
            ChronologyWithFrame(
                fixture,
                ReplaceTurn(frame, unknownAccepted)));

        ActorActionDefinition shoot = fixture.Definition.Rules.Actions
            .Single(action =>
                action.Kind == ActorActionKind.Attack
                && action.ParameterKinds.Contains(
                    ActorActionParameterKind.ShotProgram));
        var firstArgument =
            new GenericActorRuntimeActionArgument.ShotProgramArgument(
                ShotProgram.Straight);
        var secondArgument =
            new GenericActorRuntimeActionArgument.ShotProgramArgument(
                ShotProgram.Straight);
        var submitted = new GenericActorRuntimeDecision(
            shoot.Id,
            shoot.Code,
            ImmutableArray.Create<GenericActorRuntimeActionArgument>(
                firstArgument),
            DebugMessage: null);
        var projected =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                shoot.Id,
                shoot.Code,
                ImmutableArray.Create<GenericActorRuntimeActionArgument>(
                    secondArgument));
        var shootResolution = new GenericActorRuntimeActionResolution(
            projected,
            projected with
            {
                Arguments =
                    ImmutableArray.Create<
                        GenericActorRuntimeActionArgument>(
                        new GenericActorRuntimeActionArgument
                            .ShotProgramArgument(
                                ShotProgram.Straight)),
            },
            projected,
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            RuntimeFault: null);
        GenericActorMatchActorTurn semanticCopy = CopyTurn(
            original,
            submittedDecision: submitted,
            actionResolution: shootResolution);

        ChronologyWithFrame(
            fixture,
            ReplaceTurn(frame, semanticCopy));

        var wrongFault = new GenericActorRuntimeFault(
            original.ParticipantId + 1,
            original.ActorId,
            GenericActorRuntimeFault.FaultStage.TickExecution,
            "runtime-fault",
            CumulativeFaultCount: 1,
            DisqualificationTriggered: false);
        GenericActorRuntimeActionResolution wait =
            WaitResolution(fixture.Definition);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchActorTurn(
                original.Tick,
                original.ParticipantId,
                original.ActorId,
                original.Observation,
                submittedDecision: null,
                wait with
                {
                    SubmittedAction = null,
                    Outcome = GenericActorRuntimeActionResolution
                        .ActionOutcome.Faulted,
                    RuntimeFault = wrongFault,
                }));
    }

    [Fact]
    public void ChronologyMergesAllFactsAndValidatesEventSourceOrdinals()
    {
        Fixture fixture = CreateFixture();
        GenericActorWorldSnapshot state = fixture.InitialFrame.State;
        GenericActorWorldSnapshot.LifeSnapshot owner =
            state.ActiveLives[0];
        GenericActorAuthoritativeEvent startEvent = ScoreEvent(
            "tick-start-score",
            globalOrdinal: 2,
            sourceOrdinal: 2);
        GenericActorProjectileTraversal startTraversal = Traversal(
            owner,
            globalOrdinal: 3,
            GenericActorProjectileTraversal.TraversalPhase.TickStart);
        GenericActorProjectileTraversal resolutionTraversal = Traversal(
            owner,
            globalOrdinal: 4,
            GenericActorProjectileTraversal.TraversalPhase.Resolution);
        GenericActorAuthoritativeEvent resolutionEvent = ScoreEvent(
            "resolution-score",
            globalOrdinal: 5,
            sourceOrdinal: 3);
        GenericActorMatchTickFrame interleaved = FrameWithFacts(
            fixture,
            startEvent,
            startTraversal,
            resolutionEvent,
            resolutionTraversal);

        GenericActorMatchChronology chronology =
            ChronologyWithFrame(fixture, interleaved);
        Assert.Equal(
            [2L, 3L],
            chronology.Ticks[0].TickStart.Events
                .Select(item => item.Ordinal)
                .Concat(chronology.Ticks[0].TickStart.Traversals
                    .Select(item => item.Ordinal))
                .Order()
                .ToArray());

        var shiftedInitial = new GenericActorMatchInitialFrame(
            fixture.InitialFrame.State,
            fixture.InitialFrame.LifeStarts,
            fixture.InitialFrame.Events.Select(item =>
                new GenericActorAuthoritativeEvent(
                    item.EventHandle,
                    item.Tick,
                    item.GlobalOrdinal + 1,
                    item.SourceOrdinal,
                    item.Kind,
                    item.UnredactedPayload,
                    item.EventAudience)).ToArray());
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                fixture.Descriptor,
                shiftedInitial,
                ticks: [],
                result: null));

        GenericActorMatchTickFrame duplicateGlobal = FrameWithFacts(
            fixture,
            startEvent,
            Traversal(
                owner,
                globalOrdinal: 1,
                GenericActorProjectileTraversal.TraversalPhase.TickStart),
            resolutionEvent,
            resolutionTraversal);
        Assert.Throws<ArgumentException>(() =>
            ChronologyWithFrame(fixture, duplicateGlobal));

        GenericActorMatchTickFrame skippedGlobal = FrameWithFacts(
            fixture,
            startEvent,
            startTraversal,
            ScoreEvent(
                "resolution-score-global-gap",
                globalOrdinal: 6,
                sourceOrdinal: 3),
            resolutionTraversal);
        Assert.Throws<ArgumentException>(() =>
            ChronologyWithFrame(fixture, skippedGlobal));

        GenericActorMatchTickFrame skippedSource = FrameWithFacts(
            fixture,
            startEvent,
            startTraversal,
            ScoreEvent(
                "resolution-score-skipped",
                globalOrdinal: 5,
                sourceOrdinal: 4),
            resolutionTraversal);
        Assert.Throws<ArgumentException>(() =>
            ChronologyWithFrame(fixture, skippedSource));
    }

    [Fact]
    public void ChronologyTracksNextLifeIdAcrossEveryWorldBoundary()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    IncludeSplit = true,
                    MaxTicks = 2,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 719);
        GenericActorMatchChronology initial = session.Chronology;

        GenericActorWorldSnapshot invalidInitialState =
            IncrementDormantNextLifeId(
                definition,
                initial.InitialFrame.State);
        var invalidInitial = new GenericActorMatchInitialFrame(
            invalidInitialState,
            initial.InitialFrame.LifeStarts,
            initial.InitialFrame.Events);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                initial.Descriptor,
                invalidInitial,
                ticks: [],
                result: null));

        session.PrepareTick();
        session.Step();
        GenericActorMatchChronology resolved = session.Chronology;
        GenericActorMatchTickFrame original = resolved.Ticks[0];
        GenericActorWorldSnapshot invalidTickStartState =
            IncrementDormantNextLifeId(
                definition,
                original.TickStart.State);
        var invalidTickStart = new GenericActorMatchTickStart(
            original.Tick,
            invalidTickStartState,
            original.TickStart.ActiveActorIds,
            original.TickStart.LifeStarts,
            original.TickStart.Events,
            original.TickStart.Traversals);
        var invalidTickStartFrame = new GenericActorMatchTickFrame(
            invalidTickStart,
            original.ActorTurns,
            original.Events,
            original.Traversals,
            original.PostState);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                resolved.Descriptor,
                resolved.InitialFrame,
                [invalidTickStartFrame],
                result: null));

        GenericActorWorldSnapshot invalidPostState =
            IncrementDormantNextLifeId(
                definition,
                original.PostState);
        var invalidPostFrame = new GenericActorMatchTickFrame(
            original.TickStart,
            original.ActorTurns,
            original.Events,
            original.Traversals,
            invalidPostState);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                resolved.Descriptor,
                resolved.InitialFrame,
                [invalidPostFrame],
                result: null));
    }

    [Fact]
    public void ChronologyValidatesTypedDeathmatchTerminalEvidence()
    {
        Fixture fixture = CreateFixture();
        GenericActorMatchTickFrame frame = CreateFrame(fixture, tick: 0);
        GenericActorMatchResult valid =
            CreateResult(fixture, frame.PostState, endTick: 0);

        var mismatchedReason = new GenericActorMatchResult(
            "kill-limit",
            valid.EndTick,
            valid.Standings,
            valid.EligibleTeamIds,
            valid.Units,
            valid.Mode);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                fixture.Descriptor,
                fixture.InitialFrame,
                [frame],
                mismatchedReason));

        GenericActorMatchModeResult.Deathmatch deathmatch =
            Assert.IsType<GenericActorMatchModeResult.Deathmatch>(
                valid.Mode);
        var mismatchedScores = new DeathmatchScoreState(
            deathmatch.Scores.Teams.Select((score, index) =>
                new DeathmatchTeamScore(
                    score.TeamId,
                    score.Kills + (index == 0 ? 1 : 0),
                    score.Deaths,
                    score.DamageDealt))
                .ToArray());
        var mismatchedCounters = new GenericActorMatchResult(
            valid.CompletionReason,
            valid.EndTick,
            valid.Standings,
            valid.EligibleTeamIds,
            valid.Units,
            new GenericActorMatchModeResult.Deathmatch(
                deathmatch.Reason,
                mismatchedScores));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                fixture.Descriptor,
                fixture.InitialFrame,
                [frame],
                mismatchedCounters));
    }

    [Fact]
    public void ChronologyRejectsEveryForeignContractWorldSnapshot()
    {
        Fixture fixture = CreateFixture();
        ActorResolvedMatchDefinition foreignDefinition =
            WithDifferentCapabilityFingerprint(fixture.Definition);

        GenericActorWorldSnapshot foreignInitialState = CloneWorld(
            foreignDefinition,
            fixture.InitialFrame.State);
        var foreignInitial = new GenericActorMatchInitialFrame(
            foreignInitialState,
            fixture.InitialFrame.LifeStarts,
            fixture.InitialFrame.Events);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                fixture.Descriptor,
                foreignInitial,
                ticks: [],
                result: null));

        GenericActorMatchTickFrame original =
            CreateFrame(fixture, tick: 0);
        GenericActorWorldSnapshot foreignTickStartState = CloneWorld(
            foreignDefinition,
            original.TickStart.State);
        var foreignTickStart = new GenericActorMatchTickStart(
            original.Tick,
            foreignTickStartState,
            original.TickStart.ActiveActorIds,
            original.TickStart.LifeStarts,
            original.TickStart.Events,
            original.TickStart.Traversals);
        var foreignTickStartFrame = new GenericActorMatchTickFrame(
            foreignTickStart,
            original.ActorTurns,
            original.Events,
            original.Traversals,
            original.PostState);
        Assert.Throws<ArgumentException>(() =>
            ChronologyWithFrame(fixture, foreignTickStartFrame));

        GenericActorWorldSnapshot foreignPostState = CloneWorld(
            foreignDefinition,
            original.PostState);
        var foreignPostFrame = new GenericActorMatchTickFrame(
            original.TickStart,
            original.ActorTurns,
            original.Events,
            original.Traversals,
            foreignPostState);
        Assert.Throws<ArgumentException>(() =>
            ChronologyWithFrame(fixture, foreignPostFrame));
    }

    [Fact]
    public void ChronologyRejectsInitialFactsThatDivergeFromDeployment()
    {
        Fixture fixture = CreateFixture(
            GenericActorContractTestFixture.WithTransitions());
        GenericActorWorldSnapshot.LifeSnapshot original =
            fixture.InitialFrame.State.ActiveLives[0];
        GenericActorLifeStart originalStart =
            fixture.InitialFrame.LifeStarts.Single(start =>
                start.ActorId == original.ActorId);
        ActorFormDefinition child = fixture.Definition.Rules.Forms
            .Single(form => string.Equals(
                form.Id,
                "child",
                StringComparison.Ordinal));

        GenericActorWorldSnapshot.LifeSnapshot Life(
            int? generation = null,
            string? formId = null,
            Position? position = null,
            Direction? facing = null,
            int? health = null,
            int? cooldown = null,
            int? energy = null,
            GenericActorRuntimeActionResolution? previous = null,
            GenericActorRuntimeObservation.PendingSameLifeTransition?
                pending = null,
            GenericActorRuntimeStart.SpawnReason? spawnReason = null,
            ActorIdentity? parentActorId = null,
            string? sourceTransitionId = null,
            string? sourceOperationId = null) =>
            new(
                original.ActorId,
                original.ParticipantId,
                generation ?? original.Generation,
                formId ?? original.FormId,
                position ?? original.Position,
                facing ?? original.Facing,
                health ?? original.Health,
                cooldown ?? original.Cooldown,
                energy ?? original.Energy,
                original.SpawnedAtTick,
                spawnReason ?? original.SpawnReason,
                parentActorId,
                sourceTransitionId,
                sourceOperationId,
                previous,
                pending);

        void Reject(GenericActorWorldSnapshot.LifeSnapshot replacement)
        {
            GenericActorLifeStart replacementStart =
                new GenericActorLifeStart(
                    originalStart.SchemaVersion,
                    originalStart.RuntimeContractVersion,
                    replacement.ActorId,
                    replacement.ParticipantId,
                    originalStart.ActorRandomSeed,
                    new GenericActorRuntimeStart.LifeOrigin(
                        replacement.SpawnReason,
                        replacement.Generation,
                        replacement.ParentActorId,
                        replacement.SourceTransitionId,
                        replacement.SourceOperationId),
                    originalStart.MatchContractFingerprint);
            GenericActorWorldSnapshot.SlotSnapshot slot =
                fixture.InitialFrame.State.Slots.Single(value =>
                    value.TeamId == replacement.ActorId.TeamId
                    && value.UnitId == replacement.ActorId.UnitId);
            var replacementSlot =
                new GenericActorWorldSnapshot.SlotSnapshot(
                    slot.TeamId,
                    slot.UnitId,
                    slot.ParticipantId,
                    slot.NextLifeId,
                    new GenericActorRuntimeObservation.UnitSlotState.Active(
                        replacement.ActorId,
                        replacement.Generation,
                        replacement.FormId),
                    slot.PendingParentActorId,
                    slot.SplitReservation);
            GenericActorWorldSnapshot state = CloneWorld(
                fixture.Definition,
                fixture.InitialFrame.State,
                slots: fixture.InitialFrame.State.Slots
                    .Select(value =>
                        value.TeamId == replacementSlot.TeamId
                        && value.UnitId == replacementSlot.UnitId
                            ? replacementSlot
                            : value)
                    .ToArray(),
                lives: fixture.InitialFrame.State.ActiveLives
                    .Select(value => value.ActorId == replacement.ActorId
                        ? replacement
                        : value)
                    .ToArray());
            var initial = new GenericActorMatchInitialFrame(
                state,
                fixture.InitialFrame.LifeStarts
                    .Select(value => value.ActorId == replacement.ActorId
                        ? replacementStart
                        : value)
                    .ToArray(),
                fixture.InitialFrame.Events);

            Assert.Throws<ArgumentException>(() =>
                new GenericActorMatchChronology(
                    fixture.Descriptor,
                    initial,
                    ticks: [],
                    result: null));
        }

        Reject(Life(position: new Position(
            original.Position.X,
            original.Position.Y - 1)));
        Reject(Life(facing: Direction.North));
        Reject(Life(
            formId: child.Id,
            health: child.MaxHealth));
        Reject(Life(generation: original.Generation + 1));
        Reject(Life(health: original.Health - 1));
        Reject(Life(cooldown: 1));
        Reject(Life(energy: original.Energy!.Value - 1));
        Reject(Life(previous: WaitResolution(fixture.Definition)));
        Reject(Life(
            generation: original.Generation + 1,
            spawnReason:
                GenericActorRuntimeStart.SpawnReason.Fabrication,
            parentActorId: new ActorIdentity(
                original.ActorId.TeamId,
                unitId: 1,
                lifeId: 0),
            sourceTransitionId: "fabricate-child",
            sourceOperationId: "fabrication-0"));
    }

    [Fact]
    public void DynamicLineageRequiresIssuedSameControllerParentAndGeneration()
    {
        Fixture fixture = CreateFixture();
        GenericActorLifeStart parent =
            fixture.InitialFrame.LifeStarts[0];
        var childActorId = new ActorIdentity(
            parent.ActorId.TeamId,
            parent.ActorId.UnitId + 1,
            lifeId: 0);
        var child = new GenericActorLifeStart(
            parent.SchemaVersion,
            parent.RuntimeContractVersion,
            childActorId,
            parent.ParticipantId,
            actorRandomSeed: 0,
            new GenericActorRuntimeStart.LifeOrigin(
                GenericActorRuntimeStart.SpawnReason.Fabrication,
                parent.Origin.Generation + 1,
                parent.ActorId,
                "fabricate-child",
                "fabrication-0"),
            parent.MatchContractFingerprint);

        child.ValidateDynamicLineage(parent);
        Assert.Throws<ArgumentException>(() =>
            child.ValidateDynamicLineage(issuedParent: null));
        Assert.Throws<ArgumentException>(() =>
            child.ValidateDynamicLineage(CopyLifeStart(
                parent,
                participantId: parent.ParticipantId + 1)));
        GenericActorLifeStart wrongChildGeneration = CopyLifeStart(
            child,
            origin: child.Origin with
            {
                Generation = parent.Origin.Generation,
            });
        Assert.Throws<ArgumentException>(() =>
            wrongChildGeneration.ValidateDynamicLineage(parent));

        var returnActorId = new ActorIdentity(
            parent.ActorId.TeamId,
            parent.ActorId.UnitId,
            parent.ActorId.LifeId + 1);
        var validReturn = new GenericActorLifeStart(
            parent.SchemaVersion,
            parent.RuntimeContractVersion,
            returnActorId,
            parent.ParticipantId,
            actorRandomSeed: 0,
            new GenericActorRuntimeStart.LifeOrigin(
                GenericActorRuntimeStart.SpawnReason.AutomaticReturn,
                parent.Origin.Generation,
                parent.ActorId,
                SourceTransitionId: null,
                SourceOperationId: null),
            parent.MatchContractFingerprint);
        validReturn.ValidateDynamicLineage(parent);
        GenericActorLifeStart changedReturnGeneration = CopyLifeStart(
            validReturn,
            origin: validReturn.Origin with
            {
                Generation = parent.Origin.Generation + 1,
            });
        Assert.Throws<ArgumentException>(() =>
            changedReturnGeneration.ValidateDynamicLineage(parent));
    }

    private static GenericActorLifeStart CopyLifeStart(
        GenericActorLifeStart source,
        int? schemaVersion = null,
        int? runtimeContractVersion = null,
        int? participantId = null,
        ulong? actorRandomSeed = null,
        GenericActorRuntimeStart.LifeOrigin? origin = null,
        string? matchContractFingerprint = null) =>
        new(
            schemaVersion ?? source.SchemaVersion,
            runtimeContractVersion ?? source.RuntimeContractVersion,
            source.ActorId,
            participantId ?? source.ParticipantId,
            actorRandomSeed ?? source.ActorRandomSeed,
            origin ?? source.Origin,
            matchContractFingerprint
                ?? source.MatchContractFingerprint);

    private static GenericActorMatchActorTurn CopyTurn(
        GenericActorMatchActorTurn source,
        GenericActorRuntimeActionResolution actionResolution) =>
        new(
            source.Tick,
            source.ParticipantId,
            source.ActorId,
            source.Observation,
            source.SubmittedDecision,
            actionResolution);

    private static GenericActorMatchActorTurn CopyTurn(
        GenericActorMatchActorTurn source,
        GenericActorRuntimeDecision submittedDecision,
        GenericActorRuntimeActionResolution actionResolution) =>
        new(
            source.Tick,
            source.ParticipantId,
            source.ActorId,
            source.Observation,
            submittedDecision,
            actionResolution);

    private static GenericActorMatchTickFrame ReplaceTurn(
        GenericActorMatchTickFrame frame,
        GenericActorMatchActorTurn replacement) =>
        new(
            frame.TickStart,
            frame.ActorTurns.Select(turn =>
                turn.ActorId == replacement.ActorId
                    ? replacement
                    : turn).ToArray(),
            frame.Events,
            frame.Traversals,
            frame.PostState);

    private static GenericActorMatchChronology ChronologyWithFrame(
        Fixture fixture,
        GenericActorMatchTickFrame frame) =>
        new(
            fixture.Descriptor,
            fixture.InitialFrame,
            [frame],
            result: null);

    private static GenericActorAuthoritativeEvent ScoreEvent(
        string handle,
        long globalOrdinal,
        int sourceOrdinal) =>
        new(
            handle,
            tick: 0,
            globalOrdinal,
            sourceOrdinal,
            GenericActorRuntimeObservation.EventKind.ScoreChanged,
            new GenericActorRuntimeObservation.EventPayload.ScoreChanged(
                TeamId: 0,
                Channel: "kills",
                NewValue: 0),
            new GenericActorAuthoritativeEvent.Audience.Public());

    private static GenericActorProjectileTraversal Traversal(
        GenericActorWorldSnapshot.LifeSnapshot owner,
        long globalOrdinal,
        GenericActorProjectileTraversal.TraversalPhase phase)
    {
        bool tickStart = phase
            == GenericActorProjectileTraversal.TraversalPhase.TickStart;
        return new GenericActorProjectileTraversal(
            tick: 0,
            globalOrdinal,
            phase,
            tickStart
                ? GenericActorProjectileTraversal.TraversalTrigger
                    .LifecyclePlacement
                : GenericActorProjectileTraversal.TraversalTrigger
                    .ScheduledAdvance,
            projectileId: globalOrdinal,
            owner.ParticipantId,
            owner.ActorId.TeamId,
            owner.ActorId,
            "mobile-bolt",
            owner.Position,
            path: [],
            ProjectileHeading.East,
            ProjectileHeading.East,
            shotProgram: null,
            tickStart
                ? new GenericActorProjectileTraversal.TerminalDisposition
                    .LifecyclePlacementPurge(owner.Position)
                : new GenericActorProjectileTraversal.TerminalDisposition
                    .WallOrPathExhausted());
    }

    private static GenericActorMatchTickFrame FrameWithFacts(
        Fixture fixture,
        GenericActorAuthoritativeEvent startEvent,
        GenericActorProjectileTraversal startTraversal,
        GenericActorAuthoritativeEvent resolutionEvent,
        GenericActorProjectileTraversal resolutionTraversal)
    {
        GenericActorWorldSnapshot state = fixture.InitialFrame.State;
        var tickStart = new GenericActorMatchTickStart(
            tick: 0,
            state,
            state.ActiveLives.Select(life => life.ActorId).ToArray(),
            lifeStarts: [],
            events: [startEvent],
            traversals: [startTraversal]);
        return new GenericActorMatchTickFrame(
            tickStart,
            CreateTurns(fixture, state, tick: 0),
            events: [resolutionEvent],
            traversals: [resolutionTraversal],
            World(fixture.Definition, nextTick: 1));
    }

    private static ActorResolvedMatchDefinition
        WithDifferentCapabilityFingerprint(
            ActorResolvedMatchDefinition source)
    {
        ActorMatchCapabilityVersions versions =
            source.CapabilityVersions;
        var changedVersions = new ActorMatchCapabilityVersions(
            $"{versions.ContractProfileId}-foreign",
            versions.RuntimeProtocolVersion,
            versions.RuntimeConfigurationVersion,
            versions.RuntimeContractVersion,
            versions.MatchStartSchemaVersion,
            versions.ObservationSchemaVersion,
            versions.DecisionSchemaVersion,
            versions.MatchContractSchemaVersion);
        return new ActorResolvedMatchDefinition(
            source.Rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            changedVersions);
    }

    private static GenericActorWorldSnapshot CloneWorld(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot source,
        IReadOnlyCollection<GenericActorWorldSnapshot.SlotSnapshot>?
            slots = null,
        IReadOnlyCollection<GenericActorWorldSnapshot.LifeSnapshot>?
            lives = null) =>
        new(
            definition,
            source.NextTick,
            source.NextProjectileId,
            source.Participants,
            slots ?? source.Slots,
            lives ?? source.ActiveLives,
            source.PendingReplications,
            source.Projectiles,
            source.Scoreboard,
            source.Mode);

    private static GenericActorWorldSnapshot IncrementDormantNextLifeId(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot source)
    {
        GenericActorWorldSnapshot.SlotSnapshot target = source.Slots
            .First(slot => slot.State is not
                GenericActorRuntimeObservation.UnitSlotState.Active);
        var replacement = new GenericActorWorldSnapshot.SlotSnapshot(
            target.TeamId,
            target.UnitId,
            target.ParticipantId,
            checked(target.NextLifeId + 1),
            target.State,
            target.PendingParentActorId,
            target.SplitReservation);
        return new GenericActorWorldSnapshot(
            definition,
            source.NextTick,
            source.NextProjectileId,
            source.Participants,
            source.Slots.Select(slot =>
                slot.TeamId == replacement.TeamId
                && slot.UnitId == replacement.UnitId
                    ? replacement
                    : slot).ToArray(),
            source.ActiveLives,
            source.PendingReplications,
            source.Projectiles,
            source.Scoreboard,
            source.Mode);
    }

    private static Fixture CreateFixture(
        ActorResolvedMatchDefinition? resolvedDefinition = null)
    {
        ActorResolvedMatchDefinition definition =
            resolvedDefinition
            ?? GenericActorContractTestFixture.Deathmatch("head-to-head");
        GenericActorParticipantProvenance[] participants = definition
            .Topology.Participants
            .Select(participant =>
                new GenericActorParticipantProvenance(
                    participant.ParticipantId,
                    participant.TeamId,
                    $"bot-{participant.ParticipantId}",
                    "test-runtime",
                    $"artifact-{participant.ParticipantId}",
                    participant.TeamId == 0 ? "#38bdf8" : "#f97316",
                    lookId: null,
                    projectileLookId: null))
            .Reverse()
            .ToArray();
        var descriptor = new GenericActorMatchDescriptor(
            definition,
            matchSeed: 73,
            participants);
        GenericActorWorldSnapshot state = World(definition, nextTick: 0);
        GenericActorLifeStart[] starts = state.ActiveLives
            .Select(life =>
                new GenericActorLifeStart(
                    definition.CapabilityVersions.MatchStartSchemaVersion,
                    definition.CapabilityVersions.RuntimeContractVersion,
                    life.ActorId,
                    life.ParticipantId,
                    actorRandomSeed: SeedDerivation.DeriveActorSeed(
                        descriptor.MatchSeed,
                        life.ActorId,
                        definition.Rules.SeedMechanics.SeedProfileId),
                    new GenericActorRuntimeStart.LifeOrigin(
                        life.SpawnReason,
                        life.Generation,
                        life.ParentActorId,
                        life.SourceTransitionId,
                        life.SourceOperationId),
                    descriptor.MatchContractFingerprint))
            .Reverse()
            .ToArray();
        GenericActorAuthoritativeEvent[] events = state.ActiveLives
            .Select((life, index) =>
                new GenericActorAuthoritativeEvent(
                    $"initial:{index}",
                    tick: 0,
                    globalOrdinal: index,
                    GenericActorRuntimeObservation.EventKind.LifeSpawned,
                    new GenericActorRuntimeObservation.EventPayload
                        .LifeSpawned(
                            life.ActorId,
                            life.ParticipantId,
                            life.ParentActorId,
                            life.Generation,
                            life.FormId,
                            life.Health,
                            life.Position,
                            life.SpawnReason,
                            life.SourceTransitionId,
                            life.SourceOperationId),
                    new GenericActorAuthoritativeEvent.Audience.Spatial(
                        life.Position)))
            .Reverse()
            .ToArray();
        var initial = new GenericActorMatchInitialFrame(
            state,
            starts,
            events);
        return new Fixture(definition, descriptor, initial);
    }

    private static Fixture CreateTerminalFixture() =>
        CreateFixture(
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 1,
                }));

    private static GenericActorMatchTickFrame CreateFrame(
        Fixture fixture,
        int tick)
    {
        GenericActorWorldSnapshot preState =
            World(fixture.Definition, nextTick: tick);
        var tickStart = new GenericActorMatchTickStart(
            tick,
            preState,
            preState.ActiveLives
                .Select(life => life.ActorId)
                .Reverse()
                .ToArray(),
            lifeStarts: [],
            events: [],
            traversals: []);
        var turns = CreateTurns(fixture, preState, tick)
            .Reverse()
            .ToList();
        var events = new List<GenericActorAuthoritativeEvent>();
        var traversals = new List<GenericActorProjectileTraversal>();
        var frame = new GenericActorMatchTickFrame(
            tickStart,
            turns,
            events,
            traversals,
            World(fixture.Definition, nextTick: checked(tick + 1)));

        turns.Clear();
        events.Clear();
        traversals.Clear();
        return frame;
    }

    private static GenericActorMatchActorTurn[] CreateTurns(
        Fixture fixture,
        GenericActorWorldSnapshot state,
        int tick)
    {
        ActorActionDefinition wait = fixture.Definition.Rules.Actions
            .Single(action => action.Kind == ActorActionKind.Wait);
        GenericActorRuntimeActionResolution resolution =
            WaitResolution(fixture.Definition);
        return state.ActiveLives.Select(life =>
        {
            GenericActorRuntimeObservation observation =
                Observation(fixture, state, life, tick);
            var submitted = new GenericActorRuntimeDecision(
                wait.Id,
                wait.Code,
                [],
                DebugMessage: null);
            return new GenericActorMatchActorTurn(
                tick,
                life.ParticipantId,
                life.ActorId,
                observation,
                submitted,
                resolution);
        }).ToArray();
    }

    private static GenericActorRuntimeObservation Observation(
        Fixture fixture,
        GenericActorWorldSnapshot state,
        GenericActorWorldSnapshot.LifeSnapshot life,
        int tick) =>
        new(
            fixture.Definition.CapabilityVersions.ObservationSchemaVersion,
            tick,
            fixture.Descriptor.MatchContractFingerprint,
            new GenericActorRuntimeObservation.ObservedSelfState(
                life.ActorId,
                life.Generation,
                life.FormId,
                life.Position,
                life.Facing,
                life.Health,
                life.Cooldown,
                life.Energy,
                life.PreviousActionResolution,
                life.PendingSameLifeTransition),
            TeamUnits: [],
            state.Participants,
            Allies: [],
            Enemies: [],
            VisibleTiles: [],
            VisibleProjectiles: [],
            VisibleEvents: [],
            HeardSounds: null,
            state.Scoreboard,
            state.Mode,
            ActionLegalities: []);

    private static GenericActorRuntimeActionResolution WaitResolution(
        ActorResolvedMatchDefinition definition)
    {
        ActorActionDefinition wait = definition.Rules.Actions
            .Single(action => action.Kind == ActorActionKind.Wait);
        var action =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                wait.Id,
                wait.Code,
                []);
        return new GenericActorRuntimeActionResolution(
            action,
            action,
            action,
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            RuntimeFault: null);
    }

    private static GenericActorWorldSnapshot World(
        ActorResolvedMatchDefinition definition,
        int nextTick)
    {
        Dictionary<string, InitialSpawnDefinition> spawns =
            definition.InitialDeployment.Spawns.ToDictionary(
                spawn => spawn.SpawnId,
                StringComparer.Ordinal);
        Dictionary<int, int> participantTeams =
            definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant => participant.TeamId);
        Dictionary<(int TeamId, int UnitId), int> controllers =
            definition.Topology.UnitSlots.ToDictionary(
                slot => (slot.TeamId, slot.UnitId),
                slot => slot.ControllerParticipantId);
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(
                form => form.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorAttackProfileDefinition> attacks =
            definition.Rules.AttackProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);
        Dictionary<(int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            definition.LifecycleAssignments.ToDictionary(
                assignment => (assignment.TeamId, assignment.UnitId));

        GenericActorWorldSnapshot.LifeSnapshot[] lives = definition
            .InitialDeployment.Lives
            .Select(deployment =>
            {
                ActorIdentity actorId = new(
                    deployment.TeamId,
                    deployment.UnitId,
                    deployment.LifeId);
                ActorFormDefinition form = forms[deployment.FormId];
                InitialSpawnDefinition spawn = spawns[deployment.SpawnId];
                int participantId = controllers[
                    (deployment.TeamId, deployment.UnitId)];
                int generation = assignments[
                    (deployment.TeamId, deployment.UnitId)]
                    .InitialGeneration!.Value;
                int? energy = form.AttackProfileId is string attackId
                    && attacks[attackId].MaxEnergy > 0
                        ? attacks[attackId].MaxEnergy
                        : null;
                return new GenericActorWorldSnapshot.LifeSnapshot(
                    actorId,
                    participantId,
                    generation,
                    deployment.FormId,
                    spawn.Position,
                    spawn.Facing,
                    form.MaxHealth,
                    cooldown: 0,
                    energy,
                    spawnedAtTick: 0,
                    GenericActorRuntimeStart.SpawnReason.Initial,
                    parentActorId: null,
                    sourceTransitionId: null,
                    sourceOperationId: null,
                    previousActionResolution: null,
                    pendingSameLifeTransition: null);
            })
            .ToArray();
        Dictionary<(int TeamId, int UnitId),
            GenericActorWorldSnapshot.LifeSnapshot> livesBySlot =
            lives.ToDictionary(
                life => (life.ActorId.TeamId, life.ActorId.UnitId));
        GenericActorWorldSnapshot.SlotSnapshot[] slots = definition
            .Topology.UnitSlots
            .Select(slot =>
            {
                if (livesBySlot.TryGetValue(
                        (slot.TeamId, slot.UnitId),
                        out GenericActorWorldSnapshot.LifeSnapshot? life))
                {
                    return new GenericActorWorldSnapshot.SlotSnapshot(
                        slot.TeamId,
                        slot.UnitId,
                        slot.ControllerParticipantId,
                        nextLifeId: life.ActorId.LifeId + 1,
                        new GenericActorRuntimeObservation.UnitSlotState.Active(
                            life.ActorId,
                            life.Generation,
                            life.FormId),
                        pendingParentActorId: null,
                        splitReservation: null);
                }

                ActorUnitSlotLifecycleAssignmentDefinition assignment =
                    assignments[(slot.TeamId, slot.UnitId)];
                GenericActorRuntimeObservation.UnitSlotState state =
                    assignment.UnlockTick is int unlockTick
                    && unlockTick >= nextTick
                        ? new GenericActorRuntimeObservation.UnitSlotState
                            .AvailabilityPending(
                                GenericActorRuntimeObservation
                                    .AvailabilityReason.InitialUnlock,
                                unlockTick)
                        : new GenericActorRuntimeObservation.UnitSlotState
                            .Ready();
                return new GenericActorWorldSnapshot.SlotSnapshot(
                    slot.TeamId,
                    slot.UnitId,
                    slot.ControllerParticipantId,
                    nextLifeId: 0,
                    state,
                    pendingParentActorId: null,
                    splitReservation: null);
            })
            .ToArray();
        GenericActorRuntimeObservation.ObservedParticipantStatus[]
            statuses = participantTeams
                .Select(pair =>
                    new GenericActorRuntimeObservation
                        .ObservedParticipantStatus(
                            pair.Key,
                            pair.Value,
                            RuntimeFaultCount: 0,
                            Disqualified: false))
                .ToArray();
        ImmutableArray<string> channels = definition.Rules.GameMode
            .ScoreCatalog
            .Select(channel =>
                ActorContractCanonicalIds.Id(channel.Channel))
            .ToImmutableArray();
        Dictionary<int, long> activeHealth = definition.Topology.Teams
            .ToDictionary(
                team => team.TeamId,
                team => lives
                    .Where(life => life.ActorId.TeamId == team.TeamId)
                    .Sum(life => (long)life.Health));
        string activeHealthChannel = ActorContractCanonicalIds.Id(
            ScoreChannelDefinition.ChannelKind.ActiveHealth);
        var scoreboard =
            new GenericActorRuntimeObservation.ScoreboardState(
                definition.Topology.Teams
                    .Select(team =>
                        new GenericActorRuntimeObservation.TeamScoreState(
                            team.TeamId,
                            Eligible: true,
                            channels
                                .Select(channel =>
                                    new GenericActorRuntimeObservation
                                        .ScoreValue(
                                            channel,
                                            string.Equals(
                                                channel,
                                                activeHealthChannel,
                                                StringComparison.Ordinal)
                                                ? activeHealth[team.TeamId]
                                                : 0))
                                .ToImmutableArray()))
                    .ToImmutableArray());
        var mode =
            new GenericActorRuntimeObservation.ModeObservationState
                .Deathmatch(definition.Rules.GameMode.ModeId);

        return new GenericActorWorldSnapshot(
            definition,
            nextTick,
            nextProjectileId: 0,
            statuses,
            slots,
            lives,
            pendingReplications: [],
            projectiles: [],
            scoreboard,
            mode);
    }

    private static GenericActorMatchResult CreateResult(
        Fixture fixture,
        GenericActorWorldSnapshot state,
        int? endTick)
    {
        var scores = new DeathmatchScoreState(
            fixture.Definition.Topology.Teams
                .Select(team =>
                    new DeathmatchTeamScore(
                        team.TeamId,
                        kills: 0,
                        deaths: 0,
                        damageDealt: 0))
                .ToArray());
        var kernel = new DeathmatchModeKernel(
            fixture.Definition.Topology,
            Assert.IsType<DeathmatchGameModeDefinition>(
                fixture.Definition.Rules.GameMode));
        TeamStandings standings = kernel.ResolveTimeoutStandings(
            scores,
            fixture.Definition.Topology.Teams.ToDictionary(
                team => team.TeamId,
                team => state.ActiveLives
                    .Where(life =>
                        life.ActorId.TeamId == team.TeamId)
                    .Sum(life => (long)life.Health)),
            state.Scoreboard.Teams
                .Where(team => team.Eligible)
                .Select(team => team.TeamId)
                .ToArray());
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            lives = state.ActiveLives.ToDictionary(
                life => life.ActorId);
        GenericActorMatchResult.UnitTerminalFact[] units = state.Slots
            .Select(slot =>
                new GenericActorMatchResult.UnitTerminalFact(
                    slot,
                    slot.State is
                        GenericActorRuntimeObservation.UnitSlotState.Active
                            active
                        ? lives[active.ActorId]
                        : null))
            .Reverse()
            .ToArray();
        return new GenericActorMatchResult(
            "max-ticks",
            endTick,
            standings,
            eligibleTeamIds: [1, 0],
            units,
            new GenericActorMatchModeResult.Deathmatch(
                GenericDeathmatchEndReason.MaxTicks,
                scores));
    }

    private sealed record Fixture(
        ActorResolvedMatchDefinition Definition,
        GenericActorMatchDescriptor Descriptor,
        GenericActorMatchInitialFrame InitialFrame);
}
