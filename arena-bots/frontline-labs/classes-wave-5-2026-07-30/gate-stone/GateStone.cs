using BotArena.Sdk;

/// <summary>
/// GateStone — a bulwark that keeps one ledger and settles every decision in it.
///
/// <para>The ledger's unit is ONE TICK OF OBJECTIVE WEIGHT. Under a net-scaling
/// control policy the signed capture gain is linear in net weight, so the
/// marginal worth of this body standing on the active objective is exactly its
/// own weight per tick — and it is worth precisely ZERO while the redeploy pause
/// runs or while a completed capture would be spent inside an enemy hold. On the
/// other side of the ledger, a destroyed enemy body is worth its weight
/// multiplied by its own declared absence, which the lifecycle profiles publish:
/// a body that stays off the board for thirty ticks is thirty points of progress
/// its owner never collects. Every judgement below is that comparison.</para>
///
/// <para>The open arm turns the turret from a one-way sacrifice into a
/// REPEATABLE MOVE — anchor and mobilize are both reversible for the whole life,
/// and health maps proportionally with a floor in both directions, so a full
/// body cycles for free and a wounded one pays the floor each round trip. That
/// makes fortifying a rate decision rather than a vow: spend zero-value ticks in
/// the turret's faster, longer, eight-headed gun, and walk the weight back onto
/// the point the tick the point starts paying again. Free placement means the
/// cheapest tile to fortify on is usually the objective tile itself — a turret
/// there scores nothing, but it is one tick from scoring again, and it denies
/// the tile to a body that would.</para>
///
/// <para>Nothing here is arm-specific: the guard route, the fortify route, its
/// reversibility, the health-transfer policy, the bend and aim envelope, the
/// capture policy, the lifecycle clocks and the movement coupling are all read
/// from the resolved contract, so the same artifact plays kit-off, kit-on,
/// bend-off, bend-on, strict ground, open ground, and the classless
/// qualification profile.</para>
/// </summary>
public sealed class GateStone : IGenericActorBot
{
    private StoneContract? _lens;
    private StoneMemory _memory = new();
    private int _participantId;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new StoneContract(start.Contract, start.ActorId.TeamId);
        _memory = new StoneMemory();
        _participantId = start.ParticipantId;
    }

    /// <inheritdoc />
    public GenericActorDecision Tick(GenericActorContext context)
    {
        if (_lens is not StoneContract lens)
            return ArenaBasics.Wait(context, "no contract");

        _memory.Observe(lens, context);
        NoteRefusedStep(context);

        // A windup is wait-only by contract; asking for anything else is a
        // rejected action and a wasted tick.
        if (context.Self.PendingSameLifeTransition is not null)
            return ArenaBasics.Wait(context, "windup");

        if (lens.Guards(context.Self.FormId))
            return Shielded(lens, context);
        if (lens.Immobile(context.Self.FormId))
            return Fortified(lens, context);
        return Mobile(lens, context);
    }

    /// <summary>
    /// In the arc, and leaving as soon as it stops earning.
    ///
    /// <para>The shell's price was mismeasured for a whole wave, and the contract
    /// says so plainly: the guarding form declares NO attack profile, and a
    /// cooldown belongs to an attack profile, so <c>Self.Cooldown</c> does not
    /// advance while the shield is up. Measured directly — entered on tick 9 with
    /// cooldown 3, still cooldown 2 two hundred ticks later. A shell therefore
    /// banks nothing: it is not a way to spend the ticks a cooldown-3 gun cannot
    /// use, because those ticks stop passing. Every shielded tick costs a full
    /// tick of fire, and the only thing it can buy back is a bolt actually
    /// deflected.</para>
    ///
    /// <para>So the arc stays up exactly while a hostile bolt in flight is inside
    /// it, and comes down otherwise. That also removes the trap the same
    /// arithmetic set: an exit condition written as "my gun is ready" is
    /// unreachable from inside a gunless form, and under open ground — where the
    /// shield may finally rise on the objective, with an enemy permanently in the
    /// quadrant — an unreachable exit is a body that holds one tile for the rest
    /// of the match while the front walks around it.</para>
    /// </summary>
    private GenericActorDecision Shielded(
        StoneContract lens,
        GenericActorContext context)
    {
        List<StoneGround.Incoming> threats =
            StoneGround.Inbound(lens, context, context.Self.Position);
        int covered = 0;
        int exposed = 0;
        foreach (StoneGround.Incoming threat in threats)
        {
            if (StoneContract.ArcCovers(context.Self.Facing, threat.Heading))
                covered += threat.Damage;
            else
                exposed += threat.Damage;
        }

        GenericActorRulesContract.FormTransition? exit =
            lens.ReturnRoute(context.Self.FormId);
        GenericActorDecision? drop = exit is null
            ? null
            : Transform(lens, context, exit, "lowering the shield");

        // A bolt outside the arc hurts normally and a shell cannot turn, so
        // against a flanker the shield is worse than useless.
        if (exposed > 0 && drop is not null)
            return drop;
        if (covered > 0)
            return ArenaBasics.Wait(context, $"arc turning {covered}");

        // Nothing in the air for the arc to eat. Holding costs a tick of fire and
        // buys a tick of nothing, so leave — and leave even with bodies in the
        // quadrant, because their bolts do not exist yet and the shield can be
        // raised again for one windup when they do.
        return drop ?? ArenaBasics.Wait(context, "arc up, nothing to turn");
    }

    /// <summary>
    /// Whether an ARMED enemy is close enough to shoot us — optionally only from
    /// inside the quadrant this facing protects. The gun and its range come from
    /// the enemy's own declared profile: a body whose form carries no attack
    /// profile (a shell, or any future unarmed stance) threatens nothing, and
    /// treating it as a threat is how a defensive doctrine talks itself into a
    /// draw.
    /// </summary>
    private static bool Threatening(
        StoneContract lens,
        GenericActorContext context,
        bool arcOnly)
    {
        (int inArc, int flanking) = ArcBalance(lens, context, context.Self.Facing);
        return arcOnly ? inArc > 0 : inArc + flanking > 0;
    }

    /// <summary>
    /// The cardinal facing that puts every armed gun inside one arc, or null when
    /// none does. If none does, the bodies are spread and turning into the shield
    /// only chooses which bolt to refuse — so the turn is not worth its tick.
    /// </summary>
    private static Direction? ShieldFacing(
        StoneContract lens,
        GenericActorContext context)
    {
        Direction? best = null;
        int bestCovered = 0;
        foreach (Direction candidate in StoneContract.AllCardinals)
        {
            (int inArc, int flanking) = ArcBalance(lens, context, candidate);
            if (flanking == 0 && inArc > bestCovered)
            {
                bestCovered = inArc;
                best = candidate;
            }
        }
        return best;
    }

    /// <summary>
    /// Armed guns bearing on this tile, split by whether the quadrant a body
    /// facing <paramref name="facing"/> protects would cover them.
    ///
    /// <para>THE SHELL IS OPPONENT-SHAPED, and this is the measurement that
    /// says so. One arc covers three of eight sectors and cannot turn, so
    /// against a single poke it is a tax on the shooter — but against several
    /// bodies on several bearings it deflects one bolt, eats the rest, and
    /// forfeits the gun that would have thinned them. Declining the shield is
    /// therefore a doctrine, not a lapse, and the condition is exactly this:
    /// raise it only when nothing armed is outside the arc.</para>
    /// </summary>
    private static (int InArc, int Flanking) ArcBalance(
        StoneContract lens,
        GenericActorContext context,
        Direction facing)
    {
        int inArc = 0;
        int flanking = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? gun =
                lens.Attack(enemy.FormId);
            if (gun is null)
                continue;
            int distance =
                context.Self.Position.ChebyshevDistance(enemy.Position);
            if (distance > gun.Projectile.MaxTravelTiles)
                continue;
            // A bolt from that bearing arrives on the reverse of it.
            ProjectileHeading inbound = StoneAim.Toward(
                enemy.Position,
                context.Self.Position);
            if (StoneContract.ArcCovers(facing, inbound))
                inArc++;
            else
                flanking++;
        }
        return (inArc, flanking);
    }

    /// <summary>
    /// Fortified: objective weight zero, so this body has traded its scoring
    /// presence for a faster, longer, eight-headed gun. Because the mobilize
    /// route is reversible for the whole life, the trade is a LEASE rather than a
    /// sale, and this method is where the rent gets checked every tick.
    ///
    /// <para>Rent is <c>push.WeightTick</c> — the progress this body would have
    /// earned by standing instead of shooting, which is zero while the redeploy
    /// pause runs or a completion would be spent. Income is the prize: an enemy
    /// killed is its weight times its own declared absence. Mobilize the tick the
    /// rent starts exceeding the income, or the tick the point genuinely needs a
    /// body — but never before the cycle's own windups have been amortised, or
    /// the body spends its life transforming.</para>
    /// </summary>
    private GenericActorDecision Fortified(
        StoneContract lens,
        GenericActorContext context)
    {
        StoneGround.Push push = StoneGround.Price(lens, context);
        Position[] objective = StoneAim.ActiveObjective(lens, context);
        int allies =
            StoneGround.OwnWeightExcludingSelf(lens, context, objective);
        GenericActorRulesContract.FormTransition? exit =
            lens.ReturnRoute(context.Self.FormId);

        StoneAim.Shot? shot = StoneAim.Best(lens, context, _memory, 20);
        bool coversGate = Coverage(lens, context, objective) > 0;

        // The gate needs a body when nothing else of ours is on it and an enemy
        // weight is. That outranks any rate of fire: at net zero nobody scores,
        // and below zero the opponent banks progress while we shoot at it.
        bool presenceNeeded =
            push.WeightTick > 0 && push.EnemyWeight > 0 && allies == 0;

        // A turret that bears on nothing is paying rent for an empty lane. The
        // test is a SHOT, not a nearby body: eight rays are not a filled radius,
        // so an enemy standing one tile diagonally off a blocked corner is close,
        // visible, and completely unhittable — and a turret that mistakes that for
        // work holds its tile in silence while the front walks past.
        bool idle = shot is null;

        if (exit is not null && (presenceNeeded || !coversGate || idle))
        {
            // The windups are the commitment price, and they are only paid once
            // per direction — so an exit taken before the entry has been earned
            // burns two ticks for nothing. Presence that is actually needed
            // jumps that queue; a merely idle lane waits its turn.
            int settled = _memory.TicksInForm(context.Tick);
            int amortise = exit.Windup.DurationTicks + 1;
            if (presenceNeeded || settled >= amortise)
            {
                GenericActorDecision? mobilize = Transform(
                    lens,
                    context,
                    exit,
                    presenceNeeded
                        ? "the gate needs a body, not a gun"
                        : coversGate
                            ? "nothing in the lane; taking the weight back"
                            : "the front moved out of my arc");
                if (mobilize is not null)
                    return mobilize;
            }
        }

        return shot?.Decision
            ?? ArenaBasics.Wait(context, "turret holding fire");
    }

    /// <summary>The mobile ladder: parry, kill, shoot, fortify, walk, aim.</summary>
    private GenericActorDecision Mobile(
        StoneContract lens,
        GenericActorContext context)
    {
        GenericActorDecision? fabrication =
            ArenaBasics.TryFabricateReady(lens.Raw, context);
        if (fabrication is not null)
            return fabrication;

        StoneGround.Push push = StoneGround.Price(lens, context);
        StoneGround.Station station = StoneGround.Choose(lens, context, push);
        bool onStation = Contains(station.Tiles, context.Self.Position);

        List<StoneGround.Incoming> threats =
            StoneGround.Inbound(lens, context, context.Self.Position);
        int now = 0;
        foreach (StoneGround.Incoming threat in threats)
        {
            // Movement resolves before combat and a stance completes after it,
            // so a bolt landing this tick can only be answered by moving.
            if (threat.Ticks <= 1)
                now += threat.Damage;
        }

        GenericActorRulesContract.FormTransition? guard =
            lens.GuardRoute(context.Self.FormId);
        // Where the shield may rise is a per-route read, and the answer moved:
        // under strict ground every objective tile forbade it and the arc could
        // only go up on the shoulder beside the gate; under open ground the
        // forbidden set is empty and the shield can guard the point it is
        // capturing. Same line of code, opposite plan — which is the point of
        // reading the route instead of the map.
        bool stanceLegalHere = guard is not null
            && lens.RouteAllowedOn(guard, context.Self.Position);
        GenericActorDecision? raise = stanceLegalHere && guard is not null
            ? Transform(lens, context, guard, "raising the arc")
            : null;
        // The shield is a PARRY, priced from the bolts actually in the air.
        // Entry completes at the end of the tick it is requested (windup 1,
        // after the mode update), so it can meet anything still at least one
        // tick out; and because a gunless form freezes the cooldown, a tick
        // held without a deflection is a tick of fire thrown away. So the only
        // shield worth raising is one that has something to eat, with nothing
        // armed outside the arc to punish it for looking the wrong way.
        int parry = 0;
        int leaks = 0;
        foreach (StoneGround.Incoming threat in threats)
        {
            if (threat.Ticks < 1)
                continue;
            if (StoneContract.ArcCovers(context.Self.Facing, threat.Heading))
                parry += threat.Damage;
            else
                leaks += threat.Damage;
        }
        GenericActorDecision? escape =
            StoneGround.Sidestep(lens, context, _memory, station.Tiles);

        // 1. A bolt landing this tick can only be answered by moving. Stepping
        //    to ANOTHER tile of the same station answers it for free — the
        //    damage is refused and the presence is kept — so that step is taken
        //    even on ground we mean to hold. Only a step that would cost the
        //    station has to be worth a wound.
        if (now > 0 && escape is not null)
        {
            bool keepsStation = EscapeKeepsStation(escape, context, station);
            if (keepsStation
                || now >= context.Self.Health
                || !station.StandFast)
            {
                _memory.NoteDodge(context.Self.Position, context.Tick);
                return escape;
            }
        }

        // 2. A kill, or the contact that shatters an enemy arc, outranks
        //    everything defensive: the bill for waiting is another exchange.
        StoneAim.Shot? shot = StoneAim.Best(lens, context, _memory, 45);
        if (shot is not null && (shot.Score >= 120 || shot.BreaksGuard))
            return shot.Decision;

        // 3. The parry. A bolt already in the air, already inside the arc, with
        //    nothing leaking round the side — and the shield beats the dodge only
        //    because it keeps the tile, which under open ground can be the
        //    objective tile itself. A shield raised on any weaker evidence is a
        //    tick of fire donated to the opponent.
        bool stanceSettled = guard is null
            || _memory.TicksInForm(context.Tick) >= guard.Windup.DurationTicks + 1;
        if (raise is not null
            && stanceSettled
            && parry > 0
            && leaks == 0
            && (station.StandFast || onStation))
        {
            return raise;
        }

        // 4. Ordinary suppression.
        if (shot is not null)
            return shot.Decision;

        // 5. Trade presence for a gun when the ledger says the gun earns more.
        GenericActorDecision? fortify = Fortify(lens, context, _memory, push);
        if (fortify is not null)
            return fortify;

        // 6. Walk the ground.
        GenericActorDecision? advance = StoneGround.Step(
            lens,
            context,
            _memory,
            station.Tiles,
            _memory.Avoided(context.Tick));
        if (advance is not null)
            return advance;

        // 6b. Suppression rather than concession. Parked, with the cooldown up
        //     and a body somewhere in the envelope, a shot at ANY live chance
        //     beats a wait: waiting spends the same tick and buys nothing, and a
        //     bolt in the lane is a tile the opponent has to think about.
        StoneAim.Shot? suppress = StoneAim.Best(lens, context, _memory, 1);
        if (suppress is not null)
            return suppress.Decision;

        // 7. Standing still is a decision about where the gun points.
        Direction cover = CoverFacing(lens, context, _memory);
        GenericActorDecision? aim = StoneGround.Turn(
            lens,
            context,
            cover,
            $"facing {cover} to cover the {station.Reason}");
        if (aim is not null)
            return aim;

        // 8. Fabrication is worth a walk home only when nothing is happening.
        GenericActorDecision? errand = FabricationErrand(lens, context, push);
        if (errand is not null)
            return errand;

        return ArenaBasics.Wait(
            context,
            $"holding {station.Reason} at {context.Self.Position}");
    }

    /// <summary>
    /// THE CAPTURE-ARITHMETIC GATE. Anchor when the turret's gun earns more than
    /// the objective weight it deletes, and not one tick sooner.
    ///
    /// <para>Both sides of that comparison are contract reads. The rent is one
    /// tick of this body's weight — zero while the redeploy pause runs or a
    /// completion would be spent inside an enemy hold, which makes those windows
    /// free fortify time. The income is a kill: an enemy body is worth its own
    /// objective weight multiplied by the absence its lifecycle profile declares,
    /// and a slot that must be fabricated explicitly is worth more than one that
    /// returns by itself, because nobody is obliged to bring it back. Against
    /// eighteen to thirty points of absence, the handful of ticks a
    /// cooldown-1 gun needs to remove a two- or three-health body is cheap; that
    /// is the whole case for the turret, and it is why the gate opens far wider
    /// here than it did when anchoring was permanent.</para>
    ///
    /// <para>Three refusals survive the arithmetic. A body that cannot afford
    /// its own windup does not start one — the source form is retained and
    /// wait-only throughout, and lethal damage cancels it, so the windup is
    /// priced against the guns that actually bear. A wounded body does not cycle
    /// for fun: proportional health with a floor is lossless at full and lossy
    /// everywhere else, so below full it anchors only on the terms that justified
    /// a PERMANENT anchor — relief already standing, or its own hold running. And
    /// a body with no relief in sight does not leave a point it is the last
    /// weight on, unless presence has already stopped paying.</para>
    /// </summary>
    private static GenericActorDecision? Fortify(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        StoneGround.Push push)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.FortifyRoute(context.Self.FormId);
        if (route is null
            || !lens.RouteAllowedOn(route, context.Self.Position))
        {
            return null;
        }

        // Do not re-anchor the tick after mobilising: the cycle is unlimited,
        // not free, and two windups per round trip is the whole price.
        if (memory.TicksInForm(context.Tick)
            < route.Windup.DurationTicks + 1)
        {
            return null;
        }

        // The turret must actually BE an upgrade. Read both guns rather than
        // assuming the anchored one is better: a faster cadence, a longer reach,
        // or absolute aim instead of a locked facing each count, and if none of
        // them holds then anchoring is pure loss whatever the arithmetic says.
        GenericActorRulesContract.AttackProfile? mine =
            lens.Attack(context.Self.FormId);
        GenericActorRulesContract.AttackProfile? fortified =
            lens.Attack(route.TargetFormId);
        if (fortified is null)
            return null;
        if (mine is not null
            && fortified.CooldownTicks >= mine.CooldownTicks
            && fortified.Projectile.MaxTravelTiles
                <= mine.Projectile.MaxTravelTiles
            && !fortified.OmnidirectionalAim)
        {
            return null;
        }

        Position[] objective = StoneAim.ActiveObjective(lens, context);
        int covered = Coverage(lens, context, objective, route.TargetFormId);
        if (covered < 2)
            return null;

        // Can this body survive being wait-only for the whole windup?
        if (!SurvivesWindup(lens, context, route))
            return null;

        GenericActorRulesContract.FormTransition? back =
            lens.ReturnRoute(route.TargetFormId);
        bool reversible = back is not null && !route.IrreversibleForLife;
        bool lossless = reversible
            && back is not null
            && lens.HealthRoundTrip(route, back, context.Self.Health)
                >= context.Self.Health;

        int allies =
            StoneGround.OwnWeightExcludingSelf(lens, context, objective);
        if (!lossless)
        {
            // Either irreversible, or a round trip that pays the floor. Treat it
            // as the one-way commitment it effectively is, on the terms measured
            // when it always was one: enough presence to survive one death, or
            // ground the ratchet is already protecting.
            if (allies < 2
                && push.Hold is not { Mine: true, RemainingTicks: > 12 })
            {
                return null;
            }
            return Transform(
                lens,
                context,
                route,
                $"anchoring to stay: {covered} gate tiles under fire");
        }

        // A free window: progress is impossible this tick, so the weight this
        // anchor deletes was already worth nothing. Any target at all pays.
        int rent = push.WeightTick;
        if (rent == 0)
        {
            return Threatening(lens, context, arcOnly: false)
                ? Transform(
                    lens,
                    context,
                    route,
                    push.Frozen
                        ? "pause running: weight is worth zero, so is the trade"
                        : "completion would be spent; taking the faster gun")
                : null;
        }

        // Paid time. A turret cannot bank what it wins: objective weight zero
        // means the absence a kill creates only becomes progress if somebody of
        // ours is standing on the point to collect it. So the prize is real only
        // behind relief; alone on contested ground, fortifying spends presence to
        // buy a receipt nobody can cash. That refusal is not conservatism about
        // reversibility — it is the same arithmetic that made a permanent anchor
        // wrong, and reversibility does not repeal it.
        // How much relief the gate demands is set by THE BODY CURVE, recomputed
        // from the slots each side declares. Outnumbered on slots, one spare body
        // is all the relief that will ever exist and the gun has to be picked up
        // behind it; level or ahead on slots, presence is the cheaper lever and
        // the gate keeps the two-body margin that survives one death. Measured
        // both ways over the same eighteen games: demanding two everywhere turned
        // the four-slot cell from a breach at t195 into a loss, and demanding one
        // everywhere cost fifteen points of margin in the three-slot cells.
        int reliefWanted =
            lens.OpposingSlotCount() > lens.SlotCount(lens.TeamId) ? 1 : 2;
        if (allies < reliefWanted)
            return null;

        (int prize, int ticksToKill) = BestPrize(lens, context, fortified);
        return prize > 0 && prize >= rent * ticksToKill
            ? Transform(
                lens,
                context,
                route,
                $"anchoring: {covered} tiles covered, prize {prize}"
                + $" vs {rent * ticksToKill}")
            : null;
    }

    /// <summary>
    /// What the best kill in reach is worth, and roughly how long it takes.
    ///
    /// <para>Worth is the enemy's own objective weight multiplied by the absence
    /// its lifecycle profile declares, with a premium when nothing brings that
    /// slot back automatically — an explicitly fabricated body costs its owner an
    /// action and a walk home on top of the clock. Duration is the honest
    /// arithmetic of the gun that would do it: hits needed times cadence, then
    /// doubled, because roughly half of what a turret fires at a moving body
    /// misses. Both halves are read, not remembered.</para>
    /// </summary>
    private static (int Prize, int TicksToKill) BestPrize(
        StoneContract lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile gun)
    {
        int prize = 0;
        int ticks = 1;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            // "In range" is not "hittable". An absolute eight-way gun fires
            // eight rays, not a filled radius, and strict diagonal corners cut
            // several of them short — a turret anchored one tile diagonally off a
            // body cannot touch it. Ask for a clear lane, not a distance.
            if (!Reachable(lens, context.Self.Position, enemy.Position, gun))
                continue;
            (int delay, bool automatic) = lens.Absence(
                enemy.ActorId.TeamId,
                enemy.ActorId.UnitId);
            if (delay <= 0)
                continue;
            int weight = Math.Max(lens.Weight(enemy.FormId), 1);
            int value = weight * (automatic ? delay : delay * 2);
            int hits = (enemy.Health + gun.Projectile.DamagePerHit - 1)
                / Math.Max(gun.Projectile.DamagePerHit, 1);
            int span = Math.Max(
                hits * Math.Max(gun.CooldownTicks, 1) * MissAllowance,
                1);
            if (value > prize)
            {
                prize = value;
                ticks = span;
            }
        }
        return (prize, ticks);
    }

    /// <summary>
    /// Whether an absolute eight-way gun standing on <paramref name="from"/> has a
    /// clear lane to <paramref name="to"/> — one of its eight rays must actually
    /// arrive there, walls and strict diagonal corners included.
    /// </summary>
    private static bool Reachable(
        StoneContract lens,
        Position from,
        Position to,
        GenericActorRulesContract.AttackProfile gun)
    {
        for (int heading = 0; heading < 8; heading++)
        {
            foreach (Position tile in StoneAim.Ray(
                         lens,
                         from,
                         (ProjectileHeading)heading,
                         gun.Projectile.MaxTravelTiles,
                         gun.Projectile.DiagonalCornersMustBeClear))
            {
                if (tile == to)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// A windup retains the source form, permits nothing but Wait, and is
    /// cancelled by lethal damage. So the question is not "is this a good idea"
    /// but "am I still alive at the end of it": count the damage every armed
    /// enemy that bears on this tile can land in that many ticks, from its own
    /// declared cadence and damage, and refuse the commitment if it kills us.
    /// </summary>
    private static bool SurvivesWindup(
        StoneContract lens,
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route)
    {
        int window = Math.Max(route.Windup.DurationTicks, 1);
        int incoming = StoneGround.DamageWithin(
            StoneGround.Inbound(lens, context, context.Self.Position),
            window);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? gun =
                lens.Attack(enemy.FormId);
            if (gun is null)
                continue;
            int distance =
                context.Self.Position.ChebyshevDistance(enemy.Position);
            if (distance > gun.Projectile.MaxTravelTiles)
                continue;
            int shots = 1 + window / Math.Max(gun.CooldownTicks, 1);
            incoming += shots * gun.Projectile.DamagePerHit;
        }
        return context.Self.Health > incoming;
    }

    /// <summary>
    /// How many shots a turret spends per landed hit on a body that is trying not
    /// to be hit. Two is the conservative read and it is deliberately pessimistic:
    /// it makes the gate refuse marginal anchors rather than chase them.
    /// </summary>
    private const int MissAllowance = 2;

    /// <summary>
    /// Walk home for a companion only when the front is quiet and we are alone —
    /// an extra body is worth a detour, an abandoned objective is not.
    /// </summary>
    private GenericActorDecision? FabricationErrand(
        StoneContract lens,
        GenericActorContext context,
        StoneGround.Push push)
    {
        if (push.EnemyWeight > 0 || !context.Allies.IsEmpty)
            return null;
        bool ready = false;
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.State is GenericActorContext.UnitSlotState.Ready)
                ready = true;
        }
        if (!ready)
            return null;

        Position[] source = lens.FabricationSourceTiles(
            context.Self.FormId,
            _participantId,
            context.Self.ActorId.UnitId);
        if (source.Length == 0 || Contains(source, context.Self.Position))
            return null;
        return StoneGround.Step(lens, context, _memory, source, null);
    }

    private static int Coverage(
        StoneContract lens,
        GenericActorContext context,
        Position[] objective,
        string? formId = null)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(formId ?? context.Self.FormId);
        if (attack is null)
            return 0;
        int covered = 0;
        for (int heading = 0; heading < 8; heading++)
        {
            Position[] ray = StoneAim.Ray(
                lens,
                context.Self.Position,
                (ProjectileHeading)heading,
                attack.Projectile.MaxTravelTiles,
                attack.Projectile.DiagonalCornersMustBeClear);
            foreach (Position tile in ray)
            {
                if (Contains(objective, tile))
                    covered++;
            }
        }
        return covered;
    }

    /// <summary>
    /// Where a parked body should look. First choice is the facing its gun can
    /// actually reach a body from — with the aim offset pinned to zero, the turn
    /// IS the aim — then the nearest enemy's bearing, then the advance.
    /// </summary>
    private static Direction CoverFacing(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory)
    {
        Direction forward =
            ArenaBasics.AdvanceDirection(lens.Raw, context)
            ?? context.Self.Facing;
        int here = StoneAim.Reach(lens, context, memory, context.Self.Facing);
        Direction? aimed = null;
        int bestReach = here;
        foreach (Direction candidate in StoneContract.AllCardinals)
        {
            if (candidate == context.Self.Facing)
                continue;
            int reach = StoneAim.Reach(lens, context, memory, candidate);
            if (reach > bestReach + 10)
            {
                bestReach = reach;
                aimed = candidate;
            }
        }
        if (aimed is Direction turn)
            return turn;

        GenericActorContext.ObservedEnemyState? nearest = null;
        int best = int.MaxValue;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            int distance =
                context.Self.Position.ChebyshevDistance(enemy.Position);
            if (distance < best)
            {
                best = distance;
                nearest = enemy;
            }
        }
        if (nearest is null)
            return forward;
        int dx = nearest.Position.X - context.Self.Position.X;
        int dy = nearest.Position.Y - context.Self.Position.Y;
        if (Math.Abs(dx) == Math.Abs(dy))
            return forward;
        return Math.Abs(dx) > Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
    }

    private static GenericActorDecision? Transform(
        StoneContract lens,
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route,
        string reason)
    {
        foreach (GenericActorActionLegality legality in context.ActionLegalities)
        {
            if (!legality.Available
                || !string.Equals(
                    legality.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
                forms = null;
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in legality.Constraints)
            {
                if (constraint
                    is GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint candidate)
                {
                    forms = candidate;
                }
            }
            if (forms is null)
            {
                return GenericActorDecision.WithoutArguments(
                    legality.ActionId,
                    legality.ActionCode,
                    reason);
            }
            if (forms.AllowedFormIds.Contains(route.TargetFormId))
            {
                return new GenericActorDecision(
                    legality.ActionId,
                    legality.ActionCode,
                    [
                        new GenericActorActionArgument.FormTargetArgument(
                            route.TargetFormId),
                    ],
                    reason);
            }
        }
        return null;
    }

    /// <summary>
    /// Records a tile that refused a move. The authoritative outcome is the only
    /// evidence some obstructions ever produce.
    /// </summary>
    private void NoteRefusedStep(GenericActorContext context)
    {
        if (context.Self.PreviousActionResolution
            is not
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous)
        {
            return;
        }
        foreach (GenericActorActionArgument argument
                 in previous.AcceptedAction.Arguments)
        {
            if (argument
                is GenericActorActionArgument.DirectionArgument direction)
            {
                (int dx, int dy) = direction.Value.Vector();
                _memory.NoteRefused(context.Self.Position.Offset(dx, dy));
            }
        }
    }

    /// <summary>
    /// Whether a step keeps this body on the ground its station covers — the
    /// difference between dodging and conceding.
    /// </summary>
    private static bool EscapeKeepsStation(
        GenericActorDecision escape,
        GenericActorContext context,
        StoneGround.Station station)
    {
        foreach (GenericActorActionArgument argument in escape.Arguments)
        {
            if (argument
                is not GenericActorActionArgument.DirectionArgument direction)
            {
                continue;
            }
            (int dx, int dy) = direction.Value.Vector();
            return Contains(
                station.Tiles,
                context.Self.Position.Offset(dx, dy));
        }
        return false;
    }

    private static bool Contains(Position[] tiles, Position tile)
    {
        foreach (Position candidate in tiles)
        {
            if (candidate == tile)
                return true;
        }
        return false;
    }
}
