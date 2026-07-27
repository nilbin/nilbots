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
            unit => unit with { FormId = "turret" },
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
            unit => unit with { FormId = "turret" },
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
