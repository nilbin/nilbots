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
            loopProfile: ArcRelayLoopProfile.Tapline3);

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
        // A body authored to farm a Well should hold that exact service tile
        // until a Core is born; a raw single-tile ceiling therefore labels
        // correct objective work as a deadlock (this seed legitimately holds
        // the south Well for 145 ticks). Mechanical immobility is covered by
        // the G3 closed-cycle/recovery-progress scene and the Core-level
        // gallery eligibility bar, not by punishing at-target waits.
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
            "epibt-", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            ":op-", StringComparison.Ordinal));
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
            loopProfile: ArcRelayLoopProfile.Tapline3);

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
        // Mason. The old one-step chooser proposed a diagonal that a
        // facing-locked body could not execute and never tried the cardinal
        // turn-and-vacate operation. The stable product invariant is that the
        // mover does not starve; whether this changing match scene needs a
        // turn, a step, both, or an already-clear route is emergent. The
        // planner's exact joint-operation mechanics have pure coverage.
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
            loopProfile: ArcRelayLoopProfile.Tapline3);

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
    public void G3FollowerLeavesItsLeadersCourseClear()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            wellwright.Composition,
            hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, wellwright), Participant(1, hunter)],
            matchSeed: 9005,
            recordChronology: false);

        int leaderEvictionsByFollower = 0;
        int longestLeaderEvictionRun = 0;
        int currentLeaderEvictionRun = 0;
        int leaderRouteSteps = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn hunterTurn = Assert.Single(
                step.MindTurns, turn => turn.TeamId == 1);
            Dictionary<int, string> reasons = hunterTurn.Commands.ToDictionary(
                resolution => resolution.Command.UnitId,
                resolution => resolution.Command.DebugMessage ?? "");
            if (step.Tick is >= 0 and <= 45
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
            if (step.Tick is >= 0 and <= 45
                && (reasons.GetValueOrDefault(0) ?? "").Contains(
                    "shadow-hunt:march", StringComparison.Ordinal))
                leaderRouteSteps++;
        }

        // Owner catch, coordinated gallery G3 launch: follower u1 repeatedly
        // requested the route tile leader u0 was about to enter. Follow now
        // resolves an adjacent ring and excludes the leader's planned step.
        Assert.Equal(0, leaderEvictionsByFollower);
        Assert.Equal(0, longestLeaderEvictionRun);
        Assert.True(leaderRouteSteps > 0,
            "The leader never advanced under its authored route in the "
            + "opening scene.");
    }

    [Theory]
    [InlineData(9005, false)]
    [InlineData(9001, true)]
    public void GalleryScenesHaveNoClosedAlliedMovementCycleOrRecoveryOrbit(
        int seed,
        bool hunterIsWest)
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        GenericActorParticipantConfiguration[] participants = hunterIsWest
            ? [Participant(0, hunter), Participant(1, wellwright)]
            : [Participant(0, wellwright), Participant(1, hunter)];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunterIsWest ? hunter.Composition : wellwright.Composition,
            hunterIsWest ? wellwright.Composition : hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);
        using var session = new GenericActorMatchSession(
            definition,
            participants,
            matchSeed: (ulong)seed,
            recordChronology: true);

        int closedMovementCycles = 0;
        var recovery = new Dictionary<ActorIdentity, RecoveryProgress>();
        var carrierCourses = new Dictionary<ArcRelayCoreId, CarrierCourse>();
        int worstUncontestedRecoveryNoProgress = 0;
        int worstCarrierAlternation = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
                lives = step.PostState.ActiveLives.ToDictionary(
                    life => life.ActorId);
            foreach (GenericActorAuthoritativeEvent fact in
                     step.AuthoritativeEvents)
            {
                if (fact.Payload is not GenericActorRuntimeObservation.EventPayload
                    .MovementBlocked blocked)
                    continue;
                // The engine's event type intentionally exposes the same
                // actor/from/to triple as the replay. The generic cycle
                // helper below groups it after all facts for this tick.
            }
            closedMovementCycles += ClosedMovementCycles(
                step.AuthoritativeEvents);

            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                    step.PostState.Mode);
            HashSet<ArcRelayCoreId> carried = [];
            foreach (ArcRelayCoreState core in arc.VisibleCores.Where(core =>
                         core.CarrierActorId is not null))
            {
                carried.Add(core.CoreId);
                ActorIdentity carrier = core.CarrierActorId!;
                if (!carrierCourses.TryGetValue(
                        core.CoreId, out CarrierCourse? course)
                    || course.Carrier != carrier)
                {
                    carrierCourses[core.CoreId] = new CarrierCourse(
                        carrier, core.Position, null, 0);
                    continue;
                }
                if (core.Position == course.Current)
                    continue;
                int alternating = course.Previous is Position previous
                    && core.Position == previous
                        ? course.AlternatingSteps + 1
                        : 0;
                carrierCourses[core.CoreId] = course with
                {
                    Previous = course.Current,
                    Current = core.Position,
                    AlternatingSteps = alternating,
                };
                worstCarrierAlternation = Math.Max(
                    worstCarrierAlternation, alternating);
            }
            foreach (ArcRelayCoreId stale in carrierCourses.Keys
                         .Where(coreId => !carried.Contains(coreId))
                         .ToArray())
                carrierCourses.Remove(stale);

            foreach (GenericMindRuntimeTurn turn in step.MindTurns)
            {
                HashSet<ActorIdentity> seen = [];
                Position[] enemies = lives.Values
                    .Where(life => life.ActorId.TeamId != turn.TeamId)
                    .Select(life => life.Position)
                    .ToArray();
                foreach (GenericMindCommandResolution resolution in turn.Commands)
                {
                    string reason = resolution.Command.DebugMessage ?? "";
                    Position? goal = RecoveryGoal(reason);
                    ActorIdentity actor = new(
                        turn.TeamId,
                        resolution.Command.UnitId,
                        resolution.Command.LifeId);
                    if (goal is not Position healing
                        || !lives.TryGetValue(actor, out var life))
                        continue;
                    seen.Add(actor);
                    int distance = life.Position.ChebyshevDistance(healing);
                    bool contested = enemies.Any(enemy =>
                        enemy.ChebyshevDistance(healing) <= 1);
                    bool staged = reason.Contains(
                        ":at-contribution:heal:", StringComparison.Ordinal);
                    if (!recovery.TryGetValue(actor, out RecoveryProgress? prior)
                        || prior.Goal != healing
                        || life.Health > prior.Health
                        || distance < prior.BestDistance)
                    {
                        recovery[actor] = new RecoveryProgress(
                            healing, life.Health, distance, 0);
                    }
                    else
                    {
                        RecoveryProgress next = prior with
                        {
                            Health = life.Health,
                            UncontestedTicks = prior.UncontestedTicks
                                + (contested || staged ? 0 : 1),
                        };
                        recovery[actor] = next;
                        worstUncontestedRecoveryNoProgress = Math.Max(
                            worstUncontestedRecoveryNoProgress,
                            next.UncontestedTicks);
                    }
                }
                foreach (ActorIdentity stale in recovery.Keys
                             .Where(actor => actor.TeamId == turn.TeamId
                                 && !seen.Contains(actor))
                             .ToArray())
                    recovery.Remove(stale);
            }
        }

        Assert.Equal(0, closedMovementCycles);
        Assert.True(worstUncontestedRecoveryNoProgress < 18,
            $"A recovery order made no uncontested health/distance progress "
            + $"for {worstUncontestedRecoveryNoProgress} ticks.");
        Assert.True(worstCarrierAlternation < 8,
            $"A Core carrier alternated between the same two tiles for "
            + $"{worstCarrierAlternation} consecutive steps.");
    }

    [Fact]
    public void HunterMasonPlacesHardlightOnlyOnAuthoredEnemyLaneGate()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9012,
            recordChronology: false);

        var operations = new HashSet<string>(StringComparer.Ordinal);
        var placements = new List<(Position Construct, Position Builder)>();
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                step.PostState.Mode);
            foreach (ArcRelaySignatureState signature in arc.VisibleSignatures)
            {
                if (signature.Kind
                        != ArcRelaySignatureDefinition.SignatureKind.HardlightBlock
                    || signature.OwnerTeamId != 0
                    || !operations.Add(signature.OperationId))
                    continue;
                Position builder = step.PostState.ActiveLives.Single(life =>
                    life.ActorId == signature.OwnerActorId).Position;
                placements.Add((Assert.Single(signature.Positions), builder));
            }
        }

        // One semantic anchor owns both halves: movement resolves a legal
        // adjacent stand tile, then use places on the exact enemy lane gate.
        // No companion "staging anchor" or duplicate coordinate is authored.
        Assert.NotEmpty(placements);
        Assert.All(placements, placement =>
        {
            Assert.Equal(new Position(25, 13), placement.Construct);
            Assert.Equal(1,
                placement.Builder.ChebyshevDistance(placement.Construct));
        });
    }

    private static Position? RecoveryGoal(string reason)
    {
        const string marker = "recover-health";
        int recovery = reason.IndexOf(marker, StringComparison.Ordinal);
        if (recovery < 0)
            return null;
        int at = reason.IndexOf("march@", recovery, StringComparison.Ordinal);
        if (at < 0)
            return null;
        int comma = reason.IndexOf(',', at + 6);
        int colon = reason.IndexOf(':', comma + 1);
        return comma > at && colon > comma
            && int.TryParse(reason[(at + 6)..comma], out int x)
            && int.TryParse(reason[(comma + 1)..colon], out int y)
                ? new Position(x, y)
                : null;
    }

    private static int ClosedMovementCycles(
        IEnumerable<GenericActorAuthoritativeEvent> facts)
    {
        var events = facts.Select(fact => fact.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.MovementBlocked>()
            .GroupBy(value => value.ActorId.TeamId);
        int result = 0;
        foreach (var team in events)
        {
            Dictionary<Position, int> occupant = team.ToDictionary(
                value => value.From,
                value => value.ActorId.UnitId);
            Dictionary<int, int> edges = team
                .Where(value => occupant.ContainsKey(value.AttemptedTo))
                .ToDictionary(value => value.ActorId.UnitId,
                    value => occupant[value.AttemptedTo]);
            foreach (int start in edges.Keys)
            {
                var seen = new HashSet<int>();
                int current = start;
                while (edges.TryGetValue(current, out int next))
                {
                    if (!seen.Add(current))
                    {
                        if (seen.Count >= 3)
                            result++;
                        break;
                    }
                    current = next;
                }
            }
        }
        return result;
    }

    private sealed record RecoveryProgress(
        Position Goal,
        int Health,
        int BestDistance,
        int UncontestedTicks);

    private sealed record CarrierCourse(
        ActorIdentity Carrier,
        Position Current,
        Position? Previous,
        int AlternatingSteps);

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
        // Untagged ATL2 is the product form. If this scene ever silently
        // falls back to the historical greedy diagnostic, its EPIBT reason
        // assertions above fail rather than blessing the wrong planner.
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
