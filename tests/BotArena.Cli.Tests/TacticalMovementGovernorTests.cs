using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalMovementGovernorTests
{
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
