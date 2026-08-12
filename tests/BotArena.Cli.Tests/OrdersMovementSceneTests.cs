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

    [Fact]
    public void CentreWellMoverDoesNotStarveBehindAnAtTargetBody()
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
            matchSeed: 9002,
            recordChronology: true);

        int currentBlockedTicks = 0;
        int longestBlockedRun = 0;

        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn hunterTurn = Assert.Single(
                step.MindTurns, turn => turn.TeamId == 0);
            GenericMindCommandResolution? hauler = hunterTurn.Commands
                .SingleOrDefault(resolution => resolution.Command.UnitId == 2);
            if (hauler is null)
            {
                currentBlockedTicks = 0;
                continue;
            }
            string haulerReason = hauler.Command.DebugMessage ?? "";
            bool centreWellBlocked = haulerReason.Contains(
                "work-centre-well", StringComparison.Ordinal)
                && haulerReason.Contains(
                    "move-blocked:well:centre", StringComparison.Ordinal);
            currentBlockedTicks = centreWellBlocked
                ? currentBlockedTicks + 1
                : 0;
            longestBlockedRun = Math.Max(
                longestBlockedRun, currentBlockedTicks);

        }

        // Owner-replay catch (Tapline seed 9002): a normal hauler trying to
        // enter the centre Well accrued 39 debt ticks behind an at-target
        // Mason. The plane asked the Mason to clear, but the greedy chooser
        // proposed a diagonal that a facing-locked body cannot execute and
        // never tried a cardinal alternative. The stable product invariant is
        // that the mover does not starve; whether this changing match scene
        // needs a turn, a step, both, or an already-clear route is emergent.
        // The traffic governor's exact lease mechanics have pure coverage.
        Assert.True(longestBlockedRun <= 6,
            $"The centre-Well mover remained blocked for "
            + $"{longestBlockedRun} consecutive ticks.");
    }

    [Fact]
    public void RemotePreferredTargetDoesNotParkG3BodiesAtTheFightLeash()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            wellwright.Composition,
            hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline1);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, wellwright), Participant(1, hunter)],
            matchSeed: 9005,
            recordChronology: false);

        int actionableFallbacks = 0;
        int orderFallbacks = 0;
        int fightLeashHolds = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution resolution in
                     step.MindTurns.SelectMany(turn => turn.Commands))
            {
                string reason = resolution.Command.DebugMessage ?? "";
                if (reason.Contains(
                        "target-fallback-from-", StringComparison.Ordinal))
                    actionableFallbacks++;
                if (reason.Contains(
                        "after-fight-visible-enemy-no-actionable-target",
                        StringComparison.Ordinal))
                    orderFallbacks++;
                if (reason.Contains("fight-visible-enemy",
                        StringComparison.Ordinal)
                    && reason.Contains(":chase-limit:",
                        StringComparison.Ordinal))
                    fightLeashHolds++;
            }
        }

        // Owner catch, gallery G3: "weakest" chose a remote body before a
        // nearby contact and parked the west squad at its chase limit. The
        // same scene now demonstrates both deterministic escape paths: a
        // later actionable target wins, or the fight leaf yields to the next
        // authored order. It must never submit the old fight/leash hold.
        Assert.True(actionableFallbacks > 0,
            "The G3 scene never exercised a later actionable target.");
        Assert.True(orderFallbacks > 0,
            "The G3 scene never exercised order fallback after combat.");
        Assert.Equal(0, fightLeashHolds);
    }

    [Fact]
    public void G3FollowerLeavesLeaderRoomAndHealStationKeepsItsChannel()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            wellwright.Composition,
            hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline1);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, wellwright), Participant(1, hunter)],
            matchSeed: 9005,
            recordChronology: false);

        int leaderEvictionsByFollower = 0;
        int longestLeaderEvictionRun = 0;
        int currentLeaderEvictionRun = 0;
        int healChannelEvictions = 0;
        int protectedHealWaits = 0;
        var healedHunterBodies = new HashSet<ActorIdentity>();
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn hunterTurn = Assert.Single(
                step.MindTurns, turn => turn.TeamId == 1);
            Dictionary<int, string> reasons = hunterTurn.Commands.ToDictionary(
                resolution => resolution.Command.UnitId,
                resolution => resolution.Command.DebugMessage ?? "");
            if (step.Tick is >= 80 and <= 125
                && (reasons.GetValueOrDefault(0) ?? "").Contains(
                    ":make-way:u1", StringComparison.Ordinal)
                && (reasons.GetValueOrDefault(1) ?? "").Contains(
                    "shadow-hunter", StringComparison.Ordinal))
            {
                leaderEvictionsByFollower++;
                currentLeaderEvictionRun++;
            }
            else
                currentLeaderEvictionRun = 0;
            longestLeaderEvictionRun = Math.Max(
                longestLeaderEvictionRun, currentLeaderEvictionRun);
            foreach ((int unitId, string reason) in reasons)
            {
                if (reason.Contains("recover-health", StringComparison.Ordinal)
                    && TryMakeWayMover(reason, out int mover)
                    && (reasons.GetValueOrDefault(mover) ?? "").Contains(
                        "recover-health", StringComparison.Ordinal))
                {
                    healChannelEvictions++;
                }
                if (reason.Contains(
                        ":channel-heal:traffic-owner", StringComparison.Ordinal))
                {
                    protectedHealWaits++;
                }
            }
            foreach (GenericActorAuthoritativeEvent fact in
                     step.AuthoritativeEvents)
            {
                if (fact.Payload is GenericActorRuntimeObservation.EventPayload
                    .ArcRelay { Fact: ArcRelayEvent.ZoneHealed healed }
                    && healed.ActorId.TeamId == 1)
                {
                    healedHunterBodies.Add(healed.ActorId);
                }
            }
        }

        // Owner catch, gallery G3 around t90: follower u1 requested the exact
        // occupied tile of leader u0, so the routine patrol leader repeatedly
        // stepped aside. Follow now resolves an adjacent standoff ring.
        Assert.Equal(0, leaderEvictionsByFollower);
        Assert.Equal(0, longestLeaderEvictionRun);

        // The same replay later put two hurt allies into a courtesy swap on
        // one heal tile. Wait is the physical activation action; the body
        // already channelling owns that service point until recovery ends.
        Assert.Equal(0, healChannelEvictions);
        Assert.True(protectedHealWaits > 0,
            "The G3 scene never exercised protected heal-channel ownership.");
        Assert.True(healedHunterBodies.Count >= 2,
            $"Only {healedHunterBodies.Count} Hunter body completed a heal pulse.");

        static bool TryMakeWayMover(string reason, out int mover)
        {
            mover = -1;
            const string marker = ":make-way:u";
            int start = reason.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return false;
            start += marker.Length;
            int end = start;
            while (end < reason.Length && char.IsAsciiDigit(reason[end]))
                end++;
            return int.TryParse(reason[start..end], out mover);
        }
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
