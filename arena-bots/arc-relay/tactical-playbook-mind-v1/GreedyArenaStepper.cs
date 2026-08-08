using BotArena.Sdk;

/// <summary>
/// The historical stepper, lifted out of the movement wrappers unchanged:
/// every body chooses its own next tile, in the executor's body order,
/// against the tiles earlier bodies have already claimed this tick. It is
/// greedy in the exact sense that matters — nobody plans past one step and
/// nobody plans for anyone else — and it is the default so a sheet's
/// measured history keeps meaning what it meant.
/// </summary>
internal sealed class GreedyArenaStepper : IArenaStepper
{
    public bool WantsFightPrecedence => false;

    public void BeginTick(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
    }

    public void NoteCommitted(MindBody body, Position destination)
    {
    }

    public Position? Step(StepRequest request) => request.Intent switch
    {
        StepIntent.Toward => Toward(request),
        StepIntent.Homeward => Homeward(request),
        StepIntent.Routed => Routed(request),
        StepIntent.Away => Away(request),
        StepIntent.Aside => Aside(request),
        StepIntent.MakeWay => MakeWay(request),
        StepIntent.Direct => Direct(request),
        _ => null,
    };

    /// <summary>
    /// Breadth-first over the terrain step relation, expanded in the body's
    /// preferred heading order, refusing tiles that are blocked right now.
    /// </summary>
    private static Position? Toward(StepRequest request)
    {
        MindBody body = request.Body;
        if (request.Positions.Count == 0
            || request.Positions.Contains(body.Position))
        {
            return null;
        }

        HashSet<Position> blocked = ArenaBasics.BlockedNow(
            request.Contract, request.Mind, body, request.Claims,
            out HashSet<Position> queued);
        HashSet<Position> goals = request.Positions.ToHashSet();
        ProjectileHeading[] preference = ArenaBasics.PreferredHeadings(
            body,
            request.Positions,
            ArenaBasics.MirroredFrame(request.Contract, request.Mind));
        ProjectileHeading? desired = ArenaBasics.FindFirstStep(
            request.Contract.Map,
            body.Position,
            goals,
            blocked,
            ArenaBasics.RouteHeadings(request.Contract, body),
            preference);
        if (desired is not ProjectileHeading heading)
            return null;
        Position chosen = ArenaBasics.Step(body.Position, heading);
        return Queues(request, goals, blocked, queued, preference, chosen)
            ? null
            : chosen;
    }

    /// <summary>
    /// Whether the chosen step is only a detour AROUND a teammate, and a
    /// detour that buys nothing.
    ///
    /// <para>A right-of-way lane and a committed destination are one-tick
    /// reservations: the tile is free again next tick. Treating them like
    /// walls is what produced the owner's scene — a body beside a passing
    /// carrier finds its step reserved, the breadth-first search happily
    /// returns a route that starts by walking BACKWARDS, and the tick after
    /// that the wall is gone and the body walks back. Two wasted ticks and
    /// an unchanged destination: "a brief dance that was unwarranted just
    /// because the carrier was close".</para>
    ///
    /// <para>So the arbiter asks the counterfactual: where would this body
    /// go if no teammate were in the way? If that answer is one of the
    /// queued tiles and the step actually chosen makes no progress toward
    /// the goal, waiting one tick is strictly better than trading two. When
    /// the detour DOES make progress it is taken exactly as before, which is
    /// why nothing changes anywhere the systems already agreed.</para>
    /// </summary>
    private static bool Queues(
        StepRequest request,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> queued,
        ProjectileHeading[] preference,
        Position chosen)
    {
        if (queued.Count == 0 || queued.Contains(chosen))
            return false;
        GenericActorMapContract map = request.Contract.Map;
        int? here = Nearest(map, request.Body.Position, goals);
        int? taken = Nearest(map, chosen, goals);
        if (here is null || taken is null || taken < here)
            return false;
        var free = new HashSet<Position>(blocked);
        free.ExceptWith(queued);
        ProjectileHeading? unobstructed = ArenaBasics.FindFirstStep(
            map,
            request.Body.Position,
            goals,
            free,
            ArenaBasics.RouteHeadings(request.Contract, request.Body),
            preference);
        return unobstructed is ProjectileHeading heading
            && queued.Contains(ArenaBasics.Step(request.Body.Position, heading));
    }

    private static int? Nearest(
        GenericActorMapContract map,
        Position from,
        IReadOnlySet<Position> goals)
    {
        int? best = null;
        foreach (Position goal in goals)
        {
            if (ArenaBasics.StaticDistance(map, from, goal) is int distance
                && (best is null || distance < best))
            {
                best = distance;
            }
        }
        return best;
    }

    /// <summary>
    /// The legal traffic-aware step whose static map distance to the goal
    /// does not increase. Equal-distance sidesteps are permitted so a
    /// permanently reserved spawn tile cannot pin a carrier on the nominal
    /// shortest ray.
    /// </summary>
    private static Position? Homeward(StepRequest request)
    {
        MindBody body = request.Body;
        Position goal = request.Positions.First();
        int? currentDistance = ArenaBasics.StaticDistance(
            request.Contract.Map, body.Position, goal);
        if (currentDistance is null)
            return null;
        HashSet<Position> blocked = ArenaBasics.BlockedNow(
            request.Contract, request.Mind, body, request.Claims,
            out HashSet<Position> queued);
        Position? chosen = Homeward(request, goal, currentDistance, blocked);
        if (chosen is null || queued.Count == 0)
            return chosen;
        // The same queue rule the route plane uses, asked in this plane's
        // own vocabulary: an EQUAL-distance sidestep taken only because a
        // teammate has the strictly-better tile for one tick is the first
        // half of a two-tile flap. Wait for the tile instead.
        if (ArenaBasics.StaticDistance(request.Contract.Map, chosen.Value, goal)
                is not int taken
            || taken < currentDistance)
        {
            return chosen;
        }
        var free = new HashSet<Position>(blocked);
        free.ExceptWith(queued);
        return Homeward(request, goal, currentDistance, free) is Position better
            && queued.Contains(better)
            ? null
            : chosen;
    }

    private static Position? Homeward(
        StepRequest request,
        Position goal,
        int? currentDistance,
        IReadOnlySet<Position> blocked)
    {
        MindBody body = request.Body;
        ProjectileHeading facing = (ProjectileHeading)((int)body.Facing * 2);
        bool mirrored = ArenaBasics.MirroredFrame(
            request.Contract, request.Mind);
        return ArenaBasics.RouteHeadings(request.Contract, body)
            .Select(heading => (
                Heading: heading,
                Destination: ArenaBasics.Step(body.Position, heading)))
            .Where(candidate => ArenaBasics.CanStep(
                    request.Contract.Map,
                    body.Position,
                    candidate.Destination,
                    candidate.Heading)
                && !blocked.Contains(candidate.Destination))
            .Select(candidate => (
                candidate.Heading,
                candidate.Destination,
                Distance: ArenaBasics.StaticDistance(
                    request.Contract.Map, candidate.Destination, goal)))
            .Where(candidate => candidate.Distance is not null
                && candidate.Distance <= currentDistance)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Heading == facing ? 0 : 1)
            .ThenBy(candidate => ArenaBasics.FrameHeading(
                candidate.Heading, mirrored))
            .Select(candidate => (Position?)candidate.Destination)
            .FirstOrDefault();
    }

    /// <summary>
    /// A carrier's own committed plan: the terrain-shortest step toward the
    /// one goal, with visible spawn reservations treated as obstacles for
    /// the WHOLE route rather than for one step — a reservation beside a
    /// reactor has to select one deterministic way around it, or equal
    /// local choices bounce between adjacent tiles. The chosen tile is then
    /// admitted exactly like a Direct step, so traffic still refuses it and
    /// the caller falls through to its own alternatives.
    /// </summary>
    private static Position? Routed(StepRequest request)
    {
        MindBody body = request.Body;
        Position goal = request.Positions.First();
        if (ArenaBasics.StaticFirstStepAvoidingReservations(
                request.Contract, request.Mind, body, goal)
            is not Position committed)
        {
            return null;
        }
        return Direct(request with
        {
            Intent = StepIntent.Direct,
            Positions = [committed],
        });
    }

    /// <summary>
    /// The plane owes this body's tile to somebody stronger, so it leaves —
    /// the ONE displacement the movement plane authors. Preference, in
    /// order: straight on, continuing away from whoever wants through (the
    /// escort's "walk the corridor out ahead of your leader" answer, which
    /// keeps a reversing file walking instead of arguing over one tile);
    /// otherwise the legal step that puts the most ground between this body
    /// and the tiles it owes.
    /// </summary>
    private static Position? MakeWay(StepRequest request)
    {
        MindBody body = request.Body;
        if (request.Anchor is Position anchor)
        {
            var onward = new Position(
                body.Position.X + (body.Position.X - anchor.X),
                body.Position.Y + (body.Position.Y - anchor.Y));
            if (Direct(request with
                {
                    Intent = StepIntent.Direct,
                    Positions = [onward],
                }) is Position ahead
                && !request.Positions.Contains(ahead))
            {
                return ahead;
            }
        }
        return Aside(request);
    }

    /// <summary>
    /// One kiting backstep: the legal step that most improves the body's
    /// distance to the threats, or nothing when no step strictly improves
    /// it, so callers fall through instead of jittering in place.
    /// </summary>
    private static Position? Away(StepRequest request)
    {
        MindBody body = request.Body;
        IReadOnlyCollection<Position> threats = request.Positions;
        if (threats.Count == 0)
            return null;
        HashSet<Position> blocked = ArenaBasics.BlockedNow(
            request.Contract, request.Mind, body, request.Claims);
        int current = threats.Min(threat =>
            threat.ChebyshevDistance(body.Position));
        bool mirrored = ArenaBasics.MirroredFrame(
            request.Contract, request.Mind);
        return ArenaBasics.RouteHeadings(request.Contract, body)
            .Select(heading => (
                Heading: heading,
                Tile: ArenaBasics.Step(body.Position, heading)))
            .Where(candidate => ArenaBasics.CanStep(
                    request.Contract.Map,
                    body.Position,
                    candidate.Tile,
                    candidate.Heading)
                && !blocked.Contains(candidate.Tile))
            .Select(candidate => (
                candidate.Heading,
                candidate.Tile,
                Distance: threats.Min(threat =>
                    threat.ChebyshevDistance(candidate.Tile))))
            .Where(candidate => candidate.Distance > current)
            .OrderByDescending(candidate => candidate.Distance)
            .ThenBy(candidate => ArenaBasics.FrameHeading(
                candidate.Heading, mirrored))
            .Select(candidate => (Position?)candidate.Tile)
            .FirstOrDefault();
    }

    /// <summary>
    /// Out of the forbidden tiles and as far from them as one step reaches.
    /// A body already blocking a return lane must clear it even when the
    /// only exit crosses predicted fire, so hostile projectiles are not
    /// treated as obstacles here.
    /// </summary>
    private static Position? Aside(StepRequest request)
    {
        MindBody body = request.Body;
        IReadOnlyCollection<Position> forbidden = request.Positions;
        HashSet<Position> blocked = ArenaBasics.BlockedNow(
            request.Contract,
            request.Mind,
            body,
            request.Claims,
            avoidHostileProjectiles: false);
        bool mirrored = ArenaBasics.MirroredFrame(
            request.Contract, request.Mind);
        return ArenaBasics.RouteHeadings(request.Contract, body)
            .Select(heading => (
                Heading: heading,
                Tile: ArenaBasics.Step(body.Position, heading)))
            .Where(candidate => ArenaBasics.CanStep(
                    request.Contract.Map,
                    body.Position,
                    candidate.Tile,
                    candidate.Heading)
                && !blocked.Contains(candidate.Tile)
                && !forbidden.Contains(candidate.Tile))
            .OrderByDescending(candidate => forbidden.Min(tile =>
                candidate.Tile.ChebyshevDistance(tile)))
            .ThenBy(candidate => ArenaBasics.FrameHeading(
                candidate.Heading, mirrored))
            .Select(candidate => (Position?)candidate.Tile)
            .FirstOrDefault();
    }

    /// <summary>
    /// An already-chosen adjacent step, admitted only if it is a legal
    /// terrain step onto a tile nothing has claimed.
    /// </summary>
    private static Position? Direct(StepRequest request)
    {
        MindBody body = request.Body;
        Position destination = request.Positions.First();
        int dx = destination.X - body.Position.X;
        int dy = destination.Y - body.Position.Y;
        if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || dx == 0 && dy == 0)
            return null;
        ProjectileHeading heading = ArenaBasics.FromVector(dx, dy);
        if (!ArenaBasics.CanStep(
                request.Contract.Map, body.Position, destination, heading)
            || ArenaBasics.BlockedNow(
                    request.Contract, request.Mind, body, request.Claims)
                .Contains(destination))
        {
            return null;
        }
        return destination;
    }
}
