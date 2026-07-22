using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Bots.BuiltIn;

namespace BotArena.Determinism.Tests;

public class DeterminismTests
{
    private static ArenaMap LoadMap(string id) =>
        ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "maps", id + ".json")));

    private static MatchRunResult RunMatch(string bot0, string bot1, ulong seed, string mapId = "basic-01")
    {
        var configuration = new MatchConfiguration
        {
            Map = LoadMap(mapId),
            Rules = GameRules.V0_1,
            Seed = seed,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = bot0,
                    Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create(bot0)),
                },
                new MatchParticipantConfig
                {
                    Name = bot1,
                    Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create(bot1)),
                },
            ],
        };
        return new MatchEngine().Run(configuration);
    }

    [Fact]
    public void HunterVersusWander_SameSeed_ProducesIdenticalReplays()
    {
        var first = RunMatch("hunter", "wander", 42);
        var second = RunMatch("hunter", "wander", 42);
        var third = RunMatch("hunter", "wander", 42);

        Assert.Equal(first.ReplayHash, second.ReplayHash);
        Assert.Equal(first.ReplayHash, third.ReplayHash);
        Assert.Equal(
            ReplaySerializer.ToCanonicalJson(first.Replay),
            ReplaySerializer.ToCanonicalJson(second.Replay));
    }

    [Theory]
    [InlineData("idle", "wander")]
    [InlineData("idle", "hunter")]
    [InlineData("wander", "hunter")]
    [InlineData("wander", "coward")]
    [InlineData("hunter", "coward")]
    [InlineData("hunter", "hunter")]
    public void EveryBuiltInPairing_IsDeterministic(string bot0, string bot1)
    {
        foreach (ulong seed in new ulong[] { 7, 99 })
        {
            var first = RunMatch(bot0, bot1, seed);
            var second = RunMatch(bot0, bot1, seed);
            Assert.Equal(first.ReplayHash, second.ReplayHash);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentReplays()
    {
        var seed1 = RunMatch("hunter", "wander", 1);
        var seed2 = RunMatch("hunter", "wander", 2);
        Assert.NotEqual(seed1.ReplayHash, seed2.ReplayHash);
    }

    [Fact]
    public void OnTheLargeArena_MatchesAreDeterministicToo()
    {
        var first = RunMatch("hunter", "coward", 1234, "arena-01");
        var second = RunMatch("hunter", "coward", 1234, "arena-01");
        Assert.Equal(first.ReplayHash, second.ReplayHash);
    }

    [Fact]
    public void ReplayRoundTrip_PreservesHashAndContent()
    {
        var run = RunMatch("hunter", "wander", 42);
        string json = ReplaySerializer.ToJson(run.Replay);
        var document = ReplaySerializer.FromJson(json);

        Assert.Equal(run.ReplayHash, document.ReplayHash);
        var rebuilt = new Replay
        {
            Header = document.Header,
            Ticks = document.Ticks,
            Result = document.Result,
        };
        Assert.Equal(run.ReplayHash, ReplaySerializer.ComputeHash(rebuilt));
    }

    [Fact]
    public void ReplayFinalState_MatchesReportedResult()
    {
        var run = RunMatch("hunter", "wander", 42);
        var lastTick = run.Replay.Ticks[^1];
        foreach (var bot in run.Result.Bots)
        {
            var snapshot = lastTick.State.Single(s => s.Slot == bot.Slot);
            Assert.Equal(bot.FinalHealth, snapshot.Health);
            Assert.Equal(bot.FinalStatus, snapshot.Status);
        }
    }

    [Fact]
    public void MatchesFinish_WithinMaxTicks()
    {
        var run = RunMatch("wander", "wander", 5);
        Assert.True(run.Replay.Ticks.Count <= GameRules.V0_1.MaxTicks);
        Assert.Equal(run.Replay.Ticks.Count - 1, run.Result.EndTick);
    }
}
