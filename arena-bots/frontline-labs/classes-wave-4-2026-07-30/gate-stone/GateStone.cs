using BotArena.Sdk;

/// <summary>
/// GateStone — a bulwark built around the shield rather than fitted with one.
///
/// <para>The doctrine starts from three contract facts and never leaves them.
/// (1) Under a facing-locked movement profile a body can only step where it
/// looks, so a sidestep is not an evasion — it is a two-tick turn. (2) The aegis
/// arc turns a bolt arriving inside the facing quadrant into a live bolt flying
/// back at whoever fired it, and it keeps objective weight while it does. (3)
/// Weight-scaled control pays a second body for standing on the objective, and a
/// live hold makes a capture completed inside it worthless. So GateStone chooses
/// its facing as a field of fire, parries instead of conceding ground, prices
/// every push against the published hold clock, and spends surplus bodies on
/// bearings the holder does not already cover.</para>
///
/// <para>Nothing here is arm-specific: the guard route, the fortify route, the
/// bend envelope, the capture policy and the movement coupling are all read from
/// the resolved contract, so the same artifact plays kit-off, kit-on, bend-off,
/// bend-on, and the classless qualification profile.</para>
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
    /// In the arc. The shield is a STANCE, not a parry — measured, not assumed:
    /// a bulwark sees four tiles and a bolt crosses two per tick after a one-tile
    /// launch, so every bolt fired from inside our own vision is already one tick
    /// from contact, and a windup-1 entry completes only after combat. Over 600
    /// mirrored decisions, every single inbound bolt on stance-legal ground
    /// arrived with exactly one tick to live. Reacting is therefore impossible;
    /// the shield is worth something only when it is already up. So this stance
    /// is held while an enemy stands in the protected quadrant and our own gun is
    /// cold, and dropped the tick the gun comes back or the threat walks around.
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

        // The gun is the damage; the shield only buys the ticks the gun cannot
        // use. The moment the cooldown clears with a body in reach, come out.
        bool gunReady = context.Self.Cooldown == 0;
        if (gunReady && Threatening(lens, context, arcOnly: false) && drop is not null)
            return drop;
        if (Threatening(lens, context, arcOnly: true))
            return ArenaBasics.Wait(context, "arc up, body in the quadrant");
        return drop ?? ArenaBasics.Wait(context, "arc up, nothing in reach");
    }

    /// <summary>
    /// Whether an enemy is close enough to shoot us — optionally only from
    /// inside the quadrant this facing protects. The range comes from the
    /// enemy's own declared profile, not from ours.
    /// </summary>
    private static bool Threatening(
        StoneContract lens,
        GenericActorContext context,
        bool arcOnly)
    {
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
            if (!arcOnly)
                return true;
            // A bolt from that bearing arrives on the reverse of it.
            ProjectileHeading inbound = StoneAim.Toward(
                enemy.Position,
                context.Self.Position);
            if (StoneContract.ArcCovers(context.Self.Facing, inbound))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Fortified: objective weight zero, so this body has traded its scoring
    /// presence for a faster gun and eight headings. It gives the trade back the
    /// moment the objective needs a body on it.
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

        bool presenceNeeded = push.EnemyWeight > 0 && allies == 0;
        bool coversGate = Coverage(lens, context, objective) > 0;
        if ((presenceNeeded || !coversGate) && exit is not null)
        {
            GenericActorDecision? mobilize = Transform(
                lens,
                context,
                exit,
                presenceNeeded
                    ? "the gate needs a body, not a gun"
                    : "the front moved out of my arc");
            if (mobilize is not null)
                return mobilize;
        }

        StoneAim.Shot? shot = StoneAim.Best(lens, context, _memory, 20);
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
        // Every objective tile on this map forbids transition placement, so the
        // shield is legal on the shoulder beside the gate and nowhere on it.
        bool stanceLegalHere = guard is not null
            && lens.RouteAllowedOn(guard, context.Self.Position);
        GenericActorDecision? raise = stanceLegalHere && guard is not null
            ? Transform(lens, context, guard, "raising the arc")
            : null;
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

        // 3. The stance, taken BEFORE the shot rather than against it. A gun on
        //    cooldown three is idle two ticks in three; those are the ticks the
        //    shield costs nothing, and a body that pokes a raised arc shoots
        //    itself. Ground we mean to hold, an enemy in the protected quadrant,
        //    and a cold gun is the whole condition.
        bool gunCold = context.Self.Cooldown > 0 || shot is null;
        if (raise is not null
            && onStation
            && gunCold
            && Threatening(lens, context, arcOnly: true))
        {
            return raise;
        }
        // 3b. Turn into the quadrant first: the arc is chosen before the shield
        //     rises and a shell cannot rotate, so facing is the whole decision.
        if (raise is not null
            && onStation
            && gunCold
            && Threatening(lens, context, arcOnly: false))
        {
            Direction toward = CoverFacing(lens, context, _memory);
            GenericActorDecision? turn = StoneGround.Turn(
                lens,
                context,
                toward,
                $"turning {toward} to put the arc on them");
            if (turn is not null)
                return turn;
        }

        // 4. Ordinary suppression.
        if (shot is not null)
            return shot.Decision;

        // 5. Trade presence for a gun only when presence is already paid for.
        GenericActorDecision? fortify = Fortify(lens, context, push);
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
    /// Anchor, but only on the terms the class actually offers: a cheap windup,
    /// a tile that covers the gate, and someone else already standing on it.
    /// </summary>
    private static GenericActorDecision? Fortify(
        StoneContract lens,
        GenericActorContext context,
        StoneGround.Push push)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.FortifyRoute(context.Self.FormId);
        if (route is null || route.Windup.DurationTicks > 1)
            return null;

        Position[] objective = StoneAim.ActiveObjective(lens, context);
        int allies =
            StoneGround.OwnWeightExcludingSelf(lens, context, objective);
        // The turret bargain, priced: objective weight zero means fortifying
        // subtracts our own capture pressure, and under weight-scaled control
        // that is not a rounding error — it is the difference between gaining a
        // point a tick and gaining nothing. Measured over 6 mirrored games,
        // anchoring behind a SINGLE remaining body turned a 6-0 into a 1-5,
        // because the body it relied on died and the gun could not follow the
        // front. So the gate is two: enough presence to survive one death.
        if (push.EnemyWeight > 0 && allies < 2)
            return null;
        if (allies < 2)
        {
            // ...or ground the ratchet is already protecting, where the enemy's
            // presence buys nothing and a gun outvalues a body.
            bool ownHoldRuns = push.Hold is { Mine: true, RemainingTicks: > 12 };
            if (!ownHoldRuns)
                return null;
        }

        int covered = Coverage(lens, context, objective, route.TargetFormId);
        return covered >= 2
            ? Transform(
                lens,
                context,
                route,
                $"anchoring: {covered} gate tiles under fire")
            : null;
    }

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
