using System.Collections.Immutable;
using BotArena.Runtime;
using Sdk = BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class GenericDeathmatchSessionTests
{
    [Theory]
    [InlineData("head-to-head", 2, 0)]
    [InlineData("free-for-all", 4, 0)]
    [InlineData("teams", 4, 1)]
    public void RunsHeadToHeadFreeForAllAndTeamsFromTheSameWorld(
        string formatName,
        int expectedActors,
        int expectedAllies)
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                formatName,
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 1,
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
            matchSeed: 17);

        GenericDeathmatchTickStart tickStart = session.PrepareTick();

        Assert.Equal(expectedActors, tickStart.Observations.Length);
        Assert.All(
            tickStart.Observations,
            observation =>
            {
                Assert.Equal(expectedAllies, observation.Allies.Length);
                Assert.Equal(
                    definition.Topology.Participants.Length,
                    observation.Participants.Length);
                Assert.Equal(
                    definition.Topology.UnitSlots.Count(slot =>
                        slot.TeamId
                        == observation.Self.ActorId.TeamId),
                    observation.TeamUnits.Length);
                Assert.Equal(
                    definition.Topology.Teams.Length,
                    observation.Scoreboard.Teams.Length);
                Assert.IsType<
                    GenericActorRuntimeObservation.ModeObservationState
                        .Deathmatch>(observation.Mode);
            });

        GenericDeathmatchResult result = session.Run();

        Assert.Equal(GenericDeathmatchEndReason.MaxTicks, result.Reason);
        Assert.Null(result.Standings.WinnerTeamId);
        Assert.All(
            result.Standings.Standings,
            standing =>
            {
                Assert.Equal(1, standing.Rank);
                Assert.Equal(TeamStandingOutcome.Draw, standing.Outcome);
            });
    }

    [Fact]
    public void ParticipantAndObservationEnumerationOrderDoNotChangeAWorld()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        static GenericActorRuntimeDecision Decide(
            GenericActorRuntimeStart start,
            GenericActorRuntimeObservation observation) =>
            observation.Tick switch
            {
                0 => GenericDeathmatchSessionTestFixture.Move(
                    start.ActorId.TeamId == 0
                        ? Direction.East
                        : Direction.West),
                1 => GenericDeathmatchSessionTestFixture.Shoot(),
                _ => GenericDeathmatchSessionTestFixture.Wait(),
            };
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factoriesA =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                Decide);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factoriesB =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                Decide);
        using var canonical = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factoriesA),
            matchSeed: 991);
        using var reversed = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factoriesB,
                reverse: true),
            matchSeed: 991);

        while (!canonical.IsCompleted)
        {
            GenericDeathmatchTickStart canonicalStart =
                canonical.PrepareTick();
            GenericDeathmatchTickStart reversedStart =
                reversed.PrepareTick();
            GenericDeathmatchStepResult canonicalStep =
                canonical.Step(canonicalStart.Observations);
            GenericDeathmatchStepResult reversedStep =
                reversed.Step(reversedStart.Observations.Reverse());

            Assert.Equal(StateKey(canonical), StateKey(reversed));
            Assert.Equal(
                StepKey(canonicalStep),
                StepKey(reversedStep));
        }

        Assert.True(reversed.IsCompleted);
        Assert.Equal(
            canonical.Result!.Standings.WinnerTeamId,
            reversed.Result!.Standings.WinnerTeamId);
    }

    [Fact]
    public void KillLimitWinsOverTimeoutOnTheFinalAllowedTick()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                    KillsToWin = 1,
                    MaxHealth = 1,
                    DamagePerHit = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 1);

        GenericDeathmatchResult result = session.Run();

        Assert.Equal(GenericDeathmatchEndReason.KillLimit, result.Reason);
        Assert.Equal(1, result.EndTick);
        Assert.Equal(0, result.Standings.WinnerTeamId);
        Assert.Equal(
            1,
            result.Scores.Teams.Single(score => score.TeamId == 0).Kills);
    }

    [Fact]
    public void TimeoutCanProduceAnExactMultiTeamTie()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "free-for-all",
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
                factories),
            matchSeed: 2);

        GenericDeathmatchResult result = session.Run();

        Assert.Equal(GenericDeathmatchEndReason.MaxTicks, result.Reason);
        Assert.Null(result.Standings.WinnerTeamId);
        Assert.Equal(
            [1, 1, 1, 1],
            result.Standings.Standings
                .Select(standing => standing.Rank)
                .ToArray());
    }

    [Fact]
    public void AutomaticReturnUsesANewLifeRuntimeIdentityAndSeed()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 5,
                    MaxHealth = 1,
                    DamagePerHit = 1,
                    RespawnDelayTicks = 0,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10 && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Shoot()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 42);

        session.PrepareTick();
        session.Step();
        session.PrepareTick();
        GenericDeathmatchStepResult lethalStep = session.Step();
        Assert.Contains(
            lethalStep.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.Destruction);

        GenericDeathmatchTickStart returnStart = session.PrepareTick();
        Assert.Contains(
            returnStart.TickStartEvents,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned);
        session.Step();

        GenericActorRuntimeStart[] starts = factories[20].Starts.ToArray();
        Assert.Equal(2, starts.Length);
        Assert.Equal(new ActorIdentity(1, 0, 0), starts[0].ActorId);
        Assert.Equal(new ActorIdentity(1, 0, 1), starts[1].ActorId);
        Assert.NotEqual(starts[0].ActorRandomSeed, starts[1].ActorRandomSeed);
        Assert.Equal(
            GenericActorRuntimeStart.SpawnReason.AutomaticReturn,
            starts[1].Origin.Reason);
        Assert.Equal(starts[0].ActorId, starts[1].Origin.ParentActorId);
        Assert.Equal(0, starts[1].Origin.Generation);
        Assert.Equal(2, factories[20].CreateCount);
        Assert.Equal(1, factories[20].DisposedRuntimeCount);
    }

    [Fact]
    public void ImmediateUnionSharesTeamSensorsWithExactProvenance()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition("teams");
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 3);

        GenericActorRuntimeObservation[] teamZero =
            session.PrepareTick().Observations
                .Where(observation =>
                    observation.Self.ActorId.TeamId == 0)
                .ToArray();
        ActorIdentity[] sensors = teamZero
            .Select(observation => observation.Self.ActorId)
            .Order()
            .ToArray();

        Assert.Equal(2, teamZero.Length);
        Assert.All(teamZero, observation =>
        {
            Assert.Single(observation.Allies);
            Assert.Equal(2, observation.Enemies.Length);
            Assert.All(
                observation.Enemies,
                enemy => Assert.Equal(sensors, enemy.ObservedBy.ToArray()));
        });
    }

    [Fact]
    public void CatalogMovementRotationAttackCooldownAndEnergyAreResolved()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 4,
                    CooldownTicks = 2,
                    MaxEnergy = 3,
                    AttackEnergyCost = 2,
                    EnergyRegenerationIntervalTicks = 2,
                    EnergyRegenerationAmount = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId != 10
                        ? GenericDeathmatchSessionTestFixture.Wait()
                        : observation.Tick switch
                        {
                            0 => GenericDeathmatchSessionTestFixture.Rotate(
                                Direction.North),
                            1 => GenericDeathmatchSessionTestFixture.Move(
                                Direction.East),
                            2 => GenericDeathmatchSessionTestFixture.Shoot(),
                            _ => GenericDeathmatchSessionTestFixture.Wait(),
                        });
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 4);

        GenericDeathmatchStepResult rotate = Resolve(session);
        Assert.Contains(
            rotate.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.Rotation);
        GenericDeathmatchStepResult move = Resolve(session);
        Assert.Contains(
            move.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.Movement);
        GenericDeathmatchStepResult attack = Resolve(session);
        Assert.Contains(
            attack.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.Attack);

        GenericDeathmatchLifeSnapshot afterAttack =
            session.ActiveLives.Single(life => life.ParticipantId == 10);
        Assert.Equal(new Position(2, 3), afterAttack.Position);
        Assert.Equal(Direction.North, afterAttack.Facing);
        Assert.Equal(2, afterAttack.Cooldown);
        Assert.Equal(1, afterAttack.Energy);

        GenericDeathmatchStepResult final = Resolve(session);
        GenericDeathmatchLifeSnapshot afterFinalTick =
            session.ActiveLives.Single(life => life.ParticipantId == 10);
        Assert.True(final.IsCompleted);
        Assert.Equal(1, afterFinalTick.Cooldown);
        Assert.Equal(2, afterFinalTick.Energy);
    }

    [Fact]
    public void BlockedActionMapsIntoTheSdkOnTheNextTickUnchanged()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 4,
                    CooldownTicks = 2,
                });
        var sdkBot = new RepeatingShootSdkBot();
        var opponentFactory =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (_, _) => GenericDeathmatchSessionTestFixture.Wait());
        ImmutableArray<GenericActorParticipantConfiguration> participants =
        [
            new()
            {
                ParticipantId = 10,
                TeamId = 0,
                Name = "sdk-attacker",
                ArtifactHash = "fixture-sdk-attacker",
                RuntimeFactory =
                    new InProcessGenericActorRuntimeFactory(() => sdkBot),
            },
            new()
            {
                ParticipantId = 20,
                TeamId = 1,
                Name = "recording-opponent",
                ArtifactHash = "fixture-recording-opponent",
                RuntimeFactory = opponentFactory,
            },
        ];
        using var session = new GenericDeathmatchSession(
            definition,
            participants,
            matchSeed: 404);

        GenericDeathmatchStepResult successful = Resolve(session);
        GenericDeathmatchStepResult blocked = Resolve(session);
        GenericDeathmatchStepResult mapped = Resolve(session);

        Assert.Empty(successful.RuntimeTick.Faults);
        Assert.Empty(blocked.RuntimeTick.Faults);
        Assert.Empty(mapped.RuntimeTick.Faults);
        GenericActorRuntimeActionResolution resolution = blocked
            .ActionResolutions.Single(value =>
                value.ParticipantId == 10).Resolution;
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            resolution.Outcome);
        Assert.Equal("shoot", resolution.SubmittedAction!.ActionId);
        Assert.Equal("shoot", resolution.AcceptedAction.ActionId);
        Assert.Equal("shoot", resolution.ValidatedAction.ActionId);
        Assert.True(
            resolution.SubmittedAction.Arguments.SequenceEqual(
                resolution.ValidatedAction.Arguments));

        Assert.Equal(3, sdkBot.Contexts.Count);
        Sdk.GenericActorActionResolution sdkResolution =
            sdkBot.Contexts[2].Self.PreviousActionResolution!;
        Assert.Equal(
            Sdk.GenericActorActionResolution.ActionOutcome.Blocked,
            sdkResolution.Outcome);
        Assert.Equal("shoot", sdkResolution.SubmittedAction!.ActionId);
        Assert.Equal("shoot", sdkResolution.AcceptedAction.ActionId);
        Assert.Equal("shoot", sdkResolution.ValidatedAction.ActionId);
    }

    [Fact]
    public void SplitRetiresSourceAndStartsIndependentDescendantRuntimes()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 4,
                    MaxHealth = 4,
                    IncludeSplit = true,
                });
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
                            == GenericActorRuntimeStart.SpawnReason.Initial
                        && observation.Tick == 0)
                    {
                        return GenericDeathmatchSessionTestFixture.Split();
                    }
                    if (start.Origin.Reason
                        == GenericActorRuntimeStart.SpawnReason.Replication)
                    {
                        return GenericDeathmatchSessionTestFixture.Move(
                            start.ActorId.UnitId == 0
                                ? Direction.North
                                : Direction.South);
                    }
                    return GenericDeathmatchSessionTestFixture.Wait();
                });
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 5);

        GenericDeathmatchStepResult queued = Resolve(session);
        Assert.Contains(
            queued.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued);
        Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.ReplicationPending>(
            session.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1).State);

        GenericDeathmatchTickStart completed = session.PrepareTick();
        Assert.Equal(
            2,
            completed.TickStartEvents.Count(value =>
                value.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned));
        Assert.Contains(
            completed.TickStartEvents,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.LifeRetired);
        Assert.Contains(
            completed.TickStartEvents,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleCompleted);
        GenericDeathmatchLifeSnapshot[] beforeActing = session.ActiveLives
            .Where(life => life.ActorId.TeamId == 0)
            .OrderBy(life => life.ActorId)
            .ToArray();
        Assert.Equal(
            [new ActorIdentity(0, 0, 1), new ActorIdentity(0, 1, 0)],
            beforeActing.Select(life => life.ActorId).ToArray());
        Assert.All(beforeActing, life =>
        {
            Assert.Equal(1, life.Generation);
            Assert.Equal("child", life.FormId);
            Assert.Equal(2, life.Health);
        });

        session.Step(completed.Observations);

        GenericActorRuntimeStart[] descendantStarts = factories[10].Starts
            .Where(start =>
                start.Origin.Reason
                == GenericActorRuntimeStart.SpawnReason.Replication)
            .OrderBy(start => start.ActorId)
            .ToArray();
        Assert.Equal(2, descendantStarts.Length);
        Assert.All(descendantStarts, start =>
        {
            Assert.Equal(
                new ActorIdentity(0, 0, 0),
                start.Origin.ParentActorId);
            Assert.Equal("split-mobile", start.Origin.SourceTransitionId);
            Assert.NotNull(start.Origin.SourceOperationId);
        });
        Assert.NotEqual(
            descendantStarts[0].ActorRandomSeed,
            descendantStarts[1].ActorRandomSeed);
        GenericDeathmatchLifeSnapshot[] afterActing = session.ActiveLives
            .Where(life => life.ActorId.TeamId == 0)
            .OrderBy(life => life.ActorId)
            .ToArray();
        Assert.Equal(new Position(1, 1), afterActing[0].Position);
        Assert.Equal(new Position(1, 5), afterActing[1].Position);
        Assert.All(
            afterActing,
            life => Assert.Equal(
                GenericActorRuntimeActionResolution.ActionOutcome.Success,
                life.PreviousActionResolution!.Outcome));
        Assert.Equal(3, factories[10].CreateCount);
        Assert.Equal(1, factories[10].DisposedRuntimeCount);
    }

    [Fact]
    public void SplitClaimsBlockMovementForTheWholeWindup()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.DefinitionWithSplitMover(
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 4,
                    MaxHealth = 4,
                    IncludeSplit = true,
                    SplitDurationTicks = 2,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ActorId == new ActorIdentity(0, 0, 0)
                        && observation.Tick == 0)
                    {
                        return GenericDeathmatchSessionTestFixture.Split();
                    }
                    if (start.ActorId == new ActorIdentity(1, 1, 0)
                        && observation.Tick == 1)
                    {
                        return GenericDeathmatchSessionTestFixture.Move(
                            Direction.West);
                    }
                    return GenericDeathmatchSessionTestFixture.Wait();
                });
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 505);

        Resolve(session);
        GenericDeathmatchStepResult contested = Resolve(session);

        GenericDeathmatchActorResolution mover = contested.ActionResolutions
            .Single(value =>
                value.ActorId == new ActorIdentity(1, 1, 0));
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            mover.Resolution.Outcome);
        Assert.Equal(
            new Position(2, 2),
            session.ActiveLives.Single(life =>
                life.ActorId == mover.ActorId).Position);
        Assert.Contains(
            contested.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.MovementBlocked);

        GenericDeathmatchTickStart completed = session.PrepareTick();

        Assert.Equal(
            2,
            session.ActiveLives.Count(life =>
                life.ActorId.TeamId == 0));
        Assert.Equal(
            2,
            completed.TickStartEvents.Count(value =>
                value.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned));
    }

    [Fact]
    public void LethalDamageCancelsAQueuedSplitBeforeCompletion()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 2,
                    DamagePerHit = 2,
                    IncludeSplit = true,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 20 && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Shoot()
                        : start.ParticipantId == 10 && observation.Tick == 1
                            ? GenericDeathmatchSessionTestFixture.Split()
                            : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 6);

        Resolve(session);
        GenericDeathmatchStepResult lethal = Resolve(session);

        Assert.Contains(
            lethal.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued);
        Assert.Contains(
            lethal.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleCancelled);
        Assert.Contains(
            lethal.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.Destruction);
        Assert.IsType<GenericActorRuntimeObservation.UnitSlotState.Ready>(
            session.Slots.Single(slot =>
                slot.TeamId == 0 && slot.UnitId == 1).State);

        GenericDeathmatchTickStart next = session.PrepareTick();
        Assert.DoesNotContain(
            next.TickStartEvents,
            value => value.Kind
                    == GenericActorRuntimeObservation.EventKind.LifeSpawned
                && value.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.Replication);
        Assert.Single(
            session.ActiveLives.Where(life =>
                life.ActorId.TeamId == 0));
    }

    [Fact]
    public void ATeamRemainsEligibleUntilItsLastParticipantIsDisqualified()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "teams",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 4,
                    FaultsAllowedBeforeDisqualification = 0,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        factories[10] =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (_, _) => GenericDeathmatchSessionTestFixture.Unknown());
        factories[11] =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (_, observation) => observation.Tick >= 1
                    ? GenericDeathmatchSessionTestFixture.Unknown()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 7);

        GenericDeathmatchStepResult firstDisqualification = Resolve(session);

        Assert.False(firstDisqualification.IsCompleted);
        Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState.PermanentlyDormant>(
            session.Slots.Single(slot =>
                slot.TeamId == 0 && slot.ParticipantId == 10).State);
        Assert.Contains(
            session.ActiveLives,
            life => life.ParticipantId == 11);

        GenericDeathmatchStepResult final = Resolve(session);

        Assert.True(final.IsCompleted);
        Assert.Equal(
            GenericDeathmatchEndReason.FaultEligibility,
            final.Result!.Reason);
        Assert.Equal(1, final.Result.Standings.WinnerTeamId);
        Assert.All(
            session.Slots.Where(slot => slot.TeamId == 0),
            slot => Assert.IsType<
                GenericActorRuntimeObservation.UnitSlotState
                    .PermanentlyDormant>(slot.State));
    }

    [Fact]
    public void FaultEligibilityShortCircuitsResourcesAndModeScoring()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    FaultsAllowedBeforeDisqualification = 0,
                    CooldownTicks = 2,
                    MaxEnergy = 3,
                    AttackEnergyCost = 2,
                    EnergyRegenerationIntervalTicks = 2,
                    EnergyRegenerationAmount = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 20
                            && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Shoot()
                        : start.ParticipantId == 10
                            && observation.Tick == 1
                            ? GenericDeathmatchSessionTestFixture.Unknown()
                            : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 701);

        Resolve(session);
        GenericDeathmatchLifeSnapshot beforeFault =
            session.ActiveLives.Single(life => life.ParticipantId == 20);
        Assert.Equal(2, beforeFault.Cooldown);
        Assert.Equal(1, beforeFault.Energy);

        GenericDeathmatchStepResult terminal = Resolve(session);

        Assert.True(terminal.IsCompleted);
        Assert.Equal(
            GenericDeathmatchEndReason.FaultEligibility,
            terminal.Result!.Reason);
        Assert.Contains(
            terminal.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.Damage);
        Assert.Equal(
            0,
            terminal.Scores.Teams
                .Single(score => score.TeamId == 1)
                .DamageDealt);
        Assert.DoesNotContain(
            terminal.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.ScoreChanged);
        GenericDeathmatchLifeSnapshot afterFault =
            session.ActiveLives.Single(life => life.ParticipantId == 20);
        Assert.Equal(2, afterFault.Cooldown);
        Assert.Equal(1, afterFault.Energy);
    }

    [Fact]
    public void SplitCompletionPurgesProjectilesFromEveryOutputTile()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSplitProjectileOccupants(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                        MaxHealth = 4,
                        IncludeSplit = true,
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
                        : start.ActorId.TeamId == 1
                            && start.ActorId.UnitId > 0
                            && observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Shoot()
                            : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 702);

        Resolve(session);

        Assert.Equal(
            [new Position(1, 2), new Position(1, 4)],
            session.Projectiles
                .Select(projectile => projectile.Position)
                .OrderBy(position => position.Y)
                .ToArray());

        session.PrepareTick();

        Assert.Empty(session.Projectiles);
        Assert.Equal(
            [new Position(1, 2), new Position(1, 4)],
            session.ActiveLives
                .Where(life => life.ActorId.TeamId == 0)
                .Select(life => life.Position)
                .OrderBy(position => position.Y)
                .ToArray());

        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory>
            reversedFactories =
                GenericDeathmatchSessionTestFixture.Factories(
                    definition,
                    (start, observation) =>
                        start.ActorId == new ActorIdentity(0, 0, 0)
                                && observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Split()
                            : start.ActorId.TeamId == 1
                                && start.ActorId.UnitId > 0
                                && observation.Tick == 0
                                ? GenericDeathmatchSessionTestFixture.Shoot()
                                : GenericDeathmatchSessionTestFixture.Wait());
        using var reversed = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                reversedFactories,
                reverse: true),
            matchSeed: 702);
        GenericDeathmatchTickStart reversedStart = reversed.PrepareTick();
        reversed.Step(reversedStart.Observations.Reverse());
        reversed.PrepareTick();
        Assert.Empty(reversed.Projectiles);
        Assert.Equal(StateKey(session), StateKey(reversed));
    }

    [Fact]
    public void EventAudiencesAreExplicitAndAliasesHaveNoHiddenGaps()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "teams",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    VisionRange = 1,
                    FaultsAllowedBeforeDisqualification = 0,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        factories[10] =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (_, observation) => observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Unknown()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 703);

        GenericDeathmatchTickStart initial = session.PrepareTick();
        GenericActorRuntimeObservation teammateInitial =
            initial.Observations.Single(observation =>
                observation.Self.ActorId == new ActorIdentity(0, 1, 0));
        GenericActorRuntimeObservation opponentInitial =
            initial.Observations.Single(observation =>
                observation.Self.ActorId == new ActorIdentity(1, 0, 0));
        Assert.Equal(
            ["event-0", "event-1"],
            teammateInitial.VisibleEvents
                .Select(value => value.EventHandle)
                .ToArray());
        Assert.Equal(
            ["event-0", "event-1"],
            opponentInitial.VisibleEvents
                .Select(value => value.EventHandle)
                .ToArray());

        session.Step(initial.Observations);
        GenericDeathmatchTickStart next = session.PrepareTick();
        GenericActorRuntimeObservation teammate =
            next.Observations.Single(observation =>
                observation.Self.ActorId == new ActorIdentity(0, 1, 0));
        GenericActorRuntimeObservation opponent =
            next.Observations.Single(observation =>
                observation.Self.ActorId == new ActorIdentity(1, 0, 0));

        Assert.Equal(
            [
                GenericActorRuntimeObservation.EventKind.RuntimeFault,
                GenericActorRuntimeObservation.EventKind
                    .ParticipantDisqualified,
            ],
            teammate.VisibleEvents
                .Select(value => value.Kind)
                .ToArray());
        Assert.Equal(
            ["event-2", "event-3"],
            teammate.VisibleEvents
                .Select(value => value.EventHandle)
                .ToArray());
        Assert.Equal(
            [2, 3],
            teammate.VisibleEvents
                .Select(value => value.SourceOrdinal)
                .ToArray());
        GenericActorRuntimeObservation.ObservedEvent publicFact =
            Assert.Single(opponent.VisibleEvents);
        Assert.Equal(
            GenericActorRuntimeObservation.EventKind
                .ParticipantDisqualified,
            publicFact.Kind);
        Assert.Equal("event-2", publicFact.EventHandle);
        Assert.Equal(2, publicFact.SourceOrdinal);
    }

    [Fact]
    public void EnemyAttackEventsRevealLaunchButNotFutureProgram()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        var curve = new ShotProgram(
            InitialAimOffset: 0,
            BendDirection: 1,
            BendAfterTiles: 2,
            BendEveryTiles: 2,
            BendCount: 1);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10 && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Shoot(curve)
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 7031);

        Resolve(session);
        GenericDeathmatchTickStart next = session.PrepareTick();
        GenericActorRuntimeObservation owner = next.Observations.Single(
            observation => observation.Self.ActorId.TeamId == 0);
        GenericActorRuntimeObservation opponent = next.Observations.Single(
            observation => observation.Self.ActorId.TeamId == 1);
        var ownerAttack = Assert.IsType<
            GenericActorRuntimeObservation.EventPayload.Attack>(
                owner.VisibleEvents.Single(value => value.Kind
                    == GenericActorRuntimeObservation.EventKind.Attack)
                    .Payload);
        var enemyAttack = Assert.IsType<
            GenericActorRuntimeObservation.EventPayload.Attack>(
                opponent.VisibleEvents.Single(value => value.Kind
                    == GenericActorRuntimeObservation.EventKind.Attack)
                    .Payload);

        Assert.Single(ownerAttack.Action.Arguments);
        Assert.Empty(enemyAttack.Action.Arguments);
        Assert.Equal(ownerAttack.Heading, enemyAttack.Heading);
        Assert.Equal(ownerAttack.Origin, enemyAttack.Origin);
    }

    [Fact]
    public void EnemySplitSpawnDoesNotRevealAnUnseenParentThroughItsOperation()
    {
        var parent = new ActorIdentity(0, 0, 7);
        var payload =
            new GenericActorRuntimeObservation.EventPayload.LifeSpawned(
                new ActorIdentity(0, 1, 0),
                ParticipantId: 10,
                parent,
                Generation: 1,
                FormId: "child",
                Health: 1,
                new Position(2, 2),
                GenericActorRuntimeStart.SpawnReason.Replication,
                SourceTransitionId: "split-mobile",
                SourceOperationId: "split:4:0:0:7");

        GenericActorRuntimeObservation.EventPayload.LifeSpawned hidden =
            GenericDeathmatchSession.RedactLifeSpawned(
                payload,
                observingTeamId: 1,
                visibleEnemyIds: new HashSet<ActorIdentity>());
        GenericActorRuntimeObservation.EventPayload.LifeSpawned visible =
            GenericDeathmatchSession.RedactLifeSpawned(
                payload,
                observingTeamId: 1,
                visibleEnemyIds: new HashSet<ActorIdentity> { parent });

        Assert.Null(hidden.ParentActorId);
        Assert.Null(hidden.SourceOperationId);
        Assert.Equal(parent, visible.ParentActorId);
        Assert.Equal(payload.SourceOperationId, visible.SourceOperationId);
    }

    [Fact]
    public void HiddenSplitLifecycleEventsDoNotBecomeGlobalFacts()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 4,
                    VisionRange = 1,
                    IncludeSplit = true,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10
                            && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 704);

        GenericDeathmatchTickStart initial = session.PrepareTick();
        GenericActorRuntimeObservation opponentInitial =
            initial.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 1);
        Assert.Equal(
            ["event-0"],
            opponentInitial.VisibleEvents
                .Select(value => value.EventHandle)
                .ToArray());
        session.Step(initial.Observations);

        GenericDeathmatchTickStart completed = session.PrepareTick();
        GenericActorRuntimeObservation opponent =
            completed.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 1);
        Assert.Empty(opponent.VisibleEvents);
        GenericActorRuntimeObservation descendant =
            completed.Observations.First(observation =>
                observation.Self.ActorId.TeamId == 0);
        Assert.Contains(
            descendant.VisibleEvents,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued);
        Assert.Contains(
            descendant.VisibleEvents,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleCompleted);
        Assert.Equal(
            Enumerable.Range(1, descendant.VisibleEvents.Length)
                .Select(index => $"event-{index}")
                .ToArray(),
            descendant.VisibleEvents
                .Select(value => value.EventHandle)
                .ToArray());
    }

    [Fact]
    public void MovementVisibilityUsesDestinationForEntryAndExit()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithVisibilityBoundary();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 20
                        ? observation.Tick switch
                        {
                            0 => GenericDeathmatchSessionTestFixture.Move(
                                Direction.West),
                            1 => GenericDeathmatchSessionTestFixture.Move(
                                Direction.East),
                            _ => GenericDeathmatchSessionTestFixture.Wait(),
                        }
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 7041);

        Resolve(session);
        GenericActorRuntimeObservation afterEntry = session
            .PrepareTick()
            .Observations
            .Single(observation =>
                observation.Self.ActorId.TeamId == 0);
        GenericActorRuntimeObservation.ObservedEvent entry =
            Assert.Single(afterEntry.VisibleEvents.Where(value =>
                value.Kind
                    == GenericActorRuntimeObservation.EventKind.Movement));
        var entered = Assert.IsType<
            GenericActorRuntimeObservation.EventPayload.Movement>(
                entry.Payload);
        Assert.Equal(new Position(4, 3), entered.To);

        session.Step();
        GenericActorRuntimeObservation afterExit = session
            .PrepareTick()
            .Observations
            .Single(observation =>
                observation.Self.ActorId.TeamId == 0);

        Assert.DoesNotContain(
            afterExit.VisibleEvents,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind.Movement);
    }

    [Fact]
    public void DisqualificationCancelsClocksThenBundlesThenLives()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithDisqualificationWork(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 6,
                        IncludeSplit = true,
                        FaultsAllowedBeforeDisqualification = 0,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) =>
                    start.ActorId == new ActorIdentity(0, 0, 0)
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : start.ActorId == new ActorIdentity(0, 3, 0)
                            ? GenericDeathmatchSessionTestFixture.Unknown()
                            : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 705);

        GenericDeathmatchStepResult terminal = Resolve(session);

        GenericActorRuntimeObservation.ObservedEvent clock =
            terminal.Events.Single(value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled);
        var payload = Assert.IsType<
            GenericActorRuntimeObservation.EventPayload
                .LifecycleClockCancelled>(clock.Payload);
        Assert.Equal(0, payload.TargetTeamId);
        Assert.Equal(2, payload.TargetUnitId);
        var pending = Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState
                .AvailabilityPending>(payload.CancelledState);
        Assert.Equal(
            GenericActorRuntimeObservation.AvailabilityReason.InitialUnlock,
            pending.Reason);
        Assert.Equal(4, pending.DueTick);

        int clockIndex = terminal.Events.IndexOf(clock);
        int bundleIndex = EventIndex(
            terminal,
            GenericActorRuntimeObservation.EventKind.LifecycleCancelled);
        int retirementIndex = EventIndex(
            terminal,
            GenericActorRuntimeObservation.EventKind.LifeRetired);
        Assert.True(clockIndex < bundleIndex);
        Assert.True(bundleIndex < retirementIndex);
        Assert.All(
            session.Slots.Where(slot => slot.ParticipantId == 10),
            slot => Assert.IsType<
                GenericActorRuntimeObservation.UnitSlotState
                    .PermanentlyDormant>(slot.State));
    }

    [Fact]
    public void SimultaneousDisqualificationsUseOneGlobalCancellationOrder()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithDisqualificationWork(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 6,
                        IncludeSplit = true,
                        FaultsAllowedBeforeDisqualification = 0,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) =>
                    start.ActorId.UnitId == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : start.ActorId.UnitId == 3
                            ? GenericDeathmatchSessionTestFixture.Unknown()
                            : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories,
                reverse: true),
            matchSeed: 7051);

        GenericDeathmatchTickStart start = session.PrepareTick();
        GenericDeathmatchStepResult terminal =
            session.Step(start.Observations.Reverse());

        GenericActorRuntimeObservation.EventPayload
            .LifecycleClockCancelled[] clocks = terminal.Events
                .Where(value => value.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleClockCancelled)
                .Select(value => Assert.IsType<
                    GenericActorRuntimeObservation.EventPayload
                        .LifecycleClockCancelled>(value.Payload))
                .ToArray();
        Assert.Equal(
            [(0, 2), (1, 2)],
            clocks.Select(value =>
                (value.TargetTeamId, value.TargetUnitId)).ToArray());

        GenericActorRuntimeObservation.EventPayload.Lifecycle[] bundles =
            terminal.Events
                .Where(value => value.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled)
                .Select(value => Assert.IsType<
                    GenericActorRuntimeObservation.EventPayload.Lifecycle>(
                        value.Payload))
                .ToArray();
        Assert.Equal(
            [new ActorIdentity(0, 0, 0), new ActorIdentity(1, 0, 0)],
            bundles.Select(value => value.SourceActorId).ToArray());

        int lastClock = terminal.Events
            .Select((value, index) => (value, index))
            .Where(item => item.value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled)
            .Max(item => item.index);
        int firstBundle = terminal.Events
            .Select((value, index) => (value, index))
            .Where(item => item.value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleCancelled)
            .Min(item => item.index);
        int lastBundle = terminal.Events
            .Select((value, index) => (value, index))
            .Where(item => item.value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleCancelled)
            .Max(item => item.index);
        int firstRetirement = terminal.Events
            .Select((value, index) => (value, index))
            .Where(item => item.value.Kind
                == GenericActorRuntimeObservation.EventKind.LifeRetired)
            .Min(item => item.index);
        Assert.True(lastClock < firstBundle);
        Assert.True(lastBundle < firstRetirement);
        Assert.Equal(
            GenericDeathmatchEndReason.FaultEligibility,
            terminal.Result!.Reason);
        Assert.Null(terminal.Result.Standings.WinnerTeamId);
    }

    [Fact]
    public void DisqualificationTruthfullySnapshotsAnAutomaticReturnClock()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithDisqualifiedAutomaticReturn();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 20
                            && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Shoot()
                        : start.ActorId == new ActorIdentity(0, 1, 0)
                            && observation.Tick == 2
                            ? GenericDeathmatchSessionTestFixture.Unknown()
                            : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 706);

        Resolve(session);
        Resolve(session);
        var pendingBeforeFault = Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState
                .AutomaticReturnPending>(
                    session.Slots.Single(slot =>
                        slot.TeamId == 0 && slot.UnitId == 0).State);
        Assert.Equal(4, pendingBeforeFault.DueTick);

        GenericDeathmatchStepResult terminal = Resolve(session);

        var cancellation = Assert.IsType<
            GenericActorRuntimeObservation.EventPayload
                .LifecycleClockCancelled>(
                    terminal.Events.Single(value => value.Kind
                        == GenericActorRuntimeObservation.EventKind
                            .LifecycleClockCancelled).Payload);
        Assert.Equal(0, cancellation.TargetUnitId);
        var cancelledClock = Assert.IsType<
            GenericActorRuntimeObservation.UnitSlotState
                .AutomaticReturnPending>(cancellation.CancelledState);
        Assert.Equal(4, cancelledClock.DueTick);
        Assert.Equal("mobile", cancelledClock.TargetFormId);
        Assert.Equal("participant-disqualified",
            cancellation.CancellationReason);
    }

    [Fact]
    public void KnownAttackOnAFormWithoutAttackCapabilityIsRejected()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.DefinitionWithNoAttackForm(
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 7061);

        GenericDeathmatchStepResult rejected = Resolve(session);

        Assert.Empty(rejected.RuntimeTick.Faults);
        GenericActorRuntimeActionResolution resolution =
            rejected.ActionResolutions.Single(value =>
                value.ParticipantId == 10).Resolution;
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Rejected,
            resolution.Outcome);
        Assert.Equal("shoot", resolution.SubmittedAction!.ActionId);
        Assert.Equal("shoot", resolution.AcceptedAction.ActionId);
        Assert.Equal("shoot", resolution.ValidatedAction.ActionId);
        Assert.Empty(session.Projectiles);
    }

    [Theory]
    [InlineData("death")]
    [InlineData("split")]
    [InlineData("disqualification")]
    public void ThrowingRuntimeDisposalCannotInterruptGameplay(
        string retirementKind)
    {
        GenericDeathmatchSessionTestFixture.Options options =
            retirementKind switch
            {
                "death" => new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 1,
                    DamagePerHit = 1,
                },
                "split" => new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 4,
                    IncludeSplit = true,
                },
                "disqualification" =>
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                        FaultsAllowedBeforeDisqualification = 0,
                    },
                _ => throw new ArgumentOutOfRangeException(
                    nameof(retirementKind)),
            };
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                options);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    retirementKind == "death"
                            && start.ParticipantId == 20
                            && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Shoot()
                        : retirementKind == "split"
                            && start.ParticipantId == 10
                            && observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Split()
                            : retirementKind == "disqualification"
                                && start.ParticipantId == 10
                                && observation.Tick == 0
                                ? GenericDeathmatchSessionTestFixture.Unknown()
                                : GenericDeathmatchSessionTestFixture.Wait());
        factories[10] =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (start, observation) =>
                    retirementKind == "split"
                            && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : retirementKind == "disqualification"
                            && observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Unknown()
                            : GenericDeathmatchSessionTestFixture.Wait(),
                throwOnDispose: true);
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 7062);

        switch (retirementKind)
        {
            case "death":
                Resolve(session);
                GenericDeathmatchStepResult death = Resolve(session);
                Assert.Contains(
                    death.Events,
                    value => value.Kind
                        == GenericActorRuntimeObservation.EventKind
                            .Destruction);
                break;
            case "split":
                Resolve(session);
                GenericDeathmatchTickStart split = session.PrepareTick();
                Assert.Equal(
                    2,
                    split.TickStartEvents.Count(value => value.Kind
                        == GenericActorRuntimeObservation.EventKind
                            .LifeSpawned));
                break;
            case "disqualification":
                GenericDeathmatchStepResult disqualification =
                    Resolve(session);
                Assert.Equal(
                    GenericDeathmatchEndReason.FaultEligibility,
                    disqualification.Result!.Reason);
                break;
        }

        Assert.True(factories[10].DisposedRuntimeCount >= 1);
    }

    [Fact]
    public void RuntimeCallbacksCannotReenterOrDisposeTheSession()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        var failures = new List<Exception>();
        GenericDeathmatchSession? session = null;
        static void Capture(
            ICollection<Exception> failures,
            Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                {
                    if (start.ParticipantId == 10
                        && observation.Tick == 0)
                    {
                        Capture(failures, () => session!.PrepareTick());
                        Capture(failures, () => session!.Step());
                        Capture(failures, () => session!.Run());
                        Capture(failures, () => session!.Dispose());
                        Capture(
                            failures,
                            () => _ = session!.MatchDescriptor);
                        Capture(failures, () => _ = session!.Chronology);
                        Capture(failures, () => _ = session!.ActiveLives);
                        Capture(failures, () => _ = session!.Projectiles);
                        Capture(failures, () => _ = session!.Slots);
                        Capture(failures, () => _ = session!.Scores);
                    }
                    return GenericDeathmatchSessionTestFixture.Wait();
                });
        using (session = new GenericDeathmatchSession(
                   definition,
                   GenericDeathmatchSessionTestFixture.Configurations(
                       definition,
                       factories),
                   matchSeed: 707))
        {
            GenericDeathmatchStepResult first = Resolve(session);

            Assert.False(first.IsCompleted);
            Assert.Equal(10, failures.Count);
            Assert.All(
                failures,
                failure => Assert.IsType<InvalidOperationException>(failure));
            GenericDeathmatchStepResult second = Resolve(session);
            Assert.True(second.IsCompleted);
        }
    }

    [Fact]
    public void UnrepresentableSubmittedActionMapsSafelyOnTheNextSdkTick()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                    FaultsAllowedBeforeDisqualification = 1,
                });
        var sdkBot = new UnknownThenWaitSdkBot();
        var opponentFactory =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (_, _) => GenericDeathmatchSessionTestFixture.Wait());
        ImmutableArray<GenericActorParticipantConfiguration> participants =
        [
            new()
            {
                ParticipantId = 10,
                TeamId = 0,
                Name = "sdk-malformed",
                ArtifactHash = "fixture-sdk-malformed",
                RuntimeFactory =
                    new InProcessGenericActorRuntimeFactory(() => sdkBot),
            },
            new()
            {
                ParticipantId = 20,
                TeamId = 1,
                Name = "recording-opponent",
                ArtifactHash = "fixture-recording-opponent",
                RuntimeFactory = opponentFactory,
            },
        ];
        using var session = new GenericDeathmatchSession(
            definition,
            participants,
            matchSeed: 708);

        GenericDeathmatchStepResult faulted = Resolve(session);
        GenericDeathmatchActorResolution resolution =
            faulted.ActionResolutions.Single(value =>
                value.ParticipantId == 10);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
            resolution.Resolution.Outcome);
        Assert.Null(resolution.Resolution.SubmittedAction);

        GenericDeathmatchStepResult next = Resolve(session);

        Assert.Empty(next.RuntimeTick.Faults);
        Assert.Equal(2, sdkBot.Contexts.Count);
        Sdk.GenericActorActionResolution previous =
            sdkBot.Contexts[1].Self.PreviousActionResolution!;
        Assert.Equal(
            Sdk.GenericActorActionResolution.ActionOutcome.Faulted,
            previous.Outcome);
        Assert.Null(previous.SubmittedAction);
        Assert.Equal("wait", previous.AcceptedAction.ActionId);
        Assert.Equal("wait", previous.ValidatedAction.ActionId);
    }

    [Fact]
    public void SameLifeEndClockKeepsOneRuntimeAndWaitOnlyPendingState()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10
                        ? observation.Tick switch
                        {
                            0 => GenericDeathmatchSessionTestFixture
                                .Transform(),
                            1 => GenericDeathmatchSessionTestFixture.Move(
                                Direction.East),
                            _ => GenericDeathmatchSessionTestFixture.Wait(),
                        }
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 710);

        GenericDeathmatchTickStart start = session.PrepareTick();
        GenericActorRuntimeObservation west = start.Observations.Single(
            observation => observation.Self.ActorId.TeamId == 0);
        GenericActorRuntimeActionLegality transform =
            west.ActionLegalities.Single(value =>
                value.ActionId == "transform");
        Assert.True(transform.AllowedByForm);
        Assert.True(transform.Available);
        Assert.Equal(
            "anchored",
            Assert.Single(
                Assert.IsType<
                        GenericActorRuntimeActionLegality.ArgumentConstraint
                            .FormTargetConstraint>(
                        Assert.Single(transform.Constraints))
                    .AllowedFormIds));

        GenericDeathmatchStepResult queued = session.Step();

        Assert.Contains(
            queued.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted);
        Assert.DoesNotContain(
            queued.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted);
        Assert.Equal(
            "mobile",
            session.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0).FormId);

        GenericDeathmatchTickStart pendingStart = session.PrepareTick();
        GenericActorRuntimeObservation pending =
            pendingStart.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 0);
        Assert.Equal("mobile", pending.Self.FormId);
        Assert.NotNull(pending.Self.PendingSameLifeTransition);
        Assert.True(pending.ActionLegalities.Single(value =>
            value.ActionId == "wait").Available);
        Assert.All(
            pending.ActionLegalities.Where(value =>
                value.ActionId != "wait"),
            value => Assert.False(value.Available));
        GenericActorRuntimeObservation opposing =
            pendingStart.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 1);
        Assert.NotNull(
            opposing.Enemies.Single(enemy =>
                enemy.ActorId.TeamId == 0).PendingSameLifeTransition);

        GenericDeathmatchStepResult completed = session.Step();

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            completed.ActionResolutions.Single(value =>
                value.ActorId.TeamId == 0).Resolution.Outcome);
        Assert.Contains(
            completed.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted);
        Assert.Equal(
            "anchored",
            session.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0).FormId);
        Assert.Equal(1, factories[10].CreateCount);
        Assert.Single(factories[10].Starts);
        Assert.NotNull(
            session.Chronology.Ticks[0].PostState.ActiveLives.Single(
                life => life.ActorId.TeamId == 0)
                .PendingSameLifeTransition);
        Assert.Null(
            session.Chronology.Ticks[1].PostState.ActiveLives.Single(
                life => life.ActorId.TeamId == 0)
                .PendingSameLifeTransition);
    }

    [Fact]
    public void SameLifeTickStartClockCompletesBeforeDueTickObservation()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 1,
                        Completion =
                            ActorTransitionWindupDefinition
                                .ActorTransitionCompletionKind
                                .TickStartAfterDuration,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10 && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Transform()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 711);

        GenericDeathmatchStepResult queued = Resolve(session);
        Assert.Single(queued.Events.Where(value => value.Kind
            == GenericActorRuntimeObservation.EventKind
                .FormTransitionStarted));

        GenericDeathmatchTickStart due = session.PrepareTick();
        GenericActorRuntimeObservation west = due.Observations.Single(
            observation => observation.Self.ActorId.TeamId == 0);

        Assert.Equal("anchored", west.Self.FormId);
        Assert.Null(west.Self.PendingSameLifeTransition);
        Assert.Single(due.TickStartEvents.Where(value => value.Kind
            == GenericActorRuntimeObservation.EventKind
                .FormTransitionCompleted));
        Assert.Equal(1, factories[10].CreateCount);
        Assert.Single(factories[10].Starts);
    }

    [Fact]
    public void DurationOneEndClockStartsAndCompletesInOneResolution()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 1,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 1,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Transform()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 712);

        GenericDeathmatchStepResult step = Resolve(session);
        GenericActorRuntimeObservation.EventKind[] transitionEvents =
            step.Events
                .Where(value => value.Kind is
                    GenericActorRuntimeObservation.EventKind
                        .FormTransitionStarted
                    or GenericActorRuntimeObservation.EventKind
                        .FormTransitionCompleted)
                .Select(value => value.Kind)
                .ToArray();

        Assert.Equal(
            [
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted,
            ],
            transitionEvents);
        Assert.True(step.IsCompleted);
        GenericActorWorldSnapshot.LifeSnapshot life =
            session.Chronology.Ticks.Single().PostState.ActiveLives.Single(
                value => value.ActorId.TeamId == 0);
        Assert.Equal("anchored", life.FormId);
        Assert.Null(life.PendingSameLifeTransition);
        Assert.Equal(1, factories[10].CreateCount);
    }

    [Fact]
    public void LethalDamageDestroysThenCancelsPendingSameLifeTransition()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                        MaxHealth = 1,
                        DamagePerHit = 1,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 3,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) => (start.ParticipantId, observation.Tick)
                    switch
                    {
                        (10, 0) => GenericDeathmatchSessionTestFixture
                            .Transform(),
                        (20, 0) => GenericDeathmatchSessionTestFixture.Shoot(),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    });
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 713);

        Resolve(session);
        GenericDeathmatchStepResult lethal = Resolve(session);

        int destroyed = EventIndex(
            lethal,
            GenericActorRuntimeObservation.EventKind.Destruction);
        int cancelled = EventIndex(
            lethal,
            GenericActorRuntimeObservation.EventKind
                .FormTransitionCancelled);
        Assert.True(destroyed < cancelled);
        Assert.DoesNotContain(
            lethal.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted);
    }

    [Fact]
    public void QueuePlacementBlockKeepsTargetConstraintAndEmitsNoStart()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 1,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        ForbidWestSpawn = true,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Transform()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 714);

        GenericActorRuntimeActionLegality legality =
            session.PrepareTick().Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 0)
                .ActionLegalities.Single(value =>
                    value.ActionId == "transform");
        Assert.False(legality.Available);
        Assert.Equal(
            "anchored",
            Assert.Single(
                Assert.IsType<
                        GenericActorRuntimeActionLegality.ArgumentConstraint
                            .FormTargetConstraint>(
                        Assert.Single(legality.Constraints))
                    .AllowedFormIds));

        GenericDeathmatchStepResult step = session.Step();

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            step.ActionResolutions.Single(value =>
                value.ActorId.TeamId == 0).Resolution.Outcome);
        Assert.DoesNotContain(
            step.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted);
    }

    [Fact]
    public void TerminalFutureDueTransitionRemainsPending()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 1,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 3,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionSessionFactories(definition);
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 715);

        GenericDeathmatchStepResult terminal = Resolve(session);

        Assert.True(terminal.IsCompleted);
        GenericActorWorldSnapshot.LifeSnapshot west =
            session.Chronology.Ticks.Single().PostState.ActiveLives.Single(
                life => life.ActorId.TeamId == 0);
        Assert.Equal("mobile", west.FormId);
        Assert.NotNull(west.PendingSameLifeTransition);
        Assert.DoesNotContain(
            terminal.Events,
            value => value.Kind is
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted
                or GenericActorRuntimeObservation.EventKind
                    .FormTransitionCancelled);
    }

    [Fact]
    public void IrreversibleTransitionBlocksALaterReturnRouteForThatLife()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 2,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 1,
                        IncludeReverseRoute = true,
                        IrreversibleForLife = true,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10
                        ? observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Transform()
                            : GenericDeathmatchSessionTestFixture.Transform(
                                "mobile")
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 716);

        Resolve(session);
        GenericDeathmatchTickStart reverseStart = session.PrepareTick();
        GenericActorRuntimeActionLegality reverseLegality =
            reverseStart.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 0)
                .ActionLegalities.Single(value =>
                    value.ActionId == "transform");
        Assert.True(reverseLegality.AllowedByForm);
        Assert.False(reverseLegality.Available);
        Assert.Equal(
            "mobile",
            Assert.Single(
                Assert.IsType<
                        GenericActorRuntimeActionLegality.ArgumentConstraint
                            .FormTargetConstraint>(
                        Assert.Single(reverseLegality.Constraints))
                    .AllowedFormIds));

        GenericDeathmatchStepResult blocked = session.Step();

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            blocked.ActionResolutions.Single(value =>
                value.ActorId.TeamId == 0).Resolution.Outcome);
        Assert.Equal(
            "anchored",
            session.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0).FormId);
        Assert.DoesNotContain(
            blocked.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted);
    }

    [Theory]
    [InlineData(true, 2, 1)]
    [InlineData(false, null, 2)]
    public void SameLifeCompletionPreservesCooldownAndNormalizesEnergy(
        bool targetHasAttack,
        int? expectedEnergy,
        int cooldownAfterTargetWait)
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 4,
                        CooldownTicks = 3,
                        MaxEnergy = 5,
                        AttackEnergyCost = 1,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 1,
                        TargetHasAttack = targetHasAttack,
                        TargetMaxEnergy = targetHasAttack ? 2 : null,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10
                        ? observation.Tick switch
                        {
                            0 => GenericDeathmatchSessionTestFixture.Shoot(),
                            1 => GenericDeathmatchSessionTestFixture
                                .Transform(),
                            _ => GenericDeathmatchSessionTestFixture.Wait(),
                        }
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 717);

        Resolve(session);
        Resolve(session);
        GenericDeathmatchLifeSnapshot completed =
            session.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0);
        Assert.Equal(2, completed.Cooldown);
        Assert.Equal(expectedEnergy, completed.Energy);

        Resolve(session);
        GenericDeathmatchLifeSnapshot afterWait =
            session.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0);
        Assert.Equal(cooldownAfterTargetWait, afterWait.Cooldown);
        Assert.Equal(expectedEnergy, afterWait.Energy);
    }

    [Fact]
    public void DisqualificationCancelsPendingSameLifeWorkBeforeRetirement()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                        FaultsAllowedBeforeDisqualification = 0,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 3,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10
                        ? observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Transform()
                            : GenericDeathmatchSessionTestFixture.Unknown()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 718);

        Resolve(session);
        GenericDeathmatchStepResult disqualified = Resolve(session);

        Assert.True(disqualified.IsCompleted);
        int cancelled = EventIndex(
            disqualified,
            GenericActorRuntimeObservation.EventKind
                .FormTransitionCancelled);
        int retired = EventIndex(
            disqualified,
            GenericActorRuntimeObservation.EventKind.LifeRetired);
        Assert.True(cancelled < retired);
        Assert.DoesNotContain(
            disqualified.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted);
    }

    [Fact]
    public void FaultTerminalSkipsSurvivingTransitionDueInALaterPhase()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                        FaultsAllowedBeforeDisqualification = 0,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 2,
                    });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    (start.ParticipantId, observation.Tick) switch
                    {
                        (20, 0) => GenericDeathmatchSessionTestFixture
                            .Transform(),
                        (10, 1) => GenericDeathmatchSessionTestFixture
                            .Unknown(),
                        _ => GenericDeathmatchSessionTestFixture.Wait(),
                    });
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 719);

        Resolve(session);
        GenericDeathmatchStepResult terminal = Resolve(session);

        Assert.True(terminal.IsCompleted);
        Assert.Equal(
            GenericDeathmatchEndReason.FaultEligibility,
            terminal.Result!.Reason);
        Assert.DoesNotContain(
            terminal.Events,
            value => value.Kind
                == GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted);
        GenericDeathmatchLifeSnapshot survivor =
            Assert.Single(session.ActiveLives);
        Assert.Equal(1, survivor.ActorId.TeamId);
        Assert.Equal("mobile", survivor.FormId);
        Assert.NotNull(
            session.Chronology.Ticks[^1].PostState.ActiveLives
                .Single(life => life.ActorId == survivor.ActorId)
                .PendingSameLifeTransition);
        GenericActorMatchChronology chronology = session.Chronology;
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                chronology.Ticks,
                result: null));
    }

    private static Dictionary<
        int,
        GenericDeathmatchSessionTestFixture.RecordingFactory>
        GenericDeathmatchSessionSessionFactories(
            ActorResolvedMatchDefinition definition) =>
        GenericDeathmatchSessionTestFixture.Factories(
            definition,
            (start, _) => start.ParticipantId == 10
                ? GenericDeathmatchSessionTestFixture.Transform()
                : GenericDeathmatchSessionTestFixture.Wait());

    [Fact]
    public void FabricationTransitionsAreRejectedAtConstruction()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);

        Assert.Throws<NotSupportedException>(() =>
            new GenericDeathmatchSession(
                definition,
                GenericDeathmatchSessionTestFixture.Configurations(
                    definition,
                    factories),
                matchSeed: 8));
    }

    private static GenericDeathmatchStepResult Resolve(
        GenericDeathmatchSession session)
    {
        session.PrepareTick();
        return session.Step();
    }

    private static int EventIndex(
        GenericDeathmatchStepResult step,
        GenericActorRuntimeObservation.EventKind kind) =>
        step.Events
            .Select((value, index) => (value, index))
            .First(item => item.value.Kind == kind)
            .index;

    private static string StateKey(GenericDeathmatchSession session)
    {
        string lives = string.Join(
            "|",
            session.ActiveLives.Select(life =>
                $"{life.ActorId}:{life.Generation}:{life.FormId}:" +
                $"{life.Position}:{life.Facing}:{life.Health}:" +
                $"{life.Cooldown}:{life.Energy}"));
        string projectiles = string.Join(
            "|",
            session.Projectiles.Select(projectile =>
                $"{projectile.ProjectileId}:{projectile.OwnerTeamId}:" +
                $"{projectile.OwnerActorId}:{projectile.Position}:" +
                $"{projectile.Heading}:{projectile.TicksUntilAdvance}:" +
                $"{projectile.RemainingTiles}"));
        string scores = string.Join(
            "|",
            session.Scores.Teams.Select(score =>
                $"{score.TeamId}:{score.Kills}:{score.Deaths}:" +
                $"{score.DamageDealt}"));
        return $"{session.Tick};{lives};{projectiles};{scores}";
    }

    private static string StepKey(GenericDeathmatchStepResult step)
    {
        string resolutions = string.Join(
            "|",
            step.ActionResolutions.Select(resolution =>
                $"{resolution.ParticipantId}:{resolution.ActorId}:" +
                $"{resolution.Resolution.ValidatedAction.ActionId}:" +
                $"{resolution.Resolution.Outcome}"));
        string events = string.Join(
            "|",
            step.Events.Select(value =>
                $"{value.SourceOrdinal}:{value.Kind}:{value.Payload}"));
        return $"{step.Tick};{resolutions};{events};{step.IsCompleted}";
    }

    private sealed class RepeatingShootSdkBot : Sdk.IGenericActorBot
    {
        public List<Sdk.GenericActorContext> Contexts { get; } = [];

        public Sdk.GenericActorDecision Tick(
            Sdk.GenericActorContext context)
        {
            Contexts.Add(context);
            return new Sdk.GenericActorDecision(
                "shoot",
                4,
                [
                    new Sdk.GenericActorActionArgument.ShotProgramArgument(
                        Sdk.ShotProgram.Straight),
                ]);
        }
    }

    private sealed class UnknownThenWaitSdkBot : Sdk.IGenericActorBot
    {
        public List<Sdk.GenericActorContext> Contexts { get; } = [];

        public Sdk.GenericActorDecision Tick(
            Sdk.GenericActorContext context)
        {
            Contexts.Add(context);
            return context.Tick == 0
                ? Sdk.GenericActorDecision.WithoutArguments(
                    "unknown-action",
                    999)
                : Sdk.GenericActorDecision.WithoutArguments("wait", 0);
        }
    }
}
