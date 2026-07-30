using BotArena.Sdk;

/// <summary>
/// The crew layer: what GateStone's OWN bodies cost each other.
///
/// <para>Wave 5's ledger priced enemy bodies exactly and own bodies not at all.
/// It knew what a kill was worth, what a tick of objective weight was worth, and
/// what a windup cost — and it had no number at all for the tick a relief body
/// spends walking around its own gate. That is the gap this file closes: a
/// sibling's TRANSIT is priced in the same unit as everything else, ticks, and
/// the tiles that transit needs are reserved out of every other body's plan.</para>
///
/// <para>The whole layer rests on one contract fact and one consequence of it.
/// The fact: <c>one submitted artifact controls all of a participant's body
/// lives</c>, and team perception publishes each sibling's exact position,
/// facing, form, health and windup. The consequence: a sibling's plan is
/// DERIVABLE — every body can run the same assignment over the same shared
/// observation and get the same answer, so the team coordinates without a
/// message, a shared memory, or a leader. Observations are frozen before any
/// same-tick decision executes and private memory is life-scoped, so the
/// derivation is an approximation of a sibling's plan, never a promise of it.
/// Every rule below is therefore written to degrade into ordinary traffic rather
/// than into a deadlock: a yield is a step, never a permanent refusal, and a
/// reservation only ever removes a tile from a route that has an alternative.</para>
/// </summary>
internal static class StoneCrew
{
    /// <summary>
    /// C3 — THE TRANSIT LEDGER: tiles a higher-precedence sibling's route needs
    /// this tick or next are reserved, and a body standing on one yields.
    /// </summary>
    public static readonly bool TransitLedger = true;

    /// <summary>
    /// C4 — THE CHOKE PRECEDENCE RULE: a sibling entering a one-tile corridor
    /// reserves the WHOLE corridor, not just its next two tiles, because a
    /// corridor is the one place a collision cannot be resolved by stepping
    /// aside locally.
    /// </summary>
    public static readonly bool CorridorRun = true;

    /// <summary>
    /// C5 — RALLY LANE: the tile our next automatic arrival will take is read
    /// from the declared placement policy and left free while it is due.
    /// </summary>
    public static readonly bool RallyLane = true;

    /// <summary>How far ahead a sibling's route is reserved: this tick and next.</summary>
    private const int ReserveDepth = 2;

    /// <summary>
    /// Whether a body cannot vacate its tile this tick — no movement action in
    /// its form, or wait-only inside a windup. Both are contract reads: the form
    /// catalog says what a form may do, and a pending same-life transition is
    /// published on the body. Unconditional, because "this body is not going
    /// anywhere" is a fact and not a policy: the ledger must not reserve transit
    /// for a turret whatever the routing rule decides to do about it.
    /// </summary>
    public static bool Stuck(
        StoneContract lens,
        string formId,
        GenericActorContext.PendingSameLifeTransition? pending) =>
        pending is not null || lens.Immobile(formId);

    /// <summary>The crew's decision for THIS body.</summary>
    /// <param name="Mine">The station this body was assigned by the muster.</param>
    /// <param name="Reserved">
    /// Tiles a higher-precedence sibling's route needs now or next tick, plus
    /// any due arrival's rally tile. Impassable for this body's own route.
    /// </param>
    /// <param name="Yield">
    /// A step off ground this body owes a sibling, or null when it owes none.
    /// </param>
    public sealed record Crew(
        StoneGround.Station Mine,
        HashSet<Position> Reserved,
        GenericActorDecision? Yield);

    /// <summary>Self plus every visible sibling, as one roster.</summary>
    public static List<StoneGround.Body> Roster(GenericActorContext context)
    {
        var roster = new List<StoneGround.Body> { StoneGround.Own(context) };
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            roster.Add(StoneGround.Sibling(ally));
        return roster;
    }

    /// <summary>
    /// THE MUSTER ORDER — the written rule that says who yields, and the only
    /// tie-break anywhere in this file. Deterministic and computed from shared
    /// observable state alone, so every body of ours derives the same order:
    ///
    /// <list type="number">
    /// <item>a body that can CAPTURE outranks one that cannot — objective
    /// weight zero means a turret contributes nothing to the only score that
    /// exists, so it never displaces a body that does;</item>
    /// <item>then the body NEARER the active objective, because it arrives
    /// sooner and its claim is the one that starts paying first;</item>
    /// <item>then ascending actor identity, which is total and stable.</item>
    /// </list>
    ///
    /// <para>Precedence decides who reserves transit and who yields it. It does
    /// NOT decide a doorway: there a moving body outranks a resting one whatever
    /// this order says, unless the resting body is on its assigned station or the
    /// corridor is denying an enemy (see <see cref="Doorway"/>).</para>
    /// </summary>
    public static void Order(
        StoneContract lens,
        GenericActorContext context,
        List<StoneGround.Body> roster)
    {
        Position[] objective = StoneAim.ActiveObjective(lens, context);
        roster.Sort((left, right) =>
        {
            int leftRank = lens.Weight(left.FormId) > 0 ? 0 : 1;
            int rightRank = lens.Weight(right.FormId) > 0 ? 0 : 1;
            if (leftRank != rightRank)
                return leftRank.CompareTo(rightRank);
            int leftNear = Nearest(left.Position, objective);
            int rightNear = Nearest(right.Position, objective);
            return leftNear != rightNear
                ? leftNear.CompareTo(rightNear)
                : left.ActorId.CompareTo(right.ActorId);
        });
    }

    private static int Nearest(Position from, Position[] tiles)
    {
        int best = int.MaxValue;
        foreach (Position tile in tiles)
            best = Math.Min(best, from.ChebyshevDistance(tile));
        return best == int.MaxValue ? 0 : best;
    }

    /// <summary>
    /// Assigns stations to the whole crew in muster order and returns this
    /// body's row, together with the transit every higher-precedence sibling is
    /// about to spend.
    /// </summary>
    public static Crew Assemble(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        StoneGround.Push push)
    {
        List<StoneGround.Body> roster = Roster(context);
        Order(lens, context, roster);

        var reserved = new HashSet<Position>();
        StoneGround.Station? mine = null;
        string blocking = string.Empty;

        // The rally lane is not owed to a body that exists; it is owed to one
        // that is about to. It binds for everybody, so it is reserved before the
        // first station is handed out.
        if (RallyLane && ArrivalDue(lens, context) is Position rally)
        {
            reserved.Add(rally);
            blocking = "rally lane";
        }

        foreach (StoneGround.Body who in roster)
        {
            // Priced for THAT body, not for this one — see StoneGround.Price.
            StoneGround.Station station = StoneGround.Choose(
                lens,
                context,
                who.IsSelf ? push : StoneGround.Price(lens, context, who),
                who,
                roster);
            if (who.IsSelf)
            {
                mine = station;
                break;
            }

            // Price this sibling's transit. Its route is derived with the same
            // planner and NO private memory, because life-scoped memory is
            // exactly the part of its plan we cannot see.
            if (!TransitLedger || Stuck(lens, who.FormId, null))
                continue;
            StoneGround.Path? path = StoneGround.Plan(
                lens,
                context,
                null,
                who,
                new HashSet<Position>(station.Tiles),
                null,
                reserved);
            if (path is null)
                continue;
            int depth = Math.Min(ReserveDepth, path.Tiles.Length);
            // THE CHOKE PRECEDENCE RULE. Inside a one-tile corridor there is no
            // adjacent tile to resolve a meeting on, so a sibling that is
            // entering one is treated as committed to all of it: the reservation
            // runs on while it is walking corridor tiles. Precedence is the
            // muster order, unchanged — the corridor does not get a rule of its
            // own about WHO goes, only about HOW MUCH ground the decision covers.
            //
            // Measured on frontline-labs-01 this clause is EXACTLY neutral: every
            // one of the twelve games is identical with and without it, in every
            // outcome and every congestion column. That is not an accident and it
            // is why the clause stays. No corridor on this map is longer than two
            // tiles — the runs are (8,7)(9,7), (13,7)(14,7), (11,2)(11,3),
            // (11,11)(11,12), plus the singletons (5,7) and (17,7) — so a
            // two-tick reservation ALREADY covers a whole corridor, and the rule
            // and the default agree. On a map whose corridors are longer they stop
            // agreeing, and suite 5's holdout runs one (thin-fronts). A clause
            // that is free here and load-bearing there is worth its four lines.
            while (depth < path.Tiles.Length
                   && CorridorRun
                   && lens.IsChoke(path.Tiles[depth - 1]))
            {
                depth++;
            }
            for (int index = 0; index < depth; index++)
            {
                reserved.Add(path.Tiles[index]);
                blocking = $"transit of {who.ActorId}";
            }
        }

        StoneGround.Station assigned = mine
            ?? new StoneGround.Station([context.Self.Position], false, "unmustered");
        GenericActorDecision? yield = null;
        if (TransitLedger && reserved.Contains(context.Self.Position))
        {
            yield = StepAside(
                lens,
                context,
                memory,
                reserved,
                assigned,
                $"yielding {context.Self.Position} — {blocking}");
        }
        return new Crew(assigned, reserved, yield);
    }

    // THE DOORWAY YIELD — BUILT, MEASURED, NOT SHIPPED.
    //
    // A body at rest steps out of a one-tile corridor a sibling's route needs.
    // This is the single most obvious answer to the behaviour that opened this
    // wave, it was implemented twice (blanket, then with the detour priced by
    // planning the sibling's route with the tile free and with it walled, plus
    // exceptions for an assigned station and for a corridor that is denying an
    // enemy), and BOTH versions are absent from the artifact because both fail
    // their own test.
    //
    // The priced version fired 4 times in twelve games and changed NOTHING: the
    // aggregate, the same-side subtotal, and all four congestion columns are
    // byte-identical with the yield enabled and disabled. The apparent effect of
    // "the doorway rule" turned out to belong entirely to a different line — a
    // -30 station penalty on shoulder tiles that are corridors — and that line,
    // measured on its own, cost 26 points of aggregate progress AND made the
    // owner-visible silliness WORSE by the direct measure: ticks in which a body
    // of ours stood on a corridor tile that lengthened a sibling's route went
    // from 21 without it to 33 with it, and the detour those ticks charged the
    // team went from 61 to 79.
    //
    // The reason is the useful part. The transit ledger fixes corridors by
    // ROUTING — a reserved tile is simply not in the graph, so the second body
    // never walks into the first and never queues behind it. Adding a yield on
    // top makes the FIRST body move as well, and two bodies shuffling around one
    // doorway occupy more corridor tiles between them than one body standing in
    // it. The yield is the intuitive fix and the reservation is the real one.

    /// <summary>
    /// A step off ground this body owes somebody. Prefers a tile that keeps the
    /// assigned station, refuses a tile that is itself reserved or a doorway, and
    /// refuses to step into fire — a yield that trades a tick of traffic for a
    /// point of health is not a yield, it is a mistake with good manners.
    /// </summary>
    private static GenericActorDecision? StepAside(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        IReadOnlySet<Position> reserved,
        StoneGround.Station mine,
        string reason)
    {
        GenericActorActionLegality? move = null;
        foreach (GenericActorActionLegality legality in context.ActionLegalities)
        {
            if (legality.Available && lens.IsMovement(legality.ActionId))
                move = legality;
        }
        if (move is null)
            return null;
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = null;
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in move.Constraints)
        {
            if (constraint
                is GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint candidate)
            {
                directions = candidate;
            }
        }
        if (directions is null)
            return null;

        var occupied = new HashSet<Position>();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            occupied.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            occupied.Add(enemy.Position);

        Direction? best = null;
        int bestScore = int.MinValue;
        foreach (Direction direction in directions.AllowedValues)
        {
            (int dx, int dy) = direction.Vector();
            Position tile = context.Self.Position.Offset(dx, dy);
            if (lens.IsWall(tile)
                || occupied.Contains(tile)
                || reserved.Contains(tile)
                || memory.Refused(tile))
            {
                continue;
            }
            if (StoneGround.DamageWithin(
                    StoneGround.Inbound(lens, context, tile),
                    2) > 0)
            {
                continue;
            }
            int score = 0;
            foreach (Position want in mine.Tiles)
            {
                if (want == tile)
                {
                    score += 40;
                    break;
                }
            }
            if (lens.IsChoke(tile))
                score -= 25;
            score -= Nearest(tile, StoneAim.ActiveObjective(lens, context)) * 2;
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        return best is Direction chosen
            ? new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(chosen)],
                reason)
            : null;
    }

    /// <summary>
    /// The tile our next automatic arrival will take, or null when none is due.
    ///
    /// <para>Both halves are contract reads, and neither is guessable. The
    /// placement policy is declared on the lifecycle
    /// (<c>automaticReturnPlacement</c>): on this arm an arrival appears on the
    /// chain-adjacent objective region on our OWN side, in our own advance order,
    /// falling back to the assigned spawn — so the tile is derived from the
    /// objective chain and our advance delta, never from the team ID. The clock
    /// is the slot's own published due tick.</para>
    ///
    /// <para>Standing on it is not illegal; it is expensive. The placement takes
    /// the first FREE tile, so a body of ours parked there pushes its own
    /// reinforcement to worse ground — or, at the end of the region, all the way
    /// home. This is rule (3) of the coordination bar: do not rally into your own
    /// traffic where the contract gives you placement influence.</para>
    /// </summary>
    private static Position? ArrivalDue(
        StoneContract lens,
        GenericActorContext context)
    {
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return null;
        }
        bool due = false;
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.TeamId != lens.TeamId)
                continue;
            int? when = slot.State switch
            {
                GenericActorContext.UnitSlotState.AutomaticReturnPending pending
                    => pending.DueTick,
                GenericActorContext.UnitSlotState.AvailabilityPending unlock
                    => unlock.DueTick,
                GenericActorContext.UnitSlotState.LifecyclePending queued
                    => queued.DueTick,
                _ => null,
            };
            if (when is int tick && tick - context.Tick is >= 0 and <= 2)
                due = true;
        }
        if (!due)
            return null;

        int delta = lens.AdvanceDelta(lens.TeamId);
        Position[] region = lens.Objective(mode.ActivePositionIndex - delta);
        if (region.Length == 0)
            return null;

        // Only a body that CANNOT vacate takes a tile out of the running. A body
        // of ours that can step off is not an obstruction, it is a decision — and
        // reserving the tile after the one we are plugging (which is what
        // counting ourselves as occupied does) reserves ground nobody wants and
        // asks nobody to move.
        var occupied = new HashSet<Position>();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (Stuck(lens, ally.FormId, ally.PendingSameLifeTransition))
                occupied.Add(ally.Position);
        }
        if (Stuck(
                lens,
                context.Self.FormId,
                context.Self.PendingSameLifeTransition))
        {
            occupied.Add(context.Self.Position);
        }
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            occupied.Add(enemy.Position);

        // Our own advance order: rear-most first along our advance direction,
        // then by the map's own row-major order within that rank.
        Position[] ordered = region.ToArray();
        Direction? advance = lens.AdvanceFacing();
        Array.Sort(ordered, (left, right) =>
        {
            int leftRank = Projection(left, advance);
            int rightRank = Projection(right, advance);
            if (leftRank != rightRank)
                return leftRank.CompareTo(rightRank);
            return left.Y != right.Y
                ? left.Y.CompareTo(right.Y)
                : left.X.CompareTo(right.X);
        });
        foreach (Position tile in ordered)
        {
            if (!occupied.Contains(tile))
                return tile;
        }
        // Every tile is held by something that will not move: the arrival falls
        // through to its assigned spawn, and no step of ours can change that.
        return null;
    }

    private static int Projection(Position tile, Direction? advance)
    {
        if (advance is not Direction forward)
            return 0;
        (int dx, int dy) = forward.Vector();
        return tile.X * dx + tile.Y * dy;
    }

}
