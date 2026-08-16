using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalMovementGovernorTests
{
    [Fact]
    public void ClearanceChainFindsNearestVacancyThroughAlliedQueue()
    {
        TacticalClearancePlanner.Node[] nodes =
        [
            new(1, HasExit: false, OccupiedSuccessors: [2]),
            new(2, HasExit: false, OccupiedSuccessors: [1, 3]),
            new(3, HasExit: true, OccupiedSuccessors: [2]),
        ];

        Assert.Equal([1, 2, 3], TacticalClearancePlanner.Resolve(1, nodes));
    }

    [Fact]
    public void ClearanceChainUsesShortestPathThenStableUnitId()
    {
        TacticalClearancePlanner.Node[] nodes =
        [
            new(1, HasExit: false, OccupiedSuccessors: [4, 2]),
            new(2, HasExit: true, OccupiedSuccessors: []),
            new(4, HasExit: true, OccupiedSuccessors: []),
        ];

        Assert.Equal([1, 2], TacticalClearancePlanner.Resolve(1, nodes));
    }

    [Fact]
    public void ClosedClearanceCycleDoesNotInventDisplacement()
    {
        TacticalClearancePlanner.Node[] nodes =
        [
            new(1, HasExit: false, OccupiedSuccessors: [2]),
            new(2, HasExit: false, OccupiedSuccessors: [1]),
        ];

        Assert.Empty(TacticalClearancePlanner.Resolve(1, nodes));
    }

    [Fact]
    public void PostureSelectsFrontRearOrWholeDestinationWithoutInventingOffsets()
    {
        Position[] zone =
        [
            new(10, 8),
            new(11, 8),
            new(12, 8),
            new(10, 9),
            new(11, 9),
            new(12, 9),
        ];
        var own = new Position(2, 8);
        var enemy = new Position(28, 8);

        Assert.Equal(
            [new Position(12, 8), new Position(12, 9)],
            TacticalPosturePlanner.Resolve(zone, "front", own, enemy));
        Assert.Equal(
            [new Position(10, 8), new Position(10, 9)],
            TacticalPosturePlanner.Resolve(zone, "rear", own, enemy));
        Assert.Equal(
            zone,
            TacticalPosturePlanner.Resolve(zone, "line", own, enemy));
    }

    [Fact]
    public void PostureRotatesWithTeamPerspective()
    {
        Position[] westZone =
        [
            new(10, 8),
            new(11, 8),
            new(12, 8),
        ];
        Position[] eastZone = westZone.Select(point =>
            new Position(30 - point.X, 22 - point.Y)).ToArray();

        Position westFront = Assert.Single(TacticalPosturePlanner.Resolve(
            westZone, "front", new Position(2, 8), new Position(28, 14)));
        Position eastFront = Assert.Single(TacticalPosturePlanner.Resolve(
            eastZone, "front", new Position(28, 14), new Position(2, 8)));

        Assert.Equal(
            new Position(30 - westFront.X, 22 - westFront.Y),
            eastFront);
    }

    [Fact]
    public void FollowUsesAStandOffRingAndNeverTargetsTheLeaderTile()
    {
        var leader = new Position(10, 10);
        Position[] goals = TacticalFollowPlanner.StandoffGoals(
            leader,
            point => point != new Position(9, 9));

        Assert.Equal(7, goals.Length);
        Assert.DoesNotContain(leader, goals);
        Assert.DoesNotContain(new Position(9, 9), goals);
        Assert.All(goals, point => Assert.Equal(
            1, point.ChebyshevDistance(leader)));
    }

    [Fact]
    public void RouteCourseStaysChosenAcrossATemptingReplan()
    {
        var governor = new TacticalRouteGovernor();
        ActorIdentity actor = new(1, 0, 0);
        var start = new Position(29, 18);
        var north = new Position(28, 17);
        var west = new Position(27, 16);
        var goal = new Position(23, 11);
        int plans = 0;

        Position[] Plan()
        {
            plans++;
            return [start, north, west, goal];
        }

        TacticalRouteGovernor.CourseStep chosen = governor.Resolve(
            actor, "hunter-shadow-hunt", "shadow-hunt-loop",
            lap: 0, waypoint: 3, start, goal, Plan, _ => false);
        Assert.Equal(north, chosen.Next);
        Assert.Contains("route-commit", chosen.Trace);

        // Were the route derived again here, the planner could choose the
        // opposite fork. It is not consulted: reaching the committed tile
        // advances the held course instead.
        TacticalRouteGovernor.CourseStep held = governor.Resolve(
            actor, "hunter-shadow-hunt", "shadow-hunt-loop",
            lap: 0, waypoint: 3, north, goal,
            () => throw new InvalidOperationException("must stay sticky"),
            _ => false);
        Assert.Equal(west, held.Next);
        Assert.Contains("route-held", held.Trace);
        Assert.Equal(1, plans);
    }

    [Fact]
    public void SignatureMayReadHeldForwardCourseWithoutResettingIt()
    {
        var governor = new TacticalRouteGovernor();
        ActorIdentity actor = new(1, 0, 0);
        var start = new Position(10, 10);
        var first = new Position(11, 10);
        var second = new Position(12, 10);
        var goal = new Position(13, 10);
        int plans = 0;

        governor.Resolve(
            actor, "return-continue", "safe-return", 0, 2, start, goal,
            () => { plans++; return [start, first, second, goal]; },
            _ => false);
        TacticalRouteGovernor.ForwardCourseRead read =
            governor.ReadForwardCourse(actor, "safe-return", start);

        Assert.Equal([start, first, second, goal], read.Tiles);
        Assert.Equal("route-course-held", read.Trace);

        TacticalRouteGovernor.CourseStep resumed = governor.Resolve(
            actor, "return-continue", "safe-return", 0, 2, second, goal,
            () => throw new InvalidOperationException(
                "a non-movement sibling must not re-plan the held course"),
            _ => false);
        Assert.Equal(goal, resumed.Next);
        Assert.Equal(1, plans);
    }

    [Fact]
    public void ForwardCourseReadRefusesWrongRouteLifeAndActiveDetour()
    {
        var governor = new TacticalRouteGovernor();
        ActorIdentity actor = new(0, 1, 0);
        var start = new Position(5, 5);
        var next = new Position(6, 5);
        var rejoin = new Position(7, 5);
        var goal = new Position(8, 5);
        governor.Resolve(actor, "move", "route-a", 0, 1, start, goal,
            () => [start, next, rejoin, goal], _ => false);

        Assert.Equal("route-projection-absent",
            governor.ReadForwardCourse(actor, "route-b", start).Trace);
        Assert.Equal("route-projection-absent",
            governor.ReadForwardCourse(new ActorIdentity(0, 1, 1),
                "route-a", start).Trace);

        governor.TryTrafficDetour(actor, start, 1, candidate =>
            candidate == rejoin
                ? [start, new Position(5, 4), new Position(6, 4), rejoin]
                : null);
        Assert.Equal("route-course-detouring",
            governor.ReadForwardCourse(actor, "route-a", start).Trace);
    }

    [Fact]
    public void ForwardCourseReadRefusesAnAmbiguousFoldAfterRelocation()
    {
        var governor = new TacticalRouteGovernor();
        ActorIdentity actor = new(0, 1, 0);
        var start = new Position(5, 5);
        var repeated = new Position(7, 5);
        governor.Resolve(
            actor, "move", "folded-route", 0, 1, start, new Position(9, 5),
            () =>
            [
                start,
                new Position(6, 5),
                repeated,
                new Position(8, 5),
                repeated,
                new Position(9, 5),
            ],
            _ => false);

        TacticalRouteGovernor.ForwardCourseRead read =
            governor.ReadForwardCourse(actor, "folded-route", repeated);

        Assert.Empty(read.Tiles);
        Assert.Equal("route-projection-off-course", read.Trace);
    }

    [Fact]
    public void RouteForwardSelectsTheFarthestSafeStraightProgress()
    {
        Position[] course =
        [
            new(2, 6),
            new(3, 6),
            new(4, 6),
            new(5, 6),
            new(6, 6),
        ];
        TacticalRouteForwardPlanner.Resolution result =
            TacticalRouteForwardPlanner.Resolve(
                course,
                maximumTravelTiles: 3,
                minimumForwardTiles: 2,
                atFinalWaypoint: false,
                new HashSet<ProjectileHeading> { ProjectileHeading.East },
                occupied: _ => false);

        Assert.True(result.Accepted);
        Assert.Equal(new Position(5, 6), result.Destination);
        Assert.Equal(3, result.ForwardTiles);
        Assert.Equal(ProjectileHeading.East, result.Heading);
        Assert.Equal("forward-gain-3-heading-east", result.Trace);
    }

    [Fact]
    public void RouteForwardDeclinesBendsAndInsufficientUnblockedProgress()
    {
        Position[] bent =
        [
            new(2, 6),
            new(3, 6),
            new(4, 6),
            new(4, 7),
        ];
        HashSet<ProjectileHeading> east = [ProjectileHeading.East];

        TacticalRouteForwardPlanner.Resolution bend =
            TacticalRouteForwardPlanner.Resolve(
                bent, 3, 2, atFinalWaypoint: false, east, _ => false);
        TacticalRouteForwardPlanner.Resolution blocked =
            TacticalRouteForwardPlanner.Resolve(
                [new(2, 6), new(3, 6), new(4, 6), new(5, 6)],
                3, 2, atFinalWaypoint: false, east,
                tile => tile == new Position(4, 6));

        Assert.False(bend.Accepted);
        Assert.Equal("course-bends-after-2", bend.Trace);
        Assert.False(blocked.Accepted);
        Assert.Equal("forward-gain-1-below-2", blocked.Trace);
    }

    [Fact]
    public void RouteCourseResetsOnRungLapAndImpossibleStep()
    {
        var governor = new TacticalRouteGovernor();
        ActorIdentity actor = new(1, 0, 0);
        var a = new Position(10, 10);
        var b = new Position(11, 10);
        var c = new Position(10, 11);
        var goal = new Position(12, 10);

        Assert.Equal(b, governor.Resolve(
            actor, "patrol", "loop", 0, 0, a, goal,
            () => [a, b, goal], _ => false).Next);
        TacticalRouteGovernor.CourseStep replanned = governor.Resolve(
            actor, "patrol", "loop", 0, 0, a, goal,
            () => [a, c, goal], tile => tile == b);
        Assert.Equal(c, replanned.Next);
        Assert.Contains("route-replan-impossible", replanned.Trace);

        TacticalRouteGovernor.CourseStep newLap = governor.Resolve(
            actor, "patrol", "loop", 1, 0, a, goal,
            () => [a, b, goal], _ => false);
        Assert.Contains("route-commit", newLap.Trace);

        governor.Reset(actor.UnitId);
        TacticalRouteGovernor.CourseStep afterRung = governor.Resolve(
            actor, "patrol", "loop", 1, 0, a, goal,
            () => [a, c, goal], _ => false);
        Assert.Equal(c, afterRung.Next);
        Assert.Contains("route-commit", afterRung.Trace);
    }

    [Fact]
    public void TrafficDetourRejoinsHeldCourseWithoutReplanningDirection()
    {
        var governor = new TacticalRouteGovernor();
        ActorIdentity actor = new(1, 0, 0);
        var start = new Position(10, 10);
        var blocked = new Position(11, 10);
        var rejoin = new Position(12, 10);
        var after = new Position(13, 10);
        var goal = new Position(14, 10);
        var north = new Position(10, 9);
        var northEast = new Position(11, 9);
        int originalPlans = 0;

        Position[] Original()
        {
            originalPlans++;
            return [start, blocked, rejoin, after, goal];
        }

        Assert.Equal(blocked, governor.Resolve(
            actor, "patrol", "loop", 0, 0, start, goal,
            Original, _ => false).Next);

        TacticalRouteGovernor.CourseStep detour = governor.TryTrafficDetour(
            actor,
            start,
            maximumExtraSteps: 1,
            candidate => candidate == rejoin
                ? [start, north, northEast, rejoin]
                : null);
        Assert.Equal(north, detour.Next);
        Assert.Contains("route-detour-commit-rejoin-12-10", detour.Trace);

        Assert.Equal(northEast, governor.Resolve(
            actor, "patrol", "loop", 0, 0, north, goal,
            () => throw new InvalidOperationException(
                "a detour must not reselect route direction"),
            _ => false).Next);
        Assert.Equal(rejoin, governor.Resolve(
            actor, "patrol", "loop", 0, 0, northEast, goal,
            () => throw new InvalidOperationException(
                "a detour must remain sticky"),
            _ => false).Next);

        TacticalRouteGovernor.CourseStep resumed = governor.Resolve(
            actor, "patrol", "loop", 0, 0, rejoin, goal,
            () => throw new InvalidOperationException(
                "rejoining must resume the original course"),
            _ => false);
        Assert.Equal(after, resumed.Next);
        Assert.Contains("route-detour-rejoined", resumed.Trace);
        Assert.Equal(1, originalPlans);
    }

    [Fact]
    public void TrafficDetourThatExceedsCorridorBudgetChoosesToWait()
    {
        var governor = new TacticalRouteGovernor();
        ActorIdentity actor = new(0, 2, 0);
        var start = new Position(4, 4);
        var blocked = new Position(5, 4);
        var rejoin = new Position(6, 4);
        var goal = new Position(7, 4);

        governor.Resolve(
            actor, "march", "route", 0, 0, start, goal,
            () => [start, blocked, rejoin, goal], _ => false);
        TacticalRouteGovernor.CourseStep result =
            governor.TryTrafficDetour(
                actor,
                start,
                maximumExtraSteps: 1,
                candidate => candidate == rejoin
                    ? [
                        start,
                        new Position(4, 3),
                        new Position(5, 3),
                        new Position(6, 3),
                        rejoin,
                    ]
                    : null);

        Assert.Equal(blocked, result.Next);
        Assert.Contains("route-detour-considered-wait", result.Trace);
    }

    [Fact]
    public void WaypointArrivalRingCannotSwallowAShortRouteLeg()
    {
        var waypoint = new Position(8, 8);
        var next = new Position(10, 8);

        Assert.Equal(1, TacticalRouteGovernor.EffectiveArrivalRadius(
            requested: 2, waypoint, next));
        Assert.False(TacticalRouteGovernor.ReachedWaypoint(
            distance: 2, previousBestDistance: 3,
            effectiveArrivalRadius: 1));
        Assert.True(TacticalRouteGovernor.ReachedWaypoint(
            distance: 1, previousBestDistance: 2,
            effectiveArrivalRadius: 1));

        Assert.Equal(0, TacticalRouteGovernor.EffectiveArrivalRadius(
            requested: 2,
            waypoint: new Position(8, 8),
            nextWaypoint: new Position(9, 8)));
    }

    [Fact]
    public void ProximityWithoutNewApproachCannotLapTheRouteCursor()
    {
        var waypoint = new Position(8, 8);
        var next = new Position(12, 8);
        int arrival = TacticalRouteGovernor.EffectiveArrivalRadius(
            requested: 2, waypoint, next);

        Assert.False(TacticalRouteGovernor.ReachedWaypoint(
            distance: 1, previousBestDistance: 1, arrival));
        Assert.True(TacticalRouteGovernor.ReachedWaypoint(
            distance: 0, previousBestDistance: 0, arrival));
    }

    [Fact]
    public void MissingTerrainCourseIsReportedAsNoPath()
    {
        var governor = new TacticalRouteGovernor();
        var actor = new ActorIdentity(0, 3, 0);
        var start = new Position(7, 7);
        var destination = new Position(8, 6);

        TacticalRouteGovernor.CourseStep result = governor.Resolve(
            actor,
            "centre-push",
            "own-well-run",
            lap: 0,
            waypoint: 1,
            start,
            destination,
            plan: () => null,
            impossible: _ => false);

        Assert.Null(result.Next);
        Assert.Equal(
            TacticalRouteGovernor.CourseDisposition.NoPath,
            result.Disposition);
        Assert.Contains("route-commit-no-path", result.Trace);
    }

    [Fact]
    public void RecoverInterruptionCoolsOnlyTheSameInjury()
    {
        var governor = new RecoveryEpisodeGovernor();
        var actor = new ActorIdentity(0, 0, 0);

        Assert.False(governor.Observe(
            actor, tick: 0, health: 3, maximumHealth: 3,
            otherwiseEligible: false).Admitted);
        Assert.True(governor.Observe(
            actor, tick: 1, health: 1, maximumHealth: 3,
            otherwiseEligible: true).Admitted);

        governor.Interrupt(actor, tick: 2, "withdraw-preempted");

        RecoveryEpisodeGovernor.Admission same = governor.Observe(
            actor, tick: 3, health: 1, maximumHealth: 3,
            otherwiseEligible: true);
        Assert.False(same.Admitted);
        Assert.Contains("cooldown-withdraw-preempted", same.Reason);

        // Partial healing is still the same injury. Taking a fresh hit is a
        // new health-loss edge and bypasses the remaining retry clock.
        Assert.False(governor.Observe(
            actor, tick: 4, health: 2, maximumHealth: 3,
            otherwiseEligible: true).Admitted);
        Assert.True(governor.Observe(
            actor, tick: 5, health: 1, maximumHealth: 3,
            otherwiseEligible: true).Admitted);
    }

    [Fact]
    public void RecoverEpisodeTimesOutAndSameInjuryCannotImmediatelyReenter()
    {
        var governor = new RecoveryEpisodeGovernor();
        var actor = new ActorIdentity(0, 0, 0);

        Assert.True(governor.Observe(
            actor, tick: 0, health: 1, maximumHealth: 3,
            otherwiseEligible: true).Admitted);
        Assert.True(governor.Observe(
            actor,
            tick: RecoveryEpisodeGovernor.EpisodeTicks - 1,
            health: 1,
            maximumHealth: 3,
            otherwiseEligible: true).Admitted);

        RecoveryEpisodeGovernor.Admission timedOut = governor.Observe(
            actor,
            tick: RecoveryEpisodeGovernor.EpisodeTicks,
            health: 1,
            maximumHealth: 3,
            otherwiseEligible: true);
        Assert.False(timedOut.Admitted);
        Assert.Contains("cooldown-timeout", timedOut.Reason);

        Assert.True(governor.Observe(
            actor,
            tick: RecoveryEpisodeGovernor.EpisodeTicks
                + RecoveryEpisodeGovernor.RetryCooldownTicks,
            health: 1,
            maximumHealth: 3,
            otherwiseEligible: true).Admitted);
    }

    [Fact]
    public void RecoverStateIsExactLifeScoped()
    {
        var governor = new RecoveryEpisodeGovernor();
        var first = new ActorIdentity(0, 0, 0);
        var respawn = new ActorIdentity(0, 0, 1);

        Assert.True(governor.Observe(
            first, tick: 0, health: 1, maximumHealth: 3,
            otherwiseEligible: true).Admitted);
        governor.Interrupt(first, tick: 1, "unsafe");

        Assert.True(governor.Observe(
            respawn, tick: 2, health: 1, maximumHealth: 3,
            otherwiseEligible: true).Admitted);
    }

    [Fact]
    public void RecoverEpisodeLatchesOneBeaconUntilItEnds()
    {
        var governor = new RecoveryEpisodeGovernor();
        var actor = new ActorIdentity(0, 0, 0);
        var north = new Position(15, 10);
        var south = new Position(15, 16);

        RecoveryEpisodeGovernor.Admission started = governor.Observe(
            actor,
            tick: 0,
            health: 1,
            maximumHealth: 3,
            otherwiseEligible: true,
            candidateTarget: north);
        Assert.Equal(north, started.Target);

        RecoveryEpisodeGovernor.Admission stillNorth = governor.Observe(
            actor,
            tick: 1,
            health: 1,
            maximumHealth: 3,
            otherwiseEligible: true,
            candidateTarget: south);
        Assert.Equal(north, stillNorth.Target);

        governor.Interrupt(actor, tick: 2, "movement-preempted");
        Assert.Null(governor.Target(actor));
    }

    [Fact]
    public void AlliedBlocksAccruePersistentMovementDebt()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 2, 0);

        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        Assert.Equal(1, governor.Priority(actor.UnitId).AlliedBlockDebt);
        governor.Observe(1, [Sample(actor, 5, blocked: true)]);
        Assert.Equal(2, governor.Priority(actor.UnitId).AlliedBlockDebt);

        for (int tick = 2; tick < 20; tick++)
            governor.Observe(tick, [Sample(actor, 5, blocked: true)]);
        Assert.Equal(20, governor.Priority(actor.UnitId).AlliedBlockDebt);
    }

    [Fact]
    public void StrictProgressPaysDebtWithoutAnIncidentalHoldChangingIt()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 2, 0);

        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        governor.Observe(1, [Sample(actor, 5, blocked: true)]);
        Assert.Equal(2, governor.Priority(actor.UnitId).AlliedBlockDebt);

        governor.Observe(2, [Sample(actor, 4)]);
        governor.Observe(3, [Sample(actor, 4, attempted: false)]);
        Assert.Equal(0, governor.Priority(actor.UnitId).AlliedBlockDebt);
    }

    [Fact]
    public void ProgressThroughAlliedDenialKeepsABoundedPassageOpening()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity passageOwner = new(0, 3, 0);
        ActorIdentity nextWaiter = new(0, 4, 0);

        governor.Observe(0,
        [
            Sample(passageOwner, 5, blocked: true),
            Sample(nextWaiter, 5, blocked: true),
        ]);
        governor.Observe(1,
        [
            Sample(passageOwner, 4),
            Sample(nextWaiter, 5, blocked: true),
        ]);

        TacticalMovementPriority owner = governor.Priority(
            passageOwner.UnitId);
        Assert.Equal(TacticalTrafficGovernor.PassageLeaseTicks,
            owner.PassageLeaseTicksRemaining);
        Assert.Contains("+passage4", owner.Trace, StringComparison.Ordinal);
        Assert.True(owner.Rank < governor.Priority(nextWaiter.UnitId).Rank);

        // The next waiter continues aging, but cannot immediately eject the
        // body which just crossed the contested tile. This is the three-body
        // G3 failure in miniature: without hysteresis, debt payment rotated
        // passage ownership on every successful step.
        for (int tick = 2; tick < 5; tick++)
        {
            governor.Observe(tick,
            [
                Sample(passageOwner, 5 - tick),
                Sample(nextWaiter, 5, blocked: true),
            ]);
            Assert.True(governor.Priority(passageOwner.UnitId).Rank
                < governor.Priority(nextWaiter.UnitId).Rank);
        }

        governor.Observe(5,
        [
            Sample(passageOwner, 0),
            Sample(nextWaiter, 5, blocked: true),
        ]);
        Assert.Equal(0, governor.Priority(
            passageOwner.UnitId).PassageLeaseTicksRemaining);
        Assert.True(governor.Priority(nextWaiter.UnitId).Rank
            < governor.Priority(passageOwner.UnitId).Rank);
    }

    [Fact]
    public void PassageLeaseEndsEarlyWhenItsOwnerStopsMakingProgress()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 3, 0);

        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        governor.Observe(1, [Sample(actor, 4)]);
        Assert.True(governor.Priority(actor.UnitId)
            .PassageLeaseTicksRemaining > 0);

        governor.Observe(2, [Sample(actor, 4)]);
        Assert.True(governor.Priority(actor.UnitId)
            .PassageLeaseTicksRemaining > 0);
        governor.Observe(3, [Sample(actor, 4)]);
        Assert.Equal(0, governor.Priority(actor.UnitId)
            .PassageLeaseTicksRemaining);
    }

    [Fact]
    public void NewIntentCannotInheritPassageOwnership()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 3, 0);

        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        governor.Observe(1, [Sample(actor, 4)]);
        Assert.True(governor.Priority(actor.UnitId)
            .PassageLeaseTicksRemaining > 0);

        governor.Observe(2,
        [
            Sample(actor, 4, attempted: false, intent: "other"),
        ]);
        Assert.Equal(0, governor.Priority(actor.UnitId)
            .PassageLeaseTicksRemaining);
    }

    [Fact]
    public void ExactPriorityTiesUseActivationThenStableUnitId()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity low = new(0, 1, 0);
        ActorIdentity high = new(0, 6, 0);

        governor.Observe(0,
        [
            Sample(high, 5, blocked: true),
            Sample(low, 5, blocked: true),
        ]);
        governor.Observe(1,
        [
            Sample(high, 5, blocked: true),
            Sample(low, 5, blocked: true),
        ]);

        Assert.True(governor.Priority(low.UnitId).Rank
            < governor.Priority(high.UnitId).Rank);
    }

    [Fact]
    public void OlderDenialOutranksUnitIdSoTieBreakCannotBecomeBias()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity olderHighId = new(0, 7, 0);
        ActorIdentity freshLowId = new(0, 0, 0);

        governor.Observe(4,
        [
            Sample(olderHighId, 5, blocked: true),
            Sample(freshLowId, 5),
        ]);
        governor.Observe(5,
        [
            Sample(olderHighId, 5, blocked: true),
            Sample(freshLowId, 5, blocked: true),
        ]);

        Assert.True(governor.Priority(olderHighId.UnitId).Rank
            < governor.Priority(freshLowId.UnitId).Rank);
    }

    [Fact]
    public void OnlyARealMoveIntentCanAccrueDebt()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity recovery = new(0, 0, 0);
        ActorIdentity follower = new(0, 1, 0);
        ActorIdentity collector = new(0, 2, 0);

        for (int tick = 0; tick < 3; tick++)
        {
            governor.Observe(tick,
            [
                Sample(recovery, 5, blocked: true, mayAge: false,
                    intent: "recover"),
                Sample(follower, 5, blocked: true, mayAge: false,
                    intent: "follow"),
                Sample(collector, 5, blocked: true,
                    intent: "custody-pickup"),
            ]);
        }

        Assert.Equal(0, governor.Priority(recovery.UnitId).AlliedBlockDebt);
        Assert.Equal(0, governor.Priority(follower.UnitId).AlliedBlockDebt);
        Assert.Equal(3, governor.Priority(collector.UnitId).AlliedBlockDebt);
    }

    [Fact]
    public void NewIntentAndRespawnDiscardTrafficDebt()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 2, 0);
        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        governor.Observe(1, [Sample(actor, 5, blocked: true)]);
        Assert.Equal(2, governor.Priority(actor.UnitId).AlliedBlockDebt);

        governor.Observe(2, [Sample(actor, 5, blocked: false,
            intent: "other")]);
        Assert.Equal(0, governor.Priority(actor.UnitId).AlliedBlockDebt);

        governor.Observe(3, [Sample(actor, 5, blocked: true,
            intent: "other")]);
        governor.Observe(4,
        [
            Sample(new ActorIdentity(0, 2, 1), 5, blocked: true,
                intent: "other"),
        ]);
        Assert.Equal(1, governor.Priority(actor.UnitId).AlliedBlockDebt);
    }

    [Fact]
    public void AuthoredUrgencyWinsFreshTrafficWithoutChangingSourcePriority()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity routine = new(0, 0, 0);
        ActorIdentity urgent = new(0, 7, 0);

        governor.Observe(12,
        [
            Sample(routine, 4, urgency: TacticalMovementUrgency.Routine,
                activatedTick: 1),
            Sample(urgent, 4, urgency: TacticalMovementUrgency.Urgent,
                activatedTick: 12),
        ]);

        Assert.True(governor.Priority(urgent.UnitId).Rank
            < governor.Priority(routine.UnitId).Rank);
    }

    [Fact]
    public void PersistentRoutineDenialNeverOverridesAuthoredUrgency()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity routine = new(0, 7, 0);
        ActorIdentity urgent = new(0, 0, 0);

        for (int tick = 0; tick <= TacticalTrafficGovernor.DebtBandSize * 2;
             tick++)
        {
            governor.Observe(tick,
            [
                Sample(routine, 5, blocked: true,
                    urgency: TacticalMovementUrgency.Routine,
                    activatedTick: 0),
                Sample(urgent, 5, urgency: TacticalMovementUrgency.Urgent,
                    activatedTick: tick),
            ]);
        }

        Assert.True(governor.Priority(urgent.UnitId).Rank
            < governor.Priority(routine.UnitId).Rank);
    }

    [Fact]
    public void OlderDeniedPeerGetsNextOpeningWithinOneUrgencyTier()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity older = new(0, 7, 0);
        ActorIdentity recent = new(0, 0, 0);

        for (int tick = 0; tick <= TacticalTrafficGovernor.DebtBandSize * 2;
             tick++)
        {
            governor.Observe(tick,
            [
                Sample(older, 5, blocked: true,
                    urgency: TacticalMovementUrgency.Normal),
                Sample(recent, 5, blocked: true,
                    urgency: TacticalMovementUrgency.Normal),
            ]);
        }

        governor.Observe(TacticalTrafficGovernor.DebtBandSize * 2 + 1,
        [
            Sample(older, 5, blocked: true,
                urgency: TacticalMovementUrgency.Normal),
            Sample(recent, 4, blocked: false,
                urgency: TacticalMovementUrgency.Normal),
        ]);

        Assert.True(governor.Priority(older.UnitId).Rank
            < governor.Priority(recent.UnitId).Rank);
    }

    [Fact]
    public void MirroringInputOrderCannotChangeResolvedPriority()
    {
        var first = new TacticalTrafficGovernor();
        var second = new TacticalTrafficGovernor();
        ActorIdentity low = new(0, 1, 0);
        ActorIdentity high = new(0, 6, 0);
        TacticalTrafficGovernor.Sample a = Sample(low, 5, blocked: true);
        TacticalTrafficGovernor.Sample b = Sample(high, 5, blocked: true);

        first.Observe(0, [a, b]);
        second.Observe(0, [b, a]);

        Assert.Equal(first.Priority(low.UnitId), second.Priority(low.UnitId));
        Assert.Equal(first.Priority(high.UnitId), second.Priority(high.UnitId));
    }

    [Fact]
    public void RepeatedPairReversalYieldsEveryMovementAuthority()
    {
        var governor = new TacticalOscillationGovernor();
        ActorIdentity actor = new(0, 2, 0);
        var a = new Position(10, 10);
        var b = new Position(11, 10);
        var c = new Position(11, 11);

        governor.Observe(0, [Motion(actor, a)]);
        governor.Observe(1, [Motion(actor, b, a)]);
        governor.Observe(2, [Motion(actor, a, b)]);
        governor.Observe(3, [Motion(actor, b, a)]);

        TacticalOscillationGovernor.Admission tactical = governor.Admit(
            actor, 3, a, TacticalMovementAuthority.Tactical);
        Assert.False(tactical.Admitted);
        Assert.Contains("oscillation-yield", tactical.Reason);
        TacticalOscillationGovernor.Avoidance avoidance = Assert.IsType<
            TacticalOscillationGovernor.Avoidance>(
                governor.ActiveAvoidance(
                    actor, 3, TacticalMovementAuthority.Tactical));
        Assert.Equal(a, avoidance.Destination);
        Assert.Equal(tactical.Reason, avoidance.Reason);
        Assert.True(governor.Admit(
            actor, 3, c, TacticalMovementAuthority.Tactical).Admitted);
        Assert.False(governor.Admit(
            actor, 3, a, TacticalMovementAuthority.Safety).Admitted);
        Assert.False(governor.Admit(
            actor, 3, a, TacticalMovementAuthority.Clearance).Admitted);

        governor.Observe(
            3 + TacticalOscillationGovernor.YieldTicks,
            [Motion(actor, b)]);
        Assert.True(governor.Admit(
            actor,
            3 + TacticalOscillationGovernor.YieldTicks,
            a,
            TacticalMovementAuthority.Tactical).Admitted);
    }

    [Fact]
    public void MovementCadenceWaitDoesNotErasePairReversalEvidence()
    {
        var governor = new TacticalOscillationGovernor();
        ActorIdentity actor = new(0, 2, 0);
        var a = new Position(10, 10);
        var b = new Position(11, 10);

        governor.Observe(0, [Motion(actor, a)]);
        governor.Observe(1, [Motion(actor, b, a)]);
        governor.Observe(2, [Motion(actor, b)]);
        governor.Observe(3, [Motion(actor, a, b)]);
        governor.Observe(4, [Motion(actor, a)]);
        governor.Observe(5, [Motion(actor, b, a)]);

        TacticalOscillationGovernor.Admission returnToA = governor.Admit(
            actor, 5, a, TacticalMovementAuthority.Tactical);
        Assert.False(returnToA.Admitted);
        Assert.Contains("oscillation-yield", returnToA.Reason);
    }

    [Fact]
    public void LongIdleExpiresPairReversalEvidence()
    {
        var governor = new TacticalOscillationGovernor();
        ActorIdentity actor = new(0, 2, 0);
        var a = new Position(10, 10);
        var b = new Position(11, 10);

        governor.Observe(0, [Motion(actor, a)]);
        governor.Observe(1, [Motion(actor, b, a)]);
        for (int tick = 2;
             tick <= TacticalOscillationGovernor.ReversalMemoryTicks + 2;
             tick++)
        {
            governor.Observe(tick, [Motion(actor, b)]);
        }
        governor.Observe(
            TacticalOscillationGovernor.ReversalMemoryTicks + 3,
            [Motion(actor, a, b)]);

        TacticalOscillationGovernor.Admission returnToB = governor.Admit(
            actor,
            TacticalOscillationGovernor.ReversalMemoryTicks + 3,
            b,
            TacticalMovementAuthority.Tactical);
        Assert.True(returnToB.Admitted);
    }

    [Fact]
    public void OscillationDebtIsExactLifeScoped()
    {
        var governor = new TacticalOscillationGovernor();
        ActorIdentity first = new(0, 2, 0);
        ActorIdentity respawn = new(0, 2, 1);
        var a = new Position(10, 10);
        var b = new Position(11, 10);

        governor.Observe(0, [Motion(first, a)]);
        governor.Observe(1, [Motion(first, b, a)]);
        governor.Observe(2, [Motion(first, a, b)]);
        governor.Observe(3, [Motion(first, b, a)]);
        Assert.False(governor.Admit(
            first, 3, a, TacticalMovementAuthority.Tactical).Admitted);

        governor.Observe(4, [Motion(respawn, b)]);
        Assert.True(governor.Admit(
            respawn, 4, a, TacticalMovementAuthority.Tactical).Admitted);
    }

    [Fact]
    public void MakeWayLendsTheVacatedTileForABoundedWindow()
    {
        var governor = new TacticalOscillationGovernor();
        ActorIdentity actor = new(0, 2, 0);
        var doorway = new Position(10, 10);
        var aside = new Position(11, 10);

        governor.Observe(0, [Motion(actor, doorway)]);
        governor.NoteCommitted(
            actor,
            tick: 0,
            doorway,
            TacticalMovementAuthority.Clearance);

        Assert.False(governor.Admit(
            actor,
            tick: 1,
            doorway,
            TacticalMovementAuthority.Tactical).Admitted);
        Assert.True(governor.Admit(
            actor,
            tick: 1,
            aside,
            TacticalMovementAuthority.Tactical).Admitted);
        Assert.True(governor.Admit(
            actor,
            tick: 1,
            doorway,
            TacticalMovementAuthority.Safety).Admitted);

        governor.Observe(
            TacticalOscillationGovernor.YieldTicks,
            [Motion(actor, aside)]);
        Assert.True(governor.Admit(
            actor,
            TacticalOscillationGovernor.YieldTicks,
            doorway,
            TacticalMovementAuthority.Tactical).Admitted);
    }

    [Fact]
    public void SafetyStepCannotBeImmediatelyUndoneByTacticalMotion()
    {
        var governor = new TacticalOscillationGovernor();
        ActorIdentity actor = new(0, 2, 0);
        var danger = new Position(10, 10);
        var safe = new Position(11, 10);

        governor.Observe(0, [Motion(actor, danger)]);
        governor.NoteCommitted(
            actor,
            tick: 0,
            danger,
            TacticalMovementAuthority.Safety);

        Assert.False(governor.Admit(
            actor,
            tick: 1,
            danger,
            TacticalMovementAuthority.Tactical).Admitted);
        Assert.True(governor.Admit(
            actor,
            tick: 1,
            danger,
            TacticalMovementAuthority.Safety).Admitted);
        Assert.True(governor.Admit(
            actor,
            tick: 1,
            safe,
            TacticalMovementAuthority.Tactical).Admitted);
    }

    private static TacticalTrafficGovernor.Sample Sample(
        ActorIdentity actor,
        int distance,
        bool blocked = false,
        bool attempted = true,
        bool mayAge = true,
        string intent = "north-well",
        TacticalMovementUrgency urgency = TacticalMovementUrgency.Normal,
        int activatedTick = 0) => new(
        actor,
        intent,
        new Position(15, 6),
        distance,
        attempted,
        blocked,
        mayAge,
        urgency,
        activatedTick,
        blocked ? new Position(14, 6) : null);

    private static TacticalOscillationGovernor.Sample Motion(
        ActorIdentity actor,
        Position position,
        Position? previous = null) => new(
        actor,
        position,
        previous,
        previous is not null);

}
