using BotArena.Engine.Tests.Support;
using System.Text;
using System.Text.Json;

namespace BotArena.Engine.Tests;

/// <summary>
/// Owns the engine-authored replay-v2 fixtures consumed by the TypeScript
/// boundary tests. Normal test runs compare exact bytes and never write.
/// Set UPDATE_GOLDEN=1 deliberately to regenerate both files from the engine.
/// </summary>
public sealed class FrontlineReplayV2FixtureTests
{
    private const ulong FixtureSeed = 9_007_199_254_740_993UL;
    private const string CompleteFixtureName = "frontline-replay-v2.json";
    private const string PartialFixtureName =
        "frontline-replay-v2-partial-zero-tick.json";

    [Fact]
    public void EngineAuthoredDocuments_MatchCheckedInGoldenFixtures()
    {
        FrontlineActorMatchRunResult complete = RunCompleteMatch();
        AssertCompleteCoverage(complete);

        FrontlineActorMatchFailure failure = RunTickZeroFailure();
        AssertZeroTickPartialCoverage(failure);

        AssertOrUpdateFixture(CompleteFixtureName, complete.ReplayJson);
        AssertOrUpdateFixture(
            PartialFixtureName,
            failure.PartialReplayJson);
    }

    private static FrontlineActorMatchRunResult RunCompleteMatch()
    {
        var engine = new FrontlineActorMatchEngine();
        return engine.Run(Configuration(
            new FixtureRuntimeFactory(failAtTickZero: false),
            new FixtureRuntimeFactory(failAtTickZero: false)));
    }

    private static FrontlineActorMatchFailure RunTickZeroFailure()
    {
        var engine = new FrontlineActorMatchEngine();
        FrontlineActorMatchAttempt attempt = engine.RunAttempt(Configuration(
            new FixtureRuntimeFactory(failAtTickZero: true),
            new FixtureRuntimeFactory(failAtTickZero: false)));
        return Assert.IsType<FrontlineActorMatchFailed>(attempt).Failure;
    }

    private static FrontlineActorMatchConfiguration Configuration(
        IActorRuntimeFactory teamZero,
        IActorRuntimeFactory teamOne)
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 4,
            primeRespawnTicks: 1,
            shootCooldownTicks: 0) with
        {
            DamagePerHit = 3,
            ProgrammedShotLaunchTiles = 8,
        };
        return new FrontlineActorMatchConfiguration
        {
            Map = FrontlineTestDefinitions.OpenMapV2(),
            Rules = rules,
            Seed = FixtureSeed,
            Participants =
            [
                Participant(
                    participantId: 0,
                    teamId: 0,
                    name: "Fixture Zero",
                    artifactHash: "fixture-team-zero",
                    accent: "#38bdf8",
                    teamZero),
                Participant(
                    participantId: 1,
                    teamId: 1,
                    name: "Fixture One",
                    artifactHash: "fixture-team-one",
                    accent: "#f97316",
                    teamOne),
            ],
        };
    }

    private static ActorParticipantConfiguration Participant(
        int participantId,
        int teamId,
        string name,
        string artifactHash,
        string accent,
        IActorRuntimeFactory runtimeFactory) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = name,
            RuntimeFactory = runtimeFactory,
            RuntimeKind = "fixture-actor",
            ArtifactHash = artifactHash,
            Accent = accent,
        };

    private static void AssertCompleteCoverage(
        FrontlineActorMatchRunResult run)
    {
        Assert.Equal(4, run.Replay.Ticks.Length);
        Assert.Equal(
            FixtureSeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            run.Replay.Header.Seed);
        Assert.Equal(
            2,
            run.Replay.Ticks[0].Resolution.Events.Count(
                item => item.Type == FrontlineMatchEventType.Destroyed));
        Assert.All(
            run.Replay.Ticks[0].TickStart.ActiveActors,
            actor => Assert.Equal(0, actor.LifeId));
        Assert.DoesNotContain(
            run.Replay.Ticks[0].PostState.Teams
                .SelectMany(team => team.Units),
            unit => unit.ActiveLife is not null);
        Assert.Empty(run.Replay.Ticks[1].TickStart.ActiveActors);
        Assert.Empty(run.Replay.Ticks[1].Actors);
        Assert.All(
            run.Replay.Ticks[2].TickStart.ActiveActors,
            actor => Assert.Equal(1, actor.LifeId));
        Assert.All(
            run.Replay.Ticks[2].Actors,
            actor =>
            {
                Assert.Equal(1, actor.ActorId.LifeId);
                Assert.Equal(
                    ActorSpawnReason.Respawn,
                    Assert.IsType<ReplayV2LifeStart>(
                        actor.LifeStart).SpawnReason);
            });
        Assert.True(ReplayV2Serializer.VerifyHash(run.ReplayJson));
    }

    private static void AssertZeroTickPartialCoverage(
        FrontlineActorMatchFailure failure)
    {
        Assert.Equal(0, failure.Fault.Tick);
        Assert.Equal(
            FrontlineActorHostFaultCodes.RuntimeExecuteFailed,
            failure.Fault.Code);
        using JsonDocument document =
            JsonDocument.Parse(failure.PartialReplayJson);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("partial").GetBoolean());
        Assert.Empty(root.GetProperty("ticks").EnumerateArray());
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("result").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("replayHash").ValueKind);
        Assert.Equal(
            FixtureSeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            root.GetProperty("header").GetProperty("seed").GetString());
    }

    private static void AssertOrUpdateFixture(
        string fixtureName,
        string actual)
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "web",
            "tests",
            "fixtures",
            fixtureName);
        if (string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_GOLDEN"),
                "1",
                StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new UTF8Encoding(false));
        }

        Assert.True(
            File.Exists(path),
            $"Missing {fixtureName}. Regenerate deliberately with UPDATE_GOLDEN=1.");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(
                   directory.FullName,
                   "BotArena.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }

    private sealed class FixtureRuntimeFactory(
        bool failAtTickZero) : IActorRuntimeFactory
    {
        public IActorRuntime CreateRuntime() =>
            new FixtureRuntime(failAtTickZero);
    }

    private sealed class FixtureRuntime(
        bool failAtTickZero) : IActorRuntime
    {
        private ActorMatchStart? _start;

        public void StartLife(ActorMatchStart start)
        {
            _start = start;
        }

        public ActorDecision ExecuteTick(ActorObservation observation)
        {
            if (failAtTickZero && observation.Tick == 0)
            {
                throw new InvalidOperationException(
                    "fixture tick-zero actor failure");
            }

            ActorMatchStart start = _start
                ?? throw new InvalidOperationException(
                    "Fixture runtime was not started.");
            return start.SpawnReason == ActorSpawnReason.Initial
                ? ActorDecision.Shoot(ShotProgram.Straight)
                : ActorDecision.Wait();
        }
    }
}
