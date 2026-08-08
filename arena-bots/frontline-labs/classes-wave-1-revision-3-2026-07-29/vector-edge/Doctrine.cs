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
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks = new(StringComparer.Ordinal);

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
    }

    /// <summary>Resolves the immutable per-life plan from the match start.</summary>
    public static Doctrine Resolve(GenericActorMatchStart start) => new(start);

    /// <summary>The authoritative resolved match contract.</summary>
    public GenericActorResolvedMatchContract Contract { get; }
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

    /// <summary>True when the tile is outside the map or a blocking wall.</summary>
    public bool IsWall(Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= Width
        || position.Y >= Height
        || _wall[position.X, position.Y];

    /// <summary>True when ground movement may legally occupy the tile.</summary>
    public bool IsOpen(Position position) => !IsWall(position);

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
