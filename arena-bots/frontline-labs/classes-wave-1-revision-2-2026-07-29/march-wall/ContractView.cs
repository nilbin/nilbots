using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// One immutable reading of the resolved match contract for this body life.
/// Every doctrine decision resolves forms, action codes, transition routes,
/// regions, tile tags and objective ordering through this view, so the bot
/// never depends on a particular class arm, form name, numeric code, unit
/// count, or map. Anything the contract does not declare is simply absent and
/// the caller falls back.
/// </summary>
internal sealed class ContractView
{
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms;
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks;
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movements;
    private readonly Dictionary<string, GenericActorMapContract.Region> _regions;
    private readonly Dictionary<
        GenericActorMapContract.TileTagKind,
        HashSet<Position>> _tags;
    private readonly HashSet<string> _movementActionIds;
    private readonly Dictionary<
        GenericActorRulesContract.ActionKind,
        HashSet<string>> _actionIdsByKind;

    public ContractView(GenericActorMatchStart start)
    {
        Contract = start.Contract;
        Rules = start.Contract.Rules;
        Map = start.Contract.Map;
        MyTeamId = start.ActorId.TeamId;
        MyUnitId = start.ActorId.UnitId;
        MyParticipantId = start.ParticipantId;

        _forms = [];
        foreach (GenericActorRulesContract.Form form in Rules.Forms)
            _forms[form.Id] = form;

        _attacks = [];
        foreach (GenericActorRulesContract.AttackProfile attack
                 in Rules.AttackProfiles)
        {
            _attacks[attack.Id] = attack;
        }

        _movements = [];
        foreach (GenericActorRulesContract.MovementProfile movement
                 in Rules.MovementProfiles)
        {
            _movements[movement.Id] = movement;
        }

        _regions = [];
        foreach (GenericActorMapContract.Region region in Map.Regions)
            _regions[region.RegionId] = region;

        _tags = [];
        foreach (GenericActorMapContract.TileTag tag in Map.TileTags)
        {
            if (!_tags.TryGetValue(tag.Kind, out HashSet<Position>? tiles))
            {
                tiles = [];
                _tags[tag.Kind] = tiles;
            }
            tiles.UnionWith(tag.Tiles);
        }

        _actionIdsByKind = [];
        foreach (GenericActorRulesContract.ActionDefinition action
                 in Rules.Actions)
        {
            if (!_actionIdsByKind.TryGetValue(
                    action.Kind,
                    out HashSet<string>? ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                _actionIdsByKind[action.Kind] = ids;
            }
            ids.Add(action.Id);
        }
        _movementActionIds =
            ActionIds(GenericActorRulesContract.ActionKind.Movement);

        Frontline = Rules.GameMode
            as GenericActorRulesContract.FrontlineGameMode;
        if (Contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding binding)
        {
            ObjectiveRegionIds = binding.OrderedObjectiveRegionIds;
            GenericActorResolvedMatchContract.FrontlineTeamAdvance? advance =
                binding.TeamAdvances
                    .FirstOrDefault(entry => entry.TeamId == MyTeamId);
            AdvanceDelta = advance?.ObjectiveIndexDelta ?? 1;
        }
        else
        {
            ObjectiveRegionIds = [];
            AdvanceDelta = 1;
        }

        IsPrimeSlot = Contract.Topology.InitialLives
            .Any(life => life.TeamId == MyTeamId && life.UnitId == MyUnitId)
            || Contract.InitialDeployment.Lives
                .Any(life => life.TeamId == MyTeamId && life.UnitId == MyUnitId);

        HomeReference = ResolveSpawnReference(sameTeam: true);
        EnemyReference = ResolveSpawnReference(sameTeam: false);
        ReservedSpawnTiles = ResolveReservedSpawnTiles();
        FabricationTransition = Rules.FabricationTransitions
            .OfType<GenericActorRulesContract.BoundedChildFabricationTransition>()
            .FirstOrDefault();

        // Three fields jointly decide whether OUR OWN bolt is an obstacle; a bot
        // that reads only `ProjectilesBlockMovement` walks around its own
        // covering fire. Read the allied-contact policy rather than assume it.
        AlliedBoltsBlockUs =
            Rules.Collisions.ProjectilesBlockMovement
            && !Rules.Collisions.AlliedProjectileContact
                .Contains("pass-through", StringComparison.Ordinal);
    }

    public GenericActorResolvedMatchContract Contract { get; }
    public GenericActorRulesContract Rules { get; }
    public GenericActorMapContract Map { get; }
    public int MyTeamId { get; }
    public int MyUnitId { get; }
    public int MyParticipantId { get; }

    /// <summary>True when this stable slot holds the team's tick-zero body.</summary>
    public bool IsPrimeSlot { get; }

    public GenericActorRulesContract.FrontlineGameMode? Frontline { get; }
    public ImmutableArray<string> ObjectiveRegionIds { get; }

    /// <summary>Signed objective-index step of one advance for our team.</summary>
    public int AdvanceDelta { get; }

    /// <summary>Own deployment anchor, used only as a rear reference point.</summary>
    public Position HomeReference { get; }

    /// <summary>Opposing deployment anchor, used as the forward reference.</summary>
    public Position EnemyReference { get; }

    public GenericActorRulesContract.BoundedChildFabricationTransition?
        FabricationTransition
    { get; }

    /// <summary>
    /// Spawn anchors this team's own slots deploy and return into. The ruleset
    /// keeps them reserved against our own bodies, so a route planned through
    /// one is a route that blocks every tick. Reading them from the declared
    /// anchors and lifecycle assignments keeps that out of the pathfinder.
    /// </summary>
    public HashSet<Position> ReservedSpawnTiles { get; } = [];

    public int MaxTicks => Rules.Limits.MaxTicks;

    public int CaptureThreshold => Frontline?.Capture.Threshold ?? 1;

    public int PushesToBreach =>
        Frontline?.FrontlineVictory.PushesToBreach ?? 1;

    public int PositionCount =>
        Frontline?.FrontlinePositionCount
        ?? Math.Max(1, ObjectiveRegionIds.Length);

    public bool IsWall(Position position) =>
        position.X < 0
        || position.Y < 0
        || position.Y >= Map.TileRows.Length
        || position.X >= Map.TileRows[position.Y].Length
        || Map.TileRows[position.Y][position.X] == '#';

    public bool IsOpen(Position position) => !IsWall(position);

    public HashSet<string> ActionIds(
        GenericActorRulesContract.ActionKind kind) =>
        _actionIdsByKind.TryGetValue(kind, out HashSet<string>? ids)
            ? ids
            : new HashSet<string>(StringComparer.Ordinal);

    public GenericActorRulesContract.Form? Form(string formId) =>
        _forms.TryGetValue(formId, out GenericActorRulesContract.Form? form)
            ? form
            : null;

    public GenericActorRulesContract.AttackProfile? Attack(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form?.AttackProfileId is string id
            && _attacks.TryGetValue(
                id,
                out GenericActorRulesContract.AttackProfile? attack)
                ? attack
                : null;
    }

    /// <summary>
    /// A form is fortified when its declared action mask contains no movement
    /// action. That is the class-neutral way to recognize a turret-shaped form.
    /// </summary>
    public bool IsFortified(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && !form.AllowedActionIds.Any(_movementActionIds.Contains);
    }

    public int ObjectiveWeight(string formId) => Form(formId)?.ObjectiveWeight ?? 0;

    public int MaxHealth(string formId) => Form(formId)?.MaxHealth ?? 1;

    /// <summary>True when our own projectiles obstruct our own movement.</summary>
    public bool AlliedBoltsBlockUs { get; }

    /// <summary>
    /// How this form's movement profile couples facing to a step. An experiment
    /// arm may turn the body with the step (`face-movement-direction`) or allow
    /// movement only along the current facing (`facing-locked`); the inert
    /// default leaves facing alone. Facing is this chassis' whole firing
    /// envelope, so every doctrine step that plans a move asks this first.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && _movements.TryGetValue(
                form.MovementProfileId,
                out GenericActorRulesContract.MovementProfile? movement)
            ? movement.FacingCoupling
            : GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>The facing a body would have after stepping <paramref name="step"/>.</summary>
    public Direction FacingAfterStep(
        string formId,
        Direction facing,
        Direction step) =>
        Coupling(formId)
            == GenericActorRulesContract.MovementFacingCoupling.FaceMovementDirection
            ? step
            : facing;

    /// <summary>The declared same-life route from a mobile form into a turret.</summary>
    public GenericActorRulesContract.FormTransition? AnchorRoute(string formId) =>
        Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(route =>
                string.Equals(route.SourceFormId, formId, StringComparison.Ordinal)
                && IsFortified(route.TargetFormId))
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>The declared same-life route from a turret back to a mobile form.</summary>
    public GenericActorRulesContract.FormTransition? MobilizeRoute(string formId) =>
        Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(route =>
                string.Equals(route.SourceFormId, formId, StringComparison.Ordinal)
                && !IsFortified(route.TargetFormId))
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    public IReadOnlyList<Position> ObjectiveTiles(int index)
    {
        if (index < 0 || index >= ObjectiveRegionIds.Length)
            return [];
        return _regions.TryGetValue(
            ObjectiveRegionIds[index],
            out GenericActorMapContract.Region? region)
            ? region.Tiles
            : [];
    }

    public HashSet<Position> TilesTagged(
        GenericActorMapContract.TileTagKind kind) =>
        _tags.TryGetValue(kind, out HashSet<Position>? tiles)
            ? tiles
            : [];

    /// <summary>
    /// Tiles where our Prime may start a fabrication, taken from the transition's
    /// declared source region role and required tile tags.
    /// </summary>
    public IReadOnlyList<Position> FabricationSourceTiles()
    {
        if (FabricationTransition is not
            GenericActorRulesContract.BoundedChildFabricationTransition transition)
        {
            return [];
        }

        GenericActorResolvedMatchContract.ParticipantRegionAssignment? assignment =
            Contract.ParticipantRegionAssignments
                .FirstOrDefault(entry =>
                    entry.ParticipantId == MyParticipantId
                    && string.Equals(
                        entry.RegionRoleId,
                        transition.SourceRegionRoleId,
                        StringComparison.Ordinal));
        if (assignment is null
            || !_regions.TryGetValue(
                assignment.MapRegionId,
                out GenericActorMapContract.Region? region))
        {
            return [];
        }

        IEnumerable<Position> tiles = region.Tiles.Where(IsOpen);
        foreach (GenericActorMapContract.TileTagKind kind
                 in transition.RequiredSourceTileTags)
        {
            HashSet<Position> tagged = TilesTagged(kind);
            tiles = tiles.Where(tagged.Contains);
        }
        return tiles.ToArray();
    }

    /// <summary>
    /// Tiles on which the declared anchor route would be refused, from the
    /// transition's own forbidden-tag list rather than copied coordinates.
    /// </summary>
    public HashSet<Position> AnchorForbiddenTiles(
        GenericActorRulesContract.FormTransition route)
    {
        HashSet<Position> forbidden = [];
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.ForbiddenTileTags)
        {
            forbidden.UnionWith(TilesTagged(kind));
        }
        return forbidden;
    }

    public bool AnchorTileSatisfiesRequirements(
        GenericActorRulesContract.FormTransition route,
        Position tile)
    {
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.RequiredTileTags)
        {
            if (!TilesTagged(kind).Contains(tile))
                return false;
        }
        return true;
    }

    private HashSet<Position> ResolveReservedSpawnTiles()
    {
        var spawnIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Contract.LifecycleAssignments)
        {
            if (assignment.TeamId != MyTeamId)
                continue;
            if (assignment.AssignedRespawnSpawnId is string spawnId)
                spawnIds.Add(spawnId);
        }
        foreach (GenericActorResolvedMatchContract.InitialLifeDeployment life
                 in Contract.InitialDeployment.Lives)
        {
            if (life.TeamId == MyTeamId)
                spawnIds.Add(life.SpawnId);
        }

        HashSet<Position> tiles = [];
        foreach (GenericActorMapContract.SpawnAnchor anchor in Map.SpawnAnchors)
        {
            if (spawnIds.Contains(anchor.SpawnId))
                tiles.Add(anchor.Position);
        }
        foreach (GenericActorResolvedMatchContract.InitialSpawn spawn
                 in Contract.InitialDeployment.Spawns)
        {
            if (spawnIds.Contains(spawn.SpawnId))
                tiles.Add(spawn.Position);
        }
        return tiles;
    }

    private Position ResolveSpawnReference(bool sameTeam)
    {
        GenericActorResolvedMatchContract.InitialLifeDeployment? life =
            Contract.InitialDeployment.Lives
                .Where(entry => (entry.TeamId == MyTeamId) == sameTeam)
                .OrderBy(entry => entry.TeamId)
                .ThenBy(entry => entry.UnitId)
                .FirstOrDefault();
        if (life is not null)
        {
            GenericActorResolvedMatchContract.InitialSpawn? spawn =
                Contract.InitialDeployment.Spawns
                    .FirstOrDefault(entry =>
                        string.Equals(
                            entry.SpawnId,
                            life.SpawnId,
                            StringComparison.Ordinal));
            if (spawn is not null)
                return spawn.Position;
        }

        // No declared deployment for that side: fall back to the far end of the
        // objective order in the relevant direction.
        int index = sameTeam
            ? AdvanceDelta > 0 ? 0 : ObjectiveRegionIds.Length - 1
            : AdvanceDelta > 0 ? ObjectiveRegionIds.Length - 1 : 0;
        IReadOnlyList<Position> tiles = ObjectiveTiles(index);
        if (tiles.Count > 0)
            return tiles[0];
        return new Position(Math.Max(0, Map.Width / 2), Math.Max(0, Map.Height / 2));
    }
}
