using BotArena.Engine.Tests.Support;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace BotArena.Engine.Tests;

public sealed class FrontlineActorMatchEngineTests
{
    [Fact]
    public void Run_FreezesActorInputsAndEmitsCanonicalReplayV2()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1);
        var executionLog = new List<string>();
        var teamZero = new RecordingFactory(
            executionLog,
            (_, _) => new ActorDecision
            {
                ActionId = PublicActionIds.Wait,
                Payload = new ActorActionPayload(),
                FaultMessage = "stale-runtime-diagnostic",
            });
        var teamOne = new RecordingFactory(
            executionLog,
            (_, _) => new ActorDecision
            {
                ActionCode = (int)BotAction.Wait,
            });

        FrontlineActorMatchRunResult run = new FrontlineActorMatchEngine().Run(
            Configuration(
                rules,
                Participant(1, 1, "one", teamOne),
                Participant(0, 0, "zero", teamZero)));

        Assert.Equal(FrontlineMatchEndReason.MaxTicks, run.Result.Reason);
        Assert.Equal(run.ReplayHash, ReplayV2Serializer.ComputeHash(run.Replay));
        Assert.Equal(run.ReplayJson, ReplayV2Serializer.ToJson(run.Replay));
        Assert.True(ReplayV2Serializer.VerifyHash(run.ReplayJson));
        Assert.Equal(
            ReplayV2DocumentFormat.EntityV2,
            ReplayV2VersionProbe.Probe(run.ReplayJson));

        ReplayV2Tick tick = Assert.Single(run.Replay.Ticks);
        Assert.Equal(0, tick.Tick);
        Assert.Equal(0, tick.TickStart.State.Control.NextTick);
        Assert.Equal(1, tick.PostState.Control.NextTick);
        Assert.All(
            tick.TickStart.State.Teams,
            team => Assert.NotNull(Assert.Single(team.Units).ActiveLife));
        Assert.Equal(
            [new ReplayV2ActorId(0, 0, 0), new ReplayV2ActorId(1, 0, 0)],
            tick.TickStart.ActiveActors.ToArray());
        Assert.Equal(
            [new ReplayV2ActorId(0, 0, 0), new ReplayV2ActorId(1, 0, 0)],
            tick.Actors.Select(actor => actor.ActorId).ToArray());
        Assert.Equal(
            BotArenaVersions.ActorRuntimeContractVersion,
            run.Replay.Header.ActorRuntime.Version);
        Assert.Equal(
            BotArenaVersions.ActorObservationSchemaVersion,
            run.Replay.Header.ActorRuntime.ObservationSchemaVersion);
        Assert.All(
            tick.Actors,
            actor =>
            {
                Assert.NotNull(actor.LifeStart);
                Assert.Equal(actor.ActorId, actor.LifeStart.ActorId);
                Assert.Equal(
                    PublicActionIds.Wait,
                    actor.AcceptedDecision.ActionId);
                Assert.Equal(
                    (int)BotAction.Wait,
                    actor.AcceptedDecision.ActionCode);
                Assert.Null(actor.AcceptedDecision.Payload);
                Assert.Equal(
                    actor.ActorId,
                    actor.Observation.Self.ActorId);
                Assert.Equal(0, actor.Observation.Tick);
            });
        ReplayV2ActorTurn zero = tick.Actors[0];
        Assert.Equal(PublicActionIds.Wait, zero.RuntimeReply.ActionId);
        Assert.Null(zero.RuntimeReply.ActionCode);
        Assert.Null(zero.RuntimeReply.Payload);
        Assert.False(zero.RuntimeReply.Faulted);
        Assert.Equal(
            "stale-runtime-diagnostic",
            zero.RuntimeReply.FaultMessage);
        Assert.Null(zero.AcceptedDecision.FaultMessage);
        ReplayV2ActorTurn one = tick.Actors[1];
        Assert.Null(one.RuntimeReply.ActionId);
        Assert.Equal((int)BotAction.Wait, one.RuntimeReply.ActionCode);
        Assert.Null(one.RuntimeReply.Payload);

        Assert.Equal(
            ["start:0:0:0", "start:1:0:0", "tick:0:0:0", "tick:1:0:0"],
            executionLog.Take(4).ToArray());
        Assert.Single(teamZero.Runtimes);
        Assert.Single(teamOne.Runtimes);
        Assert.Equal(
            BotArenaVersions.ActorMatchStartSchemaVersion,
            teamZero.Runtimes[0].Start!.SchemaVersion);
        Assert.Equal(
            BotArenaVersions.ActorRuntimeContractVersion,
            teamZero.Runtimes[0].Start!.RuntimeContractVersion);
        Assert.Equal(
            teamZero.Runtimes[0].Start!.ActorRandomSeed.ToString(
                CultureInfo.InvariantCulture),
            tick.Actors[0].LifeStart!.ActorRandomSeed);
        Assert.True(teamZero.Runtimes[0].Disposed);
        Assert.True(teamOne.Runtimes[0].Disposed);
    }

    [Fact]
    public void Run_CreatesFreshMemoryAndSeedForRespawnedLives()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 4,
            primeRespawnTicks: 1,
            shootCooldownTicks: 0) with
        {
            DamagePerHit = 3,
            ProgrammedShotLaunchTiles = 8,
        };
        var executionLog = new List<string>();
        ActorDecision Decide(ActorMatchStart start, ActorObservation _) =>
            start.SpawnReason == ActorSpawnReason.Initial
                ? ActorDecision.Shoot(ShotProgram.Straight)
                : ActorDecision.Wait();
        var teamZero = new RecordingFactory(executionLog, Decide);
        var teamOne = new RecordingFactory(executionLog, Decide);

        FrontlineActorMatchRunResult run = new FrontlineActorMatchEngine().Run(
            Configuration(
                rules,
                Participant(0, 0, "zero", teamZero),
                Participant(1, 1, "one", teamOne)));

        Assert.Equal(4, run.Replay.Ticks.Length);
        Assert.Empty(run.Replay.Ticks[1].Actors);
        Assert.Equal(
            [new ReplayV2ActorId(0, 0, 1), new ReplayV2ActorId(1, 0, 1)],
            run.Replay.Ticks[2].Actors
                .Select(actor => actor.ActorId)
                .ToArray());
        Assert.All(
            run.Replay.Ticks[0].Actors,
            actor => Assert.Equal(
                ActorSpawnReason.Initial,
                Assert.IsType<ReplayV2LifeStart>(
                    actor.LifeStart).SpawnReason));
        Assert.All(
            run.Replay.Ticks[2].Actors,
            actor => Assert.Equal(
                ActorSpawnReason.Respawn,
                Assert.IsType<ReplayV2LifeStart>(
                    actor.LifeStart).SpawnReason));
        Assert.All(
            run.Replay.Ticks[3].Actors,
            actor => Assert.Null(actor.LifeStart));
        Assert.Equal(2, teamZero.Runtimes.Count);
        Assert.Equal(2, teamOne.Runtimes.Count);
        Assert.All(
            teamZero.Runtimes.Concat(teamOne.Runtimes),
            runtime => Assert.True(runtime.Disposed));
        Assert.Equal(
            [ActorSpawnReason.Initial, ActorSpawnReason.Respawn],
            teamZero.Runtimes.Select(runtime => runtime.Start!.SpawnReason));
        Assert.NotEqual(
            teamZero.Runtimes[0].Start!.ActorRandomSeed,
            teamZero.Runtimes[1].Start!.ActorRandomSeed);
        Assert.Contains(
            "dispose:0:0:0",
            executionLog.TakeWhile(entry => entry != "start:0:0:1"));
    }

    [Fact]
    public void Run_TransformPreservesRuntimeMemoryAndReplayShotCausality()
    {
        GameRules baseline = FrontlineTestDefinitions.ReplicationRules(
            maxTicks: 12,
            firstUnlockTick: 1,
            secondUnlockTick: 2,
            shootCooldownTicks: 1);
        GameRules rules = baseline with
        {
            MaxEnergy = 4,
            ShotEnergyCost = 2,
            EnergyRegenTicks = 1,
            Frontline = baseline.Frontline! with
            {
                AnchorWindupTicks = 1,
            },
        };
        ActorDecision Decide(
            ActorMatchStart start,
            ActorObservation observation)
        {
            int teamId = start.ActorId.TeamId;
            int unitId = start.ActorId.UnitId;
            if (unitId == 0)
            {
                return observation.Tick switch
                {
                    1 => ActorDecision.Fabricate(
                        new ObservedUnitTarget(teamId, 1)),
                    _ => ActorDecision.Wait(),
                };
            }
            if (unitId != 1)
                return ActorDecision.Wait();
            return (teamId, observation.Tick) switch
            {
                (0, 2) => ActorDecision.MoveForward(),
                (0, 3) => ActorDecision.TurnRight(),
                (0, 4 or 5 or 6) => ActorDecision.MoveForward(),
                (0, 7) => ActorDecision.TurnLeft(),
                (0, 8) => ActorDecision.MoveForward(),
                (1, 2) => ActorDecision.TurnLeft(),
                (1, 3 or 4 or 5) => ActorDecision.MoveForward(),
                (1, 6) => ActorDecision.TurnRight(),
                (1, 7) => ActorDecision.MoveForward(),
                (_, 9) => ActorDecision.Transform("turret"),
                (_, 10) => ActorDecision.ShootDirection(
                    teamId == 0
                        ? ProjectileHeading.East
                        : ProjectileHeading.West),
                _ => ActorDecision.Wait(),
            };
        }

        var teamZero = new RecordingFactory([], Decide);
        var teamOne = new RecordingFactory([], Decide);
        FrontlineActorMatchRunResult run =
            new FrontlineActorMatchEngine().Run(new()
            {
                Map = FrontlineTestDefinitions.AnchorMapV2(),
                Rules = rules,
                Seed = 42,
                Participants =
                [
                    Participant(0, 0, "zero", teamZero),
                    Participant(1, 1, "one", teamOne),
                ],
            });

        Assert.True(ReplayV2Serializer.VerifyHash(run.ReplayJson));
        Assert.Equal(2, teamZero.Runtimes.Count);
        Assert.Equal(2, teamOne.Runtimes.Count);
        RecordingRuntime childRuntime = teamZero.Runtimes.Single(runtime =>
            runtime.Start!.ActorId.UnitId == 1);
        Assert.Equal(10, childRuntime.ExecutionCount);
        Assert.Equal(
            ActorSpawnReason.Fabrication,
            childRuntime.Start!.SpawnReason);
        ReplayV2ActorTurn firstChildTurn = run.Replay.Ticks[2].Actors
            .Single(actor =>
                actor.ActorId == new ReplayV2ActorId(0, 1, 0));
        Assert.Equal(new Position(1, 3),
            firstChildTurn.Observation.Self.Position);
        Assert.Equal(Direction.East, firstChildTurn.Observation.Self.Facing);
        Assert.Equal(
            PublicActionIds.MoveForward,
            firstChildTurn.ActionResolution.ValidatedActionId);
        Assert.Equal(
            [
                new Position(1, 3),
                new Position(2, 3),
                new Position(2, 3),
                new Position(2, 4),
                new Position(2, 5),
                new Position(2, 6),
                new Position(2, 6),
                new Position(3, 6),
            ],
            run.Replay.Ticks[2..10]
                .Select(tick => tick.Actors.Single(actor =>
                        actor.ActorId == new ReplayV2ActorId(0, 1, 0))
                    .Observation.Self.Position)
                .ToArray());

        ReplayV2Tick transformTick = run.Replay.Ticks[9];
        ReplayV2ActorTurn transformTurn = transformTick.Actors.Single(actor =>
            actor.ActorId == new ReplayV2ActorId(0, 1, 0));
        Assert.Equal(new Position(3, 6),
            transformTurn.Observation.Self.Position);
        Assert.Equal(
            PublicActionIds.Transform,
            transformTurn.ActionResolution.ValidatedActionId);
        ReplayV2Event started = Assert.Single(
            transformTick.Resolution.Events,
            value => value.Type
                == FrontlineMatchEventType.FormTransitionStarted
                && value.SourceActorId == new ReplayV2ActorId(0, 1, 0));
        ReplayV2Event changed = Assert.Single(
            transformTick.Resolution.Events,
            value => value.Type == FrontlineMatchEventType.FormChanged
                && value.SourceActorId == new ReplayV2ActorId(0, 1, 0));
        Assert.Equal(PublicActionIds.Transform, started.ActionId);
        Assert.Equal("turret", changed.ToFormId);
        ReplayV2UnitState transformed = transformTick.PostState.Teams
            .Single(team => team.TeamId == 0).Units
            .Single(unit => unit.UnitId == 1);
        Assert.Equal("child-mobile", transformed.DefaultFormId);
        Assert.Equal("turret", transformed.ActiveLife!.FormId);
        Assert.Equal(new ReplayV2ActorId(0, 1, 0),
            transformed.ActiveLife.ActorId);

        ReplayV2Tick shotTick = run.Replay.Ticks[10];
        ReplayV2Event shot = Assert.Single(
            shotTick.Resolution.Events,
            value => value.Type == FrontlineMatchEventType.Shot
                && value.SourceActorId == new ReplayV2ActorId(0, 1, 0));
        Assert.Equal(PublicActionIds.ShootDirection, shot.ActionId);
        Assert.Equal(ProjectileHeading.East, shot.ProjectileHeading);
        Assert.Equal(
            ProjectileHeading.East,
            shot.ActionPayload!.LaunchHeading);
        ReplayV2ProjectileTraversal traversal = Assert.Single(
            shotTick.Resolution.ProjectileTraversals,
            value => value.ProjectileId == shot.ProjectileId);
        Assert.Equal([shot.To!.Value], traversal.Path.ToArray());
        Assert.Null(traversal.ShotProgram);
        Assert.Null(traversal.ProgrammedPath);
        ReplayV2LifeState firedLife = shotTick.PostState.Teams
            .Single(team => team.TeamId == 0).Units
            .Single(unit => unit.UnitId == 1)
            .ActiveLife!;
        Assert.Equal(1, firedLife.Cooldown);
        Assert.Equal(3, firedLife.Energy);
        Assert.Equal(ActionResult.Success,
            firedLife.PreviousActionResult);
        ReplayV2ObservedEvent observedShot = Assert.Single(
            run.Replay.Ticks[11].Actors
                .Single(actor =>
                    actor.ActorId == new ReplayV2ActorId(0, 1, 0))
                .Observation.VisibleEvents,
            value => value.Type == ObservedMatchEventType.Shot
                && value.AlliedActorId
                    == new ReplayV2ActorId(0, 1, 0));
        Assert.Equal(PublicActionIds.ShootDirection, observedShot.ActionId);
        Assert.Equal(
            PublicActionCodes.ShootDirection,
            observedShot.ActionCode);
        Assert.Equal(ProjectileHeading.East,
            observedShot.ProjectileHeading);
        Assert.Equal(ActionResult.Success, observedShot.ActionResult);

        ReplayV2UnitResult result = run.Replay.Result.Teams
            .Single(team => team.TeamId == 0).Units
            .Single(unit => unit.UnitId == 1);
        Assert.Equal("child-mobile", result.DefaultFormId);
        Assert.Equal("turret", result.FormId);

        var turretActor = new ReplayV2ActorId(0, 1, 0);
        AssertTickMutationRejected(
            run.Replay,
            tickId: 10,
            tick => ReplacePostLife(
                tick,
                turretActor,
                life => life with
                {
                    Cooldown = life.Cooldown + 1,
                }),
            "surviving turret fire");
        AssertTickMutationRejected(
            run.Replay,
            tickId: 10,
            tick => ReplacePostLife(
                tick,
                turretActor,
                life => life with
                {
                    Energy = life.Energy + 1,
                }),
            "surviving turret fire");
        AssertTickMutationRejected(
            run.Replay,
            tickId: 10,
            tick => tick with
            {
                Resolution = tick.Resolution with
                {
                    ProjectileTraversals =
                        tick.Resolution.ProjectileTraversals.Add(
                            traversal with
                            {
                                ProjectileId = "999999",
                            }),
                },
            },
            "persistence");

        ActorDecision DecideBlocked(
            ActorMatchStart start,
            ActorObservation observation) =>
            start.ActorId.TeamId == 0
            && start.ActorId.UnitId == 1
            && observation.Tick == 10
                ? ActorDecision.ShootDirection(
                    ProjectileHeading.NorthWest)
                : Decide(start, observation);
        FrontlineActorMatchRunResult blockedRun =
            new FrontlineActorMatchEngine().Run(new()
            {
                Map = FrontlineTestDefinitions.AnchorMapV2(),
                Rules = rules,
                Seed = 42,
                Participants =
                [
                    Participant(
                        0,
                        0,
                        "zero",
                        new RecordingFactory([], DecideBlocked)),
                    Participant(
                        1,
                        1,
                        "one",
                        new RecordingFactory([], DecideBlocked)),
                ],
            });
        Assert.True(ReplayV2Serializer.VerifyHash(
            blockedRun.ReplayJson));
        ReplayV2Tick blockedTick = blockedRun.Replay.Ticks[10];
        ReplayV2Event blockedShot = Assert.Single(
            blockedTick.Resolution.Events,
            value => value.Type == FrontlineMatchEventType.Shot
                && value.SourceActorId == turretActor);
        Assert.Null(blockedShot.ProjectileId);
        Assert.DoesNotContain(
            blockedTick.Resolution.ProjectileTraversals,
            value => value.OwnerActorId == turretActor);
        AssertTickMutationRejected(
            blockedRun.Replay,
            tickId: 10,
            tick => tick with
            {
                Resolution = tick.Resolution with
                {
                    Events = tick.Resolution.Events
                        .Select(value =>
                            value.EventId == blockedShot.EventId
                                ? value with
                                {
                                    ProjectileId = "999999",
                                }
                                : value)
                        .ToImmutableArray(),
                },
            },
            "blocked turret launch");
    }

    [Fact]
    public void Run_FiveSlotUnitFourFabricatesTransformsAndReplaysEndToEnd()
    {
        GameRules baseline = FrontlineTestDefinitions.ReplicationRules(
            maxTicks: 14,
            firstUnlockTick: 1,
            secondUnlockTick: 2);
        GameRules rules = baseline with
        {
            Frontline = baseline.Frontline! with
            {
                MaxUnitsPerTeam = 5,
                FabricationUnlockTicks = [1, 2, 3, 4],
                AnchorWindupTicks = 1,
            },
        };
        ActorDecision Decide(
            ActorMatchStart start,
            ActorObservation observation)
        {
            int teamId = start.ActorId.TeamId;
            if (start.ActorId.UnitId == 0)
            {
                return observation.Tick is >= 1 and <= 4
                    ? ActorDecision.Fabricate(
                        new ObservedUnitTarget(
                            teamId,
                            observation.Tick))
                    : ActorDecision.Wait();
            }
            if (start.ActorId.UnitId != 4)
                return ActorDecision.Wait();
            return (teamId, observation.Tick) switch
            {
                (0, 5) => ActorDecision.TurnRight(),
                (0, 6) => ActorDecision.MoveForward(),
                (0, 7) => ActorDecision.TurnLeft(),
                (0, 8 or 9) => ActorDecision.MoveForward(),
                (1, 5) => ActorDecision.TurnLeft(),
                (1, 6) => ActorDecision.MoveForward(),
                (1, 7) => ActorDecision.TurnRight(),
                (1, 8 or 9) => ActorDecision.MoveForward(),
                (_, 12) => ActorDecision.Transform("turret"),
                _ => ActorDecision.Wait(),
            };
        }

        var teamZero = new RecordingFactory([], Decide);
        var teamOne = new RecordingFactory([], Decide);
        FrontlineActorMatchRunResult run =
            new FrontlineActorMatchEngine().Run(new()
            {
                Map = FrontlineTestDefinitions.AnchorMapV2(),
                Rules = rules,
                Seed = 43,
                Participants =
                [
                    Participant(0, 0, "zero", teamZero),
                    Participant(1, 1, "one", teamOne),
                ],
            });

        Assert.True(ReplayV2Serializer.VerifyHash(run.ReplayJson));
        Assert.Equal(
            5,
            run.Replay.Header.Contract.Rules.Frontline!.MaxUnitsPerTeam);
        Assert.All(
            run.Replay.Header.Contract.Topology.Teams,
            team => Assert.Equal(
                [0, 1, 2, 3, 4],
                run.Replay.Header.Contract.Topology.UnitSlots
                    .Where(unit => unit.TeamId == team.TeamId)
                    .Select(unit => unit.UnitId)
                    .ToArray()));
        Assert.Contains(
            new ReplayV2ActorId(0, 4, 0),
            run.Replay.Ticks[5].TickStart.ActiveActors);
        Assert.All(
            run.Replay.Header.Contract.Topology.Teams,
            team => Assert.Equal(
                5,
                run.Replay.Ticks[5].Actors.Count(actor =>
                    actor.ActorId.TeamId == team.TeamId)));
        Assert.Equal(10, run.Replay.Ticks[5].Actors.Length);
        Assert.All(
            run.Replay.Ticks[5].Actors,
            actor => Assert.Equal(
                5,
                actor.Observation.TeamUnits.Length));
        Assert.Equal(5, teamZero.Runtimes.Count);
        Assert.Equal(5, teamOne.Runtimes.Count);
        Assert.Equal(
            5,
            teamZero.Runtimes
                .Select(runtime => runtime.Start!.ActorId.UnitId)
                .Distinct()
                .Count());
        Assert.Equal(
            5,
            teamOne.Runtimes
                .Select(runtime => runtime.Start!.ActorId.UnitId)
                .Distinct()
                .Count());
        ReplayV2ActorTurn firstUnitFourTurn =
            run.Replay.Ticks[5].Actors.Single(actor =>
                actor.ActorId == new ReplayV2ActorId(0, 4, 0));
        Assert.Equal(5, firstUnitFourTurn.Observation.TeamUnits.Length);
        Assert.Equal("child-mobile",
            firstUnitFourTurn.Observation.Self.FormId);
        Assert.Equal(ActorSpawnReason.Fabrication,
            firstUnitFourTurn.LifeStart!.SpawnReason);

        ReplayV2Tick transformTick = run.Replay.Ticks[12];
        Assert.Equal(
            2,
            transformTick.Resolution.Events.Count(value =>
                value.Type
                    == FrontlineMatchEventType.FormTransitionStarted));
        Assert.Equal(
            2,
            transformTick.Resolution.Events.Count(value =>
                value.Type == FrontlineMatchEventType.FormChanged));
        ReplayV2UnitState unitFour = transformTick.PostState.Teams
            .Single(team => team.TeamId == 0).Units
            .Single(unit => unit.UnitId == 4);
        Assert.Equal("child-mobile", unitFour.DefaultFormId);
        Assert.Equal("turret", unitFour.ActiveLife!.FormId);
        Assert.Equal(
            "turret",
            run.Replay.Ticks[13].Actors
                .Single(actor =>
                    actor.ActorId == new ReplayV2ActorId(0, 4, 0))
                .Observation.TeamUnits
                .Single(unit => unit.UnitId == 4)
                .FormId);

        Assert.All(
            run.Replay.Result.Teams,
            team =>
            {
                Assert.Equal(5, team.Units.Length);
                ReplayV2UnitResult result =
                    team.Units.Single(unit => unit.UnitId == 4);
                Assert.Equal("child-mobile", result.DefaultFormId);
                Assert.Equal("turret", result.FormId);
                Assert.Equal(
                    new ReplayV2ActorId(team.TeamId, 4, 0),
                    result.ActiveActorId);
            });
    }

    [Fact]
    public void Run_IsIndependentOfParticipantInputOrder()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 2);

        FrontlineActorMatchRunResult first = new FrontlineActorMatchEngine().Run(
            Configuration(
                rules,
                Participant(
                    0,
                    0,
                    "zero",
                    new RecordingFactory([], (_, _) => ActorDecision.Wait())),
                Participant(
                    1,
                    1,
                    "one",
                    new RecordingFactory([], (_, _) => ActorDecision.Wait()))));
        FrontlineActorMatchRunResult second = new FrontlineActorMatchEngine().Run(
            Configuration(
                rules,
                Participant(
                    1,
                    1,
                    "one",
                    new RecordingFactory([], (_, _) => ActorDecision.Wait())),
                Participant(
                    0,
                    0,
                    "zero",
                    new RecordingFactory([], (_, _) => ActorDecision.Wait()))));

        Assert.Equal(first.ReplayHash, second.ReplayHash);
        Assert.Equal(first.ReplayJson, second.ReplayJson);
    }

    [Fact]
    public void Run_UsesExactTopologyControllerAssignments()
    {
        var teamZero = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());
        var teamOne = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());
        var topology = new PublicMatchTopology
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(20, 1)],
            UnitSlots = [new(0, 0, 10), new(1, 0, 20)],
            InitialLives =
            [
                new(0, 0, 0, "prime-mobile"),
                new(1, 0, 0, "prime-mobile"),
            ],
        };
        var configuration = new FrontlineActorMatchConfiguration
        {
            Map = FrontlineTestDefinitions.OpenMapV2(),
            Rules = FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1),
            Seed = 42,
            Topology = topology,
            Participants =
            [
                Participant(20, 1, "one", teamOne),
                Participant(10, 0, "zero", teamZero),
            ],
        };

        FrontlineActorMatchRunResult run =
            new FrontlineActorMatchEngine().Run(configuration);

        Assert.Equal(10, Assert.Single(teamZero.Runtimes).Start!.ParticipantId);
        Assert.Equal(20, Assert.Single(teamOne.Runtimes).Start!.ParticipantId);
        Assert.Equal(
            [10, 20],
            run.Replay.Header.Participants
                .OrderBy(participant => participant.ParticipantId)
                .Select(participant => participant.ParticipantId));
    }

    [Fact]
    public void Run_RejectsParticipantMismatchBeforeCreatingRuntimes()
    {
        var factory = new RecordingFactory([], (_, _) => ActorDecision.Wait());
        FrontlineActorMatchConfiguration configuration = Configuration(
            FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1),
            Participant(0, 1, "wrong team", factory),
            Participant(
                1,
                1,
                "one",
                new RecordingFactory([], (_, _) => ActorDecision.Wait())));

        Assert.Throws<ArgumentException>(
            () => new FrontlineActorMatchEngine().Run(configuration));
        Assert.Empty(factory.Runtimes);
    }

    [Fact]
    public void Run_RejectsInvalidHostLimitsAndMissingArtifactProvenance()
    {
        var factory = new RecordingFactory([], (_, _) => ActorDecision.Wait());
        GameRules invalidLimits =
            FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1) with
            {
                MaxDebugBytesPerTick = -1,
            };
        ActorParticipantConfiguration missingHash =
            Participant(0, 0, "zero", factory) with
            {
                ArtifactHash = " ",
            };

        Assert.Throws<ArgumentException>(() =>
            new FrontlineActorMatchEngine().Run(Configuration(
                invalidLimits,
                Participant(0, 0, "zero", factory),
                Participant(
                    1,
                    1,
                    "one",
                    new RecordingFactory(
                        [],
                        (_, _) => ActorDecision.Wait())))));
        Assert.Throws<ArgumentException>(() =>
            new FrontlineActorMatchEngine().Run(Configuration(
                FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1),
                missingHash,
                Participant(
                    1,
                    1,
                    "one",
                    new RecordingFactory(
                        [],
                        (_, _) => ActorDecision.Wait())))));
        Assert.Empty(factory.Runtimes);
    }

    [Fact]
    public void Run_RejectsReusedLifeInstanceAndDisposesOwnedRuntime()
    {
        var runtime = new RecordingRuntime(
            [],
            (_, _) => ActorDecision.Wait());
        var factory = new ReusingFactory(runtime);

        FrontlineActorHostException exception = Assert.Throws<
            FrontlineActorHostException>(() =>
            new FrontlineActorMatchEngine().Run(Configuration(
                FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1),
                Participant(0, 0, "zero", factory),
                Participant(1, 1, "one", factory))));

        Assert.Equal(
            FrontlineActorHostStage.CreateRuntime,
            exception.Stage);
        Assert.True(runtime.Disposed);
    }

    [Fact]
    public void Run_RuntimeFailureAbortsWithoutLeakingLifeInstances()
    {
        var teamZero = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());
        var teamOne = new RecordingFactory(
            [],
            (_, _) => throw new FrontlineActorHostException(
                new ActorIdentity(99, 99, 99),
                999,
                FrontlineActorHostStage.CreateRuntime,
                "spoofed host attribution"));

        FrontlineActorHostException exception = Assert.Throws<
            FrontlineActorHostException>(() =>
            new FrontlineActorMatchEngine().Run(Configuration(
                FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1),
                Participant(0, 0, "zero", teamZero),
                Participant(1, 1, "one", teamOne))));

        Assert.Equal(new ActorIdentity(1, 0, 0), exception.ActorId);
        Assert.Equal(0, exception.Tick);
        Assert.Equal(
            FrontlineActorHostStage.ExecuteTick,
            exception.Stage);
        Assert.Equal(1, exception.ParticipantId);
        Assert.Equal(
            FrontlineActorHostFaultCodes.RuntimeExecuteFailed,
            exception.Code);
        Assert.NotNull(exception.Failure);
        Assert.Equal(
            FrontlineActorHostFaultCodes.RuntimeExecuteFailed,
            exception.Failure.Fault.Code);
        Assert.Equal(1, exception.Failure.Fault.ParticipantId);
        using (JsonDocument partial = JsonDocument.Parse(
                   exception.Failure.PartialReplayJson))
        {
            Assert.True(partial.RootElement.GetProperty("partial").GetBoolean());
            Assert.Empty(
                partial.RootElement.GetProperty("ticks").EnumerateArray());
            Assert.Equal(
                JsonValueKind.Null,
                partial.RootElement.GetProperty("result").ValueKind);
            Assert.Equal(
                JsonValueKind.Null,
                partial.RootElement.GetProperty("replayHash").ValueKind);
        }
        Assert.IsType<FrontlineActorHostException>(
            exception.InnerException);
        Assert.All(
            teamZero.Runtimes.Concat(teamOne.Runtimes),
            runtime => Assert.True(runtime.Disposed));
    }

    [Fact]
    public void RunAttempt_ReturnsOnlyFullyResolvedTicksWithStableFault()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 3);
        var teamZero = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());
        var teamOne = new RecordingFactory(
            [],
            (_, observation) => observation.Tick == 1
                ? throw new InvalidOperationException(
                    "artifact-controlled diagnostic")
                : ActorDecision.Wait());

        FrontlineActorMatchAttempt attempt =
            new FrontlineActorMatchEngine().RunAttempt(Configuration(
                rules,
                Participant(0, 0, "zero", teamZero),
                Participant(1, 1, "one", teamOne)));

        FrontlineActorMatchFailed failed =
            Assert.IsType<FrontlineActorMatchFailed>(attempt);
        Assert.Equal(
            new FrontlineActorHostFault
            {
                SchemaVersion =
                    BotArenaVersions.ActorHostFaultSchemaVersion,
                Code =
                    FrontlineActorHostFaultCodes.RuntimeExecuteFailed,
                Stage = FrontlineActorHostStage.ExecuteTick,
                ParticipantId = 1,
                ActorId = new ActorIdentity(1, 0, 0),
                Tick = 1,
            },
            failed.Failure.Fault);
        Assert.DoesNotContain(
            "artifact-controlled diagnostic",
            failed.Failure.PartialReplayJson,
            StringComparison.Ordinal);

        using JsonDocument partial = JsonDocument.Parse(
            failed.Failure.PartialReplayJson);
        JsonElement root = partial.RootElement;
        Assert.True(root.GetProperty("partial").GetBoolean());
        JsonElement tick = Assert.Single(
            root.GetProperty("ticks").EnumerateArray());
        Assert.Equal(0, tick.GetProperty("tick").GetInt32());
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("result").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("replayHash").ValueKind);
        Assert.All(
            teamZero.Runtimes.Concat(teamOne.Runtimes),
            runtime => Assert.True(runtime.Disposed));
    }

    [Fact]
    public void Run_BoundsAllCapturedRuntimeDiagnosticTextByUtf8Budget()
    {
        GameRules rules =
            FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1) with
            {
                MaxDebugBytesPerTick = 4,
                MaxDebugBytesPerMatch = 6,
            };
        var teamZero = new RecordingFactory(
            [],
            (_, _) => new ActorDecision
            {
                ActionId = PublicActionIds.Wait,
                DebugMessage = "å",
                FaultMessage = "abcdef",
            });
        var teamOne = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());

        FrontlineActorMatchRunResult run = new FrontlineActorMatchEngine().Run(
            Configuration(
                rules,
                Participant(0, 0, "zero", teamZero),
                Participant(1, 1, "one", teamOne)));

        ReplayV2ActorTurn turn = run.Replay.Ticks[0].Actors[0];
        Assert.Equal("å", turn.RuntimeReply.DebugMessage);
        Assert.Equal("ab", turn.RuntimeReply.FaultMessage);
        Assert.Equal("å", turn.AcceptedDecision.DebugMessage);
        Assert.Null(turn.AcceptedDecision.FaultMessage);
    }

    [Fact]
    public void Run_AttributesInvalidShotProgramToSubmittingLife()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1);
        var invalid = new ShotProgram(
            rules.ProgrammedShotMaxInitialAimOctants + 1,
            0,
            0,
            1,
            0);
        var teamZero = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Shoot(invalid));
        var teamOne = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());

        FrontlineActorHostException exception = Assert.Throws<
            FrontlineActorHostException>(() =>
            new FrontlineActorMatchEngine().Run(Configuration(
                rules,
                Participant(0, 0, "zero", teamZero),
                Participant(1, 1, "one", teamOne))));

        Assert.Equal(new ActorIdentity(0, 0, 0), exception.ActorId);
        Assert.Equal(0, exception.Tick);
        Assert.Equal(
            FrontlineActorHostStage.ValidateDecision,
            exception.Stage);
        Assert.All(
            teamZero.Runtimes.Concat(teamOne.Runtimes),
            runtime => Assert.True(runtime.Disposed));
    }

    [Fact]
    public void Run_FabricatedLifeGetsIndependentRuntimeSeedMemoryAndPerBodyTickBudget()
    {
        GameRules rules = FrontlineTestDefinitions.ReplicationRules(
                maxTicks: 3) with
        {
            MaxDebugBytesPerTick = 4,
            MaxDebugBytesPerMatch = 6,
        };
        var teamZero = new RecordingFactory(
            [],
            (start, observation) =>
            {
                if (start.ActorId.UnitId == 0 && observation.Tick == 1)
                {
                    return ActorDecision.Fabricate(
                        new ObservedUnitTarget(0, 1));
                }
                return observation.Tick == 2
                    ? ActorDecision.Wait("abcd")
                    : ActorDecision.Wait();
            });
        var teamOne = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());

        FrontlineActorMatchRunResult run =
            new FrontlineActorMatchEngine().Run(
                ReplicationConfiguration(
                    rules,
                    Participant(0, 0, "zero", teamZero),
                    Participant(1, 1, "one", teamOne)));

        Assert.Equal(2, teamZero.Runtimes.Count);
        RecordingRuntime prime = teamZero.Runtimes.Single(runtime =>
            runtime.Start!.ActorId.UnitId == 0);
        RecordingRuntime child = teamZero.Runtimes.Single(runtime =>
            runtime.Start!.ActorId.UnitId == 1);
        Assert.Equal(3, prime.ExecutionCount);
        Assert.Equal(1, child.ExecutionCount);
        Assert.Equal(ActorSpawnReason.Fabrication, child.Start!.SpawnReason);
        Assert.NotEqual(
            prime.Start!.ActorRandomSeed,
            child.Start.ActorRandomSeed);

        ReplayV2Tick tick2 = run.Replay.Ticks[2];
        ReplayV2ActorTurn primeTurn = tick2.Actors.Single(actor =>
            actor.ActorId == new ReplayV2ActorId(0, 0, 0));
        ReplayV2ActorTurn childTurn = tick2.Actors.Single(actor =>
            actor.ActorId == new ReplayV2ActorId(0, 1, 0));
        Assert.Equal("abcd", primeTurn.AcceptedDecision.DebugMessage);
        Assert.Equal("ab", childTurn.AcceptedDecision.DebugMessage);
        Assert.Equal(
            ActorSpawnReason.Fabrication,
            childTurn.LifeStart!.SpawnReason);
        ReplayV2Event fabricated = Assert.Single(
            tick2.TickStart.LifecycleEvents,
            value =>
                value.Type == FrontlineMatchEventType.Fabricated
                && value.TeamId == 0);
        AssertFabricatedEventMutationRejected(
            run.Replay,
            fabricated with
            {
                To = new Position(
                    fabricated.To!.Value.X + 1,
                    fabricated.To.Value.Y),
            });
        AssertFabricatedEventMutationRejected(
            run.Replay,
            fabricated with
            {
                ToFacing = fabricated.ToFacing == Direction.North
                    ? Direction.East
                    : Direction.North,
            });
        AssertFabricatedEventMutationRejected(
            run.Replay,
            fabricated with
            {
                NewHealth = fabricated.NewHealth + 1,
            });
        AssertTickStartUnitMutationRejected(
            run.Replay,
            tickId: 0,
            teamId: 0,
            unitId: 1,
            unit => unit with { DefaultFormId = "turret" },
            "deployment default form");
        ReplayV2LifeState primeLife = run.Replay.Ticks[0]
            .TickStart.State.Teams
            .Single(team => team.TeamId == 0)
            .Units.Single(unit => unit.UnitId == 0)
            .ActiveLife!;
        AssertTickStartUnitMutationRejected(
            run.Replay,
            tickId: 0,
            teamId: 0,
            unitId: 1,
            unit => unit with
            {
                LifecycleStatus = FrontlineLifecycleStatus.Active,
                ActiveLife = primeLife with
                {
                    ActorId = new ReplayV2ActorId(0, 1, 0),
                    Position = new Position(2, 5),
                },
                HasSpawned = true,
                NextLifeId = 1,
            },
            "exact initial-life topology");
        AssertTickStartUnitMutationRejected(
            run.Replay,
            tickId: 2,
            teamId: 0,
            unitId: 1,
            unit => unit with { DefaultFormId = "turret" },
            "lifecycle transition");
        AssertFrontlineContractMutationRejected(
            run.Replay,
            rules,
            frontline => frontline with
            {
                Fabrication = frontline.Fabrication with
                {
                    FabricatorUnitId = 1,
                },
            });
        AssertFrontlineContractMutationRejected(
            run.Replay,
            rules,
            frontline => frontline with
            {
                Fabrication = frontline.Fabrication with
                {
                    ActionId = PublicActionIds.Wait,
                },
            });
        AssertFrontlineContractMutationRejected(
            run.Replay,
            rules,
            frontline => frontline with
            {
                Fabrication = frontline.Fabrication with
                {
                    SpawnDelayTicks =
                        frontline.Fabrication.SpawnDelayTicks + 1,
                },
            });
        Assert.All(teamZero.Runtimes, runtime => Assert.True(runtime.Disposed));
    }

    [Fact]
    public void Run_InvalidFabricationTargetHasActorAttributedHostFailure()
    {
        GameRules rules = FrontlineTestDefinitions.ReplicationRules(maxTicks: 3);
        var teamZero = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Fabricate(
                new ObservedUnitTarget(1, 1)));
        var teamOne = new RecordingFactory(
            [],
            (_, _) => ActorDecision.Wait());

        FrontlineActorHostException exception = Assert.Throws<
            FrontlineActorHostException>(() =>
            new FrontlineActorMatchEngine().Run(
                ReplicationConfiguration(
                    rules,
                    Participant(0, 0, "zero", teamZero),
                    Participant(1, 1, "one", teamOne))));

        Assert.Equal(new ActorIdentity(0, 0, 0), exception.ActorId);
        Assert.Equal(0, exception.Tick);
        Assert.Equal(
            FrontlineActorHostStage.ValidateDecision,
            exception.Stage);
        Assert.Equal(
            FrontlineActorHostFaultCodes.DecisionRejected,
            exception.Code);
        Assert.Empty(
            JsonDocument.Parse(exception.Failure!.PartialReplayJson)
                .RootElement.GetProperty("ticks").EnumerateArray());
    }

    private static FrontlineActorMatchConfiguration Configuration(
        GameRules rules,
        params ActorParticipantConfiguration[] participants) =>
        new()
        {
            Map = FrontlineTestDefinitions.OpenMapV2(),
            Rules = rules,
            Seed = 42,
            Participants = participants,
        };

    private static void AssertFabricatedEventMutationRejected(
        ReplayV2 replay,
        ReplayV2Event replacement)
    {
        ReplayV2 mutated = replay with
        {
            Ticks = replay.Ticks
                .Select(tick => tick.Tick != replacement.Tick
                    ? tick
                    : tick with
                    {
                        TickStart = tick.TickStart with
                        {
                            LifecycleEvents = tick.TickStart.LifecycleEvents
                                .Select(value =>
                                    value.EventId == replacement.EventId
                                        ? replacement
                                        : value)
                                .ToImmutableArray(),
                        },
                    })
                .ToImmutableArray(),
        };
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            ReplayV2Serializer.ComputeHash(mutated));
        Assert.Contains(
            "lifecycle transition",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertTickStartUnitMutationRejected(
        ReplayV2 replay,
        int tickId,
        int teamId,
        int unitId,
        Func<ReplayV2UnitState, ReplayV2UnitState> replace,
        string expectedMessage)
    {
        ReplayV2 mutated = replay with
        {
            Ticks = replay.Ticks
                .Select(tick => tick.Tick != tickId
                    ? tick
                    : tick with
                    {
                        TickStart = tick.TickStart with
                        {
                            State = tick.TickStart.State with
                            {
                                Teams = tick.TickStart.State.Teams
                                    .Select(team => team.TeamId != teamId
                                        ? team
                                        : team with
                                        {
                                            Units = team.Units
                                                .Select(unit =>
                                                    unit.UnitId != unitId
                                                        ? unit
                                                        : replace(unit))
                                                .ToImmutableArray(),
                                        })
                                    .ToImmutableArray(),
                            },
                        },
                    })
                .ToImmutableArray(),
        };
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            ReplayV2Serializer.ComputeHash(mutated));
        Assert.Contains(
            expectedMessage,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertTickMutationRejected(
        ReplayV2 replay,
        int tickId,
        Func<ReplayV2Tick, ReplayV2Tick> replace,
        string expectedMessage)
    {
        ReplayV2 mutated = replay with
        {
            Ticks = replay.Ticks
                .Select(tick => tick.Tick == tickId
                    ? replace(tick)
                    : tick)
                .ToImmutableArray(),
        };
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            ReplayV2Serializer.ComputeHash(mutated));
        Assert.Contains(
            expectedMessage,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ReplayV2Tick ReplacePostLife(
        ReplayV2Tick tick,
        ReplayV2ActorId actorId,
        Func<ReplayV2LifeState, ReplayV2LifeState> replace) =>
        tick with
        {
            PostState = tick.PostState with
            {
                Teams = tick.PostState.Teams
                    .Select(team => team.TeamId != actorId.TeamId
                        ? team
                        : team with
                        {
                            Units = team.Units
                                .Select(unit =>
                                    unit.UnitId != actorId.UnitId
                                    || unit.ActiveLife is null
                                        ? unit
                                        : unit with
                                        {
                                            ActiveLife = replace(
                                                unit.ActiveLife),
                                        })
                                .ToImmutableArray(),
                        })
                    .ToImmutableArray(),
            },
        };

    private static void AssertFrontlineContractMutationRejected(
        ReplayV2 replay,
        GameRules sourceRules,
        Func<PublicFrontlineDefinition, PublicFrontlineDefinition> mutate)
    {
        PublicRulesManifest rules = replay.Header.Contract.Rules with
        {
            RulesFingerprint = "",
            Frontline = mutate(replay.Header.Contract.Rules.Frontline!),
        };
        rules = rules with
        {
            RulesFingerprint = MatchContractFingerprint.ComputeRules(
                rules,
                sourceRules),
        };
        PublicMatchContractManifest contract =
            replay.Header.Contract with
            {
                Rules = rules,
                MatchContractFingerprint = "",
            };
        contract = contract with
        {
            MatchContractFingerprint =
                MatchContractFingerprint.ComputeMatch(contract),
        };
        ReplayV2 mutated = replay with
        {
            Header = replay.Header with { Contract = contract },
            Ticks = replay.Ticks
                .Select(tick => tick with
                {
                    Actors = tick.Actors
                        .Select(actor => actor with
                        {
                            LifeStart = actor.LifeStart is { } lifeStart
                                ? lifeStart with
                                {
                                    MatchContractFingerprint =
                                        contract.MatchContractFingerprint,
                                }
                                : null,
                            Observation = actor.Observation with
                            {
                                MatchContractFingerprint =
                                    contract.MatchContractFingerprint,
                            },
                        })
                        .ToImmutableArray(),
                })
                .ToImmutableArray(),
        };

        _ = Assert.Throws<ArgumentException>(() =>
            ReplayV2Serializer.ComputeHash(mutated));
    }

    private static FrontlineActorMatchConfiguration ReplicationConfiguration(
        GameRules rules,
        params ActorParticipantConfiguration[] participants) =>
        new()
        {
            Map = FrontlineTestDefinitions.ReplicationMapV2(),
            Rules = rules,
            Seed = 42,
            Participants = participants,
        };

    private static ActorParticipantConfiguration Participant(
        int participantId,
        int teamId,
        string name,
        IActorRuntimeFactory factory) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = name,
            RuntimeFactory = factory,
            RuntimeKind = "test",
            ArtifactHash = $"artifact-{participantId}",
            Accent = participantId == 0 ? "#00aaff" : "#ff5500",
        };

    private sealed class RecordingFactory(
        List<string> log,
        Func<ActorMatchStart, ActorObservation, ActorDecision> decide)
        : IActorRuntimeFactory
    {
        public List<RecordingRuntime> Runtimes { get; } = [];

        public IActorRuntime CreateRuntime()
        {
            var runtime = new RecordingRuntime(log, decide);
            Runtimes.Add(runtime);
            return runtime;
        }
    }

    private sealed class ReusingFactory(RecordingRuntime runtime)
        : IActorRuntimeFactory
    {
        public IActorRuntime CreateRuntime() => runtime;
    }

    private sealed class RecordingRuntime(
        List<string> log,
        Func<ActorMatchStart, ActorObservation, ActorDecision> decide)
        : IActorRuntime
    {
        public ActorMatchStart? Start { get; private set; }
        public bool Disposed { get; private set; }
        public int ExecutionCount { get; private set; }

        public void StartLife(ActorMatchStart start)
        {
            Assert.Null(Start);
            Start = start;
            log.Add($"start:{start.ActorId}");
        }

        public ActorDecision ExecuteTick(ActorObservation observation)
        {
            Assert.NotNull(Start);
            ExecutionCount++;
            log.Add($"tick:{observation.Self.ActorId}");
            return decide(Start, observation);
        }

        public void Dispose()
        {
            if (Disposed)
                return;
            Disposed = true;
            log.Add($"dispose:{Start?.ActorId}");
        }
    }
}
