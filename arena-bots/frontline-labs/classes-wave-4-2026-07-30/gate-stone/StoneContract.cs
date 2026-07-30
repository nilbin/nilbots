using BotArena.Sdk;

/// <summary>
/// Everything GateStone is allowed to believe about the ruleset, read ONCE from
/// the resolved contract at <c>StartLife</c> and never guessed. The doctrine
/// asks this lens questions ("does my form have a guard route?", "how long does
/// a completed advance hold?", "may I anchor on this tile?") so the same
/// artifact plays the kit-on and kit-off arms, the bend-on and bend-off arms,
/// and the classless qualification profile without a single arm-specific
/// branch.
/// </summary>
internal sealed class StoneContract
{
    private static readonly Direction[] Cardinals =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movementProfiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.ActionDefinition>
        _actions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _movementActions =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _rotationActions =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _attackActions =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _fabricationActions =
        new(StringComparer.Ordinal);
    private readonly List<GenericActorRulesContract.FormTransition> _routes =
        new();
    private readonly HashSet<Position> _transitionForbidden = new();
    private readonly Dictionary<GenericActorMapContract.TileTagKind,
        HashSet<Position>> _tagged = new();
    private readonly Position[][] _objectives;
    private readonly Dictionary<int, int> _advanceDelta = new();

    public StoneContract(
        GenericActorResolvedMatchContract contract,
        int teamId)
    {
        Raw = contract;
        TeamId = teamId;
        Capture = ArenaBasics.Capture(contract);

        foreach (GenericActorRulesContract.Form form in contract.Rules.Forms)
            _forms[form.Id] = form;
        foreach (GenericActorRulesContract.AttackProfile attack
                 in contract.Rules.AttackProfiles)
        {
            _attacks[attack.Id] = attack;
        }
        foreach (GenericActorRulesContract.MovementProfile profile
                 in contract.Rules.MovementProfiles)
        {
            _movementProfiles[profile.Id] = profile;
        }
        foreach (GenericActorRulesContract.ActionDefinition action
                 in contract.Rules.Actions)
        {
            _actions[action.Id] = action;
            switch (action.Kind)
            {
                case GenericActorRulesContract.ActionKind.Movement:
                    _movementActions.Add(action.Id);
                    break;
                case GenericActorRulesContract.ActionKind.Rotation:
                    _rotationActions.Add(action.Id);
                    break;
                case GenericActorRulesContract.ActionKind.Attack:
                    _attackActions.Add(action.Id);
                    break;
                case GenericActorRulesContract.ActionKind.Fabrication:
                    _fabricationActions.Add(action.Id);
                    break;
                default:
                    break;
            }
        }
        foreach (GenericActorRulesContract.SameLifeTransition transition
                 in contract.Rules.SameLifeTransitions)
        {
            if (transition is GenericActorRulesContract.FormTransition route)
                _routes.Add(route);
        }
        foreach (GenericActorMapContract.TileTag tag in contract.Map.TileTags)
        {
            if (!_tagged.TryGetValue(tag.Kind, out HashSet<Position>? tiles))
            {
                tiles = new HashSet<Position>();
                _tagged[tag.Kind] = tiles;
            }
            foreach (Position tile in tag.Tiles)
            {
                tiles.Add(tile);
                if (tag.Kind
                    == GenericActorMapContract.TileTagKind
                        .TransitionPlacementForbidden)
                {
                    _transitionForbidden.Add(tile);
                }
            }
        }

        int positions =
            contract.ModeMapBinding
                is GenericActorResolvedMatchContract.FrontlineModeMapBinding
                    binding
                ? binding.OrderedObjectiveRegionIds.Length
                : 0;
        _objectives = new Position[positions][];
        for (int index = 0; index < positions; index++)
            _objectives[index] = ArenaBasics.ObjectiveTiles(contract, index);
        if (contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding chain)
        {
            foreach (GenericActorResolvedMatchContract.FrontlineTeamAdvance
                     advance in chain.TeamAdvances)
            {
                _advanceDelta[advance.TeamId] = advance.ObjectiveIndexDelta;
            }
        }

        AlliedBoltsPass = contract.Rules.Collisions.AlliedProjectileContact
            .Contains("pass-through", StringComparison.Ordinal);
        Width = contract.Map.Width;
        Height = contract.Map.Height;
    }

    /// <summary>The resolved contract itself, for the scaffold helpers.</summary>
    public GenericActorResolvedMatchContract Raw { get; }
    /// <summary>This life's scoring team.</summary>
    public int TeamId { get; }
    /// <summary>Capture policy values, or null outside an objective mode.</summary>
    public ArenaBasics.CaptureRules? Capture { get; }
    /// <summary>Whether our own bolts pass through our own bodies.</summary>
    public bool AlliedBoltsPass { get; }
    /// <summary>Map width in tiles.</summary>
    public int Width { get; }
    /// <summary>Map height in tiles.</summary>
    public int Height { get; }
    /// <summary>Number of ordered objective positions (0 outside Frontline).</summary>
    public int ObjectiveCount => _objectives.Length;

    /// <summary>Tiles of one objective in the ordered chain.</summary>
    public Position[] Objective(int index) =>
        index >= 0 && index < _objectives.Length ? _objectives[index] : [];

    /// <summary>Signed chain step one advance moves for a team.</summary>
    public int AdvanceDelta(int teamId) =>
        _advanceDelta.TryGetValue(teamId, out int delta) ? delta : 0;

    /// <summary>Whether a tile blocks bodies and consumes bolts.</summary>
    public bool IsWall(Position tile) =>
        tile.X < 0
        || tile.Y < 0
        || tile.X >= Width
        || tile.Y >= Height
        || Raw.Map.TileRows[tile.Y][tile.X] == '#';

    /// <summary>Whether any transition may complete on a tile.</summary>
    public bool TransitionAllowedOn(Position tile) =>
        !_transitionForbidden.Contains(tile);

    /// <summary>
    /// Whether ONE route may complete on a tile, by its own declared required
    /// and forbidden tags. This is the fact that reshapes a bulwark's whole
    /// plan: every objective tile on this map is transition-forbidden, so a
    /// shield can never be raised on the ground it is defending — it is raised
    /// on the shoulder beside it.
    /// </summary>
    public bool RouteAllowedOn(
        GenericActorRulesContract.FormTransition route,
        Position tile)
    {
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.ForbiddenTileTags)
        {
            if (_tagged.TryGetValue(kind, out HashSet<Position>? forbidden)
                && forbidden.Contains(tile))
            {
                return false;
            }
        }
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.RequiredTileTags)
        {
            if (!_tagged.TryGetValue(kind, out HashSet<Position>? required)
                || !required.Contains(tile))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>One form from the catalog, or null when unknown.</summary>
    public GenericActorRulesContract.Form? Form(string formId) =>
        _forms.TryGetValue(formId, out GenericActorRulesContract.Form? form)
            ? form
            : null;

    /// <summary>The attack profile a form fires through, or null.</summary>
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
    /// How this form's movement profile couples facing to a step. Absent means
    /// preserve-facing, which the SDK default already encodes — so this is a
    /// read, not an inference.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling(
        string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && _movementProfiles.TryGetValue(
                form.MovementProfileId,
                out GenericActorRulesContract.MovementProfile? profile)
            ? profile.FacingCoupling
            : GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>Objective weight a form contributes while it stands.</summary>
    public int Weight(string formId) => Form(formId)?.ObjectiveWeight ?? 0;

    /// <summary>Whether a form deflects contacts inside its facing quadrant.</summary>
    public bool Guards(string formId) =>
        Form(formId)?.ProjectileGuard
        == GenericActorRulesContract.FormProjectileGuard
            .FacingQuadrantContactsDeflected;

    /// <summary>Whether a form has no movement action at all.</summary>
    public bool Immobile(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        if (form is null)
            return false;
        foreach (string actionId in form.AllowedActionIds)
        {
            if (_movementActions.Contains(actionId))
                return false;
        }
        return true;
    }

    /// <summary>Whether a form may rotate.</summary>
    public bool CanRotate(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        if (form is null)
            return false;
        foreach (string actionId in form.AllowedActionIds)
        {
            if (_rotationActions.Contains(actionId))
                return true;
        }
        return false;
    }

    /// <summary>Whether an action ID is an attack in the catalog.</summary>
    public bool IsAttack(string actionId) => _attackActions.Contains(actionId);
    /// <summary>Whether an action ID is a movement in the catalog.</summary>
    public bool IsMovement(string actionId) =>
        _movementActions.Contains(actionId);
    /// <summary>Whether an action ID is a rotation in the catalog.</summary>
    public bool IsRotation(string actionId) =>
        _rotationActions.Contains(actionId);
    /// <summary>Whether an action ID creates lives in other slots.</summary>
    public bool IsFabrication(string actionId) =>
        _fabricationActions.Contains(actionId);

    /// <summary>Every same-life route leaving one form.</summary>
    public IEnumerable<GenericActorRulesContract.FormTransition> RoutesFrom(
        string formId)
    {
        foreach (GenericActorRulesContract.FormTransition route in _routes)
        {
            if (string.Equals(
                    route.SourceFormId,
                    formId,
                    StringComparison.Ordinal))
            {
                yield return route;
            }
        }
    }

    /// <summary>
    /// The route into a guarding stance — the aegis shell wherever it exists,
    /// and nothing at all on an arm that ships no guard. Found by the target
    /// form's declared <c>projectileGuard</c>, never by its name.
    /// </summary>
    public GenericActorRulesContract.FormTransition? GuardRoute(string formId)
    {
        foreach (GenericActorRulesContract.FormTransition route
                 in RoutesFrom(formId))
        {
            if (Guards(route.TargetFormId))
                return route;
        }
        return null;
    }

    /// <summary>
    /// The route into a fortified (objective-weight-zero) form — Anchor. Found
    /// by declared weight, so it survives a renamed turret.
    /// </summary>
    public GenericActorRulesContract.FormTransition? FortifyRoute(string formId)
    {
        foreach (GenericActorRulesContract.FormTransition route
                 in RoutesFrom(formId))
        {
            if (Weight(route.TargetFormId) == 0 && !Guards(route.TargetFormId))
                return route;
        }
        return null;
    }

    /// <summary>
    /// The parameterless return route out of a stance — the mobilize the engine
    /// also fires for us when a stance budget runs out.
    /// </summary>
    public GenericActorRulesContract.FormTransition? ReturnRoute(string formId)
    {
        foreach (GenericActorRulesContract.FormTransition route
                 in RoutesFrom(formId))
        {
            if (_actions.TryGetValue(
                    route.ActionId,
                    out GenericActorRulesContract.ActionDefinition? action)
                && action.ParameterKinds.IsEmpty)
            {
                return route;
            }
        }
        return null;
    }

    /// <summary>
    /// How many deflections a guarding form survives before the engine forces
    /// its return, or null when the stance carries no budget. Read from the
    /// return route's <c>automaticReturn</c>; absent means unbudgeted.
    /// </summary>
    public int? GuardBudget(string guardFormId)
    {
        GenericActorRulesContract.FormTransition? exit =
            ReturnRoute(guardFormId);
        return exit?.AutomaticReturn?.Threshold;
    }

    /// <summary>
    /// Tiles a body of this form may fabricate from: the map region bound to the
    /// fabrication route's declared source role for this participant, falling
    /// back to the slot's own return anchor. Empty when the contract declares no
    /// fabrication at all, which is every class arm.
    /// </summary>
    public Position[] FabricationSourceTiles(
        string formId,
        int participantId,
        int unitId)
    {
        string? role = null;
        foreach (GenericActorRulesContract.FabricationTransition transition
                 in Raw.Rules.FabricationTransitions)
        {
            if (transition
                    is GenericActorRulesContract
                        .BoundedChildFabricationTransition bounded
                && bounded.SourceFormIds.Contains(formId))
            {
                role = bounded.SourceRegionRoleId;
                break;
            }
        }
        if (role is null)
            return [];

        foreach (GenericActorResolvedMatchContract.ParticipantRegionAssignment
                 assignment in Raw.ParticipantRegionAssignments)
        {
            if (assignment.ParticipantId != participantId
                || !string.Equals(
                    assignment.RegionRoleId,
                    role,
                    StringComparison.Ordinal))
            {
                continue;
            }
            foreach (GenericActorMapContract.Region region in Raw.Map.Regions)
            {
                if (string.Equals(
                        region.RegionId,
                        assignment.MapRegionId,
                        StringComparison.Ordinal))
                {
                    return region.Tiles.ToArray();
                }
            }
        }

        foreach (GenericActorResolvedMatchContract.LifecycleAssignment slot
                 in Raw.LifecycleAssignments)
        {
            if (slot.TeamId != TeamId
                || slot.UnitId != unitId
                || slot.AssignedRespawnSpawnId is not string spawnId)
            {
                continue;
            }
            foreach (GenericActorResolvedMatchContract.InitialSpawn spawn
                     in Raw.InitialDeployment.Spawns)
            {
                if (string.Equals(
                        spawn.SpawnId,
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return [spawn.Position];
                }
            }
        }
        return [];
    }

    /// <summary>
    /// Whether a bolt arriving on <paramref name="incoming"/> lands inside the
    /// quadrant a body facing <paramref name="facing"/> protects. A quadrant is
    /// the facing octant plus its two neighbours, so a bolt coming at us from
    /// there travels the reverse of one of those three bearings.
    /// </summary>
    public static bool ArcCovers(Direction facing, ProjectileHeading incoming)
    {
        int reverse = ((int)facing.ToProjectileHeading() + 4) % 8;
        int difference = ((int)incoming - reverse + 8) % 8;
        return difference is 0 or 1 or 7;
    }

    /// <summary>Cardinal directions in canonical order.</summary>
    public static Direction[] AllCardinals => Cardinals;
}
