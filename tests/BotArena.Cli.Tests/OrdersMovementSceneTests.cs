using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public void LockboxCentreScreenPocketIsOpenOnBothTeamBindings()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        using JsonDocument mapPlan = JsonDocument.Parse(
            lockbox.NormalizedLayout);
        int[] rect = mapPlan.RootElement
            .GetProperty("zones")
            .GetProperty("centre-screen-pocket")
            .GetProperty("rect")
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        Assert.Equal([12, 11, 14, 13], rect);

        ActorMapDefinition map = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline6).Map;
        Position[] west = (from y in Enumerable.Range(
                               rect[1], rect[3] - rect[1] + 1)
                           from x in Enumerable.Range(
                               rect[0], rect[2] - rect[0] + 1)
                           select new Position(x, y)).ToArray();
        Position[] east = west.Select(tile => new Position(
            map.Width - 1 - tile.X,
            map.Height - 1 - tile.Y)).ToArray();

        // Tapline-6 added a wall through the inherited Tapline-5 rectangle.
        // Zone expansion silently removed those cells and packed Lockbox's
        // centre screen into a choke. A formation pocket is authored floor,
        // not a rectangle whose useful shape changes when a map wall moves.
        Assert.Equal(9, west.Length);
        Assert.All(west.Concat(east), tile => Assert.False(map.IsWall(tile)));
    }

    [Fact]
    public void SquadPlannerConsumesOscillationAvoidanceBeforeSubmission()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation demolition = Compile(
            root, "demolition-march-v1.json");
        TacticalPlaybookCompilation lockbox = CompileLockboxWithPocket(
            root, [10, 11, 12, 13]);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            demolition.Composition,
            lockbox.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, demolition), Participant(1, lockbox)],
            matchSeed: 9001,
            recordChronology: false);

        int plannerAvoidances = 0;
        var commandSeamDenials = new List<string>();
        while (!session.IsCompleted && session.Tick < 150)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn turn = Assert.Single(
                step.MindTurns, candidate => candidate.TeamId == 1);
            foreach (GenericMindCommandResolution command in turn.Commands)
            {
                string reason = command.Command.DebugMessage ?? "";
                if (reason.Contains(
                        ":planner-oscillation-yield@",
                        StringComparison.Ordinal))
                {
                    plannerAvoidances++;
                }
                if (reason.Contains(
                        ":oscillation-yield@",
                        StringComparison.Ordinal))
                {
                    commandSeamDenials.Add(
                        $"t{step.Tick}:u{command.Command.UnitId}:{reason}");
                }
            }
        }

        // Tapline-6 G1 owner catch: a respawned Repulsor walked A-B-A,
        // the oscillation governor denied B, and the squad planner selected B
        // again on every subsequent tick. Keep the old pocket as a fixture so
        // this match still exercises the planner/command seam independently
        // of the product geometry repair: avoidance must appear in planning
        // and must never leak through as a rejected move at submission. The
        // following product-scene test pins the alternative motion itself.
        Assert.True(plannerAvoidances > 0);
        Assert.Empty(commandSeamDenials);
    }

    [Fact]
    public void AlliedTrafficDebtOpensTheLongerDetourInsteadOfWaitingForever()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation demolition = Compile(
            root, "demolition-march-v1.json");
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            demolition.Composition,
            lockbox.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, demolition), Participant(1, lockbox)],
            matchSeed: 9001,
            recordChronology: false);

        int[] watchedUnits = [4, 6];
        var previous = new Dictionary<int, Position>();
        var stationary = watchedUnits.ToDictionary(unit => unit, _ => 0);
        var worst = watchedUnits.ToDictionary(unit => unit, _ => 0);
        bool sawDebtBackedDetour = false;
        while (!session.IsCompleted && session.Tick < 95)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn lockboxTurn = Assert.Single(
                step.MindTurns, candidate => candidate.TeamId == 1);
            foreach (int unit in watchedUnits)
            {
                GenericActorWorldSnapshot.LifeSnapshot? life = step.PostState
                    .ActiveLives.SingleOrDefault(candidate =>
                        candidate.ActorId.TeamId == 1
                        && candidate.ActorId.UnitId == unit
                        && candidate.ActorId.LifeId == 0);
                GenericMindCommandResolution? command = lockboxTurn.Commands
                    .SingleOrDefault(candidate =>
                        candidate.Command.UnitId == unit
                        && candidate.Command.LifeId == 0);
                string reason = command?.Command.DebugMessage ?? "";
                bool staging = life is not null && reason.Contains(
                    "stage-return-lane-ambush", StringComparison.Ordinal);
                if (!staging)
                {
                    stationary[unit] = 0;
                    if (life is not null)
                        previous[unit] = life.Position;
                    continue;
                }
                stationary[unit] = previous.GetValueOrDefault(unit)
                        == life!.Position
                    ? stationary[unit] + 1
                    : 0;
                worst[unit] = Math.Max(worst[unit], stationary[unit]);
                previous[unit] = life.Position;
                sawDebtBackedDetour |= reason.Contains(
                        "urgency-urgent+b2", StringComparison.Ordinal)
                    && reason.Contains(
                        ":epibt-move:", StringComparison.Ordinal);
            }
        }

        // G1 follow-on catch: u4 at (10,12) and u6 at (9,12) each wanted
        // the other's shortest exit while an enemy occupied the remaining
        // mouth. EPIBT generated a valid north/west detour, but WWW ranked
        // ahead because that first step temporarily increased goal distance.
        // Proven allied-block debt must open the already-generated detour.
        Assert.True(sawDebtBackedDetour);
        Assert.All(watchedUnits, unit => Assert.True(
            worst[unit] < 18,
            $"Lockbox u{unit} remained stationary under its ambush move for "
            + $"{worst[unit]} ticks."));
    }

    [Fact]
    public void DirectMoveCourseDoesNotAlternateTurnsAtDeployment()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        TacticalPlaybookCompilation demolition = Compile(
            root, "demolition-march-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            lockbox.Composition,
            demolition.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, lockbox), Participant(1, demolition)],
            matchSeed: 9002,
            recordChronology: false);

        const int watchedUnit = 3;
        Position start = session.ActiveLives.Single(life =>
            life.ActorId.TeamId == 1
            && life.ActorId.UnitId == watchedUnit).Position;
        var visited = new HashSet<Position>();
        int farthest = 0;
        while (!session.IsCompleted && session.Tick < 21)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericActorWorldSnapshot.LifeSnapshot? life = step.PostState
                .ActiveLives.SingleOrDefault(candidate =>
                    candidate.ActorId.TeamId == 1
                    && candidate.ActorId.UnitId == watchedUnit
                    && candidate.ActorId.LifeId == 0);
            if (life is null)
                continue;
            visited.Add(life.Position);
            farthest = Math.Max(
                farthest, start.ChebyshevDistance(life.Position));
        }

        // Cross-sheet catch from the G1 repair gate: a direct move (no
        // authored route) selected a valid persistent course around the
        // deployment reservations, but Chebyshev scoring treated its lateral
        // first step as worse than turning. The body alternated headings on
        // its spawn tile for 100 ticks. A direct course must be just as real
        // to EPIBT as an authored waypoint course.
        Assert.True(visited.Count >= 10,
            $"Demolition u{watchedUnit} visited only {visited.Count} tiles "
            + "during its opening march.");
        Assert.True(farthest >= 7,
            $"Demolition u{watchedUnit} never got more than {farthest} tiles "
            + "from its deployment anchor.");
    }

    [Fact]
    public void CrownedRailIngressDoesNotOrbitTheEastDeploymentRow()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation relay = Compile(root, "relay-circuit-v1.json");
        TacticalPlaybookCompilation rail = Compile(root, "crowned-rail-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            relay.Composition,
            rail.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline4);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, relay), Participant(1, rail)],
            matchSeed: 9001,
            recordChronology: false);

        int[] watched = [3, 5];
        Dictionary<int, Position> starts = session.ActiveLives
            .Where(life => life.ActorId.TeamId == 1
                && watched.Contains(life.ActorId.UnitId))
            .ToDictionary(life => life.ActorId.UnitId, life => life.Position);
        Dictionary<int, HashSet<Position>> visited = watched.ToDictionary(
            unit => unit, _ => new HashSet<Position>());
        Dictionary<int, int> farthest = watched.ToDictionary(unit => unit, _ => 0);

        while (!session.IsCompleted && session.Tick < 31)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericActorWorldSnapshot.LifeSnapshot life in
                     step.PostState.ActiveLives.Where(life =>
                         life.ActorId.TeamId == 1
                         && life.ActorId.LifeId == 0
                         && watched.Contains(life.ActorId.UnitId)))
            {
                int unit = life.ActorId.UnitId;
                visited[unit].Add(life.Position);
                farthest[unit] = Math.Max(
                    farthest[unit],
                    starts[unit].ChebyshevDistance(life.Position));
            }
        }

        // Owner gallery catch, Tapline-4 G1: east Mason u3 alternated two
        // headings on its spawn tile for the whole match, while Lantern u5
        // orbited a handful of nearby cells. The held route had correctly
        // chosen a detour around the deployment reservations, but EPIBT's
        // terrain-only progress score rejected that necessary first step.
        // Both bodies must now commit real geographic progress through the
        // same authored ingress without naming either role in the executor.
        Assert.All(watched, unit =>
        {
            Assert.True(visited[unit].Count >= 10,
                $"East u{unit} visited only {visited[unit].Count} tiles "
                + "during its opening ingress.");
            Assert.True(farthest[unit] >= 5,
                $"East u{unit} never got more than {farthest[unit]} tiles "
                + "from its deployment anchor.");
        });
    }

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
    public void G2BodiesWithoutReachableFiringGroundFallThroughInsteadOfHolding()
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
            recordChronology: false);

        int orderFallbacks = 0;
        int noFiringGroundHolds = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution resolution in
                     step.MindTurns.SelectMany(turn => turn.Commands))
            {
                string reason = resolution.Command.DebugMessage ?? "";
                if (reason.Contains(
                        "after-fight-visible-enemy-no-actionable-target",
                        StringComparison.Ordinal))
                    orderFallbacks++;
                if (reason.Contains(
                        "fight-no-firing-ground", StringComparison.Ordinal))
                    noFiringGroundHolds++;
            }
        }

        // Owner replay catch, gallery G2 around t247: three east-side bodies
        // accepted the ordinary fight leaf, one closed while two stood in the
        // south Well under fight-no-firing-ground. Target selection and intent
        // planning now share exact reachable firing geometry, so such a body
        // runs its next authored order instead of entering a false fight.
        Assert.True(orderFallbacks > 0,
            "The G2 scene never exercised combat-to-order fallback.");
        Assert.Equal(0, noFiringGroundHolds);
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

    [Fact]
    public void LoadedStandardCarrierDoesNotSelectAForbiddenSwiftStrafe()
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
            recordChronology: false);

        int loadedReturnTicks = 0;
        int submittedMovement = 0;
        int falseMoveTracesOnWait = 0;
        int explainedCadenceHolds = 0;
        int longestStationaryReturn = 0;
        var priorPositions = new Dictionary<ActorIdentity, Position>();
        var stationaryReturns = new Dictionary<ActorIdentity, int>();
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                    step.PostState.Mode);
            HashSet<ActorIdentity> carriers = arc.VisibleCores
                .Where(core => core.CarrierActorId is not null)
                .Select(core => core.CarrierActorId!)
                .ToHashSet();
            GenericMindRuntimeTurn hunterTurn = Assert.Single(
                step.MindTurns, turn => turn.TeamId == 0);
            var seenReturning = new HashSet<ActorIdentity>();
            foreach (GenericMindCommandResolution resolution in
                     hunterTurn.Commands)
            {
                GenericActorWorldSnapshot.LifeSnapshot? life =
                    step.PostState.ActiveLives.FirstOrDefault(candidate =>
                        candidate.ActorId.TeamId == 0
                        && candidate.ActorId.UnitId
                            == resolution.Command.UnitId
                        && candidate.ActorId.LifeId
                            == resolution.Command.LifeId);
                if (life is null || !carriers.Contains(life.ActorId))
                    continue;
                string reason = resolution.Command.DebugMessage ?? "";
                if (!reason.Contains(
                        "return-to-reactor", StringComparison.Ordinal))
                    continue;
                seenReturning.Add(life.ActorId);
                loadedReturnTicks++;
                bool moveTrace = reason.Contains(
                    ":epibt-move:", StringComparison.Ordinal);
                bool submitted = string.Equals(
                    resolution.Command.ActionId,
                    "move-eight-way",
                    StringComparison.Ordinal);
                if (submitted)
                    submittedMovement++;
                // A simultaneous collision can authoritatively block a real
                // submitted move; that is honest. The regression is a move
                // trace attached to a submitted Wait because an action row
                // with AllowedByForm=false was selected and then discarded.
                if (moveTrace && string.Equals(
                        resolution.Command.ActionId,
                        "wait",
                        StringComparison.Ordinal))
                    falseMoveTracesOnWait++;
                if (!submitted && reason.Contains(
                        "carry-relocation-cooldown-until-",
                        StringComparison.Ordinal))
                    explainedCadenceHolds++;
                bool moved = priorPositions.TryGetValue(
                        life.ActorId, out Position prior)
                    && prior != life.Position;
                int stationaryReturn = moved
                    ? 0
                    : stationaryReturns.GetValueOrDefault(life.ActorId) + 1;
                stationaryReturns[life.ActorId] = stationaryReturn;
                longestStationaryReturn = Math.Max(
                    longestStationaryReturn, stationaryReturn);
            }
            foreach (ActorIdentity stale in stationaryReturns.Keys
                         .Where(actor => !seenReturning.Contains(actor))
                         .ToArray())
                stationaryReturns.Remove(stale);
            foreach (GenericActorWorldSnapshot.LifeSnapshot life in
                     step.PostState.ActiveLives)
                priorPositions[life.ActorId] = life.Position;
        }

        // Owner catch, G1 around t30: west u6 held the centre Core at (13,11)
        // until destruction. EPIBT selected north-west every tick, but the
        // action chooser preferred strafe-eight-way solely because it would
        // preserve aim—even though Towline's legality row said
        // AllowedByForm=false. Submission then fell back to Wait while the
        // trace falsely retained epibt-move. A selected physical move must
        // become a real chassis-owned movement command.
        Assert.True(loadedReturnTicks > 0);
        Assert.True(submittedMovement > 0);
        Assert.Equal(0, falseMoveTracesOnWait);
        Assert.True(explainedCadenceHolds > 0);
        Assert.True(longestStationaryReturn < 8,
            $"A loaded returning carrier stayed still for "
            + $"{longestStationaryReturn} ticks.");
    }

    [Theory]
    [InlineData(
        "tapline-split-intercept-v1",
        "tapline-shadow-scissors-v2",
        9307)]
    [InlineData(
        "tapline-shadow-scissors-v2",
        "tapline-distributed-wells-v1",
        9306)]
    [InlineData(
        "tapline-split-intercept-v2",
        "tapline-shadow-scissors-v3",
        9403)]
    [InlineData(
        "tapline-distributed-wells-v2",
        "tapline-shadow-scissors-v3",
        9406)]
    public void HostileMoveContentionIsRememberedAcrossCarrierCadenceHold(
        string westStrategy,
        string eastStrategy,
        int seed)
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation west = CompileStrategy(
            root, westStrategy);
        TacticalPlaybookCompilation east = CompileStrategy(
            root, eastStrategy);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            west.Composition,
            east.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, west), Participant(1, east)],
            matchSeed: (ulong)seed,
            recordChronology: false);

        var stationary = new Dictionary<ArcRelayCoreId, CarrierStation>();
        int longestStationaryCarrier = 0;
        int deniedStepTraces = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution resolution in
                     step.MindTurns.SelectMany(turn => turn.Commands))
            {
                if ((resolution.Command.DebugMessage ?? "").Contains(
                        ":avoid-denied-", StringComparison.Ordinal))
                    deniedStepTraces++;
            }

            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                    step.PostState.Mode);
            var carried = new HashSet<ArcRelayCoreId>();
            foreach (ArcRelayCoreState core in arc.VisibleCores.Where(core =>
                         core.CarrierActorId is not null))
            {
                carried.Add(core.CoreId);
                CarrierStation next = stationary.TryGetValue(
                        core.CoreId, out CarrierStation? prior)
                    && prior.Carrier == core.CarrierActorId
                    && prior.Position == core.Position
                        ? prior with { Ticks = prior.Ticks + 1 }
                        : new CarrierStation(
                            core.CarrierActorId!, core.Position, 1);
                stationary[core.CoreId] = next;
                longestStationaryCarrier = Math.Max(
                    longestStationaryCarrier, next.Ticks);
            }
            foreach (ArcRelayCoreId stale in stationary.Keys
                         .Where(core => !carried.Contains(core)).ToArray())
                stationary.Remove(stale);
        }

        // These two scenes independently produced the same failure: opposing
        // bodies repeatedly entered one visibly empty tile together. The
        // carrier's mandatory relocation wait then erased the generic
        // PreviousActionResolution memory, so it retried forever (59 and 32
        // ticks respectively). Two later three-step-planner scenes exposed
        // the second half of the same contract: making the denied tile illegal
        // without putting it in the carrier's distance field scored WAIT as
        // cheaper than every valid detour and produced 30/34-tick stalls.
        // The planner must carry that denial across the cadence hold and score
        // a genuinely different operation against the same obstacle set.
        Assert.True(deniedStepTraces > 0,
            "The scenes never exercised remembered denied-step routing.");
        Assert.True(longestStationaryCarrier < 30,
            $"A contested carrier stayed on one tile for "
            + $"{longestStationaryCarrier} ticks.");
    }

    [Fact]
    public void ShiftedSquadOperationNeverPublishesUnavailableFirstMove()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation west = CompileStrategy(
            root, "tapline-distributed-wells-v4");
        TacticalPlaybookCompilation east = CompileStrategy(
            root, "tapline-mid-rush-control-v3");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            west.Composition,
            east.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, west), Participant(1, east)],
            matchSeed: 9012,
            recordChronology: false);

        var falseMoveTraces = new List<string>();
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindRuntimeTurn turn in step.MindTurns)
            foreach (GenericMindCommandResolution resolution in turn.Commands)
            {
                string reason = resolution.Command.DebugMessage ?? "";
                if (string.Equals(
                        resolution.Command.ActionId,
                        "wait",
                        StringComparison.Ordinal)
                    && reason.Contains(
                        ":epibt-move:", StringComparison.Ordinal)
                    && reason.Contains(
                        ":move-blocked:", StringComparison.Ordinal))
                {
                    falseMoveTraces.Add(
                        $"t{step.Tick}:team{turn.TeamId}:"
                        + $"u{resolution.Command.UnitId}:{reason}");
                }
            }
        }

        Assert.Empty(falseMoveTraces);
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
    private sealed record CarrierStation(
        ActorIdentity Carrier,
        Position Position,
        int Ticks);

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

    private static TacticalPlaybookCompilation CompileLockboxWithPocket(
        string root,
        int[] rect)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string playbook = Path.Combine(package, "playbooks", "lockbox-v1.json");
        string mapPlan = Path.Combine(
            package, "map-plans", "tapline-6", "lockbox-v1.json");
        JsonObject map = JsonNode.Parse(File.ReadAllText(mapPlan))!.AsObject();
        map["zones"]!["centre-screen-pocket"]!["rect"] = new JsonArray(
            rect.Select(value => JsonValue.Create(value)).ToArray());
        return ArcRelayTacticalPlaybookCompiler.Compile(
            File.ReadAllBytes(playbook),
            Encoding.UTF8.GetBytes(map.ToJsonString()),
            playbook,
            mapPlan + "#test-pocket");
    }

    private static TacticalPlaybookCompilation CompileStrategy(
        string root,
        string strategyId) => ArcRelayTacticalPlaybookCompiler.Compile(
        Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "strategy-depth",
            strategyId,
            "orders.json"));

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
