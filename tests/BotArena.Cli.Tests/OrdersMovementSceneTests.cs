using System.Collections.Immutable;
using System.Security.Cryptography;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Cli.Tests;

/// <summary>
/// Fast product-scene coverage for the real ATL2 executor. Small pure tests in
/// <see cref="TacticalMovementGovernorTests"/> pin arbitration mechanics; this
/// fixture catches the integration failures those cannot: bodies queueing in
/// the reactor, a fight/recovery/carrier plan never publishing traffic, empty
/// reasons, or high-level intent disappearing during clearance.
/// </summary>
public sealed class OrdersMovementSceneTests
{
    [Fact]
    public void TaplineExercisesUrgencyAndClearsBothReactorDeployments()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline1);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9001,
            recordChronology: true);

        Dictionary<ActorIdentity, Position> initial = session.ActiveLives
            .ToDictionary(life => life.ActorId, life => life.Position);
        var movedInitialLives = new HashSet<ActorIdentity>();
        var reasons = new List<string>();
        var banksByTeam = new Dictionary<int, int>();
        var runs = new Dictionary<ActorIdentity, (Position Position, int Ticks)>();
        int longestSingleTileRun = 0;
        GenericActorMatchStepResult? last = null;

        while (!session.IsCompleted)
        {
            last = session.Step();
            foreach (GenericMindRuntimeTurn turn in last.MindTurns)
            {
                Assert.Null(turn.RuntimeFault);
                foreach (GenericMindCommandResolution resolution in turn.Commands)
                {
                    Assert.Equal(GenericMindCommandOutcome.Accepted,
                        resolution.Outcome);
                    string reason = resolution.Command.DebugMessage ?? "";
                    Assert.False(string.IsNullOrWhiteSpace(reason));
                    reasons.Add(reason);
                }
            }
            foreach (GenericActorWorldSnapshot.LifeSnapshot life
                     in last.PostState.ActiveLives)
            {
                if (initial.TryGetValue(life.ActorId, out Position start)
                    && life.Position != start)
                {
                    movedInitialLives.Add(life.ActorId);
                }
                (Position Position, int Ticks) prior =
                    runs.GetValueOrDefault(life.ActorId);
                int ticks = prior.Position == life.Position
                    ? prior.Ticks + 1
                    : 0;
                runs[life.ActorId] = (life.Position, ticks);
                longestSingleTileRun = Math.Max(longestSingleTileRun, ticks);
            }
            foreach (GenericActorAuthoritativeEvent fact
                     in last.AuthoritativeEvents)
            {
                if (fact.Payload is GenericActorRuntimeObservation.EventPayload
                    .ArcRelay { Fact: ArcRelayEvent.CoreBanked banked })
                {
                    banksByTeam[banked.TeamId] =
                        banksByTeam.GetValueOrDefault(banked.TeamId) + 1;
                }
            }
        }

        Assert.NotNull(last);
        Assert.True(last.IsCompleted);
        Assert.Equal(initial.Count, movedInitialLives.Count);
        Assert.True(longestSingleTileRun <= 100,
            $"A real Tapline body stayed on one tile for "
            + $"{longestSingleTileRun} ticks.");
        Assert.All(new[] { 0, 1 }, team =>
            Assert.True(banksByTeam.GetValueOrDefault(team) > 0,
                $"Team {team} never banked a Core."));

        // These are semantic scenes, not hashes or outcome expectations. The
        // same match must exercise all three authored weights, carrier return,
        // recovery traffic, queue clearance, and combat approach while keeping
        // the selected order path in every reason. Detour/rejoin is pinned by
        // the smaller route-governor scenes; whether this seed needs one is an
        // emergent traffic fact and therefore not an integration invariant.
        Assert.Contains(reasons, reason => reason.Contains(
            "urgency-routine", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "urgency-normal", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "urgency-urgent", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            ":march@", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "score-a-core", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "recover-health", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            ":make-way:", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "fight-visible-enemy", StringComparison.Ordinal));
        Assert.DoesNotContain(reasons, string.IsNullOrWhiteSpace);
    }

    private static GenericActorParticipantConfiguration Participant(
        int participantId,
        TacticalPlaybookCompilation playbook) => new()
    {
        ParticipantId = participantId,
        TeamId = participantId,
        Name = participantId == 0 ? "Hunter" : "Wellwright",
        MindRuntimeFactory = new InProcessGenericMindRuntimeFactory(
            static () => new global::ArcRelayTacticalPlaybookMind()),
        RuntimeKind = "orders-movement-scene",
        ArtifactHash = "orders-movement-scene",
        MindDataHash = Convert.ToHexStringLower(
            SHA256.HashData(playbook.LinkedData)),
        MindEvaluationData = ImmutableArray.Create(playbook.LinkedData),
        Accent = participantId == 0 ? "#22d3ee" : "#fb5360",
        LookId = "arc-relay-sheet",
        ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
    };

    private static TacticalPlaybookCompilation Compile(
        string root,
        string fileName) => ArcRelayTacticalPlaybookCompiler.Compile(
        Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "playbooks",
            fileName));

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root is missing.");
    }
}
