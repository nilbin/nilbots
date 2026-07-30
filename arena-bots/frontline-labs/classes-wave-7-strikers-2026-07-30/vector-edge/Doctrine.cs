using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Static facts resolved once from the delivered match contract.
/// Every value here is read from <see cref="GenericActorMatchStart.Contract"/>:
/// no participant ID, team ID, unit count, unlock tick, form name, action code,
/// map coordinate, or projectile constant is hard-coded anywhere in this bot.
/// </summary>
internal sealed class Doctrine
{
    /// <summary>Wall symbol used by the map-contract tile encoding.</summary>
    private const char WallSymbol = '#';

    private readonly bool[,] _wall;
    private readonly ImmutableDictionary<Position, int> _chokeRun;
    private readonly Dictionary<int, ImmutableArray<Position>> _chokeRuns = [];
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks = new(StringComparer.Ordinal);
    private readonly Dictionary<
        GenericActorMapContract.TileTagKind,
        ImmutableHashSet<Position>> _tagged = [];

    private Doctrine(GenericActorMatchStart start)
    {
        Contract = start.Contract;
        TeamId = start.ActorId.TeamId;
        ParticipantId = start.ParticipantId;

        GenericActorMapContract map = Contract.Map;
        Width = map.Width;
        Height = map.Height;
        _wall = new bool[Math.Max(Width, 1), Math.Max(Height, 1)];
        for (int y = 0; y < Height && y < map.TileRows.Length; y++)
        {
            string row = map.TileRows[y];
            for (int x = 0; x < Width && x < row.Length; x++)
                _wall[x, y] = row[x] == WallSymbol;
        }

        foreach (GenericActorRulesContract.Form form in Contract.Rules.Forms)
            _forms[form.Id] = form;
        foreach (GenericActorRulesContract.AttackProfile attack
                 in Contract.Rules.AttackProfiles)
        {
            _attacks[attack.Id] = attack;
        }

        ObjectivePositions = ResolveObjectivePositions();
        AdvanceDelta = ResolveAdvanceDelta();
        Capture = Contract.Rules.GameMode
            is GenericActorRulesContract.FrontlineGameMode frontline
            ? frontline.Capture
            : null;
        TransitionForbidden = TilesTagged(
            GenericActorMapContract.TileTagKind.TransitionPlacementForbidden);
        SpawnProtected = TilesTagged(
            GenericActorMapContract.TileTagKind.SpawnProtected);
        FabricationSourceTiles = ResolveFabricationSourceTiles();
        SlotReservedTiles = ResolveSlotReservedTiles();
        ProjectileTicksPerAdvance = Contract.Rules.AttackProfiles.Length == 0
            ? 1
            : Contract.Rules.AttackProfiles
                .Select(profile =>
                    Math.Max(1, profile.Projectile.TicksPerAdvance))
                .Min();
        _chokeRun = ResolveChokeRuns();
    }

    /// <summary>Resolves the immutable per-life plan from the match start.</summary>
    public static Doctrine Resolve(GenericActorMatchStart start)
    {
        var doctrine = new Doctrine(start);
        doctrine.Skills = Skills.Resolve(doctrine);
        doctrine.Arms = new Arms(doctrine);
        return doctrine;
    }

    /// <summary>The authoritative resolved match contract.</summary>
    public GenericActorResolvedMatchContract Contract { get; }
    /// <summary>
    /// The class-skill kit this contract declares, recognized by shape rather
    /// than by name. Empty of routes on a contract that carries no kit, which
    /// is what keeps the stance code inert on the base arms.
    /// </summary>
    public Skills Skills { get; private set; } = null!;
    /// <summary>
    /// The aperture every gun in this contract declares: the initial aim
    /// offsets it may launch with, and therefore which bearings are firing
    /// seats and which of them more than one facing buys. Collapses to
    /// revision 4's cardinal-only geometry wherever the aim bounds are zero.
    /// </summary>
    public Arms Arms { get; private set; } = null!;
    /// <summary>Scoring team owning this life.</summary>
    public int TeamId { get; }
    /// <summary>Submitted participant controlling this life.</summary>
    public int ParticipantId { get; }
    /// <summary>Map width in tiles.</summary>
    public int Width { get; }
    /// <summary>Map height in tiles.</summary>
    public int Height { get; }
    /// <summary>Objective tiles for each ordered Frontline position.</summary>
    public ImmutableArray<ImmutableArray<Position>> ObjectivePositions { get; }
    /// <summary>Signed objective-index delta produced by one own advance.</summary>
    public int AdvanceDelta { get; }
    /// <summary>Frontline capture mechanics, when the mode declares them.</summary>
    public GenericActorRulesContract.FrontlineCapture? Capture { get; }
    /// <summary>Tiles on which a same-life transition may not complete.</summary>
    public ImmutableHashSet<Position> TransitionForbidden { get; }
    /// <summary>Tiles carrying the ruleset's spawn-protection semantics.</summary>
    public ImmutableHashSet<Position> SpawnProtected { get; }
    /// <summary>Tiles from which this participant may start a fabrication.</summary>
    public ImmutableHashSet<Position> FabricationSourceTiles { get; }
    /// <summary>
    /// Spawn tile each of this team's stable slots keeps reserved for its own
    /// returning life. Walking another slot's reservation is a blocked tick, so
    /// routes are planned around them.
    /// </summary>
    public ImmutableDictionary<int, Position> SlotReservedTiles { get; }
    /// <summary>Shortest declared ticks between two projectile advances.</summary>
    public int ProjectileTicksPerAdvance { get; }

    /// <summary>
    /// Largest damage any declared attack profile deals with one contact — what
    /// an unseen bolt costs, worst case. A body about to enter a form it cannot
    /// dodge in has to be able to survive that, and the number is the
    /// contract's rather than an assumption that every bolt costs one.
    /// </summary>
    public int HardestHit => Contract.Rules.AttackProfiles.Length == 0
        ? 1
        : Contract.Rules.AttackProfiles
            .Select(profile => Math.Max(0, profile.Projectile.DamagePerHit))
            .Max();

    /// <summary>True when the tile is outside the map or a blocking wall.</summary>
    public bool IsWall(Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= Width
        || position.Y >= Height
        || _wall[position.X, position.Y];

    /// <summary>True when ground movement may legally occupy the tile.</summary>
    public bool IsOpen(Position position) => !IsWall(position);

    /// <summary>
    /// True when this tile is a ONE-TILE CHOKE: an open tile whose open cardinal
    /// neighbours lie on a single axis, so a body standing here fills the
    /// passage and nothing can pass it. Derived from the wall grid, never from a
    /// map name or a coordinate, so it holds on the holdout map too.
    /// </summary>
    public bool IsChoke(Position position) =>
        _chokeRun.ContainsKey(position);

    /// <summary>
    /// Identifier of the connected corridor this tile belongs to, or -1 outside
    /// one. Two chokes in the same run are one resource: entering the far end of
    /// a corridor an ally is walking through is the same jam one tick later.
    /// </summary>
    public int ChokeRun(Position position) =>
        _chokeRun.TryGetValue(position, out int run) ? run : -1;

    /// <summary>Every tile of the corridor run containing this tile.</summary>
    public ImmutableArray<Position> ChokeRunTiles(Position position) =>
        _chokeRuns.TryGetValue(ChokeRun(position),
            out ImmutableArray<Position> tiles)
            ? tiles
            : [];

    /// <summary>Number of open orthogonal neighbours; 2 or fewer is a corridor.</summary>
    public int Openness(Position position)
    {
        int open = 0;
        foreach (Direction direction in Field.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            if (IsOpen(position.Offset(dx, dy)))
                open++;
        }
        return open;
    }

    /// <summary>Looks up a catalog form by its stable identifier.</summary>
    public GenericActorRulesContract.Form? FormFor(string formId) =>
        _forms.TryGetValue(formId, out GenericActorRulesContract.Form? form)
            ? form
            : null;

    /// <summary>Resolves the sensor model a form observes the field with.</summary>
    public GenericActorRulesContract.VisionProfile? VisionFor(string formId)
    {
        GenericActorRulesContract.Form? form = FormFor(formId);
        if (form is null)
            return null;
        return Contract.Rules.VisionProfiles.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Id,
                form.VisionProfileId,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// True when a body with the given sensor model, pose, and form would have
    /// the tile inside its declared sight envelope. A shooter standing outside
    /// that envelope is one the target cannot react to.
    /// </summary>
    public bool CanSee(
        GenericActorRulesContract.VisionProfile vision,
        Position observer,
        Direction facing,
        Position tile)
    {
        int distance = observer.ChebyshevDistance(tile);
        if (distance <= vision.OmnidirectionalProximityRange)
            return true;
        if (distance > vision.Range)
            return false;
        if (!vision.Shape.Contains("quadrant", StringComparison.Ordinal))
            return true;
        return InQuadrant(observer, facing, tile);
    }

    /// <summary>
    /// True when <paramref name="tile"/> lies inside the 90-degree quadrant a
    /// body at <paramref name="observer"/> facing <paramref name="facing"/>
    /// covers. One definition serves two contract facts that share it: the
    /// quadrant a sensor sees, and the quadrant a projectile guard deflects
    /// contacts arriving from. A guard cannot rotate while raised, so this arc
    /// is fixed geometry for as long as the shield is up — which is why going
    /// around one always works and poking its face never does.
    /// </summary>
    public static bool InQuadrant(
        Position observer,
        Direction facing,
        Position tile)
    {
        (int fx, int fy) = facing.Vector();
        int dx = tile.X - observer.X;
        int dy = tile.Y - observer.Y;
        int forward = dx * fx + dy * fy;
        int lateral = Math.Abs(dx * fy - dy * fx);
        return forward > 0 && lateral <= forward;
    }

    /// <summary>
    /// How a step changes this form's facing. Facing is the striker's aim and
    /// its sight quadrant at the same time, so under a coupled profile a dodge
    /// is never just a dodge — it is a turn, priced in re-aim ticks and in the
    /// quadrant it stops watching.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling CouplingFor(
        string formId)
    {
        GenericActorRulesContract.Form? form = FormFor(formId);
        if (form is null)
            return GenericActorRulesContract.MovementFacingCoupling
                .PreserveFacing;
        foreach (GenericActorRulesContract.MovementProfile profile
                 in Contract.Rules.MovementProfiles)
        {
            if (string.Equals(
                    profile.Id,
                    form.MovementProfileId,
                    StringComparison.Ordinal))
            {
                return profile.FacingCoupling;
            }
        }
        return GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>Resolves the attack profile a form currently fires with.</summary>
    public GenericActorRulesContract.AttackProfile? AttackFor(string formId)
    {
        GenericActorRulesContract.Form? form = FormFor(formId);
        if (form?.AttackProfileId is not string attackId)
            return null;
        return _attacks.TryGetValue(
            attackId,
            out GenericActorRulesContract.AttackProfile? attack)
            ? attack
            : null;
    }

    /// <summary>
    /// The declared same-life route between two forms, or null when the contract
    /// declares none. Routes carry their own placement legality, so the route is
    /// the only honest place to ask whether a transition may complete on a tile.
    /// </summary>
    public GenericActorRulesContract.FormTransition? RouteFor(
        string sourceFormId,
        string targetFormId)
    {
        foreach (GenericActorRulesContract.FormTransition route
                 in Contract.Rules.SameLifeTransitions
                     .OfType<GenericActorRulesContract.FormTransition>())
        {
            if (string.Equals(
                    route.SourceFormId,
                    sourceFormId,
                    StringComparison.Ordinal)
                && string.Equals(
                    route.TargetFormId,
                    targetFormId,
                    StringComparison.Ordinal))
            {
                return route;
            }
        }
        return null;
    }

    /// <summary>
    /// True when a body standing in <paramref name="formId"/> has a declared
    /// same-life route back into a form that carries objective weight — so its
    /// absence from the capture count is a posture it can drop, not a body it
    /// has spent.
    ///
    /// <para>This is opponent modelling, and it is the read the once-per-life
    /// era did not need. A fortification whose exit route does not exist has
    /// deleted itself from every capture for the rest of that life, and is worth
    /// very little to shoot. One that can walk back out — the more so when its
    /// own return route reports <c>irreversibleForLife: false</c> and it may
    /// fortify again and again — is a body that will be standing on the
    /// objective again shortly, and pricing it as gone is how a duelist ignores
    /// the thing that is about to contest its ground.</para>
    /// </summary>
    public bool CanRegainObjectiveWeight(string formId)
    {
        foreach (GenericActorRulesContract.FormTransition route
                 in Contract.Rules.SameLifeTransitions
                     .OfType<GenericActorRulesContract.FormTransition>())
        {
            if (!string.Equals(
                    route.SourceFormId,
                    formId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if ((FormFor(route.TargetFormId)?.ObjectiveWeight ?? 0) > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when a route's own declared placement legality permits it to
    /// complete on this tile.
    ///
    /// <para>Revision 4 asked the MAP instead, unioning every
    /// transition-placement-forbidden tag on the board and refusing to transform
    /// on any of them. That was right while every route forbade the same tags,
    /// and it is wrong now: a ground arm may empty a route's
    /// <c>forbiddenTileTags</c> while the map keeps the tags it always carried,
    /// so a stance or an anchor becomes legal on exactly the objective tiles the
    /// old test still refuses. Ask the route.</para>
    /// </summary>
    public bool PlacementAllows(
        GenericActorRulesContract.FormTransition route,
        Position tile)
    {
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.RequiredTileTags)
        {
            if (!Tagged(kind).Contains(tile))
                return false;
        }
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.ForbiddenTileTags)
        {
            if (Tagged(kind).Contains(tile))
                return false;
        }
        return true;
    }

    /// <summary>Objective tiles of one ordered Frontline position.</summary>
    public ImmutableArray<Position> TilesAt(int positionIndex) =>
        positionIndex >= 0 && positionIndex < ObjectivePositions.Length
            ? ObjectivePositions[positionIndex]
            : [];

    /// <summary>
    /// Tick at which a projectile occupies the path tile at
    /// <paramref name="index"/>, relative to the tick it was fired.
    /// </summary>
    public static int ArrivalOffset(
        GenericActorRulesContract.Projectile projectile,
        int index)
    {
        int launch = Math.Max(1, projectile.LaunchTiles);
        int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
        int ticks = Math.Max(1, projectile.TicksPerAdvance);
        int firstBatch = projectile.AdvancesOnLaunchTick
            ? launch + perAdvance
            : launch;
        return index < firstBatch
            ? 0
            : ticks * (1 + (index - firstBatch) / perAdvance);
    }

    private ImmutableArray<ImmutableArray<Position>> ResolveObjectivePositions()
    {
        if (Contract.ModeMapBinding
            is not GenericActorResolvedMatchContract.FrontlineModeMapBinding
                binding)
        {
            return [];
        }

        var builder =
            ImmutableArray.CreateBuilder<ImmutableArray<Position>>(
                binding.OrderedObjectiveRegionIds.Length);
        foreach (string regionId in binding.OrderedObjectiveRegionIds)
        {
            GenericActorMapContract.Region? region = Contract.Map.Regions
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.RegionId,
                        regionId,
                        StringComparison.Ordinal));
            builder.Add(region?.Tiles ?? []);
        }
        return builder.ToImmutable();
    }

    private int ResolveAdvanceDelta()
    {
        if (Contract.ModeMapBinding
            is not GenericActorResolvedMatchContract.FrontlineModeMapBinding
                binding)
        {
            return 0;
        }
        foreach (GenericActorResolvedMatchContract.FrontlineTeamAdvance advance
                 in binding.TeamAdvances)
        {
            if (advance.TeamId == TeamId)
                return advance.ObjectiveIndexDelta;
        }
        return 0;
    }

    private ImmutableHashSet<Position> Tagged(
        GenericActorMapContract.TileTagKind kind)
    {
        if (_tagged.TryGetValue(kind, out ImmutableHashSet<Position>? known))
            return known;
        ImmutableHashSet<Position> tiles = TilesTagged(kind);
        _tagged[kind] = tiles;
        return tiles;
    }

    private ImmutableHashSet<Position> TilesTagged(
        GenericActorMapContract.TileTagKind kind)
    {
        ImmutableHashSet<Position>.Builder builder =
            ImmutableHashSet.CreateBuilder<Position>();
        foreach (GenericActorMapContract.TileTag tag in Contract.Map.TileTags)
        {
            if (tag.Kind != kind)
                continue;
            foreach (Position tile in tag.Tiles)
                builder.Add(tile);
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Finds every one-tile corridor and groups the connected ones into runs.
    ///
    /// <para>A choke is an open tile whose open cardinal neighbours lie on one
    /// axis — north and south open with east and west walled, or the reverse.
    /// That is exactly the geometry in which one body fills the passage: there is
    /// no second lane, so two bodies in one run cannot pass and a corridor needs
    /// a precedence rule rather than a preference. Runs are the connected
    /// components of that set, because the jam is the same whether the ally is on
    /// the tile this body wants or three tiles up the same passage.</para>
    /// </summary>
    private ImmutableDictionary<Position, int> ResolveChokeRuns()
    {
        var chokes = new HashSet<Position>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var tile = new Position(x, y);
                if (IsWall(tile))
                    continue;
                bool north = IsOpen(tile.Offset(0, -1));
                bool south = IsOpen(tile.Offset(0, 1));
                bool west = IsOpen(tile.Offset(-1, 0));
                bool east = IsOpen(tile.Offset(1, 0));
                if (north && south && !west && !east
                    || west && east && !north && !south)
                {
                    chokes.Add(tile);
                }
            }
        }

        ImmutableDictionary<Position, int>.Builder builder =
            ImmutableDictionary.CreateBuilder<Position, int>();
        int next = 0;
        foreach (Position start in chokes.OrderBy(tile => tile.Y)
                     .ThenBy(tile => tile.X))
        {
            if (builder.ContainsKey(start))
                continue;
            int run = next++;
            var component = new List<Position>();
            var queue = new Queue<Position>();
            queue.Enqueue(start);
            builder[start] = run;
            while (queue.Count > 0)
            {
                Position tile = queue.Dequeue();
                component.Add(tile);
                foreach (Direction direction in Field.Cardinals)
                {
                    (int dx, int dy) = direction.Vector();
                    Position neighbour = tile.Offset(dx, dy);
                    if (chokes.Contains(neighbour)
                        && !builder.ContainsKey(neighbour))
                    {
                        builder[neighbour] = run;
                        queue.Enqueue(neighbour);
                    }
                }
            }
            _chokeRuns[run] = [.. component];
        }
        return builder.ToImmutable();
    }

    private ImmutableDictionary<int, Position> ResolveSlotReservedTiles()
    {
        ImmutableDictionary<int, Position>.Builder builder =
            ImmutableDictionary.CreateBuilder<int, Position>();
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Contract.LifecycleAssignments)
        {
            if (assignment.TeamId != TeamId
                || assignment.AssignedRespawnSpawnId is not string spawnId)
            {
                continue;
            }
            GenericActorMapContract.SpawnAnchor? anchor =
                Contract.Map.SpawnAnchors.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.SpawnId,
                        spawnId,
                        StringComparison.Ordinal));
            if (anchor is not null)
                builder[assignment.UnitId] = anchor.Position;
        }
        return builder.ToImmutable();
    }

    private ImmutableHashSet<Position> ResolveFabricationSourceTiles()
    {
        ImmutableHashSet<Position>.Builder builder =
            ImmutableHashSet.CreateBuilder<Position>();
        foreach (GenericActorRulesContract.FabricationTransition transition
                 in Contract.Rules.FabricationTransitions)
        {
            if (transition
                is not GenericActorRulesContract
                    .BoundedChildFabricationTransition bounded)
            {
                continue;
            }
            foreach (GenericActorResolvedMatchContract.ParticipantRegionAssignment
                         assignment in Contract.ParticipantRegionAssignments)
            {
                if (assignment.ParticipantId != ParticipantId
                    || !string.Equals(
                        assignment.RegionRoleId,
                        bounded.SourceRegionRoleId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                GenericActorMapContract.Region? region = Contract.Map.Regions
                    .FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.RegionId,
                            assignment.MapRegionId,
                            StringComparison.Ordinal));
                if (region is null)
                    continue;
                foreach (Position tile in region.Tiles)
                {
                    if (IsOpen(tile))
                        builder.Add(tile);
                }
            }
        }
        return builder.ToImmutable();
    }
}
