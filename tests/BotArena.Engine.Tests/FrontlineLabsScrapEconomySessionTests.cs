using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The battlefield economy driven live through whole scripted matches on the
/// real arm. The kernel tests pin the arithmetic; these pin that the SESSION
/// derives the right facts from the world — which bodies stood on a pile,
/// which one died where, and which team could afford what — that the three
/// published facts carry them, that a purchase actually moves the stat it
/// names, and that every history they produce survives the chronology
/// validator, which re-derives the whole economy from the recorded document
/// rather than trusting it.
/// </summary>
public sealed class FrontlineLabsScrapEconomySessionTests
{
    private const int Harvester = 0;
    private const int Idle = 1;

    private static readonly Position NorthVein = new(11, 1);
    private static readonly Position VeinApproach = new(10, 1);
    private static readonly Position HomePad = new(2, 7);

    /// <summary>Where the striker parks, down the lane from the deposit.</summary>
    private static readonly Position AmbushTile = new(16, 1);

    /// <summary>
    /// The tick the ambush opens: after the courier has banked enough to buy
    /// its tier and is standing on the vein with a fresh load still on it.
    /// </summary>
    private const int AmbushFromTick = 205;

    /// <summary>
    /// The scripted harvest. Team 0's prime walks to the tile beside the
    /// north deposit, waits for the metronome — standing ON the site at the
    /// spawn tick would displace the deposit rather than collect it — steps
    /// on, and walks the load home. Team 0's child, once it unlocks, buys
    /// <paramref name="track"/> the first tick the mask offers it. Team 1
    /// never leaves home.
    /// </summary>
    private static (
        ActorResolvedMatchDefinition Definition,
        GenericActorMatchChronology Chronology) Run(
        string? track = null,
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.Scrap,
        int shootFromTick = int.MaxValue,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Standard)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Bulwark),
                economy: economy,
                horizon: horizon);
        return (
            definition,
            FrontlineLabsSkillArmTestFixture.Run(
                definition,
                (start, observation) => start.ActorId.TeamId != 0
                    ? GenericDeathmatchSessionTestFixture.Wait()
                    : TeamZero(observation, track, shootFromTick)));
    }

    private static GenericActorRuntimeDecision TeamZero(
        GenericActorRuntimeObservation observation,
        string? track,
        int shootFromTick)
    {
        if (observation.Self.ActorId.UnitId == Idle)
            return Invest(observation, track);
        if (observation.Self.ActorId.UnitId != Harvester)
            return GenericDeathmatchSessionTestFixture.Wait();
        if (observation.Self.ActorId.LifeId != 0)
            return GenericDeathmatchSessionTestFixture.Wait();
        if (observation.Tick >= shootFromTick)
        {
            // The bulwark does not bend, so its mobile gun is the
            // parameterless straight action.
            return FrontlineLabsSkillArmTestFixture.Allows(
                observation,
                "shoot-straight")
                ? FrontlineLabsSkillArmTestFixture.ShootStraight()
                : GenericDeathmatchSessionTestFixture.Wait();
        }
        return Harvest(observation);
    }

    /// <summary>
    /// Out to the approach tile, on to the deposit the tick after it lands,
    /// then home. Two full round trips, so the bank clears the ladder's price.
    /// </summary>
    private static GenericActorRuntimeDecision Harvest(
        GenericActorRuntimeObservation observation)
    {
        Position self = observation.Self.Position;
        bool loaded = observation.Self.CarriedScrap > 0;
        // v1.1 leaves a remainder on the tile (a deposit of 8 against an
        // assay plus a carry of 6), so "am I standing on the vein" is no
        // longer a reason to stay: the pile itself is.
        Position target = loaded
            ? HomePad
            : DepositIsStanding(observation)
                ? NorthVein
                : VeinApproach;
        // Row 1 first, then along it: the northern shoulder is open the whole
        // way up column 2, and the lane itself is a clean corridor.
        if (!loaded && self.Y > 1)
            return GenericDeathmatchSessionTestFixture.Move(Direction.North);
        if (loaded && self.X > HomePad.X)
            return GenericDeathmatchSessionTestFixture.Move(Direction.West);
        if (loaded && self.Y < HomePad.Y)
            return GenericDeathmatchSessionTestFixture.Move(Direction.South);
        if (self.X < target.X)
            return GenericDeathmatchSessionTestFixture.Move(Direction.East);
        if (self.X > target.X)
            return GenericDeathmatchSessionTestFixture.Move(Direction.West);
        return GenericDeathmatchSessionTestFixture.Wait();
    }

    private static bool DepositIsStanding(
        GenericActorRuntimeObservation observation) =>
        Mode(observation).ScrapPiles.Any(pile =>
            pile.Position == NorthVein);

    /// <summary>
    /// Buys the named track the first tick it is offered. Affordability lives
    /// in the mask, so the probe reads the mask exactly as a bot would rather
    /// than pricing the ladder itself.
    /// </summary>
    private static GenericActorRuntimeDecision Invest(
        GenericActorRuntimeObservation observation,
        string? track)
    {
        if (track is null || !Offers(observation, track))
            return GenericDeathmatchSessionTestFixture.Wait();
        return new GenericActorRuntimeDecision(
            "invest",
            PublicActionCodes.Invest,
            [
                new GenericActorRuntimeActionArgument.UpgradeTrackArgument(
                    track),
            ],
            null);
    }

    private static bool Offers(
        GenericActorRuntimeObservation observation,
        string track) =>
        Tracks(observation).Contains(track, StringComparer.Ordinal);

    private static ImmutableArray<string> Tracks(
        GenericActorRuntimeObservation observation) =>
        observation.ActionLegalities
            .Where(legality => string.Equals(
                legality.ActionId,
                "invest",
                StringComparison.Ordinal))
            .SelectMany(legality => legality.Constraints)
            .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                .UpgradeTrackConstraint>()
            .SelectMany(constraint => constraint.AllowedTrackIds)
            .ToImmutableArray();

    private static GenericActorRuntimeObservation.ModeObservationState
        .Frontline Mode(GenericActorRuntimeObservation observation) =>
        (GenericActorRuntimeObservation.ModeObservationState.Frontline)
            observation.Mode;

    private static GenericActorRuntimeObservation.ModeObservationState
        .Frontline PostMode(GenericActorMatchTickFrame frame) =>
        (GenericActorRuntimeObservation.ModeObservationState.Frontline)
            frame.PostState.Mode;

    /// <summary>
    /// The whole loop, observed: a deposit lands on schedule and is published;
    /// stepping onto it banks the assay instantly and loads the rest, which
    /// the carrier publishes to itself and to the enemy that can see it; and
    /// the load banks in full the tick the carrier reaches its own pad.
    /// </summary>
    [Fact]
    public void AHarvesterBanksTheAssayAtTheTileAndTheLoadAtHome()
    {
        GenericActorMatchChronology run = Run().Chronology;

        GenericActorMatchTickFrame deposit = run.Ticks.Single(
            frame => frame.Tick == 60);
        Assert.Contains(
            PostMode(deposit).ScrapPiles,
            pile => pile.Position == NorthVein
                && pile.Amount == 8
                && pile.ExpiresAtTick == 140);

        GenericActorMatchTickFrame pickup = run.Ticks.First(frame =>
            PostMode(frame).ScrapTeams
                .Single(team => team.TeamId == 0)
                .Bank > 0);
        Assert.Equal(61, pickup.Tick);
        Assert.Equal(
            1,
            PostMode(pickup).ScrapTeams.Single(team => team.TeamId == 0).Bank);
        // A deposit of 8 against an assay of 1 and a carry of 6 leaves a
        // remainder standing with its original expiry: one body cannot lift a
        // whole vein.
        Assert.Equal(
            1,
            Assert.Single(
                    PostMode(pickup).ScrapPiles,
                    pile => pile.Position == NorthVein)
                .Amount);

        // The load is published on the carrier's own next observation, and on
        // the enemy's if it can see it. Zero on everybody else.
        GenericActorRuntimeObservation carrier = run.Ticks
            .Single(frame => frame.Tick == 62)
            .ActorTurns
            .Single(turn =>
                turn.ActorId.TeamId == 0
                && turn.ActorId.UnitId == Harvester)
            .Observation;
        Assert.Equal(6, carrier.Self.CarriedScrap);
        Assert.All(carrier.Allies, ally => Assert.Equal(0, ally.CarriedScrap));

        GenericActorMatchTickFrame banked = run.Ticks.First(frame =>
            PostMode(frame).ScrapTeams
                .Single(team => team.TeamId == 0)
                .Bank == 7);
        Assert.True(banked.Tick > pickup.Tick);
        Assert.Contains(
            banked.ActorTurns,
            turn => turn.Observation.Self.CarriedScrap == 6);
        // The load left the carrier the tick it banked.
        GenericActorMatchTickFrame after = run.Ticks.Single(frame =>
            frame.Tick == banked.Tick + 1);
        Assert.All(
            after.ActorTurns,
            turn => Assert.Equal(0, turn.Observation.Self.CarriedScrap));
    }

    /// <summary>
    /// The owner's third ruling of the window, driven live: with the total cap
    /// removed, one committed harvester over a 750-tick match banks the whole
    /// SIX-tier board — +2 gun travel, +2 spawn health, +2 sight — and the
    /// legality mask closes only when the board is full. The economy is now
    /// allowed to decide the match, and this is what deciding it looks like.
    /// </summary>
    [Fact]
    public void ACommittedHarvesterBanksTheWholeSixTierBoard()
    {
        GenericActorMatchChronology run = Run(
            track: null,
            economy: FrontlineLabsEconomyArm.ScrapFlat,
            horizon: FrontlineLabsHorizonArm.Long).Chronology;

        GenericActorRuntimeObservation.ScrapTeamState settled =
            PostMode(run.Ticks[^1]).ScrapTeams
                .Single(team => team.TeamId == 0);
        Assert.Equal([2, 2, 2], settled.TierLevels.ToArray());
        Assert.Equal(
            FrontlineLabsScrapEconomy.MaxTotalTiers,
            settled.TierLevels.Sum());
        // Six tiers cost sixty, and the harvester earned every one of them
        // out of deposits, remainders and the assay.
        Assert.True(
            settled.Bank
                + settled.TierLevels.Sum() * FrontlineLabsScrapEconomy.TierCost
                >= 60,
            $"the board cost more than the run earned: {settled.Bank}");

        // The same script on the invest arm reaches the same affordability;
        // what differs is only who spends it.
        GenericActorMatchChronology arm = Run(
            track: "edge",
            horizon: FrontlineLabsHorizonArm.Long).Chronology;
        GenericActorRuntimeObservation.ScrapTeamState armTeam =
            PostMode(arm.Ticks[^1]).ScrapTeams
                .Single(team => team.TeamId == 0);
        Assert.True(
            armTeam.Bank + armTeam.TierLevels.Sum() * 10 >= 60,
            $"the invest arm earned less: {armTeam.Bank}");
        // Two in a track is still the cap the mask enforces.
        Assert.Equal(2, armTeam.TierLevels[0]);
    }

    /// <summary>
    /// Zero new event kinds: every bank and tier change rides the existing
    /// mode-changed fact carrying the complete post-change state, so the enemy
    /// reads the purchase on the tick it happens with no inference.
    /// </summary>
    [Fact]
    public void EveryBankChangeRidesAModeChangedFact()
    {
        GenericActorMatchChronology run = Run("plate").Chronology;

        foreach (GenericActorMatchTickFrame frame in run.Ticks)
        {
            GenericActorRuntimeObservation.ModeObservationState.Frontline
                before = (GenericActorRuntimeObservation.ModeObservationState
                    .Frontline)frame.TickStart.State.Mode;
            GenericActorRuntimeObservation.ModeObservationState.Frontline
                after = PostMode(frame);
            bool moved =
                !before.ScrapTeams.SequenceEqual(after.ScrapTeams)
                || !before.ScrapPiles.SequenceEqual(after.ScrapPiles);
            if (!moved)
                continue;
            GenericActorRuntimeObservation.EventPayload.ModeChanged published =
                Assert.Single(
                    frame.Events
                        .Where(item => item.Kind
                            == GenericActorRuntimeObservation.EventKind
                                .ModeChanged)
                        .Select(item =>
                            (GenericActorRuntimeObservation.EventPayload
                                .ModeChanged)item.Payload));
            Assert.Equal(after, published.State);
            Assert.All(
                frame.Events
                    .Where(item => item.Kind
                        == GenericActorRuntimeObservation.EventKind
                            .ModeChanged),
                item => Assert.IsType<
                    GenericActorAuthoritativeEvent.Audience.Public>(
                        item.EventAudience));
        }
    }

    /// <summary>
    /// The mask is the author surface: nothing is offered while the bank
    /// cannot cover a tier, everything is offered once it can, and the whole
    /// verb goes unavailable the moment the caps are reached.
    /// </summary>
    [Fact]
    public void TheMaskOffersOnlyAffordableUncappedTracks()
    {
        GenericActorMatchChronology run = Run("plate").Chronology;

        GenericActorMatchActorTurn[] investor = run.Ticks
            .SelectMany(frame => frame.ActorTurns)
            .Where(turn =>
                turn.ActorId.TeamId == 0 && turn.ActorId.UnitId == Idle)
            .OrderBy(turn => turn.Observation.Tick)
            .ToArray();

        // Nothing is buyable while the bank is empty, and the verb itself is
        // reported unavailable rather than merely unconstrained.
        GenericActorMatchActorTurn broke = investor.First();
        Assert.Empty(Tracks(broke.Observation));
        Assert.False(
            FrontlineLabsSkillArmTestFixture.Allows(
                broke.Observation,
                "invest"));

        GenericActorMatchActorTurn affordable = investor.First(turn =>
            Tracks(turn.Observation).Length > 0);
        Assert.Equal(
            ["edge", "optic", "plate"],
            Tracks(affordable.Observation)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.True(
            FrontlineLabsSkillArmTestFixture.Allows(
                affordable.Observation,
                "invest"));
        Assert.True(
            Mode(affordable.Observation).ScrapTeams
                .Single(team => team.TeamId == 0)
                .Bank >= 10);

        // The purchase resolves that tick and the bank drops by exactly the
        // declared price.
        GenericActorMatchTickFrame purchase = run.Ticks.Single(frame =>
            frame.Tick == affordable.Observation.Tick);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            purchase.ActorTurns
                .Single(turn =>
                    turn.ActorId.TeamId == 0
                    && turn.ActorId.UnitId == Idle)
                .ActionResolution
                .Outcome);
        GenericActorRuntimeObservation.ScrapTeamState team =
            PostMode(purchase).ScrapTeams.Single(value => value.TeamId == 0);
        Assert.Equal([0, 1, 0], team.TierLevels.ToArray());
        Assert.Equal(
            Mode(affordable.Observation).ScrapTeams
                .Single(value => value.TeamId == 0)
                .Bank - 10,
            team.Bank);
    }

    /// <summary>
    /// OPTIC is the cheapest tier to observe: sight range is read fresh every
    /// tick, so the prime simply sees more tiles from the tick after the
    /// purchase — and the ally that is NOT the prime slot sees exactly what it
    /// saw before, which is the whole prime-scope rule.
    /// </summary>
    [Fact]
    public void OpticWidensThePrimeSightAndNothingElse()
    {
        GenericActorMatchChronology optic = Run("optic").Chronology;
        GenericActorMatchChronology none = Run().Chronology;
        int purchaseTick = PurchaseTick(optic);

        // The two runs share one script — the purchase costs the CHILD's
        // action, and the prime walks the same route in both — so the same
        // tick in each is the same body on the same tile. On the purchase
        // tick they see identically; on the next tick the upgraded prime sees
        // strictly more, which is the tier applying from the tick AFTER it
        // resolves.
        Assert.Equal(
            VisibleTiles(none, purchaseTick, Harvester),
            VisibleTiles(optic, purchaseTick, Harvester));
        int plain = VisibleTiles(none, purchaseTick + 1, Harvester);
        int widened = VisibleTiles(optic, purchaseTick + 1, Harvester);
        Assert.True(
            widened > plain,
            $"optic did not widen the prime's sight ({plain} -> {widened})");

        // And the buyer itself — a child, not the prime slot — gains nothing:
        // the whole reward is flat per team however many slots it fields.
        Assert.Equal(
            VisibleTiles(none, purchaseTick + 1, Idle),
            VisibleTiles(optic, purchaseTick + 1, Idle));
    }

    /// <summary>
    /// EDGE moves the gun's declared travel at the point of use: the bolt the
    /// prime fires after the purchase carries one more tile of reach than the
    /// same bolt fired on the same tick without it. It applies from the tick
    /// AFTER the purchase, so a tier bought this tick never lengthens this
    /// tick's shot.
    /// </summary>
    [Fact]
    public void EdgeLengthensTheNextBoltAndNotThisOne()
    {
        int purchaseTick = PurchaseTick(Run("edge").Chronology);

        int plain = FirstBoltRemaining(
            Run(shootFromTick: purchaseTick + 1).Chronology);
        Assert.True(plain > 0, "the probe never fired");

        // A bolt fired ON the purchase tick is unchanged: the tier settles
        // after every bolt has flown, so it never lengthens this tick's shot.
        Assert.Equal(
            plain,
            FirstBoltRemaining(
                Run("edge", shootFromTick: purchaseTick).Chronology));

        // The next one carries exactly one more tile of reach.
        Assert.Equal(
            plain + 1,
            FirstBoltRemaining(
                Run("edge", shootFromTick: purchaseTick + 1).Chronology));
    }

    /// <summary>
    /// The control arm removes the spend side entirely: no verb exists in the
    /// catalog, no body ever casts one, and the bank still converts — greedily,
    /// in declared track order, at the end of the tick it can first afford a
    /// tier.
    /// </summary>
    [Fact]
    public void TheFlatControlBuysWithoutAnyVerb()
    {
        GenericActorMatchChronology flat = Run(
            track: null,
            economy: FrontlineLabsEconomyArm.ScrapFlat).Chronology;

        Assert.DoesNotContain(
            flat.Ticks.SelectMany(frame => frame.ActorTurns),
            turn => turn.Observation.ActionLegalities.Any(legality =>
                string.Equals(
                    legality.ActionId,
                    "invest",
                    StringComparison.Ordinal)));

        GenericActorMatchTickFrame bought = flat.Ticks.First(frame =>
            PostMode(frame).ScrapTeams
                .Single(team => team.TeamId == 0)
                .TierLevels
                .Sum() > 0);
        GenericActorRuntimeObservation.ScrapTeamState team =
            PostMode(bought).ScrapTeams.Single(value => value.TeamId == 0);
        // `edge` is first in declared order and every tier costs the same, so
        // the greedy buyer takes it.
        Assert.Equal([1, 0, 0], team.TierLevels.ToArray());
        Assert.True(team.Bank < 10);

        // The arm and its control differ in the spend side alone: the same
        // script earns the same scrap in both, and the control's is simply
        // already spent.
        GenericActorMatchChronology arm = Run().Chronology;
        GenericActorRuntimeObservation.ScrapTeamState settled =
            PostMode(flat.Ticks[^1]).ScrapTeams
                .Single(value => value.TeamId == 0);
        Assert.Equal(
            BankedTotal(arm),
            settled.Bank + settled.TierLevels.Sum() * 10);
        Assert.Equal(0, ArmTiers(arm));
    }

    /// <summary>
    /// The chronology validator re-derives the whole economy from the recorded
    /// document — deposits from the declared schedule, wreckage from the
    /// recorded destructions, pickups and banking from the recorded bodies,
    /// purchases from the recorded resolutions — so a forged bank cannot
    /// reconcile with the boundary the same document published.
    /// </summary>
    [Fact]
    public void TheValidatorAcceptsTheHistoryAndRefusesAForgedBank()
    {
        (ActorResolvedMatchDefinition definition,
            GenericActorMatchChronology run) = Run("plate");
        // The session already validated it on the way out; re-running the
        // evidence explicitly is what makes the refusal below meaningful.
        GenericFrontlineChronologyEvidence.Validate(
            definition,
            run.InitialFrame,
            run.Ticks,
            run.Result);

        int purchaseTick = PurchaseTick(run);
        GenericActorMatchTickFrame frame = run.Ticks.Single(
            value => value.Tick == purchaseTick);
        GenericActorRuntimeObservation.ModeObservationState.Frontline honest =
            PostMode(frame);

        // A bank nobody earned, and a pile nobody dropped: both are
        // re-derived rather than read, so neither can reconcile.
        foreach (GenericActorRuntimeObservation.ModeObservationState.Frontline
                     forged in new[]
                     {
                         honest with
                         {
                             ScrapTeams =
                             [
                                 .. honest.ScrapTeams.Select(team =>
                                     team.TeamId == 0
                                         ? team with
                                         {
                                             Bank = team.Bank + 40,
                                         }
                                         : team),
                             ],
                         },
                         honest with
                         {
                             ScrapPiles =
                             [
                                 .. honest.ScrapPiles,
                                 new GenericActorRuntimeObservation.ScrapPile(
                                     new Position(3, 3),
                                     9,
                                     purchaseTick + 80),
                             ],
                         },
                     })
        {
            var forgedFrame = new GenericActorMatchTickFrame(
                frame.TickStart,
                frame.ActorTurns,
                frame.Events,
                frame.Traversals,
                new GenericActorWorldSnapshot(
                    definition,
                    frame.PostState.NextTick,
                    frame.PostState.NextProjectileId,
                    frame.PostState.Participants,
                    frame.PostState.Slots,
                    frame.PostState.ActiveLives,
                    frame.PostState.PendingReplications,
                    frame.PostState.Projectiles,
                    frame.PostState.Scoreboard,
                    forged));
            ArgumentException failure = Assert.Throws<ArgumentException>(() =>
                GenericFrontlineChronologyEvidence.Validate(
                    definition,
                    run.InitialFrame,
                    [
                        .. run.Ticks.Select(item =>
                            item.Tick == frame.Tick ? forgedFrame : item),
                    ],
                    result: null));
            Assert.Contains(
                "Frontline",
                failure.Message,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Regression for the wave-8 abort (four authors independently): a bolt
    /// launched down an OPEN lane after the edge tier settles carries a
    /// committed path one tile longer than the raw profile's trace, and the
    /// world-snapshot validator used to re-trace with the raw profile and
    /// fault the whole match ("A retained projectile must preserve its
    /// exact resolved committed path"). The earlier edge probe fired into a
    /// near wall, so its truncated trace was identical either way and the
    /// defect slipped through. This one buys edge, walks the lane east, and
    /// fires along ~15 open tiles with the bolt alive across snapshot
    /// boundaries — the match must complete and validate.
    /// </summary>
    [Fact]
    public void TheEdgeTierDoesNotAbortAMatchWithALongBoltInFlight()
    {
        int purchaseTick = PurchaseTick(Run("edge").Chronology);
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Bulwark),
                economy: FrontlineLabsEconomyArm.Scrap);
        GenericActorMatchChronology run =
            FrontlineLabsSkillArmTestFixture.Run(
                definition,
                (start, observation) =>
                {
                    if (start.ActorId.TeamId != 0)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    if (observation.Self.ActorId.UnitId == Idle)
                        return Invest(observation, "edge");
                    if (observation.Self.ActorId.UnitId != Harvester
                        || observation.Self.ActorId.LifeId != 0)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    if (observation.Tick <= purchaseTick)
                        return Harvest(observation);
                    // Post-purchase: hold the open north lane and fire east
                    // down it — facing-locked, so the eastward step arms the
                    // eastward gun.
                    if (observation.Self.Position.Y > 1)
                        return GenericDeathmatchSessionTestFixture.Move(
                            Direction.North);
                    if (observation.Self.Position.X < 5)
                        return GenericDeathmatchSessionTestFixture.Move(
                            Direction.East);
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "shoot-straight")
                        ? FrontlineLabsSkillArmTestFixture.ShootStraight()
                        : GenericDeathmatchSessionTestFixture.Wait();
                });

        // The purchase happened and at least one post-purchase eastward bolt
        // lived past its launch tick — the exact shape that used to abort.
        Assert.True(PurchaseTick(run) > 0, "the probe never bought edge");
        Assert.Contains(
            run.Ticks,
            frame => frame.Tick > purchaseTick
                && frame.PostState.Projectiles.Any(projectile =>
                    projectile.OwnerActorId.TeamId == 0
                    && projectile.CommittedPath.Length
                        > frame.Tick - projectile.SpawnedAtTick));
    }

    private static int PurchaseTick(GenericActorMatchChronology run) =>
        run.Ticks
            .First(frame =>
                PostMode(frame).ScrapTeams
                    .Single(team => team.TeamId == 0)
                    .TierLevels
                    .Sum() > 0)
            .Tick;

    /// <summary>
    /// The interception run. Team 0 is a fabricator — the 2-HP prime, so it
    /// dies to the count of bolts the probe can guarantee — harvesting the
    /// north lane and buying <c>plate</c> the moment it can. From the third
    /// deposit it stops running the load home and stands in the lane instead,
    /// inside the reach of a team-1 striker parked down the same corridor.
    /// </summary>
    private static (
        ActorResolvedMatchDefinition Definition,
        GenericActorMatchChronology Chronology) RunInterception()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Striker),
                economy: FrontlineLabsEconomyArm.Scrap);
        return (
            definition,
            FrontlineLabsSkillArmTestFixture.Run(
                definition,
                (start, observation) => start.ActorId.TeamId == 1
                    ? Ambush(observation)
                    : Courier(observation)));
    }

    /// <summary>
    /// The striker: park in the lane, hold fire while the courier does its
    /// earlier round trips — the probe wants an interception, not a
    /// checkpoint — then fire straight down the corridor.
    /// </summary>
    private static GenericActorRuntimeDecision Ambush(
        GenericActorRuntimeObservation observation)
    {
        if (observation.Self.ActorId.UnitId != 0)
            return GenericDeathmatchSessionTestFixture.Wait();
        Position self = observation.Self.Position;
        if (self.Y > AmbushTile.Y)
            return GenericDeathmatchSessionTestFixture.Move(Direction.North);
        if (self.X > AmbushTile.X)
            return GenericDeathmatchSessionTestFixture.Move(Direction.West);
        return observation.Tick >= AmbushFromTick
            && FrontlineLabsSkillArmTestFixture.Allows(observation, "shoot")
            ? GenericDeathmatchSessionTestFixture.Shoot()
            : GenericDeathmatchSessionTestFixture.Wait();
    }

    /// <summary>
    /// The courier: harvest and bank while it can afford nothing, buy
    /// <c>plate</c> the first tick the mask offers it, and from the third
    /// deposit onward stand in the lane with the load still on it.
    /// </summary>
    private static GenericActorRuntimeDecision Courier(
        GenericActorRuntimeObservation observation)
    {
        if (observation.Self.ActorId.UnitId != Harvester)
            return GenericDeathmatchSessionTestFixture.Wait();
        if (Offers(observation, "plate"))
            return Invest(observation, "plate");
        if (observation.Self.ActorId.LifeId != 0)
            return GenericDeathmatchSessionTestFixture.Wait();
        if (observation.Tick >= 200 && observation.Self.CarriedScrap > 0)
            return GenericDeathmatchSessionTestFixture.Wait();
        return Harvest(observation);
    }

    /// <summary>
    /// A loaded body that dies leaves ONE pile worth its wreck plus its whole
    /// load, on the tile it died on — the largest single transfer in the
    /// design, and it belongs to whoever did the killing.
    /// </summary>
    [Fact]
    public void AKilledCourierLeavesItsWreckAndItsLoadOnOneTile()
    {
        GenericActorMatchChronology run = RunInterception().Chronology;

        GenericActorMatchTickFrame kill = run.Ticks.First(frame =>
            frame.Events.Any(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.Destruction
                && ((GenericActorRuntimeObservation.EventPayload.Destruction)
                    item.Payload).ActorId.TeamId == 0));
        var destroyed = (GenericActorRuntimeObservation.EventPayload
            .Destruction)kill.Events
            .Single(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.Destruction)
            .Payload;
        int load = run.Ticks
            .Single(frame => frame.Tick == kill.Tick)
            .ActorTurns
            .Single(turn => turn.ActorId == destroyed.ActorId)
            .Observation
            .Self
            .CarriedScrap;
        Assert.True(load > 0, "the courier died empty");

        FrontlineScrapPileFact pile = Assert.Single(
            PostMode(kill).ScrapPiles
                .Where(value => value.Position == destroyed.Position)
                .Select(value => new FrontlineScrapPileFact(
                    value.Amount,
                    value.ExpiresAtTick)));
        Assert.Equal(
            load + FrontlineLabsScrapEconomy.WreckAmount,
            pile.Amount);
        Assert.Equal(kill.Tick + 80, pile.ExpiresAtTick);
    }

    /// <summary>
    /// PLATE raises the ceiling and never heals: no standing body's health
    /// moves on the tick the tier resolves, and the next life of the prime
    /// slot arrives with the declared maximum plus the tier.
    /// </summary>
    [Fact]
    public void PlateRaisesTheNextSpawnAndHealsNobody()
    {
        GenericActorMatchChronology run = RunInterception().Chronology;
        int purchaseTick = PurchaseTick(run);

        GenericActorMatchTickFrame purchase = run.Ticks.Single(
            frame => frame.Tick == purchaseTick);
        Dictionary<ActorIdentity, int> before = purchase.TickStart.State
            .ActiveLives
            .ToDictionary(life => life.ActorId, life => life.Health);
        foreach (GenericActorWorldSnapshot.LifeSnapshot life in
                 purchase.PostState.ActiveLives)
        {
            Assert.Equal(before[life.ActorId], life.Health);
        }

        GenericActorRuntimeObservation.EventPayload.LifeSpawned respawn =
            run.Ticks
                .Where(frame => frame.Tick > purchaseTick)
                // Lives are created during the NEXT tick's preparation, so a
                // respawn is a tick-start fact rather than a resolution one.
                .SelectMany(frame => frame.TickStart.Events)
                .Where(item =>
                    item.Kind
                    == GenericActorRuntimeObservation.EventKind.LifeSpawned)
                .Select(item =>
                    (GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                        item.Payload)
                .First(payload =>
                    payload.ActorId.TeamId == 0
                    && payload.ActorId.UnitId == Harvester
                    && payload.ActorId.LifeId > 0);
        Assert.Equal(
            FrontlineLabsClassDefinition.Fabricator.PrimeMaxHealth + 1,
            respawn.Health);
    }

    private sealed record FrontlineScrapPileFact(
        int Amount,
        int ExpiresAtTick);

    private static int ArmTiers(GenericActorMatchChronology run) =>
        PostMode(run.Ticks[^1]).ScrapTeams
            .Single(team => team.TeamId == 0)
            .TierLevels
            .Sum();

    private static long BankedTotal(GenericActorMatchChronology run) =>
        PostMode(run.Ticks[^1]).ScrapTeams
            .Single(team => team.TeamId == 0)
            .Bank;

    private static int VisibleTiles(
        GenericActorMatchChronology run,
        int tick,
        int unitId) =>
        run.Ticks
            .Single(frame => frame.Tick == tick)
            .ActorTurns
            .Single(turn =>
                turn.ActorId.TeamId == 0 && turn.ActorId.UnitId == unitId)
            .Observation
            .VisibleTiles
            .Count(tile => tile.ObservedBy.Any(observer =>
                observer.UnitId == unitId));

    /// <summary>
    /// The reach still left on the first bolt the prime fires, read from the
    /// shooter's own published projectile one tick after the launch. Two runs
    /// of the same script differ here by exactly the edge tier.
    /// </summary>
    private static int FirstBoltRemaining(GenericActorMatchChronology run)
    {
        foreach (GenericActorMatchTickFrame frame in run.Ticks)
        {
            if (frame.ActorTurns
                    .SingleOrDefault(turn =>
                        turn.ActorId.TeamId == 0
                        && turn.ActorId.UnitId == Harvester)
                    ?.Observation
                    .VisibleProjectiles is not { } projectiles)
            {
                continue;
            }
            GenericActorRuntimeObservation.ObservedProjectile? bolt =
                projectiles
                    .Where(value => value.OwnerTeamId == 0)
                    .OrderBy(value => value.ProjectileId)
                    .FirstOrDefault();
            if (bolt is not null)
                return bolt.RemainingTiles;
        }
        return -1;
    }
}
