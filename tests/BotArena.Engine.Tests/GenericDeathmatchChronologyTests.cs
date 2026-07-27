namespace BotArena.Engine.Tests;

public sealed class GenericDeathmatchChronologyTests
{
    [Fact]
    public void RecordsFactoryFreePartialAndTerminalChronology()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
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
                factories,
                reverse: true),
            matchSeed: 9_007_199_254_740_993UL);

        GenericActorMatchChronology initial = session.Chronology;

        Assert.True(initial.Partial);
        Assert.Empty(initial.Ticks);
        Assert.Equal(0, initial.InitialFrame.State.NextTick);
        Assert.Equal(
            definition.Topology.InitialLives.Length,
            initial.InitialFrame.LifeStarts.Length);
        Assert.Equal(
            9_007_199_254_740_993UL,
            initial.Descriptor.MatchSeed);
        Assert.Equal(
            definition.Topology.Participants
                .Select(value => value.ParticipantId),
            initial.Descriptor.Participants
                .Select(value => value.ParticipantId));
        Assert.All(
            initial.Descriptor.Participants,
            participant => Assert.StartsWith(
                "fixture-participant-",
                participant.ArtifactHash));

        GenericDeathmatchTickStart prepared = session.PrepareTick();
        GenericDeathmatchStepResult first = session.Step();
        GenericActorMatchChronology partial = session.Chronology;

        Assert.False(first.IsCompleted);
        GenericActorMatchTickFrame firstFrame =
            Assert.Single(partial.Ticks);
        Assert.Equal(0, firstFrame.Tick);
        Assert.Equal(0, firstFrame.TickStart.State.NextTick);
        Assert.Equal(1, firstFrame.PostState.NextTick);
        Assert.Equal(
            firstFrame.TickStart.ActiveActorIds.ToArray(),
            firstFrame.ActorTurns
                .Select(value => value.ActorId)
                .ToArray());
        foreach (GenericActorMatchActorTurn turn in firstFrame.ActorTurns)
        {
            Assert.Same(
                prepared.Observations.Single(observation =>
                    observation.Self.ActorId == turn.ActorId),
                turn.Observation);
            Assert.Equal("wait", turn.SubmittedDecision!.ActionId);
            Assert.Equal(
                "wait",
                turn.ActionResolution.ValidatedAction.ActionId);
        }

        session.PrepareTick();
        GenericDeathmatchStepResult terminal = session.Step();
        GenericActorMatchChronology complete = session.Chronology;

        Assert.True(terminal.IsCompleted);
        Assert.False(complete.Partial);
        Assert.Equal([0, 1], complete.Ticks.Select(value => value.Tick));
        Assert.Equal(1, complete.Result!.EndTick);
        Assert.Equal(
            "max-ticks",
            complete.Result.CompletionReason);
        Assert.IsType<GenericActorMatchModeResult.Deathmatch>(
            complete.Result.Mode);
        Assert.Equal(
            definition.Topology.UnitSlots.Length,
            complete.Result.Units.Length);
        Assert.Equal(
            complete.Ticks[^1].PostState.Slots
                .Select(value => (value.TeamId, value.UnitId)),
            complete.Result.Units
                .Select(value => (value.TeamId, value.UnitId)));
    }

    [Fact]
    public void SplitRecordsReplacementStartsAndExactLineage()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 4,
                    IncludeSplit = true,
                    SplitDurationTicks = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ActorId == new ActorIdentity(0, 0, 0)
                    && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 701);

        session.PrepareTick();
        session.Step();
        session.PrepareTick();
        session.Step();

        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame queued = chronology.Ticks[0];
        GenericActorMatchTickFrame completed = chronology.Ticks[1];
        GenericActorMatchActorTurn sourceTurn =
            queued.ActorTurns.Single(value =>
                value.ActorId == new ActorIdentity(0, 0, 0));

        Assert.Equal("split", sourceTurn.SubmittedDecision!.ActionId);
        Assert.Equal(
            "split",
            sourceTurn.ActionResolution.ValidatedAction.ActionId);
        Assert.Contains(
            queued.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued);

        Assert.Equal(2, completed.TickStart.LifeStarts.Length);
        Assert.All(
            completed.TickStart.LifeStarts,
            start =>
            {
                Assert.Equal(
                    GenericActorRuntimeStart.SpawnReason.Replication,
                    start.Origin.Reason);
                Assert.Equal(
                    new ActorIdentity(0, 0, 0),
                    start.Origin.ParentActorId);
                Assert.Equal(
                    chronology.Descriptor.MatchContractFingerprint,
                    start.MatchContractFingerprint);
            });
        Assert.Contains(
            completed.TickStart.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.LifeRetired);
        Assert.Equal(
            2,
            completed.TickStart.Events.Count(value =>
                value.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned));
        Assert.Equal(
            completed.TickStart.LifeStarts
                .Select(value => value.ActorId),
            completed.TickStart.State.ActiveLives
                .Where(value => value.ActorId.TeamId == 0)
                .Select(value => value.ActorId));

        var incompleteTickStart = new GenericActorMatchTickStart(
            completed.Tick,
            completed.TickStart.State,
            completed.TickStart.ActiveActorIds,
            lifeStarts: [],
            completed.TickStart.Events,
            completed.TickStart.Traversals);
        var incompleteFrame = new GenericActorMatchTickFrame(
            incompleteTickStart,
            completed.ActorTurns,
            completed.Events,
            completed.Traversals,
            completed.PostState);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [queued, incompleteFrame],
                result: null));
    }

    [Fact]
    public void RecordsExactProjectileTraversalAndTerminalCause()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) => observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 702);

        session.PrepareTick();
        session.Step();
        session.PrepareTick();
        session.Step();

        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame launchFrame = chronology.Ticks[0];
        GenericActorMatchTickFrame contactFrame = chronology.Ticks[1];

        Assert.Equal(2, launchFrame.Traversals.Length);
        Assert.All(
            launchFrame.Traversals,
            traversal =>
            {
                Assert.Equal(
                    GenericActorProjectileTraversal.TraversalPhase.Resolution,
                    traversal.Phase);
                Assert.Equal(
                    GenericActorProjectileTraversal.TraversalTrigger
                        .AttackLaunch,
                    traversal.Trigger);
                Assert.NotEmpty(traversal.Path);
                Assert.IsType<
                    GenericActorProjectileTraversal.TerminalDisposition
                        .Retained>(traversal.Terminal);
            });
        Assert.Equal(2, launchFrame.PostState.Projectiles.Length);

        Assert.Equal(2, contactFrame.Traversals.Length);
        Assert.All(
            contactFrame.Traversals,
            traversal =>
            {
                Assert.Equal(
                    GenericActorProjectileTraversal.TraversalTrigger
                        .ScheduledAdvance,
                    traversal.Trigger);
                Assert.NotEmpty(traversal.Path);
                Assert.IsType<
                    GenericActorProjectileTraversal.TerminalDisposition
                        .ActorContact>(traversal.Terminal);
            });
        Assert.Empty(contactFrame.PostState.Projectiles);
        Assert.False(chronology.Partial);

        long[] factOrdinals =
        [
            .. chronology.InitialFrame.Events.Select(value =>
                value.GlobalOrdinal),
            .. chronology.Ticks.SelectMany(frame =>
                frame.TickStart.Events
                    .Select(value => value.GlobalOrdinal)
                    .Concat(frame.TickStart.Traversals.Select(value =>
                        value.GlobalOrdinal))
                    .Order()
                    .Concat(
                        frame.Events
                            .Select(value => value.GlobalOrdinal)
                            .Concat(frame.Traversals.Select(value =>
                                value.GlobalOrdinal))
                            .Order())),
        ];
        Assert.Equal(
            factOrdinals.Order(),
            factOrdinals);
        Assert.Equal(
            factOrdinals.Length,
            factOrdinals.Distinct().Count());
    }
}
