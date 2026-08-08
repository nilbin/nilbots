using BotArena.Sdk;

/// <summary>
/// Movement under a coupling that may make facing and travel the same decision.
/// Routes are planned on map geometry and the rotation that unlocks the planned
/// step is spent explicitly, because the movement legality mask under a
/// facing-locked coupling offers only the current facing: a search seeded from
/// the mask does not make a bot slower, it makes it immobile.
/// </summary>
internal static class ArcMove
{
    /// <summary>
    /// One step toward <paramref name="goals"/>, or the rotation that unlocks
    /// it. Declines when the step would walk into a bolt while standing still is
    /// safe — under a facing-locked coupling that refusal is often the only
    /// evasion available.
    /// </summary>
    public static GenericActorDecision? Toward(
        ArcFacts facts,
        GenericActorContext context,
        ArcThreat threat,
        ArcTraffic traffic,
        IReadOnlyCollection<Position> goals,
        IReadOnlySet<Position> blocked,
        Direction[] order)
    {
        var goalSet = goals.ToHashSet();
        if (goalSet.Count == 0 || goalSet.Contains(context.Self.Position))
            return null;

        GenericActorActionLegality? move = Legality(
            facts,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || allowed is null)
            return null;

        var firstStepBlocked = blocked.ToHashSet();
        if (context.Self.PreviousActionResolution
                is { Outcome: GenericActorActionResolution.ActionOutcome.Blocked }
                previous
            && previous.AcceptedAction.Arguments
                    .OfType<GenericActorActionArgument.DirectionArgument>()
                    .SingleOrDefault()
                is { } stalled)
        {
            firstStepBlocked.Add(ArcBoard.Step(context.Self.Position, stalled.Value));
        }

        // C1/C2. A tile a stronger sibling's committed route needs next tick is
        // a first-step blocker, not a veto applied after the fact: the engine
        // resolves a same-destination claim by blocking BOTH bodies, so the tick
        // is only saved if the route search never proposed the collision. Wave 5
        // walked into this 27.9 times a match and re-walked into it 23.7 of
        // those times, which is the "silly decision" this wave was opened for.
        traffic.AddRefusedFirstSteps(context.Self.Position, firstStepBlocked);

        // ROUTE COMMITMENT. Under a facing-locked coupling a change of plan costs
        // a whole tick, so a router that re-derives its heading from scratch every
        // tick spends more ticks turning than walking — measured at 209 rotations
        // in 500 ticks against 247 steps, which is a third of the match paid to
        // tie-breaking. If the step this body is ALREADY facing lies on a shortest
        // route, take it: the plan is only re-opened when the current heading has
        // stopped being one of the right answers.
        Position ahead = ArcBoard.Step(context.Self.Position, context.Self.Facing);
        int hereDistance =
            ArcBoard.StepDistance(facts, context.Self.Position, goals, 24)
            ?? int.MaxValue;
        bool aheadCloses = !facts.Impassable(ahead)
            && !firstStepBlocked.Contains(ahead)
            && hereDistance != int.MaxValue
            && (goalSet.Contains(ahead)
                || (ArcBoard.StepDistance(facts, ahead, goals, 24)
                    ?? int.MaxValue) < hereDistance);

        Direction? planned = aheadCloses
            ? context.Self.Facing
            : ArcBoard.FirstStep(
                facts,
                context.Self.Position,
                goalSet,
                firstStepBlocked,
                order);
        if (planned is not Direction direction)
            return null;

        if (allowed.AllowedValues.Contains(direction))
        {
            Position destination = ArcBoard.Step(context.Self.Position, direction);
            bool hot = threat.Threatened(destination, 1);
            if (hot)
            {
                // Prefer a different step that closes just as much and is not
                // about to be occupied by a bolt. Suppression is not a reason to
                // concede ground, so if no such step exists this body walks into
                // the shot anyway — unless the shot kills it.
                int here = ArcBoard.StepDistance(facts, context.Self.Position, goals, 24)
                    ?? int.MaxValue;
                Direction? detour = null;
                int detourDistance = here;
                foreach (Direction candidate in allowed.AllowedValues)
                {
                    Position tile = ArcBoard.Step(context.Self.Position, candidate);
                    if (facts.Impassable(tile)
                        || firstStepBlocked.Contains(tile)
                        || threat.Threatened(tile, 1))
                    {
                        continue;
                    }
                    int distance = ArcBoard.StepDistance(facts, tile, goals, 24)
                        ?? int.MaxValue;
                    if (distance < detourDistance)
                    {
                        detourDistance = distance;
                        detour = candidate;
                    }
                }
                if (detour is Direction sidestep)
                {
                    return new GenericActorDecision(
                        move.ActionId,
                        move.ActionCode,
                        [new GenericActorActionArgument.DirectionArgument(sidestep)],
                        $"advancing {sidestep} around a bolt");
                }
                if (threat.Incoming(destination) is { } arriving
                    && arriving.Damage >= context.Self.Health)
                {
                    return null;
                }
            }
            return new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"advancing {direction}");
        }

        return Rotate(facts, context, direction, "unlocking the next step");
    }

    /// <summary>
    /// Leave a tile a bolt is about to reach. Under a facing-locked coupling the
    /// only legal step is the current facing, so the honest answer is often a
    /// rotation that buys the escape one tick later — which only helps when the
    /// bolt is more than one tick out.
    /// </summary>
    public static GenericActorDecision? Escape(
        ArcFacts facts,
        GenericActorContext context,
        ArcThreat threat,
        ArcTraffic traffic,
        IReadOnlyCollection<Position> goals,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position>? restrictTo = null)
    {
        if (threat.Incoming(context.Self.Position) is not { } incoming)
            return null;

        GenericActorActionLegality? move = Legality(
            facts,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();

        if (move is not null && allowed is not null)
        {
            Direction? best = null;
            int bestScore = int.MinValue;
            foreach (Direction direction in allowed.AllowedValues)
            {
                Position destination = ArcBoard.Step(
                    context.Self.Position,
                    direction);
                if (facts.Impassable(destination)
                    || blocked.Contains(destination)
                    || (restrictTo is not null
                        && !restrictTo.Contains(destination)))
                {
                    continue;
                }
                (int Ticks, int Damage)? arriving = threat.Incoming(destination);
                // Retreating along a bolt's own lane is a legitimate escape: the
                // bolt has finite remaining travel, so a tile it reaches in two
                // ticks is a tile this body will have left. Only an arrival on the
                // very next tick disqualifies a destination.
                if (arriving is not null && arriving.Value.Ticks <= 1)
                    continue;
                // C1 as a PENALTY, not a refusal: colliding with a sibling on an
                // escape tick leaves this body standing exactly where the bolt
                // is going, so the preference is strong — but a yield must never
                // be the reason a body had no exit at all.
                int score = (arriving is null ? 60 : arriving.Value.Ticks * 8)
                    - (ArcBoard.StepDistance(facts, destination, goals, 20) ?? 12)
                    - traffic.TravelPenalty(destination);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = direction;
                }
            }
            if (best is Direction escape)
            {
                return new GenericActorDecision(
                    move.ActionId,
                    move.ActionCode,
                    [new GenericActorActionArgument.DirectionArgument(escape)],
                    $"stepping {escape} off an incoming bolt");
            }
        }

        if (incoming.Ticks < 2 || restrictTo is not null)
            return null;
        Direction? turn = null;
        int turnScore = int.MinValue;
        foreach (Direction direction in ArcBoard.Cardinals)
        {
            if (direction == context.Self.Facing)
                continue;
            Position destination = ArcBoard.Step(context.Self.Position, direction);
            if (facts.Impassable(destination))
                continue;
            (int Ticks, int Damage)? arriving = threat.Incoming(destination);
            if (arriving is not null && arriving.Value.Ticks <= 1)
                continue;
            int score = (arriving is null ? 60 : arriving.Value.Ticks * 8)
                - (ArcBoard.StepDistance(facts, destination, goals, 20) ?? 12);
            if (score > turnScore)
            {
                turnScore = score;
                turn = direction;
            }
        }
        return turn is Direction facing
            ? Rotate(facts, context, facing, "turning to open an escape lane")
            : null;
    }

    /// <summary>
    /// One step that unmasks a shot without giving up the ground.
    ///
    /// <para>This exists because of a contract fact that costs a duel twenty
    /// ticks if you miss it: in the class arms the mobile gun's declared initial
    /// aim range is zero, and a bend cannot start before the first tile — so a
    /// DIAGONALLY ADJACENT body is unhittable by an ordinary shot, and two bodies
    /// standing on the same objective cluster can stare at each other forever.
    /// One step inside the cluster turns the diagonal into a cardinal.</para>
    ///
    /// <para>Destinations are restricted to <paramref name="keep"/> so presence
    /// is never traded for aim, and are scored by the best lane the destination
    /// offers over any facing, discounted for the rotation that would cost.</para>
    /// </summary>
    public static GenericActorDecision? Unmask(
        ArcFacts facts,
        GenericActorContext context,
        ArcGun gun,
        ArcThreat threat,
        ArcTraffic traffic,
        IReadOnlySet<Position> keep,
        IReadOnlySet<Position> blocked)
    {
        GenericActorActionLegality? move = Legality(
            facts,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || allowed is null || context.Enemies.IsEmpty)
            return null;

        int here = gun.BestLaneValueFrom(
            context.Self.Position,
            context.Self.Facing,
            ticksFromNow: 0);
        Direction? best = null;
        int bestValue = here;
        foreach (Direction direction in allowed.AllowedValues)
        {
            Position destination = ArcBoard.Step(context.Self.Position, direction);
            if (!keep.Contains(destination)
                || facts.Impassable(destination)
                || blocked.Contains(destination)
                // A bolt three tiles out with two tiles of range left cannot
                // reach THIS tile and can reach the next one. Repositioning has
                // to price the destination, or the cure walks into the disease.
                || threat.Threatened(destination, 2)
                // C1/C2/C3: unmasking a lane is never urgent enough to take a
                // tile a sibling needs, to occupy a doorway, or to sit on the
                // tile the next arrival is due on.
                || !traffic.MayTravel(destination)
                || !traffic.RallyClear(destination, 2))
            {
                continue;
            }
            int value = gun.BestLaneValueFrom(
                destination,
                context.Self.Facing,
                ticksFromNow: 1);
            if (value > bestValue)
            {
                bestValue = value;
                best = direction;
            }
        }
        return best is Direction step
            ? new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(step)],
                $"stepping {step} to unmask a lane")
            : null;
    }

    /// <summary>
    /// The diagonal kite: keep moving while staying armed.
    ///
    /// <para>Under a facing-locked coupling, facing IS the direction of travel,
    /// so before the ±1 launch offsets existed a body that walked could only
    /// shoot along the line it walked, and moving away from a body meant
    /// disarming. With the offsets declared, a step carries three headings with
    /// it — the travel lane and both 45-degree lanes — and the aim-only diagonal
    /// costs no bend. That turns repositioning from a disarmed retreat into an
    /// armed one, which is the only way a three-slot chassis trades evenly with a
    /// swarm that can afford to walk into fire.</para>
    ///
    /// <para>So a kite step is scored by what the DESTINATION's lanes reach with
    /// the facing the step itself forces, minus what standing here costs. It is
    /// deliberately not an escape: <see cref="Escape"/> answers a bolt, this
    /// answers a bearing.</para>
    /// </summary>
    public static GenericActorDecision? Kite(
        ArcFacts facts,
        GenericActorContext context,
        ArcGun gun,
        ArcThreat threat,
        ArcTraffic traffic,
        IReadOnlySet<Position> hot,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position>? keep)
    {
        if (context.Enemies.IsEmpty)
            return null;
        GenericActorActionLegality? move = Legality(
            facts,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        if (move is null)
            return null;

        string form = context.Self.FormId;
        int here = gun.LaneValue(form, context.Self.Facing, context.Self.Position, 0)
            - (hot.Contains(context.Self.Position) ? 6 : 0)
            // C1: a kite is a discretionary step, so standing on a tile a
            // stronger sibling needs is the cheapest thing on the board to give
            // up. Charging the CURRENT pose for it is what turns the kite into
            // the step-aside.
            - (traffic.BlockingSibling ? 30 : 0);
        Direction? best = null;
        int bestScore = here;
        bool bestIsStep = false;
        foreach (Direction direction in ArcBoard.Cardinals)
        {
            Position destination = ArcBoard.Step(context.Self.Position, direction);
            if (facts.Impassable(destination)
                || blocked.Contains(destination)
                || (keep is not null && !keep.Contains(destination))
                || threat.Threatened(destination, 1)
                || !traffic.MayTravel(destination)
                || !traffic.RallyClear(destination, 2))
            {
                continue;
            }
            // The step forces the facing, so the destination's lanes are scored
            // from the direction travelled — that is the whole point.
            bool needsTurn = direction != context.Self.Facing;
            int score = gun.LaneValue(form, direction, destination, needsTurn ? 2 : 1)
                - (hot.Contains(destination) ? 6 : 0)
                - (needsTurn ? 3 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
                bestIsStep = !needsTurn;
            }
        }
        if (best is not Direction kite)
            return null;
        if (!bestIsStep)
            return Rotate(facts, context, kite, "turning into a kiting lane");

        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        return allowed is not null && allowed.AllowedValues.Contains(kite)
            ? new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(kite)],
                $"kiting {kite} with the diagonal still on")
            : null;
    }

    /// <summary>
    /// Get out of the way — the half of a precedence rule that the yielding body
    /// owes.
    ///
    /// <para>Refusing to step INTO a stronger sibling's tile stops the mutual
    /// block. It does not help when this body is already STANDING on the tile
    /// that sibling needs, or on the tile the next arrival is due to take: there
    /// the sibling's move blocks against a body, the arrival's placement moves on
    /// to the next tile, and nothing in the observation tells either of them why.
    /// So the yield is completed here, actively, on a tick this body had nothing
    /// better to do with.</para>
    ///
    /// <para>Presence is never traded for politeness: when this body's objective
    /// weight is load-bearing the step is restricted to other tiles of the same
    /// objective, and when no such tile exists the body stays and the sibling
    /// routes around it — which it now can, because the same traffic model tells
    /// it this tile is taken.</para>
    /// </summary>
    public static GenericActorDecision? StepAside(
        ArcFacts facts,
        GenericActorContext context,
        ArcThreat threat,
        ArcTraffic traffic,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position>? keep)
    {
        bool inTheWay = traffic.BlockingSibling
            || !traffic.RallyClear(context.Self.Position, 2);
        if (!inTheWay)
            return null;

        GenericActorActionLegality? move = Legality(
            facts,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || allowed is null)
            return null;

        foreach (Direction direction in allowed.AllowedValues)
        {
            Position destination = ArcBoard.Step(context.Self.Position, direction);
            if (facts.Impassable(destination)
                || blocked.Contains(destination)
                || (keep is not null && !keep.Contains(destination))
                || threat.Threatened(destination, 1)
                || !traffic.MayTravel(destination)
                || !traffic.RallyClear(destination, 2))
            {
                continue;
            }
            return new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"clearing {direction} out of a sibling's way");
        }
        return null;
    }

    /// <summary>An absolute rotation, when the mask offers that heading.</summary>
    public static GenericActorDecision? Rotate(
        ArcFacts facts,
        GenericActorContext context,
        Direction facing,
        string reason)
    {
        if (context.Self.Facing == facing)
            return null;
        GenericActorActionLegality? rotate = Legality(
            facts,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            headings = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (rotate is null
            || headings is null
            || !headings.AllowedValues.Contains(facing))
        {
            return null;
        }
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(facing)],
            $"{reason} ({facing})");
    }

    private static GenericActorActionLegality? Legality(
        ArcFacts facts,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> ids = facts.Contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
