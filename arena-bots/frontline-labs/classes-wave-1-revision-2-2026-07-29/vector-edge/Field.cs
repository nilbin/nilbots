using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Per-tick derived view of the frozen observation: occupancy, incoming and
/// predicted fire, objective geometry, distance fields, and the deterministic
/// role this life takes among its team's currently active bodies.
/// </summary>
internal sealed class Field
{
    /// <summary>Closed set of absolute cardinal map directions.</summary>
    public static readonly Direction[] Cardinals =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    private const int UnreachableDistance = 1 << 20;

    private readonly Doctrine _doctrine;
    private readonly int[,] _objectiveDistance;
    private readonly Dictionary<Position, int> _threat = [];
    private readonly HashSet<Position> _predictedLanes = [];
    private readonly HashSet<Position> _blocked = [];
    private readonly HashSet<Position> _reserved = [];
    private readonly HashSet<Position> _objectiveTiles = [];

    public Field(Doctrine doctrine, GenericActorContext context)
    {
        _doctrine = doctrine;
        Context = context;
        Self = context.Self.Position;
        Facing = context.Self.Facing;
        Health = context.Self.Health;
        Cooldown = context.Self.Cooldown;
        FormId = context.Self.FormId;

        Coupling = doctrine.CouplingFor(FormId);
        // Advance-first, retreat-last, perpendiculars broken by this life's own
        // deterministic stream. An absolute preference here is a systematic
        // side bias on a mirror-symmetric map, because both teams share it.
        Order = ArenaBasics.OrderedDirections(doctrine.Contract, context);

        Frontline = context.Mode as GenericActorContext.ModeObservationState
            .Frontline;
        ActiveIndex = Frontline?.ActivePositionIndex ?? -1;
        Objective = doctrine.TilesAt(ActiveIndex);
        foreach (Position tile in Objective)
            _objectiveTiles.Add(tile);
        ClaimingTeam = Frontline?.ClaimingTeamId;
        CaptureProgress = Frontline?.CaptureProgress ?? 0;
        ControlPaused = Frontline is not null
            && context.Tick < Frontline.ControlResumesAtTick;

        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            _blocked.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            _blocked.Add(enemy.Position);
        // A bolt of this team's own is only an obstacle where the contract
        // says allied contact stops it. Treating it as one anyway is how a
        // duelist walls off the corridor it just fired down: shoot east, find
        // east blocked, step west, step east, shoot again — a three-tick loop
        // that hands the front to anyone willing to keep walking. Read the
        // declared policy instead of assuming either answer.
        bool alliedBoltsPass = doctrine.Contract.Rules.Collisions
            .AlliedProjectileContact
            .Contains("pass-through", StringComparison.Ordinal);
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (alliedBoltsPass
                && projectile.OwnerTeamId == doctrine.TeamId)
            {
                continue;
            }
            _blocked.Add(projectile.Position);
        }
        // Another slot's reservation is not traffic that will clear: it is a
        // permanent hole in this life's map, and a route planned through one
        // is a route that never arrives.
        foreach (KeyValuePair<int, Position> reservation
                 in doctrine.SlotReservedTiles)
        {
            if (reservation.Key != context.Self.ActorId.UnitId)
                _reserved.Add(reservation.Value);
        }
        if (context.Self.PreviousActionResolution is
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous
            && previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .FirstOrDefault() is { } blockedStep)
        {
            (int dx, int dy) = blockedStep.Value.Vector();
            _blocked.Add(Self.Offset(dx, dy));
        }

        BuildThreat();
        BuildPredictedLanes();
        _objectiveDistance = BuildObjectiveDistance();
        ResolveRole();
    }

    /// <summary>The frozen pre-tick observation this view was derived from.</summary>
    public GenericActorContext Context { get; }
    /// <summary>This life's current tile.</summary>
    public Position Self { get; }
    /// <summary>This life's current absolute facing.</summary>
    public Direction Facing { get; }
    /// <summary>This life's current health.</summary>
    public int Health { get; }
    /// <summary>Ticks remaining before this life may attack again.</summary>
    public int Cooldown { get; }
    /// <summary>This life's current form identifier.</summary>
    public string FormId { get; }
    /// <summary>How a successful step would change this life's facing.</summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling { get; }
    /// <summary>
    /// Mirror-fair direction preference for this tick: advance first, retreat
    /// last, perpendiculars in a per-life random order. Used as the tie-break
    /// wherever two directions are otherwise equal.
    /// </summary>
    public Direction[] Order { get; }
    /// <summary>True when a step turns the body under the current profile.</summary>
    public bool MoveTurns => Coupling
        == GenericActorRulesContract.MovementFacingCoupling
            .FaceMovementDirection;
    /// <summary>True when only the current facing is a legal travel direction.</summary>
    public bool MoveLocked => Coupling
        == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;

    /// <summary>
    /// The facing this life would hold after stepping <paramref name="step"/>.
    /// Under a coupled profile the step is the turn, which is why a dodge has
    /// to be priced against the aim and the sight quadrant it spends.
    /// </summary>
    public Direction FacingAfter(Direction step) =>
        MoveTurns ? step : Facing;
    /// <summary>Public Frontline objective state, when the mode declares it.</summary>
    public GenericActorContext.ModeObservationState.Frontline? Frontline { get; }
    /// <summary>Current ordered objective index, or -1 when unavailable.</summary>
    public int ActiveIndex { get; }
    /// <summary>Tiles of the currently contested objective.</summary>
    public ImmutableArray<Position> Objective { get; }
    /// <summary>Team currently accumulating capture progress, if any.</summary>
    public int? ClaimingTeam { get; }
    /// <summary>Current capture progress on the active objective.</summary>
    public int CaptureProgress { get; }
    /// <summary>True while the post-advance redeploy pause suppresses control.</summary>
    public bool ControlPaused { get; }
    /// <summary>True when this life stands on the active objective.</summary>
    public bool OnObjective => _objectiveTiles.Contains(Self);
    /// <summary>True when at least one visible enemy stands on the objective.</summary>
    public bool EnemyOnObjective { get; private set; }
    /// <summary>True when an allied body already stands on the objective.</summary>
    public bool AllyOnObjective { get; private set; }
    /// <summary>
    /// True when this life is the team's nearest active body to the objective
    /// and therefore owns the capture.
    /// </summary>
    public bool IsCapturer { get; private set; }
    /// <summary>Count of allied active bodies that can contest an objective.</summary>
    public int AlliedObjectiveBodies { get; private set; }

    /// <summary>
    /// Objective advances the opposing side still needs before it breaches.
    /// Two or more is room to spend a dozen ticks somewhere else.
    /// </summary>
    public int EnemyAdvancesRemaining
    {
        get
        {
            int positions = _doctrine.ObjectivePositions.Length;
            if (positions == 0 || ActiveIndex < 0)
                return int.MaxValue;
            return _doctrine.AdvanceDelta >= 0
                ? ActiveIndex + 1
                : positions - ActiveIndex;
        }
    }

    /// <summary>True when a tile belongs to the currently contested objective.</summary>
    public bool IsObjective(Position tile) => _objectiveTiles.Contains(tile);

    /// <summary>True when another body or projectile already occupies a tile.</summary>
    public bool IsOccupied(Position tile) =>
        _blocked.Contains(tile) || _reserved.Contains(tile);

    /// <summary>
    /// True when this life could ever stand on the tile. Bodies and
    /// projectiles move; walls and other slots' reservations do not.
    /// </summary>
    public bool IsPassable(Position tile) =>
        _doctrine.IsOpen(tile) && !_reserved.Contains(tile);

    /// <summary>
    /// Earliest tick offset at which a visible hostile projectile enters the
    /// tile; <see langword="null"/> when no tracked projectile reaches it.
    /// </summary>
    public int? ThreatAt(Position tile) =>
        _threat.TryGetValue(tile, out int offset) ? offset : null;

    /// <summary>True when a visible enemy's current facing sweeps this tile.</summary>
    public bool InPredictedLane(Position tile) => _predictedLanes.Contains(tile);

    /// <summary>Walk distance to the nearest active objective tile.</summary>
    public int DistanceToObjective(Position tile) =>
        tile.X < 0
        || tile.Y < 0
        || tile.X >= _doctrine.Width
        || tile.Y >= _doctrine.Height
            ? UnreachableDistance
            : _objectiveDistance[tile.X, tile.Y];

    /// <summary>
    /// Open, currently free tiles whose walk distance to the objective falls
    /// inside the given band — the supporting ring a second body covers the
    /// approaches from without stacking onto the capture itself.
    /// </summary>
    public HashSet<Position> Ring(int minimum, int maximum)
    {
        var tiles = new HashSet<Position>();
        for (int y = 0; y < _doctrine.Height; y++)
        {
            for (int x = 0; x < _doctrine.Width; x++)
            {
                var tile = new Position(x, y);
                int distance = DistanceToObjective(tile);
                if (distance >= minimum
                    && distance <= maximum
                    && (!IsOccupied(tile) || tile == Self))
                {
                    tiles.Add(tile);
                }
            }
        }
        return tiles;
    }

    /// <summary>True when the tile is open and holds no body or projectile.</summary>
    public bool CanEnter(Position tile) =>
        IsPassable(tile) && !_blocked.Contains(tile);

    /// <summary>
    /// Every first step that starts a shortest obstacle-aware route to some
    /// goal tile, in this tick's mirror-fair preference order. Returning the
    /// whole tied set — not one arbitrary winner — is what lets a coupled
    /// profile pick the equally short step that also lays the gun.
    /// </summary>
    public List<Direction> StepsToward(
        IReadOnlySet<Position> goals,
        IReadOnlySet<Direction> allowedFirstSteps,
        IReadOnlySet<Position>? avoid = null)
    {
        var steps = new List<Direction>();
        if (goals.Count == 0 || goals.Contains(Self))
            return steps;

        int[,] distance = BuildDistance(goals);
        int best = int.MaxValue;
        foreach (Direction direction in Order)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
            (int dx, int dy) = direction.Vector();
            Position next = Self.Offset(dx, dy);
            if (!CanEnter(next) || avoid?.Contains(next) == true)
                continue;
            int reach = At(distance, next);
            if (reach >= UnreachableDistance)
                continue;
            if (reach < best)
            {
                best = reach;
                steps.Clear();
            }
            if (reach == best)
                steps.Add(direction);
        }
        return steps;
    }

    /// <summary>
    /// First legal step of a shortest obstacle-aware route to any goal tile,
    /// or <see langword="null"/> when no route exists.
    /// </summary>
    public Direction? StepToward(
        IReadOnlySet<Position> goals,
        IReadOnlySet<Direction> allowedFirstSteps,
        IReadOnlySet<Position>? avoid = null)
    {
        List<Direction> steps = StepsToward(goals, allowedFirstSteps, avoid);
        return steps.Count == 0 ? null : steps[0];
    }

    private int At(int[,] distance, Position tile) =>
        tile.X < 0
        || tile.Y < 0
        || tile.X >= _doctrine.Width
        || tile.Y >= _doctrine.Height
            ? UnreachableDistance
            : distance[tile.X, tile.Y];

    private int[,] BuildDistance(IReadOnlySet<Position> goals)
    {
        var distance = new int[Math.Max(_doctrine.Width, 1),
            Math.Max(_doctrine.Height, 1)];
        for (int x = 0; x < _doctrine.Width; x++)
        {
            for (int y = 0; y < _doctrine.Height; y++)
                distance[x, y] = UnreachableDistance;
        }
        var queue = new Queue<Position>();
        foreach (Position tile in goals)
        {
            if (!IsPassable(tile))
                continue;
            distance[tile.X, tile.Y] = 0;
            queue.Enqueue(tile);
        }
        while (queue.Count > 0)
        {
            Position tile = queue.Dequeue();
            int next = distance[tile.X, tile.Y] + 1;
            foreach (Direction direction in Cardinals)
            {
                (int dx, int dy) = direction.Vector();
                Position neighbour = tile.Offset(dx, dy);
                if (!IsPassable(neighbour)
                    || At(distance, neighbour) <= next)
                {
                    continue;
                }
                distance[neighbour.X, neighbour.Y] = next;
                queue.Enqueue(neighbour);
            }
        }
        return distance;
    }

    private void BuildThreat()
    {
        GenericActorContext.ObservedProjectile[] hostile =
            (Context.VisibleProjectiles ?? [])
            .Where(projectile => projectile.OwnerTeamId != _doctrine.TeamId)
            .OrderBy(projectile => projectile.ProjectileId)
            .ToArray();
        foreach (GenericActorContext.ObservedProjectile projectile in hostile)
        {
            (int dx, int dy) = projectile.Heading.Vector();
            Position cursor = projectile.Position;
            int remaining = projectile.RemainingTiles;
            int offset = Math.Max(0, projectile.TicksUntilAdvance - 1);
            int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
            for (int advance = 0; advance < 3 && remaining > 0; advance++)
            {
                for (int step = 0; step < perAdvance && remaining > 0; step++)
                {
                    Position next = cursor.Offset(dx, dy);
                    if (_doctrine.IsWall(next))
                    {
                        remaining = 0;
                        break;
                    }
                    if (dx != 0
                        && dy != 0
                        && (_doctrine.IsWall(cursor.Offset(dx, 0))
                            || _doctrine.IsWall(cursor.Offset(0, dy))))
                    {
                        remaining = 0;
                        break;
                    }
                    cursor = next;
                    remaining--;
                    if (!_threat.TryGetValue(cursor, out int known)
                        || offset < known)
                    {
                        _threat[cursor] = offset;
                    }
                }
                offset += _doctrine.ProjectileTicksPerAdvance;
            }
        }
    }

    private void BuildPredictedLanes()
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in Context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                _doctrine.AttackFor(enemy.FormId);
            if (attack is null)
                continue;
            int minAim = attack.ShotProgram.Enabled
                ? attack.ShotProgram.MinInitialAimSteps
                : 0;
            int maxAim = attack.ShotProgram.Enabled
                ? attack.ShotProgram.MaxInitialAimSteps
                : 0;
            if (attack.OmnidirectionalAim)
            {
                minAim = -4;
                maxAim = 3;
            }
            for (int aim = minAim; aim <= maxAim; aim++)
            {
                foreach (Position tile in Ballistics.Trace(
                             _doctrine,
                             enemy.Position,
                             enemy.Facing.ToProjectileHeading().Turned(aim),
                             bendDirection: 0,
                             bendAfterTiles: 0,
                             bendEveryTiles: 1,
                             bendCount: 0,
                             attack.Projectile.MaxTravelTiles,
                             attack.Projectile.DiagonalCornersMustBeClear))
                {
                    _predictedLanes.Add(tile);
                }
            }
        }
    }

    private int[,] BuildObjectiveDistance()
    {
        var distance = new int[Math.Max(_doctrine.Width, 1),
            Math.Max(_doctrine.Height, 1)];
        for (int x = 0; x < _doctrine.Width; x++)
        {
            for (int y = 0; y < _doctrine.Height; y++)
                distance[x, y] = UnreachableDistance;
        }

        var queue = new Queue<Position>();
        foreach (Position tile in Objective)
        {
            if (!_doctrine.IsOpen(tile))
                continue;
            distance[tile.X, tile.Y] = 0;
            queue.Enqueue(tile);
        }
        while (queue.Count > 0)
        {
            Position tile = queue.Dequeue();
            int next = distance[tile.X, tile.Y] + 1;
            foreach (Direction direction in Cardinals)
            {
                (int dx, int dy) = direction.Vector();
                Position neighbour = tile.Offset(dx, dy);
                if (!_doctrine.IsOpen(neighbour)
                    || distance[neighbour.X, neighbour.Y] <= next)
                {
                    continue;
                }
                distance[neighbour.X, neighbour.Y] = next;
                queue.Enqueue(neighbour);
            }
        }
        return distance;
    }

    private void ResolveRole()
    {
        EnemyOnObjective = Context.Enemies.Any(enemy =>
            _objectiveTiles.Contains(enemy.Position)
            && (_doctrine.FormFor(enemy.FormId)?.ObjectiveWeight ?? 1) > 0);
        AllyOnObjective = Context.Allies.Any(ally =>
            _objectiveTiles.Contains(ally.Position)
            && (_doctrine.FormFor(ally.FormId)?.ObjectiveWeight ?? 1) > 0);

        bool selfCounts =
            (_doctrine.FormFor(FormId)?.ObjectiveWeight ?? 1) > 0;
        AlliedObjectiveBodies = (selfCounts ? 1 : 0)
            + Context.Allies.Count(ally =>
                (_doctrine.FormFor(ally.FormId)?.ObjectiveWeight ?? 1) > 0);

        if (!selfCounts)
        {
            IsCapturer = false;
            return;
        }

        int myDistance = DistanceToObjective(Self);
        ActorIdentity myId = Context.Self.ActorId;
        IsCapturer = true;
        foreach (GenericActorContext.ObservedAllyState ally in Context.Allies)
        {
            if ((_doctrine.FormFor(ally.FormId)?.ObjectiveWeight ?? 1) <= 0)
                continue;
            int allyDistance = DistanceToObjective(ally.Position);
            if (allyDistance < myDistance
                || allyDistance == myDistance
                && ally.ActorId.CompareTo(myId) < 0)
            {
                IsCapturer = false;
                return;
            }
        }
    }
}
