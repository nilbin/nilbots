using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorFabricationSessionTests
{
    [Fact]
    public void QueuePendingDue_CreatesFreshChildThatActsOnCreationTick()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId != 10)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    if (start.Origin.Reason
                        == GenericActorRuntimeStart.SpawnReason.Fabrication)
                    {
                        return GenericDeathmatchSessionTestFixture.Shoot();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        2 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.West),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 4_001);

        session.Step(session.PrepareTick().Observations);
        GenericActorMatchStepResult queued =
            session.Step(session.PrepareTick().Observations);

        GenericActorWorldSnapshot.SlotSnapshot pending =
            queued.PostState.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1);
        var pendingState = Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.FabricationPending>(
            pending.State);
        Assert.Equal(2, pendingState.DueTick);
        Assert.Equal(new Position(3, 3), pendingState.ReservedPosition);
        Assert.Contains(
            queued.Events,
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued);

        GenericActorMatchPreparedTick creation = session.PrepareTick();
        GenericActorRuntimeObservation childObservation =
            creation.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 0
                && observation.Self.ActorId.UnitId == 1);
        Assert.Equal(2, childObservation.Tick);
        Assert.Equal(2, creation.Observations.Count(observation =>
            observation.Self.ActorId.TeamId == 0));
        GenericActorMatchStepResult creationStep =
            session.Step(creation.Observations);

        GenericActorWorldSnapshot.LifeSnapshot child =
            creationStep.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0 && life.ActorId.UnitId == 1);
        Assert.Equal(new ActorIdentity(0, 1, 0), child.ActorId);
        Assert.Equal(1, child.Generation);
        Assert.Equal("child", child.FormId);
        Assert.Equal(2, child.Health);
        Assert.Equal(Direction.East, child.Facing);
        Assert.Equal(new ActorIdentity(0, 0, 0), child.ParentActorId);
        Assert.Equal(
            GenericActorRuntimeStart.SpawnReason.Fabrication,
            child.SpawnReason);
        Assert.Equal("fabricate-child", child.SourceTransitionId);
        Assert.Contains(
            creationStep.Events,
            item => item.Payload is
                GenericActorRuntimeObservation.EventPayload.Attack attack
                && attack.ActorId == child.ActorId);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            child.PreviousActionResolution?.Outcome);
        Assert.Equal(new Position(1, 3), creationStep.PostState.ActiveLives
            .Single(life => life.ActorId == new ActorIdentity(0, 0, 0))
            .Position);

        GenericDeathmatchSessionTestFixture.RecordingFactory factory =
            factories[10];
        Assert.Equal(2, factory.CreateCount);
        Assert.Equal(2, factory.Starts.Count);
        Assert.Equal(
            [
                GenericActorRuntimeStart.SpawnReason.Initial,
                GenericActorRuntimeStart.SpawnReason.Fabrication,
            ],
            factory.Starts.Select(start => start.Origin.Reason));
        Assert.NotEqual(
            factory.Starts[0].ActorRandomSeed,
            factory.Starts[1].ActorRandomSeed);
        Assert.Collection(
            session.Chronology.Ticks[2].TickStart.LifeStarts,
            start => Assert.Equal(child.ActorId, start.ActorId));
    }

    [Fact]
    public void PendingFabrication_SurvivesSourceDeath()
    {
        ActorResolvedMatchDefinition definition = WithAttackDamage(
            GenericActorContractTestFixture.WithTransitions(
                fabricationDelayTicks: 3,
                includeMovement: true),
            damagePerHit: 6);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId == 20)
                    {
                        return observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Shoot()
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    if (start.Origin.Reason
                        == GenericActorRuntimeStart.SpawnReason.Fabrication)
                    {
                        return GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        GenericActorMatchStepResult step = null!;
        for (int tick = 0; tick <= 3; tick++)
            step = session.Step(session.PrepareTick().Observations);

        Assert.DoesNotContain(
            step.PostState.ActiveLives,
            life => life.ActorId == new ActorIdentity(0, 0, 0));
        GenericActorWorldSnapshot.SlotSnapshot pending =
            step.PostState.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1);
        Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.FabricationPending>(
            pending.State);
        Assert.Contains(
            session.Chronology.Ticks.SelectMany(tick => tick.Events),
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.Destruction);
        Assert.DoesNotContain(
            step.Events,
            item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && lifecycle.TransitionId == "fabricate-child");

        GenericActorMatchPreparedTick due = session.PrepareTick();
        GenericActorRuntimeObservation child = due.Observations.Single(
            observation =>
                observation.Self.ActorId
                    == new ActorIdentity(0, 1, 0));
        Assert.Equal(4, child.Tick);
        session.Step(due.Observations);
        GenericActorLifeStart start = Assert.Single(
            session.Chronology.Ticks[4].TickStart.LifeStarts);
        Assert.Equal(new ActorIdentity(0, 0, 0), start.Origin.ParentActorId);
        Assert.Equal(
            GenericActorRuntimeStart.SpawnReason.Fabrication,
            start.Origin.Reason);
    }

    [Fact]
    public void DueFabrication_PurgesProjectileOnReservedTileBeforeSpawn()
    {
        ActorResolvedMatchDefinition definition =
            WithAttackDamage(
                GenericActorContractTestFixture.WithTransitions(
                    fabricationDelayTicks: 3,
                    includeMovement: true),
                damagePerHit: 1,
                tilesPerAdvance: 1);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId == 20)
                    {
                        return observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Shoot()
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        GenericActorMatchStepResult beforeDue =
            session.Step(session.PrepareTick().Observations);
        Assert.Contains(
            beforeDue.PostState.Projectiles,
            projectile => projectile.Position == new Position(3, 3));

        GenericActorMatchPreparedTick due = session.PrepareTick();
        Assert.Contains(
            due.Observations,
            observation =>
                observation.Self.ActorId
                    == new ActorIdentity(0, 1, 0));
        session.Step(due.Observations);
        GenericActorMatchTickStart tickStart =
            session.Chronology.Ticks[4].TickStart;
        GenericActorProjectileTraversal purge = Assert.Single(
            tickStart.Traversals);
        Assert.Equal(
            GenericActorProjectileTraversal.TraversalTrigger
                .LifecyclePlacement,
            purge.Trigger);
        Assert.IsType<GenericActorProjectileTraversal.TerminalDisposition
            .LifecyclePlacementPurge>(purge.Terminal);
        Assert.DoesNotContain(
            due.Observations.SelectMany(observation =>
                observation.VisibleProjectiles ?? []),
            projectile => projectile.Position == new Position(3, 3));
    }

    [Fact]
    public void SourceCanOwnMultipleOutstandingFabrications()
    {
        ActorResolvedMatchDefinition definition = WithExtraDormantSlots(
            GenericActorContractTestFixture.WithTransitions(
                fabricationCandidateOffsets:
                [
                    new(1, 0),
                    new(1, 1),
                ],
                fabricationDelayTicks: 3,
                includeMovement: true),
            extraUnitIds: [2]);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId != 10
                        || start.Origin.Reason
                            != GenericActorRuntimeStart.SpawnReason.Initial)
                    {
                        return GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        2 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            2),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        GenericActorMatchStepResult secondQueue =
            session.Step(session.PrepareTick().Observations);

        Assert.Equal(
            2,
            secondQueue.PostState.Slots.Count(slot =>
                slot.TeamId == 0
                && slot.State is GenericActorRuntimeObservation
                    .UnitSlotState.FabricationPending));
        Assert.Equal(
            2,
            session.Chronology.Ticks
                .SelectMany(tick => tick.Events)
                .Count(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued));

        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        GenericActorMatchStepResult bothCreated =
            session.Step(session.PrepareTick().Observations);
        Assert.Equal(
            [0, 1, 2],
            bothCreated.PostState.ActiveLives
                .Where(life => life.ActorId.TeamId == 0)
                .Select(life => life.ActorId.UnitId)
                .Order()
                .ToArray());
        Assert.Equal(3, factories[10].CreateCount);
    }

    [Fact]
    public void LaterSplitRetirement_DoesNotCancelPendingFabrication()
    {
        ActorResolvedMatchDefinition definition = WithExtraDormantSlots(
            GenericActorContractTestFixture.WithTransitions(
                fabricationDelayTicks: 3,
                includeMovement: true),
            extraUnitIds: [2]);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId != 10
                        || start.Origin.Reason
                            != GenericActorRuntimeStart.SpawnReason.Initial)
                    {
                        return GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        2 => GenericDeathmatchSessionTestFixture.Split(),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        GenericActorMatchStepResult splitCompleted =
            session.Step(session.PrepareTick().Observations);

        Assert.DoesNotContain(
            splitCompleted.PostState.ActiveLives,
            life => life.ActorId == new ActorIdentity(0, 0, 0));
        Assert.Contains(
            splitCompleted.PostState.ActiveLives,
            life => life.ActorId == new ActorIdentity(0, 0, 1));
        Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.FabricationPending>(
            splitCompleted.PostState.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1).State);
        Assert.DoesNotContain(
            splitCompleted.Events,
            item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && lifecycle.TransitionId == "fabricate-child");

        GenericActorMatchStepResult fabricationCompleted =
            session.Step(session.PrepareTick().Observations);
        GenericActorWorldSnapshot.LifeSnapshot fabricated =
            fabricationCompleted.PostState.ActiveLives.Single(life =>
                life.ActorId == new ActorIdentity(0, 1, 0));
        Assert.Equal(new ActorIdentity(0, 0, 0), fabricated.ParentActorId);
        Assert.Equal(
            GenericActorRuntimeStart.SpawnReason.Fabrication,
            fabricated.SpawnReason);
    }

    [Fact]
    public void PendingFabricationTile_BlocksMovement()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                fabricationDelayTicks: 4,
                includeMovement: true);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId == 20)
                    {
                        return observation.Tick <= 3
                            ? GenericDeathmatchSessionTestFixture.Move(
                                Direction.West)
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        GenericActorMatchStepResult step = null!;
        for (int tick = 0; tick <= 3; tick++)
            step = session.Step(session.PrepareTick().Observations);

        ActorIdentity enemy = new(1, 0, 0);
        Assert.Equal(
            new Position(4, 3),
            step.PostState.ActiveLives.Single(life =>
                life.ActorId == enemy).Position);
        Assert.Contains(
            step.Events,
            item => item.Payload is GenericActorRuntimeObservation
                .EventPayload.MovementBlocked blocked
                && blocked.ActorId == enemy
                && blocked.AttemptedTo == new Position(3, 3));
    }

    [Fact]
    public void Disqualification_CancelsFabricationBeforeRetiringLives()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                fabricationDelayTicks: 3,
                includeMovement: true);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId != 10)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        2 => GenericDeathmatchSessionTestFixture.Unknown(),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        GenericActorMatchStepResult disqualified =
            session.Step(session.PrepareTick().Observations);

        GenericActorAuthoritativeEvent[] events =
            session.Chronology.Ticks[2].Events.ToArray();
        int cancellationIndex = Array.FindIndex(
            events,
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleCancelled);
        int retirementIndex = Array.FindIndex(
            events,
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifeRetired);
        int disqualificationIndex = Array.FindIndex(
            events,
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .ParticipantDisqualified);
        Assert.True(cancellationIndex >= 0);
        Assert.True(cancellationIndex < retirementIndex);
        Assert.True(retirementIndex < disqualificationIndex);
        var cancellation =
            (GenericActorRuntimeObservation.EventPayload.Lifecycle)
                events[cancellationIndex].Payload;
        Assert.Equal("fabricate-child", cancellation.TransitionId);
        Assert.Equal(
            "participant-disqualified",
            cancellation.CancellationReason);
        Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.PermanentlyDormant>(
            disqualified.PostState.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1).State);
    }

    [Fact]
    public void LethalDisqualification_RetainsDestructionThenTransitionCancellation()
    {
        ActorResolvedMatchDefinition definition = WithAttackDamage(
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true),
            damagePerHit: 6,
            tilesPerAdvance: 8);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId == 20)
                    {
                        return observation.Tick == 2
                            ? GenericDeathmatchSessionTestFixture.Shoot()
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    if (start.Origin.Reason
                        == GenericActorRuntimeStart.SpawnReason.Fabrication)
                    {
                        return observation.Tick == 3
                            ? new GenericActorRuntimeDecision(
                                "anchor",
                                101,
                                [],
                                null)
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        3 => GenericDeathmatchSessionTestFixture.Unknown(),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using GenericActorMatchSession session = Session(
            definition,
            factories);

        for (int tick = 0; tick <= 3; tick++)
            session.Step(session.PrepareTick().Observations);

        ActorIdentity child = new(0, 1, 0);
        GenericActorAuthoritativeEvent[] events =
            session.Chronology.Ticks[3].Events.ToArray();
        int destructionIndex = Array.FindIndex(
            events,
            item => item.Payload is
                GenericActorRuntimeObservation.EventPayload.Destruction
                    destruction
                && destruction.ActorId == child);
        int cancellationIndex = Array.FindIndex(
            events,
            item => item.Payload is
                GenericActorRuntimeObservation.EventPayload.FormTransition
                    transition
                && transition.ActorId == child
                && item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .FormTransitionCancelled);
        Assert.True(destructionIndex >= 0);
        Assert.True(cancellationIndex > destructionIndex);
    }

    [Fact]
    public void Rebuild_UsesNextLifeIdAndAnotherFreshRuntime()
    {
        ActorResolvedMatchDefinition definition = WithAttackDamage(
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true),
            damagePerHit: 2);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId == 20)
                    {
                        return observation.Tick == 2
                            ? GenericDeathmatchSessionTestFixture.Shoot()
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    if (start.Origin.Reason
                        == GenericActorRuntimeStart.SpawnReason.Fabrication)
                    {
                        return GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 or 7 =>
                            GenericDeathmatchSessionTestFixture.Fabricate(
                                0,
                                1),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        GenericActorMatchStepResult step = null!;
        for (int tick = 0; tick <= 8; tick++)
            step = session.Step(session.PrepareTick().Observations);

        GenericActorWorldSnapshot.LifeSnapshot rebuilt =
            step.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0 && life.ActorId.UnitId == 1);
        Assert.Equal(new ActorIdentity(0, 1, 1), rebuilt.ActorId);
        Assert.Equal(1, rebuilt.Generation);
        Assert.Equal(new ActorIdentity(0, 0, 0), rebuilt.ParentActorId);
        Assert.Equal(
            GenericActorRuntimeStart.SpawnReason.Fabrication,
            rebuilt.SpawnReason);
        Assert.Equal(
            2,
            step.PostState.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1).NextLifeId);

        GenericDeathmatchSessionTestFixture.RecordingFactory factory =
            factories[10];
        Assert.Equal(3, factory.CreateCount);
        GenericActorRuntimeStart[] children = factory.Starts
            .Where(start => start.ActorId.UnitId == 1)
            .OrderBy(start => start.ActorId.LifeId)
            .ToArray();
        Assert.Equal(
            [new ActorIdentity(0, 1, 0), new ActorIdentity(0, 1, 1)],
            children.Select(start => start.ActorId));
        Assert.NotEqual(
            children[0].ActorRandomSeed,
            children[1].ActorRandomSeed);
        Assert.True(factory.DisposedRuntimeCount >= 1);
    }

    [Fact]
    public void FabricatedChild_ContributesFrontlinePresenceOnSpawnTick()
    {
        ActorResolvedMatchDefinition definition = AsFrontline(
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true));
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId != 10
                        || start.Origin.Reason
                            != GenericActorRuntimeStart.SpawnReason.Initial)
                    {
                        return GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using var session = Session(definition, factories);

        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);
        GenericActorMatchStepResult spawnTick =
            session.Step(session.PrepareTick().Observations);

        var mode = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.Frontline>(
            spawnTick.PostState.Mode);
        Assert.Equal(2, mode.ActivePositionIndex);
        Assert.Equal(
            1,
            spawnTick.PostState.Scoreboard.Teams
                .Single(team => team.TeamId == 0)
                .Scores.Single(score =>
                    score.Channel == "territorial-progress")
                .Value);
        GenericActorAuthoritativeEvent spawn =
            session.Chronology.Ticks[2].TickStart.Events.Single(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.LifeSpawned);
        GenericActorAuthoritativeEvent score =
            session.Chronology.Ticks[2].Events.First(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.ScoreChanged);
        Assert.True(spawn.Ordinal < score.Ordinal);
    }

    [Fact]
    public void ChronologyRejectsPendingFabricationWithoutQueueCausality()
    {
        GenericActorMatchChronology chronology =
            RecordFabricationChronology(
                fabricationDelayTicks: 3,
                executedTicks: 2,
                disqualifyAtTickTwo: false);
        GenericActorMatchTickFrame queued = chronology.Ticks[1];
        GenericActorAuthoritativeEvent queueEvent = queued.Events.Single(
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued);
        GenericActorAuthoritativeEvent falseCompletion = ReplaceEvent(
            queueEvent,
            GenericActorRuntimeObservation.EventKind.LifecycleCompleted,
            queueEvent.Payload);
        GenericActorMatchTickFrame forged = ReplaceResolutionEvent(
            queued,
            queueEvent,
            falseCompletion);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], forged],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsPrematureAvailabilityReadiness()
    {
        ActorResolvedMatchDefinition definition = WithDelayedDormantUnlock(
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true),
            teamId: 0,
            unitId: 1,
            unlockTick: 3);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, _) => GenericDeathmatchSessionTestFixture.Wait());
        using GenericActorMatchSession session = Session(
            definition,
            factories);
        session.Step(session.PrepareTick().Observations);

        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame recorded = chronology.Ticks[0];
        GenericActorWorldSnapshot.SlotSnapshot pendingSlot =
            recorded.TickStart.State.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1);
        var pending = Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.AvailabilityPending>(
            pendingSlot.State);
        Assert.Equal(3, pending.DueTick);
        var prematureReady = new GenericActorWorldSnapshot.SlotSnapshot(
            pendingSlot.TeamId,
            pendingSlot.UnitId,
            pendingSlot.ParticipantId,
            pendingSlot.NextLifeId,
            new GenericActorRuntimeObservation.UnitSlotState.Ready(),
            pendingParentActorId: null,
            splitReservation: null);
        GenericActorWorldSnapshot forgedTickStartState = CopyWorld(
            recorded.TickStart.State,
            definition,
            slots: recorded.TickStart.State.Slots.Select(slot =>
                slot.TeamId == prematureReady.TeamId
                && slot.UnitId == prematureReady.UnitId
                    ? prematureReady
                    : slot));
        GenericActorWorldSnapshot forgedPostState = CopyWorld(
            recorded.PostState,
            definition,
            slots: recorded.PostState.Slots.Select(slot =>
                slot.TeamId == prematureReady.TeamId
                && slot.UnitId == prematureReady.UnitId
                    ? prematureReady
                    : slot));
        var forgedTickStart = new GenericActorMatchTickStart(
            recorded.Tick,
            forgedTickStartState,
            recorded.TickStart.ActiveActorIds,
            recorded.TickStart.LifeStarts,
            recorded.TickStart.Events,
            recorded.TickStart.Traversals);
        var forged = new GenericActorMatchTickFrame(
            forgedTickStart,
            recorded.ActorTurns,
            recorded.Events,
            recorded.Traversals,
            forgedPostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [forged],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsFabricationSpawnWithoutCompletionCausality()
    {
        GenericActorMatchChronology chronology =
            RecordFabricationChronology(
                fabricationDelayTicks: 1,
                executedTicks: 3,
                disqualifyAtTickTwo: false);
        GenericActorMatchTickFrame completed = chronology.Ticks[2];
        GenericActorAuthoritativeEvent completionEvent =
            completed.TickStart.Events.Single(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCompleted);
        GenericActorAuthoritativeEvent falseQueue = ReplaceEvent(
            completionEvent,
            GenericActorRuntimeObservation.EventKind.LifecycleQueued,
            completionEvent.Payload);
        GenericActorMatchTickFrame forged = ReplaceTickStartEvent(
            completed,
            completionEvent,
            falseQueue);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], chronology.Ticks[1], forged],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsFabricationCancellationWithoutDisqualificationReason()
    {
        GenericActorMatchChronology chronology =
            RecordFabricationChronology(
                fabricationDelayTicks: 3,
                executedTicks: 3,
                disqualifyAtTickTwo: true);
        GenericActorMatchTickFrame cancelled = chronology.Ticks[2];
        GenericActorAuthoritativeEvent cancellationEvent =
            cancelled.Events.Single(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled);
        var payload =
            (GenericActorRuntimeObservation.EventPayload.Lifecycle)
                cancellationEvent.Payload;
        GenericActorAuthoritativeEvent falseCancellation = ReplaceEvent(
            cancellationEvent,
            cancellationEvent.Kind,
            payload with { CancellationReason = "source-destroyed" });
        GenericActorMatchTickFrame forged = ReplaceResolutionEvent(
            cancelled,
            cancellationEvent,
            falseCancellation);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], chronology.Ticks[1], forged],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsNonCanonicalFabricationOffset()
    {
        ActorResolvedMatchDefinition definition = WithExtraDormantSlots(
            GenericActorContractTestFixture.WithTransitions(
                fabricationCandidateOffsets:
                [
                    new(1, 0),
                    new(1, 1),
                ],
                fabricationDelayTicks: 3,
                includeMovement: true),
            extraUnitIds: []);
        GenericActorMatchChronology chronology =
            RecordFabricationChronology(
                definition,
                executedTicks: 2,
                disqualifyAtTickTwo: false);
        GenericActorMatchTickFrame queued = chronology.Ticks[1];
        GenericActorWorldSnapshot.SlotSnapshot target =
            queued.PostState.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1);
        var pending = Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.FabricationPending>(
            target.State);
        Assert.Equal(new Position(3, 3), pending.ReservedPosition);
        var forgedTarget = new GenericActorWorldSnapshot.SlotSnapshot(
            target.TeamId,
            target.UnitId,
            target.ParticipantId,
            target.NextLifeId,
            new GenericActorRuntimeObservation.UnitSlotState
                .FabricationPending(
                    pending.DueTick,
                    pending.SourceActorId,
                    pending.TransitionId,
                    pending.OperationId,
                    pending.TargetFormId,
                    new Position(3, 4)),
            target.PendingParentActorId,
            target.SplitReservation);
        GenericActorWorldSnapshot forgedPostState = CopyWorld(
            queued.PostState,
            definition,
            slots: queued.PostState.Slots.Select(slot =>
                slot.TeamId == forgedTarget.TeamId
                && slot.UnitId == forgedTarget.UnitId
                    ? forgedTarget
                    : slot));
        var forged = new GenericActorMatchTickFrame(
            queued.TickStart,
            queued.ActorTurns,
            queued.Events,
            queued.Traversals,
            forgedPostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], forged],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsFabricationQueuedOutsideSourceRegion()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Fabricate(0, 1)
                    : GenericDeathmatchSessionTestFixture.Wait());
        using GenericActorMatchSession session = Session(
            definition,
            factories);
        session.Step(session.PrepareTick().Observations);
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame blocked = chronology.Ticks[0];
        ActorIdentity sourceActorId = new(0, 0, 0);
        GenericActorMatchActorTurn sourceTurn = blocked.ActorTurns.Single(
            turn => turn.ActorId == sourceActorId);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            sourceTurn.ActionResolution.Outcome);
        GenericActorRuntimeActionResolution forgedResolution =
            sourceTurn.ActionResolution with
            {
                Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome
                        .Success,
            };
        var forgedTurn = new GenericActorMatchActorTurn(
            sourceTurn.Tick,
            sourceTurn.ParticipantId,
            sourceTurn.ActorId,
            sourceTurn.Observation,
            sourceTurn.SubmittedDecision,
            forgedResolution);
        string operationId =
            "fabrication:0:0:0:0:fabricate-child:0:1";
        GenericActorWorldSnapshot.SlotSnapshot target =
            blocked.PostState.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1);
        var forgedTarget = new GenericActorWorldSnapshot.SlotSnapshot(
            target.TeamId,
            target.UnitId,
            target.ParticipantId,
            target.NextLifeId,
            new GenericActorRuntimeObservation.UnitSlotState
                .FabricationPending(
                    dueTick: 1,
                    sourceActorId,
                    transitionId: "fabricate-child",
                    operationId,
                    targetFormId: "child",
                    reservedPosition: new Position(3, 3)),
            pendingParentActorId: null,
            splitReservation: null);
        GenericActorWorldSnapshot forgedPostState = CopyWorld(
            blocked.PostState,
            definition,
            slots: blocked.PostState.Slots.Select(slot =>
                slot.TeamId == forgedTarget.TeamId
                && slot.UnitId == forgedTarget.UnitId
                    ? forgedTarget
                    : slot),
            lives: blocked.PostState.ActiveLives.Select(life =>
                life.ActorId == sourceActorId
                    ? CopyLife(
                        life,
                        previousActionResolution: forgedResolution)
                    : life));
        long ordinal = chronology.InitialFrame.Events
            .Max(item => item.Ordinal) + 1;
        int sourceOrdinal = chronology.InitialFrame.Events
            .Where(item => item.Tick == 0)
            .Max(item => item.SourceOrdinal) + 1;
        var queueEvent = new GenericActorAuthoritativeEvent(
            "forged-source-region-fabrication",
            tick: 0,
            ordinal,
            sourceOrdinal,
            GenericActorRuntimeObservation.EventKind.LifecycleQueued,
            new GenericActorRuntimeObservation.EventPayload.Lifecycle(
                "fabricate-child",
                operationId,
                sourceActorId,
                TargetTeamId: 0,
                TargetUnitId: 1,
                DueTick: 1,
                CancellationReason: null),
            new GenericActorAuthoritativeEvent.Audience.Spatial(
                new Position(1, 3)));
        var forgedFrame = new GenericActorMatchTickFrame(
            blocked.TickStart,
            blocked.ActorTurns.Select(turn =>
                turn.ActorId == sourceActorId ? forgedTurn : turn)
                .ToArray(),
            [queueEvent],
            blocked.Traversals,
            forgedPostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [forgedFrame],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsForgedPostMovementLifecycleSnapshot()
    {
        GenericActorMatchChronology chronology =
            RecordFabricationChronology(
                fabricationDelayTicks: 3,
                executedTicks: 2,
                disqualifyAtTickTwo: false);
        GenericActorMatchTickFrame moved = chronology.Ticks[0];
        GenericActorAuthoritativeEvent movementEvent =
            moved.Events.Single(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.Movement
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Movement
                        movement
                && movement.ActorId == new ActorIdentity(0, 0, 0));
        var payload =
            (GenericActorRuntimeObservation.EventPayload.Movement)
                movementEvent.Payload;
        GenericActorAuthoritativeEvent forgedMovement = ReplaceEvent(
            movementEvent,
            movementEvent.Kind,
            payload with { To = new Position(2, 4) });
        GenericActorMatchTickFrame forged = ReplaceResolutionEvent(
            moved,
            movementEvent,
            forgedMovement);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [forged],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsSuccessfulMoveForgedAcrossDuplicateTarget()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) => observation.Tick <= 2
                    ? GenericDeathmatchSessionTestFixture.Move(
                        start.ParticipantId == 10
                            ? Direction.East
                            : Direction.West)
                    : GenericDeathmatchSessionTestFixture.Wait());
        using GenericActorMatchSession session = Session(
            definition,
            factories);
        for (int tick = 0; tick <= 2; tick++)
            session.Step(session.PrepareTick().Observations);

        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame conflict = chronology.Ticks[2];
        ActorIdentity forgedActorId = new(0, 0, 0);
        GenericActorMatchActorTurn turn = conflict.ActorTurns.Single(item =>
            item.ActorId == forgedActorId);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            turn.ActionResolution.Outcome);
        GenericActorRuntimeActionResolution forgedResolution =
            turn.ActionResolution with
            {
                Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome.Success,
            };
        var forgedTurn = new GenericActorMatchActorTurn(
            turn.Tick,
            turn.ParticipantId,
            turn.ActorId,
            turn.Observation,
            turn.SubmittedDecision,
            forgedResolution);
        GenericActorAuthoritativeEvent blockedEvent =
            conflict.Events.Single(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload
                        .MovementBlocked blocked
                && blocked.ActorId == forgedActorId);
        var blockedPayload =
            (GenericActorRuntimeObservation.EventPayload.MovementBlocked)
                blockedEvent.Payload;
        var forgedMovement = new GenericActorAuthoritativeEvent(
            blockedEvent.EventHandle,
            blockedEvent.Tick,
            blockedEvent.Ordinal,
            blockedEvent.SourceOrdinal,
            GenericActorRuntimeObservation.EventKind.Movement,
            new GenericActorRuntimeObservation.EventPayload.Movement(
                forgedActorId,
                blockedPayload.Action,
                blockedPayload.From,
                blockedPayload.AttemptedTo,
                blockedPayload.Facing),
            new GenericActorAuthoritativeEvent.Audience.Spatial(
                blockedPayload.AttemptedTo));
        GenericActorWorldSnapshot forgedPostState = CopyWorld(
            conflict.PostState,
            definition,
            lives: conflict.PostState.ActiveLives.Select(life =>
                life.ActorId == forgedActorId
                    ? CopyLife(
                        life,
                        position: blockedPayload.AttemptedTo,
                        previousActionResolution: forgedResolution)
                    : life));
        var forged = new GenericActorMatchTickFrame(
            conflict.TickStart,
            conflict.ActorTurns.Select(item =>
                item.ActorId == forgedActorId ? forgedTurn : item)
                .ToArray(),
            conflict.Events.Select(item =>
                ReferenceEquals(item, blockedEvent)
                    ? forgedMovement
                    : item)
                .ToArray(),
            conflict.Traversals,
            forgedPostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [
                    chronology.Ticks[0],
                    chronology.Ticks[1],
                    forged,
                ],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsSuccessForgedAcrossJointFabricationSplitConflict()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId == 10)
                    {
                        return observation.Tick switch
                        {
                            0 => GenericDeathmatchSessionTestFixture.Move(
                                Direction.East),
                            5 => GenericDeathmatchSessionTestFixture
                                .Fabricate(0, 1),
                            _ => GenericDeathmatchSessionTestFixture.Wait(),
                        };
                    }
                    return observation.Tick switch
                    {
                        0 or 1 or 2 or 3 =>
                            GenericDeathmatchSessionTestFixture.Move(
                                Direction.West),
                        4 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.North),
                        5 => GenericDeathmatchSessionTestFixture.Split(),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using GenericActorMatchSession session = Session(
            definition,
            factories);
        for (int tick = 0; tick <= 5; tick++)
        {
            Assert.False(
                session.IsCompleted,
                $"Match completed before conflict tick {tick}.");
            session.Step(session.PrepareTick().Observations);
        }
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame conflict = chronology.Ticks[5];
        Assert.All(
            conflict.ActorTurns,
            turn => Assert.Equal(
                GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
                turn.ActionResolution.Outcome));
        Assert.DoesNotContain(
            conflict.Events,
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued);

        ActorIdentity fabricator = new(0, 0, 0);
        GenericActorMatchActorTurn sourceTurn = conflict.ActorTurns.Single(
            turn => turn.ActorId == fabricator);
        var forgedTurn = new GenericActorMatchActorTurn(
            sourceTurn.Tick,
            sourceTurn.ParticipantId,
            sourceTurn.ActorId,
            sourceTurn.Observation,
            sourceTurn.SubmittedDecision,
            sourceTurn.ActionResolution with
            {
                Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome
                        .Success,
            });
        var forged = new GenericActorMatchTickFrame(
            conflict.TickStart,
            conflict.ActorTurns.Select(turn =>
                turn.ActorId == fabricator ? forgedTurn : turn)
                .ToArray(),
            conflict.Events,
            conflict.Traversals,
            conflict.PostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [
                    chronology.Ticks[0],
                    chronology.Ticks[1],
                    chronology.Ticks[2],
                    chronology.Ticks[3],
                    chronology.Ticks[4],
                    forged,
                ],
                result: null));
    }

    [Fact]
    public void ChronologyRejectsForgedSplitDescendantOperationLineage()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                includeMovement: true);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10 && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using GenericActorMatchSession session = Session(
            definition,
            factories);
        session.Step(session.PrepareTick().Observations);
        session.Step(session.PrepareTick().Observations);

        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame completed = chronology.Ticks[1];
        ActorIdentity descendantId = completed.TickStart.State.ActiveLives
            .First(life =>
                life.SpawnReason
                    == GenericActorRuntimeStart.SpawnReason.Replication)
            .ActorId;
        const string forgedOperationId = "forged-split-operation";
        GenericActorLifeStart originalStart =
            completed.TickStart.LifeStarts.Single(start =>
                start.ActorId == descendantId);
        var forgedStart = new GenericActorLifeStart(
            originalStart.SchemaVersion,
            originalStart.RuntimeContractVersion,
            originalStart.ActorId,
            originalStart.ParticipantId,
            originalStart.ActorRandomSeed,
            originalStart.Origin with
            {
                SourceOperationId = forgedOperationId,
            },
            originalStart.MatchContractFingerprint);
        GenericActorAuthoritativeEvent originalSpawn =
            completed.TickStart.Events.Single(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.ActorId == descendantId);
        var spawnPayload =
            (GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                originalSpawn.Payload;
        GenericActorAuthoritativeEvent forgedSpawn = ReplaceEvent(
            originalSpawn,
            originalSpawn.Kind,
            spawnPayload with
            {
                SourceOperationId = forgedOperationId,
            });
        GenericActorWorldSnapshot forgedTickStartState = CopyWorld(
            completed.TickStart.State,
            definition,
            lives: completed.TickStart.State.ActiveLives.Select(life =>
                life.ActorId == descendantId
                    ? CopyLife(
                        life,
                        sourceOperationId: forgedOperationId)
                    : life));
        GenericActorWorldSnapshot forgedPostState = CopyWorld(
            completed.PostState,
            definition,
            lives: completed.PostState.ActiveLives.Select(life =>
                life.ActorId == descendantId
                    ? CopyLife(
                        life,
                        sourceOperationId: forgedOperationId)
                    : life));
        var forgedTickStart = new GenericActorMatchTickStart(
            completed.Tick,
            forgedTickStartState,
            completed.TickStart.ActiveActorIds,
            completed.TickStart.LifeStarts.Select(start =>
                start.ActorId == descendantId ? forgedStart : start)
                .ToArray(),
            completed.TickStart.Events.Select(item =>
                ReferenceEquals(item, originalSpawn)
                    ? forgedSpawn
                    : item)
                .ToArray(),
            completed.TickStart.Traversals);
        var forged = new GenericActorMatchTickFrame(
            forgedTickStart,
            completed.ActorTurns,
            completed.Events,
            completed.Traversals,
            forgedPostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], forged],
                result: null));
    }

    private static GenericActorMatchSession Session(
        ActorResolvedMatchDefinition definition,
        IReadOnlyDictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory>
            factories) =>
        new(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 4_002);

    private static GenericActorMatchChronology RecordFabricationChronology(
        int fabricationDelayTicks,
        int executedTicks,
        bool disqualifyAtTickTwo)
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions(
                fabricationDelayTicks: fabricationDelayTicks,
                includeMovement: true);
        return RecordFabricationChronology(
            definition,
            executedTicks,
            disqualifyAtTickTwo);
    }

    private static GenericActorMatchChronology RecordFabricationChronology(
        ActorResolvedMatchDefinition definition,
        int executedTicks,
        bool disqualifyAtTickTwo)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId != 10)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    return observation.Tick switch
                    {
                        0 => GenericDeathmatchSessionTestFixture.Move(
                            Direction.East),
                        1 => GenericDeathmatchSessionTestFixture.Fabricate(
                            0,
                            1),
                        2 when disqualifyAtTickTwo =>
                            GenericDeathmatchSessionTestFixture.Unknown(),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    };
                });
        using GenericActorMatchSession session = Session(
            definition,
            factories);
        for (int tick = 0; tick < executedTicks; tick++)
            session.Step(session.PrepareTick().Observations);
        return session.Chronology;
    }

    private static GenericActorWorldSnapshot CopyWorld(
        GenericActorWorldSnapshot source,
        ActorResolvedMatchDefinition definition,
        IEnumerable<GenericActorWorldSnapshot.SlotSnapshot>? slots = null,
        IEnumerable<GenericActorWorldSnapshot.LifeSnapshot>? lives = null) =>
        new(
            definition,
            source.NextTick,
            source.NextProjectileId,
            source.Participants,
            slots is null ? source.Slots : slots.ToArray(),
            lives is null ? source.ActiveLives : lives.ToArray(),
            source.PendingReplications,
            source.Projectiles,
            source.Scoreboard,
            source.Mode);

    private static GenericActorWorldSnapshot.LifeSnapshot CopyLife(
        GenericActorWorldSnapshot.LifeSnapshot source,
        Position? position = null,
        GenericActorRuntimeActionResolution? previousActionResolution =
            null,
        int? spawnedAtTick = null,
        string? sourceOperationId = null) =>
        new(
            source.ActorId,
            source.ParticipantId,
            source.Generation,
            source.FormId,
            position ?? source.Position,
            source.Facing,
            source.Health,
            source.Cooldown,
            source.Energy,
            spawnedAtTick ?? source.SpawnedAtTick,
            source.SpawnReason,
            source.ParentActorId,
            source.SourceTransitionId,
            sourceOperationId ?? source.SourceOperationId,
            previousActionResolution ?? source.PreviousActionResolution,
            source.PendingSameLifeTransition);

    private static GenericActorMatchTickFrame ReplaceResolutionEvent(
        GenericActorMatchTickFrame source,
        GenericActorAuthoritativeEvent original,
        GenericActorAuthoritativeEvent replacement) =>
        new(
            source.TickStart,
            source.ActorTurns,
            source.Events.Select(item =>
                ReferenceEquals(item, original) ? replacement : item)
                .ToArray(),
            source.Traversals,
            source.PostState);

    private static GenericActorMatchTickFrame ReplaceTickStartEvent(
        GenericActorMatchTickFrame source,
        GenericActorAuthoritativeEvent original,
        GenericActorAuthoritativeEvent replacement)
    {
        var tickStart = new GenericActorMatchTickStart(
            source.Tick,
            source.TickStart.State,
            source.TickStart.ActiveActorIds,
            source.TickStart.LifeStarts,
            source.TickStart.Events.Select(item =>
                ReferenceEquals(item, original) ? replacement : item)
                .ToArray(),
            source.TickStart.Traversals);
        return new GenericActorMatchTickFrame(
            tickStart,
            source.ActorTurns,
            source.Events,
            source.Traversals,
            source.PostState);
    }

    private static GenericActorAuthoritativeEvent ReplaceEvent(
        GenericActorAuthoritativeEvent source,
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload payload) =>
        new(
            source.EventHandle,
            source.Tick,
            source.GlobalOrdinal,
            source.SourceOrdinal,
            kind,
            payload,
            source.EventAudience);

    private static ActorResolvedMatchDefinition WithAttackDamage(
        ActorResolvedMatchDefinition source,
        int damagePerHit,
        int? tilesPerAdvance = null)
    {
        ActorAttackProfileDefinition baseline =
            source.Rules.AttackProfiles.Single();
        ActorProjectileDefinition projectile = baseline.Projectile;
        var changedProjectile = new ActorProjectileDefinition(
            projectile.Mode,
            damagePerHit,
            projectile.MaxTravelTiles,
            projectile.TicksPerAdvance,
            tilesPerAdvance ?? projectile.TilesPerAdvance,
            projectile.LaunchTiles,
            projectile.AdvancesOnLaunchTick,
            projectile.DamageAppliedSimultaneously,
            projectile.DiagonalCornersMustBeClear);
        var changedAttack = new ActorAttackProfileDefinition(
            baseline.Id,
            baseline.OmnidirectionalAim,
            changedProjectile,
            baseline.CooldownTicks,
            baseline.MaxEnergy,
            baseline.AttackEnergyCost,
            baseline.EnergyRegenerationIntervalTicks,
            baseline.EnergyRegenerationAmount,
            baseline.ShotProgram);
        var rules = new ActorRulesDefinition(
            source.Rules.RulesetId,
            source.Rules.Limits,
            source.Rules.SeedMechanics,
            source.Rules.GameMode,
            source.Rules.Lifecycle,
            source.Rules.Forms,
            source.Rules.MovementProfiles,
            source.Rules.VisionProfiles,
            [changedAttack],
            source.Rules.Actions,
            source.Rules.FabricationTransitions,
            source.Rules.SameLifeTransitions,
            source.Rules.ReplicationTransitions,
            source.Rules.TeamPerception,
            source.Rules.Collisions,
            source.Rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition WithDelayedDormantUnlock(
        ActorResolvedMatchDefinition source,
        int teamId,
        int unitId,
        int unlockTick)
    {
        ActorUnitSlotLifecycleAssignmentDefinition target =
            source.LifecycleAssignments.Single(assignment =>
                assignment.TeamId == teamId
                && assignment.UnitId == unitId);
        var changed =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                target.TeamId,
                target.UnitId,
                target.LifecycleProfileId,
                initialGeneration: null,
                target.AllowedFormIds,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick,
                target.AssignedRespawnSpawnId);
        return new ActorResolvedMatchDefinition(
            source.Rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments.Select(assignment =>
                assignment.TeamId == teamId
                && assignment.UnitId == unitId
                    ? changed
                    : assignment),
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition WithExtraDormantSlots(
        ActorResolvedMatchDefinition source,
        IReadOnlyCollection<int> extraUnitIds)
    {
        PublicUnitSlot[] addedSlots = source.Topology.Teams
            .SelectMany(team => extraUnitIds.Select(unitId =>
            {
                int participantId = source.Topology.Participants
                    .Single(participant =>
                        participant.TeamId == team.TeamId)
                    .ParticipantId;
                return new PublicUnitSlot(
                    team.TeamId,
                    unitId,
                    participantId);
            }))
            .ToArray();
        var topology = new PublicMatchTopology
        {
            Teams = source.Topology.Teams,
            Participants = source.Topology.Participants,
            UnitSlots =
            [
                .. source.Topology.UnitSlots,
                .. addedSlots,
            ],
            InitialLives = source.Topology.InitialLives,
        };
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments,
            .. addedSlots.Select(slot =>
                new ActorUnitSlotLifecycleAssignmentDefinition(
                    slot.TeamId,
                    slot.UnitId,
                    "child-ready",
                    initialGeneration: null,
                    allowedFormIds: ["child", "turret"],
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.DormantUnlockAtTick,
                    unlockTick: 0,
                assignedRespawnSpawnId: null)),
        ];
        ActorMapRegionDefinition[] regions = source.Map.Regions
            .Select(region => region.RegionId switch
            {
                "output-west" => region with
                {
                    Tiles =
                    [
                        .. region.Tiles,
                        new Position(3, 4),
                    ],
                },
                "output-east" => region with
                {
                    Tiles =
                    [
                        .. region.Tiles,
                        new Position(5, 2),
                    ],
                },
                _ => region,
            })
            .ToArray();
        var map = new ActorMapDefinition(
            source.Map.Id,
            source.Map.Version,
            source.Map.TileRows,
            source.Map.SpawnAnchors,
            [.. regions],
            source.Map.TileTags);
        return new ActorResolvedMatchDefinition(
            source.Rules,
            map,
            source.Format,
            topology,
            source.InitialDeployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition AsFrontline(
        ActorResolvedMatchDefinition source)
    {
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
                    ScoreChannelDefinition.ChannelKind
                        .TerritorialProgress),
            ],
            frontlinePositionCount: 3,
            new FrontlineCaptureDefinition(
                threshold: 1,
                gainPerSoleTeamTick: 1,
                decayAmount: 0,
                decayIntervalTicks: 0,
                redeployPauseTicks: 0));
        ActorFormDefinition[] forms = source.Rules.Forms
            .Select(form =>
                new ActorFormDefinition(
                    form.Id,
                    form.MaxHealth,
                    form.MovementProfileId,
                    form.VisionProfileId,
                    form.AttackProfileId,
                    objectiveWeight: form.Id == "child" ? 1 : 0,
                    form.AllowedActionIds))
            .ToArray();
        var rules = new ActorRulesDefinition(
            "generic-frontline-fabrication-fixture",
            source.Rules.Limits,
            source.Rules.SeedMechanics,
            mode,
            source.Rules.Lifecycle,
            forms,
            source.Rules.MovementProfiles,
            source.Rules.VisionProfiles,
            source.Rules.AttackProfiles,
            source.Rules.Actions,
            source.Rules.FabricationTransitions,
            source.Rules.SameLifeTransitions,
            source.Rules.ReplicationTransitions,
            source.Rules.TeamPerception,
            source.Rules.Collisions,
            source.Rules.TickResolution);
        ActorMapRegionDefinition[] objectiveRegions =
        [
            new(
                "low-objective",
                ActorMapRegionDefinition.RegionKind.Objective,
                [new Position(2, 2)]),
            new(
                "centre-objective",
                ActorMapRegionDefinition.RegionKind.Objective,
                [new Position(3, 3)]),
            new(
                "high-objective",
                ActorMapRegionDefinition.RegionKind.Objective,
                [new Position(4, 2)]),
        ];
        var map = new ActorMapDefinition(
            source.Map.Id,
            source.Map.Version,
            source.Map.TileRows,
            source.Map.SpawnAnchors,
            [
                .. source.Map.Regions,
                .. objectiveRegions,
            ],
            source.Map.TileTags);
        var binding = new FrontlineActorModeMapBindingDefinition(
            [
                "low-objective",
                "centre-objective",
                "high-objective",
            ],
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
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            binding,
            source.CapabilityVersions);
    }
}
