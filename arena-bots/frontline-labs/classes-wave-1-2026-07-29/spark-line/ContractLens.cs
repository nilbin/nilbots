using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Everything SparkLine needs to know about the resolved match, read once per
/// life from <see cref="GenericActorMatchStart.Contract"/>. Nothing here is
/// hard-coded: forms, actions, fabrication routes, placement offsets, objective
/// regions, unlock ticks, and pad geometry all come from the contract, so the
/// same policy runs on the class arm, hosted v1, and the automatic-companion
/// arm without branching on a ruleset name.
/// </summary>
internal sealed class ContractLens
{
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.ActionDefinition>
        _actions = new(StringComparer.Ordinal);
    private readonly bool[] _wall;
    private readonly bool[] _closedToMe;

    public ContractLens(GenericActorMatchStart start)
    {
        Contract = start.Contract;
        TeamId = start.ActorId.TeamId;
        UnitId = start.ActorId.UnitId;
        ParticipantId = start.ParticipantId;

        GenericActorRulesContract rules = Contract.Rules;
        foreach (GenericActorRulesContract.Form form in rules.Forms)
            _forms[form.Id] = form;
        foreach (GenericActorRulesContract.AttackProfile attack
                 in rules.AttackProfiles)
        {
            _attacks[attack.Id] = attack;
        }
        foreach (GenericActorRulesContract.ActionDefinition action
                 in rules.Actions)
        {
            _actions[action.Id] = action;
        }

        Width = Contract.Map.Width;
        Height = Contract.Map.Height;
        _wall = new bool[Width * Height];
        _closedToMe = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            string row = Contract.Map.TileRows[y];
            for (int x = 0; x < Width; x++)
                _wall[(y * Width) + x] = row[x] == '#';
        }

        MyStartTiles = StartTiles(teamMatches: true);
        EnemyStartTiles = StartTiles(teamMatches: false);
        ForwardPoint = EnemyStartTiles.Length > 0
            ? EnemyStartTiles[0]
            : new Position(Width / 2, Height / 2);

        // Spawn protection blocks opposing ground entry only. Attribute each
        // protected tile to whichever side's declared start tiles are nearer.
        foreach (GenericActorMapContract.TileTag tag in Safe(Contract.Map.TileTags))
        {
            if (tag.Kind != GenericActorMapContract.TileTagKind.SpawnProtected)
                continue;
            foreach (Position tile in tag.Tiles)
            {
                if (!InBounds(tile))
                    continue;
                if (NearestDistance(tile, EnemyStartTiles)
                    < NearestDistance(tile, MyStartTiles))
                {
                    _closedToMe[(tile.Y * Width) + tile.X] = true;
                }
            }
        }

        ObjectiveTiles = ResolveObjectives();
        AdvanceDelta = ResolveAdvanceDelta();
        ResolveFabrication();
        ReservedSpawnTiles = ResolveReservedSpawnTiles();
    }

    /// <summary>Frozen contract delivered to this life.</summary>
    public GenericActorResolvedMatchContract Contract { get; }
    /// <summary>Scoring team this life belongs to.</summary>
    public int TeamId { get; }
    /// <summary>Stable unit slot this life occupies.</summary>
    public int UnitId { get; }
    /// <summary>Participant that controls this life.</summary>
    public int ParticipantId { get; }
    /// <summary>Map width in tiles.</summary>
    public int Width { get; }
    /// <summary>Map height in tiles.</summary>
    public int Height { get; }
    /// <summary>Ordered objective regions resolved to tile sets.</summary>
    public Position[][] ObjectiveTiles { get; } = [];
    /// <summary>Signed objective-index delta for one advance by my team.</summary>
    public int AdvanceDelta { get; private set; } = 1;
    /// <summary>Declared start tiles owned by my team.</summary>
    public Position[] MyStartTiles { get; } = [];
    /// <summary>Declared start tiles owned by every other team.</summary>
    public Position[] EnemyStartTiles { get; } = [];
    /// <summary>Anchor used to decide which side of a position faces the enemy.</summary>
    public Position ForwardPoint { get; }
    /// <summary>Tiles a fabricating source must stand on, empty when unknown.</summary>
    public HashSet<Position> FabricationSourceTiles { get; } = [];
    /// <summary>Tiles a fabricated child may legally occupy.</summary>
    public HashSet<Position> FabricationOutputTiles { get; } = [];
    /// <summary>Facing-relative placement candidates, in declared priority order.</summary>
    public ImmutableArray<GenericActorRulesContract.RelativePositionOffset>
        PlacementOffsets
    { get; private set; } = [];
    /// <summary>Forms permitted to start the fabrication transition.</summary>
    public ImmutableArray<string> FabricationSourceForms { get; private set; } = [];
    /// <summary>Spawn tiles permanently reserved for my own slots.</summary>
    public HashSet<Position> ReservedSpawnTiles { get; } = [];

    /// <summary>True when the tile is outside the map or a gameplay wall.</summary>
    public bool IsWall(Position position) =>
        !InBounds(position) || _wall[(position.Y * Width) + position.X];

    /// <summary>True when my ground bodies may never enter the tile.</summary>
    public bool IsClosed(Position position) =>
        IsWall(position) || _closedToMe[(position.Y * Width) + position.X];

    /// <summary>True when the coordinate lies on the map.</summary>
    public bool InBounds(Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < Width
        && position.Y < Height;

    /// <summary>Form catalog entry, or null when the ID is unknown.</summary>
    public GenericActorRulesContract.Form? Form(string formId) =>
        _forms.TryGetValue(formId, out GenericActorRulesContract.Form? form)
            ? form
            : null;

    /// <summary>Attack profile bound to a form, or null when it cannot attack.</summary>
    public GenericActorRulesContract.AttackProfile? AttackFor(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        if (form?.AttackProfileId is not string id)
            return null;
        return _attacks.TryGetValue(
            id,
            out GenericActorRulesContract.AttackProfile? attack)
            ? attack
            : null;
    }

    /// <summary>Action catalog entry, or null when the ID is unknown.</summary>
    public GenericActorRulesContract.ActionDefinition? Action(string actionId) =>
        _actions.TryGetValue(
            actionId,
            out GenericActorRulesContract.ActionDefinition? action)
            ? action
            : null;

    /// <summary>Semantic kind of an action ID, or null when unknown.</summary>
    public GenericActorRulesContract.ActionKind? KindOf(string actionId) =>
        Action(actionId)?.Kind;

    /// <summary>True when the action declares the given typed parameter.</summary>
    public bool HasParameter(
        string actionId,
        GenericActorRulesContract.ActionParameterKind kind) =>
        Action(actionId)?.ParameterKinds.Contains(kind) ?? false;

    /// <summary>True when the named form can start a fabrication transition.</summary>
    public bool CanFabricate(string formId) =>
        !FabricationSourceForms.IsDefaultOrEmpty
        && FabricationSourceForms.Contains(formId, StringComparer.Ordinal);

    /// <summary>Active objective tiles for the given ordered index.</summary>
    public Position[] ObjectiveAt(int index) =>
        index >= 0 && index < ObjectiveTiles.Length ? ObjectiveTiles[index] : [];

    private static ImmutableArray<T> Safe<T>(ImmutableArray<T> values) =>
        values.IsDefault ? ImmutableArray<T>.Empty : values;

    private static int NearestDistance(Position from, Position[] tiles)
    {
        int best = int.MaxValue;
        foreach (Position tile in tiles)
            best = Math.Min(best, from.ChebyshevDistance(tile));
        return best;
    }

    private Position[] StartTiles(bool teamMatches)
    {
        var spawnById = new Dictionary<string, Position>(StringComparer.Ordinal);
        foreach (GenericActorResolvedMatchContract.InitialSpawn spawn
                 in Safe(Contract.InitialDeployment.Spawns))
        {
            spawnById[spawn.SpawnId] = spawn.Position;
        }

        var tiles = new List<Position>();
        foreach (GenericActorResolvedMatchContract.InitialLifeDeployment life
                 in Safe(Contract.InitialDeployment.Lives))
        {
            if (life.TeamId == TeamId != teamMatches)
                continue;
            if (spawnById.TryGetValue(life.SpawnId, out Position position))
                tiles.Add(position);
        }
        return tiles.ToArray();
    }

    private Position[][] ResolveObjectives()
    {
        if (Contract.ModeMapBinding
            is not GenericActorResolvedMatchContract.FrontlineModeMapBinding
                binding)
        {
            return [];
        }

        var byId = new Dictionary<string, Position[]>(StringComparer.Ordinal);
        foreach (GenericActorMapContract.Region region in Safe(Contract.Map.Regions))
            byId[region.RegionId] = region.Tiles.ToArray();

        var ordered = new Position[binding.OrderedObjectiveRegionIds.Length][];
        for (int index = 0; index < ordered.Length; index++)
        {
            ordered[index] = byId.TryGetValue(
                binding.OrderedObjectiveRegionIds[index],
                out Position[]? tiles)
                ? tiles
                : [];
        }
        return ordered;
    }

    private int ResolveAdvanceDelta()
    {
        if (Contract.ModeMapBinding
            is not GenericActorResolvedMatchContract.FrontlineModeMapBinding
                binding)
        {
            return 1;
        }
        foreach (GenericActorResolvedMatchContract.FrontlineTeamAdvance advance
                 in binding.TeamAdvances)
        {
            if (advance.TeamId == TeamId)
                return advance.ObjectiveIndexDelta;
        }
        return 1;
    }

    private void ResolveFabrication()
    {
        GenericActorRulesContract
            .BoundedChildFabricationTransition? transition = null;
        foreach (GenericActorRulesContract.FabricationTransition candidate
                 in Safe(Contract.Rules.FabricationTransitions))
        {
            if (candidate
                is GenericActorRulesContract.BoundedChildFabricationTransition
                    bounded)
            {
                transition = bounded;
                break;
            }
        }
        if (transition is null)
            return;

        PlacementOffsets = transition.CandidateOffsets;
        FabricationSourceForms = transition.SourceFormIds;

        var regionsById =
            new Dictionary<string, Position[]>(StringComparer.Ordinal);
        foreach (GenericActorMapContract.Region region in Safe(Contract.Map.Regions))
            regionsById[region.RegionId] = region.Tiles.ToArray();

        foreach (GenericActorResolvedMatchContract.ParticipantRegionAssignment
                     assignment in Safe(Contract.ParticipantRegionAssignments))
        {
            if (assignment.ParticipantId != ParticipantId)
                continue;
            if (!regionsById.TryGetValue(
                    assignment.MapRegionId,
                    out Position[]? tiles))
            {
                continue;
            }
            if (string.Equals(
                    assignment.RegionRoleId,
                    transition.SourceRegionRoleId,
                    StringComparison.Ordinal))
            {
                foreach (Position tile in tiles)
                    FabricationSourceTiles.Add(tile);
            }
            if (string.Equals(
                    assignment.RegionRoleId,
                    transition.OutputRegionRoleId,
                    StringComparison.Ordinal))
            {
                foreach (Position tile in tiles)
                    FabricationOutputTiles.Add(tile);
            }
        }

        foreach (GenericActorMapContract.TileTagKind forbidden
                 in transition.ForbiddenOutputTileTags)
        {
            foreach (GenericActorMapContract.TileTag tag in Safe(Contract.Map.TileTags))
            {
                if (tag.Kind != forbidden)
                    continue;
                foreach (Position tile in tag.Tiles)
                    FabricationOutputTiles.Remove(tile);
            }
        }
    }

    private HashSet<Position> ResolveReservedSpawnTiles()
    {
        var spawnById = new Dictionary<string, Position>(StringComparer.Ordinal);
        foreach (GenericActorResolvedMatchContract.InitialSpawn spawn
                 in Safe(Contract.InitialDeployment.Spawns))
        {
            spawnById[spawn.SpawnId] = spawn.Position;
        }
        foreach (GenericActorMapContract.SpawnAnchor anchor
                 in Safe(Contract.Map.SpawnAnchors))
        {
            spawnById[anchor.SpawnId] = anchor.Position;
        }

        var reserved = new HashSet<Position>();
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Safe(Contract.LifecycleAssignments))
        {
            if (assignment.TeamId != TeamId)
                continue;
            if (assignment.AssignedRespawnSpawnId is not string spawnId
                || !spawnById.TryGetValue(spawnId, out Position position))
            {
                continue;
            }
            reserved.Add(position);
            // A slot's assigned return tile stays reserved against every other
            // allied body for the whole match, so treating it as walkable
            // guarantees a permanently blocked step.
            if (assignment.UnitId != UnitId && InBounds(position))
                _closedToMe[(position.Y * Width) + position.X] = true;
        }
        return reserved;
    }
}
