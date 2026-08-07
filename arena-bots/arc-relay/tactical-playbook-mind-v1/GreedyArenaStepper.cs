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
        StepIntent.Away => Away(request),
        StepIntent.Aside => Aside(request),
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
            request.Contract, request.Mind, body, request.Claims);
        ProjectileHeading? desired = ArenaBasics.FindFirstStep(
            request.Contract.Map,
            body.Position,
            request.Positions.ToHashSet(),
            blocked,
            ArenaBasics.RouteHeadings(request.Contract, body),
            ArenaBasics.PreferredHeadings(
                body,
                request.Positions,
                ArenaBasics.MirroredFrame(request.Contract, request.Mind)));
        return desired is ProjectileHeading heading
            ? ArenaBasics.Step(body.Position, heading)
            : null;
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
            request.Contract, request.Mind, body, request.Claims);
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
