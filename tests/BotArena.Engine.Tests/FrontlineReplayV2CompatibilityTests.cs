using BotArena.Bots.BuiltIn;
using BotArena.Runtime;

namespace BotArena.Engine.Tests;

/// <summary>
/// End-to-end characterization shield for the current Frontline actor path.
/// This complements the handcrafted replay-v2 serializer golden by pinning a
/// real alpha ruleset, map, actor host, reference-bot, and replay projection.
/// </summary>
public sealed class FrontlineReplayV2CompatibilityTests
{
    private const ulong MatchSeed = 42;
    private const string RulesetId = "frontline-alpha-1";
    private const string TeamZeroBot = "frontline-rusher";
    private const string TeamOneBot = "frontline-counterpunch";
    private const string ExpectedReplayHash =
        "f04022ae83e13630da7fe8760d93f980ab7397a26878c9e08a3a615bd6786589";

    [Fact]
    public void Alpha1ActorRun_ReproducesPinnedReplayV2Hash()
    {
        using var teamZero = new InProcessActorRuntimeFactory(
            () => BuiltInActorBotCatalog.Create(TeamZeroBot));
        using var teamOne = new InProcessActorRuntimeFactory(
            () => BuiltInActorBotCatalog.Create(TeamOneBot));

        FrontlineActorMatchRunResult run = new FrontlineActorMatchEngine().Run(
            new FrontlineActorMatchConfiguration
            {
                Map = LoadMap(),
                Rules = ExperimentalFrontlineRules.Resolve(RulesetId),
                Seed = MatchSeed,
                Participants =
                [
                    Participant(0, 0, TeamZeroBot, teamZero),
                    Participant(1, 1, TeamOneBot, teamOne),
                ],
            });

        Assert.Equal(2, run.Replay.Header.ReplayVersion);
        Assert.Equal(
            RulesetId,
            run.Replay.Header.GameRulesVersion);
        Assert.True(ReplayV2Serializer.VerifyHash(run.ReplayJson));
        Assert.Equal(ExpectedReplayHash, run.ReplayHash);
    }

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
            RuntimeKind = "in-process-reference",
            ArtifactHash = $"builtin:{name}",
            Accent = BuiltInActorBotCatalog.Accent(name),
            LookId = BuiltInActorBotCatalog.Look(name),
            ProjectileLookId = BuiltInActorBotCatalog.ProjectileLook(name),
        };

    private static ArenaMap LoadMap() =>
        ArenaMap.FromJson(File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "maps",
            "experimental",
            "frontline-01.json")));

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
}
