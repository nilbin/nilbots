using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The pure economy arithmetic, driven directly: deposit schedule and
/// displacement, wreckage merged with a killed carrier's load, pile decay and
/// the hard bound, the assay-then-carry pickup, home-pad banking, the
/// weight-zero drop, the upgrade ladder's caps and prices, and the control
/// arm's automatic buyer. The session tests pin that the world derives the
/// right inputs; these pin what the kernel does with them.
/// <para>Every case also asserts <see cref="FrontlineScrapState.IsConserved"/>,
/// because the whole subsystem is built around one equation:
/// <c>spawned = banked + spent + carried + on-piles + evaporated</c>.</para>
/// </summary>
public sealed class FrontlineScrapKernelTests
{
    private static readonly Position NorthVein = new(11, 1);
    private static readonly Position SouthVein = new(11, 13);
    private static readonly Position TeamZeroPad = new(2, 7);

    private static ActorResolvedMatchDefinition Definition(
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.Scrap) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.EnemySoleDecay,
            (FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker),
            economy: economy);

    private static FrontlineScrapKernel Kernel(
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.Scrap)
    {
        ActorResolvedMatchDefinition definition = Definition(economy);
        var mode = (FrontlineGameModeDefinition)definition.Rules.GameMode;
        return new FrontlineScrapKernel(
            definition.Topology,
            definition.Map,
            definition.Rules.Forms,
            definition.LifecycleAssignments,
            mode.ScrapEconomy!);
    }

    /// <summary>The bulwark's prime form: objective weight 1, so it may carry.</summary>
    private static FrontlineScrapBody Prime(
        int teamId,
        Position position,
        int unitId = 0,
        int lifeId = 0) =>
        new(
            ActorIdentity.FromTeamUnitLife(teamId, unitId, lifeId),
            FrontlineLabsClassDefinition.Bulwark.PrimeFormId,
            position);

    /// <summary>An anchored turret: objective weight 0, so it may not.</summary>
    private static FrontlineScrapBody Turret(
        int teamId,
        Position position,
        int unitId = 1,
        int lifeId = 0) =>
        new(
            ActorIdentity.FromTeamUnitLife(teamId, unitId, lifeId),
            FrontlineLabsClassDefinition.Bulwark.PrimeTurretFormId,
            position);

    private static FrontlineScrapState Step(
        FrontlineScrapKernel kernel,
        FrontlineScrapState state,
        int tick,
        IReadOnlyCollection<FrontlineScrapBody>? lives = null,
        IReadOnlyCollection<FrontlineScrapDestruction>? destructions = null)
    {
        FrontlineScrapState next = kernel.ApplyJointTick(
            state,
            tick,
            lives ?? [],
            destructions ?? []);
        Assert.True(next.IsConserved(), $"tick {tick} lost scrap");
        return next;
    }

    /// <summary>
    /// The schedule is exactly the four declared ticks, both sites every
    /// time, and nothing else. It is fully derivable from the contract before
    /// tick zero, which is the point.
    /// </summary>
    [Fact]
    public void DepositsArriveOnTheDeclaredScheduleAtBothSites()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        var spawnTicks = new List<int>();
        for (int tick = 0; tick <= 500; tick++)
        {
            long before = state.SpawnedTotal;
            state = Step(kernel, state, tick);
            if (state.SpawnedTotal > before)
                spawnTicks.Add(tick);
        }

        Assert.Equal([120, 200, 280, 360], spawnTicks);
        // Four events x two sites x six scrap is the whole pot.
        Assert.Equal(48, state.SpawnedTotal);
        // Nothing was ever collected, so all of it evaporated on schedule.
        Assert.Equal(48, state.EvaporatedTotal);
        Assert.Empty(state.Piles);
    }

    /// <summary>
    /// A pile is gone the FIRST tick <c>tick &gt;= expiresAtTick</c>, and the
    /// lifetime is exactly one cadence — so a deposit nobody took disappears
    /// as the next pair arrives and at most one cycle is ever live.
    /// </summary>
    [Fact]
    public void PilesExpireExactlyOneCadenceAfterTheyAppear()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(kernel, state, 120);

        Assert.Equal(2, state.Piles.Length);
        Assert.All(state.Piles, pile => Assert.Equal(200, pile.ExpiresAtTick));

        state = Step(kernel, state, 199);
        Assert.Equal(2, state.Piles.Length);

        // Tick 200 expires the old pair and deposits the new one in the same
        // update, so the count never grows past one cycle.
        state = Step(kernel, state, 200);
        Assert.Equal(2, state.Piles.Length);
        Assert.All(state.Piles, pile => Assert.Equal(280, pile.ExpiresAtTick));
        Assert.Equal(12, state.EvaporatedTotal);
    }

    /// <summary>
    /// The one rule, and its three consequences: 1 banked instantly at the
    /// tile, the rest loaded up to the cap, and the remainder left standing
    /// with its original expiry.
    /// </summary>
    [Fact]
    public void PickupBanksTheAssayAndLoadsTheRestUpToTheCap()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(kernel, state, 120);

        state = Step(kernel, state, 121, [Prime(0, NorthVein)]);
        Assert.Equal(1, state.Team(0).Bank);
        Assert.Equal(
            5,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        Assert.DoesNotContain(state.Piles, pile => pile.Position == NorthVein);

        // The cap binds on the second pile: the assay still pays, but the
        // carry cannot grow past six. The body steps ON the following tick,
        // because standing on the site AT the spawn tick displaces the
        // deposit instead of collecting it.
        state = Step(kernel, state, 200, [Prime(0, new Position(10, 1))]);
        state = Step(kernel, state, 201, [Prime(0, NorthVein)]);
        Assert.Equal(2, state.Team(0).Bank);
        Assert.Equal(
            6,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        FrontlineScrapPile remainder = Assert.Single(
            state.Piles,
            pile => pile.Position == NorthVein);
        Assert.Equal(4, remainder.Amount);
        Assert.Equal(280, remainder.ExpiresAtTick);
    }

    /// <summary>
    /// The transport leg was the price, so banking is free: a body of the
    /// owning team standing on its own home-pad region converts the whole
    /// load automatically, with no action.
    /// </summary>
    [Fact]
    public void AFullLoadBanksOnTheOwningTeamsHomePad()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(kernel, state, 120);
        state = Step(kernel, state, 121, [Prime(0, NorthVein)]);
        Assert.Equal(1, state.Team(0).Bank);

        state = Step(kernel, state, 137, [Prime(0, TeamZeroPad)]);
        Assert.Equal(6, state.Team(0).Bank);
        Assert.Empty(state.CarriedByActor);

        // The other team's pad is not a bank for this body.
        state = Step(kernel, state, 200, [Prime(0, new Position(10, 1))]);
        state = Step(kernel, state, 201, [Prime(0, NorthVein)]);
        Assert.Equal(
            5,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        state = Step(kernel, state, 202, [Prime(0, new Position(20, 7))]);
        Assert.Equal(
            5,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        Assert.Equal(7, state.Team(0).Bank);
    }

    /// <summary>
    /// A killed carrier is simply a bigger wreck: one pile at the death tile
    /// worth <c>1 + load</c>. This is the largest single transfer in the game
    /// and it is available to whoever did the killing.
    /// </summary>
    [Fact]
    public void AKilledCarrierDropsItsWreckAndItsLoadAsOnePile()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(kernel, state, 120);
        state = Step(kernel, state, 121, [Prime(0, NorthVein)]);
        Assert.Equal(
            5,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));

        var grave = new Position(9, 1);
        state = Step(
            kernel,
            state,
            122,
            lives: [],
            destructions:
            [
                new FrontlineScrapDestruction(
                    ActorIdentity.FromTeamUnitLife(0, 0, 0),
                    grave),
            ]);

        FrontlineScrapPile pile = Assert.Single(
            state.Piles,
            value => value.Position == grave);
        Assert.Equal(6, pile.Amount);
        Assert.Equal(202, pile.ExpiresAtTick);
        Assert.Empty(state.CarriedByActor);

        // And the interceptor collects it exactly as it would a deposit.
        state = Step(kernel, state, 123, [Prime(1, grave)]);
        Assert.Equal(1, state.Team(1).Bank);
        Assert.Equal(
            5,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(1, 0, 0)));
    }

    /// <summary>
    /// Objective weight gates the economy — the one rule the whole class
    /// slate already rests on. A turret cannot pick up, and a carrier that
    /// becomes one puts its load back on the floor.
    /// </summary>
    [Fact]
    public void AWeightZeroFormNeitherCarriesNorPicksUp()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(kernel, state, 120);

        // Standing on a live deposit as a turret collects nothing at all.
        state = Step(kernel, state, 121, [Turret(0, NorthVein)]);
        Assert.Equal(0, state.Team(0).Bank);
        Assert.Empty(state.CarriedByActor);
        Assert.Contains(state.Piles, pile => pile.Position == NorthVein);

        // A loaded body that anchors drops the whole load where it stands.
        state = Step(kernel, state, 122, [Prime(0, SouthVein)]);
        Assert.Equal(
            5,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        state = Step(
            kernel,
            state,
            123,
            [Turret(0, new Position(11, 12), unitId: 0)]);
        Assert.Empty(state.CarriedByActor);
        FrontlineScrapPile dropped = Assert.Single(
            state.Piles,
            pile => pile.Position == new Position(11, 12));
        Assert.Equal(5, dropped.Amount);
    }

    /// <summary>
    /// Camping the tile denies nothing: an occupied site displaces the
    /// deposit to the nearest free floor tile in the same row, breaking ties
    /// toward the lower column. Deterministic, and no randomness added.
    /// </summary>
    [Fact]
    public void AnOccupiedSiteDisplacesTheDepositAlongItsOwnRow()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(
            kernel,
            state,
            120,
            [Turret(0, NorthVein), Turret(1, SouthVein, unitId: 2)]);

        Assert.Equal(2, state.Piles.Length);
        Assert.Contains(
            state.Piles,
            pile => pile.Position == new Position(10, 1));
        Assert.Contains(
            state.Piles,
            pile => pile.Position == new Position(10, 13));
        Assert.Equal(12, state.SpawnedTotal);
    }

    /// <summary>
    /// The published collection is provably small: a seventeenth pile evicts
    /// the shortest-lived one rather than growing the array, and the evicted
    /// scrap is accounted rather than lost.
    /// </summary>
    [Fact]
    public void TheHardPileBoundEvictsRatherThanGrowing()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        var deaths = new List<FrontlineScrapDestruction>();
        for (int index = 0; index < 20; index++)
        {
            deaths.Add(new FrontlineScrapDestruction(
                ActorIdentity.FromTeamUnitLife(0, 0, index),
                new Position(1 + index, 7)));
        }

        state = Step(kernel, state, 10, lives: [], destructions: deaths);
        Assert.Equal(16, state.Piles.Length);
        Assert.Equal(20, state.SpawnedTotal);
        Assert.Equal(4, state.EvaporatedTotal);
    }

    /// <summary>
    /// The ladder's caps and prices. Flat pricing means deep and broad both
    /// cost 30 at every point in the match, the total cap is three, and no
    /// track goes past two — so a maxed-out bank still converts to exactly
    /// three integer stat steps.
    /// </summary>
    [Fact]
    public void TheLadderEnforcesItsPricesAndBothCaps()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = Banked(kernel, 0, 100);

        Assert.Equal(
            ["edge", "plate", "optic"],
            kernel.InvestableTracks(state, 0).ToArray());
        Assert.True(kernel.TryInvest(state, 0, "edge", out state));
        Assert.Equal(90, state.Team(0).Bank);
        Assert.True(kernel.TryInvest(state, 0, "edge", out state));
        Assert.Equal(80, state.Team(0).Bank);

        // Two in one track is the per-track cap.
        Assert.DoesNotContain("edge", kernel.InvestableTracks(state, 0));
        Assert.False(kernel.TryInvest(state, 0, "edge", out _));

        Assert.True(kernel.TryInvest(state, 0, "plate", out state));
        // Three total is the whole-team cap: nothing is offered any more.
        Assert.Empty(kernel.InvestableTracks(state, 0));
        Assert.False(kernel.TryInvest(state, 0, "optic", out _));
        Assert.Equal([2, 1, 0], state.Team(0).TierLevels.ToArray());
        Assert.Equal(70, state.Team(0).Bank);
        Assert.False(kernel.TryInvest(state, 0, "no-such-track", out _));

        // Affordability is the mask's other half.
        FrontlineScrapState poor = Banked(kernel, 1, 9);
        Assert.Empty(kernel.InvestableTracks(poor, 1));
        Assert.False(kernel.TryInvest(poor, 1, "plate", out _));
    }

    /// <summary>
    /// The tiers are typed modifiers resolved at the point of use, scoped to
    /// the PRIME slot's lives. A child of the same team gains nothing, which
    /// is what keeps the reward flat per team however many slots it fields.
    /// </summary>
    [Fact]
    public void TiersApplyToThePrimeSlotOnly()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = Banked(kernel, 0, 100);
        Assert.True(kernel.TryInvest(state, 0, "edge", out state));
        Assert.True(kernel.TryInvest(state, 0, "plate", out state));
        Assert.True(kernel.TryInvest(state, 0, "optic", out state));

        GenericActorModeStatModifiers prime = kernel.ModifiersFor(
            state,
            ActorIdentity.FromTeamUnitLife(0, 0, 4));
        Assert.Equal(1, prime.AttackTravelTilesDelta);
        Assert.Equal(1, prime.MaxHealthDelta);
        Assert.Equal(1, prime.VisionRangeDelta);

        Assert.True(
            kernel
                .ModifiersFor(
                    state,
                    ActorIdentity.FromTeamUnitLife(0, 1, 0))
                .IsNone);
        Assert.True(
            kernel
                .ModifiersFor(
                    state,
                    ActorIdentity.FromTeamUnitLife(1, 0, 0))
                .IsNone);
    }

    /// <summary>
    /// The control arm's buyer: no verb, no action cost, no body — the bank
    /// takes the cheapest legal next tier at the end of every update and
    /// breaks ties by declared track order, which at a flat price is simply
    /// the declared order.
    /// </summary>
    [Fact]
    public void TheControlArmBuysGreedilyInDeclaredOrder()
    {
        FrontlineScrapKernel kernel = Kernel(FrontlineLabsEconomyArm.ScrapFlat);
        FrontlineScrapState state = kernel.CreateInitialState();

        // Nine scrap buys nothing; the tenth buys the first declared track.
        state = Deposit(kernel, state, teamId: 0, amount: 9);
        Assert.Equal([0, 0, 0], state.Team(0).TierLevels.ToArray());
        state = Deposit(kernel, state, teamId: 0, amount: 1);
        Assert.Equal([1, 0, 0], state.Team(0).TierLevels.ToArray());
        Assert.Equal(0, state.Team(0).Bank);

        // A windfall buys everything it can and then stops at the cap.
        state = Deposit(kernel, state, teamId: 0, amount: 100);
        Assert.Equal([2, 1, 0], state.Team(0).TierLevels.ToArray());
        Assert.Equal(3, state.Team(0).TotalTiers);
        Assert.Equal(80, state.Team(0).Bank);
    }

    /// <summary>
    /// A whole scripted cycle: deposits arrive, a carrier collects and dies,
    /// an interceptor takes the pile home, the rest expires. The ledger has
    /// to close at every single tick, not only at the end.
    /// </summary>
    [Fact]
    public void ScrapIsConservedAcrossAWholeScriptedCycle()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        ActorIdentity carrier = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        ActorIdentity raider = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        var grave = new Position(9, 1);

        // The raider stays in the world for every tick of its walk home: a
        // body that is not among the post-combat survivors is a body that
        // left, and its load is accounted as evaporated rather than
        // teleported.
        for (int tick = 100; tick <= 460; tick++)
        {
            FrontlineScrapBody[] lives = tick switch
            {
                < 121 => [],
                121 => [Prime(0, NorthVein)],
                122 => [],
                123 => [Prime(1, grave)],
                < 140 => [Prime(1, new Position(10, 1))],
                _ => [Prime(1, new Position(20, 7))],
            };
            FrontlineScrapDestruction[] deaths = tick == 122
                ? [new FrontlineScrapDestruction(carrier, grave)]
                : [];
            state = Step(kernel, state, tick, lives, deaths);
        }

        Assert.True(state.IsConserved());
        // Four events x two sites x six, plus the one wreck the carrier left.
        Assert.Equal(49, state.SpawnedTotal);
        // Team 0 banked only its assay before it was intercepted; team 1
        // banked the assay on the wreck plus the whole load it carried home.
        Assert.Equal(1, state.Team(0).Bank);
        Assert.Equal(6, state.Team(1).Bank);
        Assert.Equal(0, kernel.ModifiersFor(state, raider).MaxHealthDelta);
        Assert.Empty(state.CarriedByActor);
        Assert.Empty(state.Piles);
    }

    /// <summary>Banks an exact amount by handing one body a pile's worth.</summary>
    private static FrontlineScrapState Banked(
        FrontlineScrapKernel kernel,
        int teamId,
        int amount) =>
        Deposit(kernel, kernel.CreateInitialState(), teamId, amount);

    /// <summary>
    /// Adds <paramref name="amount"/> to one team's bank the only way the
    /// kernel allows — through a pile and a body — so the ledger still closes.
    /// </summary>
    private static FrontlineScrapState Deposit(
        FrontlineScrapKernel kernel,
        FrontlineScrapState state,
        int teamId,
        int amount)
    {
        FrontlineScrapState current = state;
        Position pad = teamId == 0 ? TeamZeroPad : new Position(20, 7);
        int tick = 1;
        for (int paid = 0; paid < amount; paid++)
        {
            // One wreck is worth exactly one, so the arithmetic is exact and
            // the conservation ledger stays readable.
            current = kernel.ApplyJointTick(
                current,
                tick++,
                [],
                [
                    new FrontlineScrapDestruction(
                        ActorIdentity.FromTeamUnitLife(teamId, 2, paid),
                        pad),
                ]);
            current = kernel.ApplyJointTick(
                current,
                tick++,
                [Prime(teamId, pad)],
                []);
            Assert.True(current.IsConserved());
        }
        return current;
    }
}
