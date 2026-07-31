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
    /// The schedule is exactly the nine declared ticks, both sites every time,
    /// and nothing else. It is fully derivable from the contract before tick
    /// zero, which is the point — and it runs to the LONG horizon, so a
    /// standard 500-tick cell simply never sees the last three.
    /// </summary>
    [Fact]
    public void DepositsArriveOnTheDeclaredScheduleAtBothSites()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        var spawnTicks = new List<int>();
        for (int tick = 0; tick <= 700; tick++)
        {
            long before = state.SpawnedTotal;
            state = Step(kernel, state, tick);
            if (state.SpawnedTotal > before)
                spawnTicks.Add(tick);
        }

        Assert.Equal(
            [60, 130, 200, 270, 340, 410, 480, 550, 620],
            spawnTicks);
        // Nine events x two sites x eight scrap is the whole v1.1 pot over a
        // 750-tick match; seven of them (112) land inside a 500-tick one.
        Assert.Equal(144, state.SpawnedTotal);
        Assert.Equal(
            112,
            spawnTicks.Count(tick => tick < 500)
                * FrontlineLabsScrapEconomy.VeinSites.Length
                * FrontlineLabsScrapEconomy.VeinAmount);
        // Nothing was ever collected, so all of it evaporated on schedule.
        Assert.Equal(144, state.EvaporatedTotal);
        Assert.Empty(state.Piles);
    }

    /// <summary>
    /// A pile is gone the FIRST tick <c>tick &gt;= expiresAtTick</c>. At v1.1
    /// the 80-tick lifetime outlives the 70-tick cadence, so a deposit nobody
    /// took is still standing when the next one lands on the same tile and the
    /// two MERGE, carrying the later expiry: a neglected lane accumulates
    /// instead of evaporating. The extraction rate is unchanged — assay plus a
    /// carry of six per visit — so what grows is the prize, not the pace.
    /// </summary>
    [Fact]
    public void AnUntakenDepositRollsIntoTheNextCycleAndLoneWrecksExpire()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        var grave = new Position(9, 7);
        state = Step(
            kernel,
            state,
            60,
            lives: [],
            destructions:
            [
                new FrontlineScrapDestruction(
                    ActorIdentity.FromTeamUnitLife(0, 1, 0),
                    grave),
            ]);

        Assert.Equal(3, state.Piles.Length);
        Assert.All(
            state.Piles,
            pile => Assert.Equal(140, pile.ExpiresAtTick));

        // The second cycle lands on the untaken first one and rolls it over.
        state = Step(kernel, state, 130);
        Assert.Equal(3, state.Piles.Length);
        Assert.All(
            state.Piles.Where(pile => pile.Position != grave),
            pile =>
            {
                Assert.Equal(16, pile.Amount);
                Assert.Equal(210, pile.ExpiresAtTick);
            });
        Assert.Equal(0, state.EvaporatedTotal);

        // The wreck is fed by nothing, so it still dies on its own 80 ticks.
        state = Step(kernel, state, 140);
        Assert.Equal(2, state.Piles.Length);
        Assert.DoesNotContain(state.Piles, pile => pile.Position == grave);
        Assert.Equal(
            FrontlineLabsScrapEconomy.WreckAmount,
            state.EvaporatedTotal);
    }

    /// <summary>
    /// The one rule, and its three consequences: 1 banked instantly at the
    /// tile, the rest loaded up to the cap, and the remainder left standing
    /// with its original expiry. At v1.1 the deposit of 8 binds the carry cap
    /// on the FIRST pile — a body cannot lift a whole vein, which is what
    /// keeps a deposit a place rather than a package.
    /// </summary>
    [Fact]
    public void PickupBanksTheAssayAndLoadsTheRestUpToTheCap()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(kernel, state, 60);

        state = Step(kernel, state, 61, [Prime(0, NorthVein)]);
        Assert.Equal(1, state.Team(0).Bank);
        Assert.Equal(
            6,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        FrontlineScrapPile remainder = Assert.Single(
            state.Piles,
            pile => pile.Position == NorthVein);
        Assert.Equal(1, remainder.Amount);
        Assert.Equal(140, remainder.ExpiresAtTick);

        // A full carrier still pays itself the assay on the next pile it
        // steps on, and loads nothing: the floor under every trip.
        state = Step(kernel, state, 62, [Prime(0, SouthVein)]);
        Assert.Equal(2, state.Team(0).Bank);
        Assert.Equal(
            6,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        Assert.Equal(
            7,
            Assert.Single(state.Piles, pile => pile.Position == SouthVein)
                .Amount);
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
        state = Step(kernel, state, 60);
        state = Step(kernel, state, 61, [Prime(0, NorthVein)]);
        Assert.Equal(1, state.Team(0).Bank);

        state = Step(kernel, state, 77, [Prime(0, TeamZeroPad)]);
        Assert.Equal(7, state.Team(0).Bank);
        Assert.Empty(state.CarriedByActor);

        // The other team's pad is not a bank for this body.
        state = Step(kernel, state, 200, [Prime(0, new Position(10, 1))]);
        state = Step(kernel, state, 201, [Prime(0, NorthVein)]);
        Assert.Equal(
            6,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        state = Step(kernel, state, 202, [Prime(0, new Position(20, 7))]);
        Assert.Equal(
            6,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        Assert.Equal(8, state.Team(0).Bank);
    }

    /// <summary>
    /// A killed carrier is simply a bigger wreck: one pile at the death tile
    /// worth <c>wreck + load</c>, which at v1.1 is 2 + 6. This is the largest
    /// single transfer in the game and it is available to whoever did the
    /// killing.
    /// </summary>
    [Fact]
    public void AKilledCarrierDropsItsWreckAndItsLoadAsOnePile()
    {
        FrontlineScrapKernel kernel = Kernel();
        FrontlineScrapState state = kernel.CreateInitialState();
        state = Step(kernel, state, 60);
        state = Step(kernel, state, 61, [Prime(0, NorthVein)]);
        Assert.Equal(
            6,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));

        var grave = new Position(9, 1);
        state = Step(
            kernel,
            state,
            62,
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
        Assert.Equal(8, pile.Amount);
        Assert.Equal(142, pile.ExpiresAtTick);
        Assert.Empty(state.CarriedByActor);

        // And the interceptor collects it exactly as it would a deposit.
        state = Step(kernel, state, 63, [Prime(1, grave)]);
        Assert.Equal(1, state.Team(1).Bank);
        Assert.Equal(
            6,
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
        state = Step(kernel, state, 60);

        // Standing on a live deposit as a turret collects nothing at all.
        state = Step(kernel, state, 61, [Turret(0, NorthVein)]);
        Assert.Equal(0, state.Team(0).Bank);
        Assert.Empty(state.CarriedByActor);
        Assert.Contains(state.Piles, pile => pile.Position == NorthVein);

        // A loaded body that anchors drops the whole load where it stands.
        state = Step(kernel, state, 62, [Prime(0, SouthVein)]);
        Assert.Equal(
            6,
            state.CarriedBy(ActorIdentity.FromTeamUnitLife(0, 0, 0)));
        state = Step(
            kernel,
            state,
            63,
            [Turret(0, new Position(11, 12), unitId: 0)]);
        Assert.Empty(state.CarriedByActor);
        FrontlineScrapPile dropped = Assert.Single(
            state.Piles,
            pile => pile.Position == new Position(11, 12));
        Assert.Equal(6, dropped.Amount);
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
            60,
            [Turret(0, NorthVein), Turret(1, SouthVein, unitId: 2)]);

        Assert.Equal(2, state.Piles.Length);
        Assert.Contains(
            state.Piles,
            pile => pile.Position == new Position(10, 1));
        Assert.Contains(
            state.Piles,
            pile => pile.Position == new Position(10, 13));
        Assert.Equal(16, state.SpawnedTotal);
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
        Assert.Equal(40, state.SpawnedTotal);
        Assert.Equal(8, state.EvaporatedTotal);
    }

    /// <summary>
    /// The ladder's caps and prices. Flat pricing means deep and broad cost
    /// the same at every point in the match, no track goes past two — and the
    /// whole board is now SIX tiers, because the owner removed the three-tier
    /// total cap ("ideally scraps should weigh in and decide the game").
    /// Sixty scrap buys +2 travel, +2 spawn health and +2 sight, and then the
    /// verb closes for good.
    /// </summary>
    [Fact]
    public void TheLadderEnforcesItsPricesAndTheFullBoard()
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

        // Two in one track is the per-track cap, and it is the only cap that
        // binds before the board is full.
        Assert.DoesNotContain("edge", kernel.InvestableTracks(state, 0));
        Assert.False(kernel.TryInvest(state, 0, "edge", out _));
        Assert.Equal(
            ["plate", "optic"],
            kernel.InvestableTracks(state, 0).ToArray());

        foreach (string track in new[]
                 {
                     "plate",
                     "plate",
                     "optic",
                     "optic",
                 })
        {
            Assert.True(kernel.TryInvest(state, 0, track, out state));
        }

        // The full board: six tiers, sixty scrap, and the verb closes.
        Assert.Equal([2, 2, 2], state.Team(0).TierLevels.ToArray());
        Assert.Equal(
            FrontlineLabsScrapEconomy.MaxTotalTiers,
            state.Team(0).TotalTiers);
        Assert.Equal(40, state.Team(0).Bank);
        Assert.Empty(kernel.InvestableTracks(state, 0));
        Assert.False(kernel.TryInvest(state, 0, "optic", out _));
        Assert.False(kernel.TryInvest(state, 0, "no-such-track", out _));

        // Affordability is the mask's other half.
        FrontlineScrapState poor = Banked(kernel, 1, 8);
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

        // Eight scrap buys nothing; the tenth buys the first declared track.
        state = Deposit(kernel, state, teamId: 0, amount: 8);
        Assert.Equal([0, 0, 0], state.Team(0).TierLevels.ToArray());
        state = Deposit(kernel, state, teamId: 0, amount: 2);
        Assert.Equal([1, 0, 0], state.Team(0).TierLevels.ToArray());
        Assert.Equal(0, state.Team(0).Bank);

        // A windfall buys the whole board and then stops: six tiers, and the
        // greedy buyer works down the declared order at a flat price.
        state = Deposit(kernel, state, teamId: 0, amount: 100);
        Assert.Equal([2, 2, 2], state.Team(0).TierLevels.ToArray());
        Assert.Equal(6, state.Team(0).TotalTiers);
        Assert.Equal(50, state.Team(0).Bank);
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
        for (int tick = 100; tick <= 490; tick++)
        {
            FrontlineScrapBody[] lives = tick switch
            {
                < 131 => [],
                131 => [Prime(0, NorthVein)],
                132 => [],
                133 => [Prime(1, grave)],
                < 150 => [Prime(1, new Position(10, 1))],
                _ => [Prime(1, new Position(20, 7))],
            };
            FrontlineScrapDestruction[] deaths = tick == 132
                ? [new FrontlineScrapDestruction(carrier, grave)]
                : [];
            state = Step(kernel, state, tick, lives, deaths);
        }

        Assert.True(state.IsConserved());
        // Six events land inside the scripted window (130 through 480; the
        // first is at 60 and the last two are past it), two sites at eight
        // each, plus the wreck the carrier left.
        Assert.Equal(98, state.SpawnedTotal);
        // Team 0 banked only its assay before it was intercepted; team 1
        // banked the assay on the wreck plus the whole load it carried home.
        Assert.Equal(1, state.Team(0).Bank);
        Assert.Equal(7, state.Team(1).Bank);
        Assert.Equal(0, kernel.ModifiersFor(state, raider).MaxHealthDelta);
        Assert.Empty(state.CarriedByActor);
        // The two neglected lanes rolled over instead of evaporating — the
        // v1.1 pile rule — so what is left standing is a prize rather than a
        // leak, and the ledger still closes around it.
        Assert.Equal(2, state.Piles.Length);
        Assert.All(state.Piles, pile => Assert.True(pile.Amount > 0));
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
        Assert.True(
            amount % FrontlineLabsScrapEconomy.WreckAmount == 0,
            "bank exact multiples of a wreck so the ledger stays readable");
        FrontlineScrapState current = state;
        Position pad = teamId == 0 ? TeamZeroPad : new Position(20, 7);
        int tick = 1;
        for (int paid = 0;
             paid < amount;
             paid += FrontlineLabsScrapEconomy.WreckAmount)
        {
            // One wreck is worth exactly WreckAmount, so the arithmetic is
            // exact and the conservation ledger stays readable.
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
