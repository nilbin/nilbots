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
    public void TwoAlliedBlocksGrantABoundedTrafficTicket()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 2, 0);

        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        Assert.False(governor.HasTicket(actor.UnitId));
        governor.Observe(1, [Sample(actor, 5, blocked: true)]);
        Assert.True(governor.HasTicket(actor.UnitId));

        for (int tick = 2; tick <= TacticalTrafficGovernor.TicketTicks + 1;
             tick++)
        {
            governor.Observe(tick, [Sample(actor, 5, blocked: true)]);
        }
        Assert.False(governor.HasTicket(actor.UnitId));
    }

    [Fact]
    public void TwoProgressStepsReleaseTicketEvenWithAHoldBetweenThem()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 2, 0);

        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        governor.Observe(1, [Sample(actor, 5, blocked: true)]);
        Assert.True(governor.HasTicket(actor.UnitId));

        governor.Observe(2, [Sample(actor, 4)]);
        governor.Observe(3, [Sample(actor, 4, attempted: false)]);
        Assert.True(governor.HasTicket(actor.UnitId));
        governor.Observe(4, [Sample(actor, 3)]);
        Assert.False(governor.HasTicket(actor.UnitId));
    }

    [Fact]
    public void OppositeDirectionTicketsHaveOneDeterministicWinner()
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

        Assert.True(governor.HasTicket(low.UnitId));
        Assert.True(governor.HasTicket(high.UnitId));
        Assert.True(governor.Rank(low.UnitId) < governor.Rank(high.UnitId));
        Assert.True(TacticalTrafficGovernor.StrongestProtectedRank
            < governor.Rank(low.UnitId));
        Assert.True(governor.Rank(high.UnitId)
            < TacticalTrafficGovernor.FreeTrafficRank);
    }

    [Fact]
    public void RecoveryAndFollowersNeverAgeButCustodyMovementCan()
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

        Assert.False(governor.HasTicket(recovery.UnitId));
        Assert.False(governor.HasTicket(follower.UnitId));
        Assert.True(governor.HasTicket(collector.UnitId));
    }

    [Fact]
    public void NewIntentAndRespawnDiscardTrafficDebt()
    {
        var governor = new TacticalTrafficGovernor();
        ActorIdentity actor = new(0, 2, 0);
        governor.Observe(0, [Sample(actor, 5, blocked: true)]);
        governor.Observe(1, [Sample(actor, 5, blocked: true)]);
        Assert.True(governor.HasTicket(actor.UnitId));

        governor.Observe(2, [Sample(actor, 5, blocked: false,
            intent: "other")]);
        Assert.False(governor.HasTicket(actor.UnitId));

        governor.Observe(3, [Sample(actor, 5, blocked: true,
            intent: "other")]);
        governor.Observe(4,
        [
            Sample(new ActorIdentity(0, 2, 1), 5, blocked: true,
                intent: "other"),
        ]);
        Assert.False(governor.HasTicket(actor.UnitId));
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
        string intent = "north-well") => new(
        actor,
        intent,
        new Position(15, 6),
        distance,
        attempted,
        blocked,
        mayAge);

    private static TacticalOscillationGovernor.Sample Motion(
        ActorIdentity actor,
        Position position,
        Position? previous = null) => new(
        actor,
        position,
        previous,
        previous is not null);
}
