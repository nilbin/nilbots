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
    private readonly Dictionary<string, GenericActorRulesContract.LifecycleProfile>
        _lifecycleProfiles = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Team, int Unit),
        GenericActorResolvedMatchContract.LifecycleAssignment> _slots = new();

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

        foreach (GenericActorRulesContract.LifecycleProfile profile
                 in contract.Rules.Lifecycle.Profiles)
        {
            _lifecycleProfiles[profile.ProfileId] = profile;
        }
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment slot
                 in contract.LifecycleAssignments)
        {
            _slots[(slot.TeamId, slot.UnitId)] = slot;
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

    /// <summary>Maximum health a form's life may hold.</summary>
    public int MaxHealth(string formId) => Form(formId)?.MaxHealth ?? 1;

    /// <summary>Ticks between shots for the gun a form fires, or 0 when unarmed.</summary>
    public int FireInterval(string formId) => Attack(formId)?.CooldownTicks ?? 0;

    /// <summary>Whether a form carries a gun at all — the deadlock question.</summary>
    public bool Armed(string formId) => Attack(formId) is not null;

    /// <summary>
    /// Health a life would carry through one same-life route, computed from the
    /// route's DECLARED transfer policy rather than from any remembered rule.
    /// The open arm replaced the old flat entry heal with a proportional map, so
    /// this is the number the whole fortify cycle is priced on. An unrecognized
    /// policy is treated as "no gain", which makes the caller conservative
    /// instead of wrong.
    /// </summary>
    public int HealthThrough(
        GenericActorRulesContract.FormTransition route,
        int current)
    {
        int sourceMax = Math.Max(MaxHealth(route.SourceFormId), 1);
        int targetMax = Math.Max(MaxHealth(route.TargetFormId), 1);
        GenericActorRulesContract.SameLifeHealth health = route.Health;
        int mapped = health.Policy switch
        {
            "preserve-ratio-floor-minimum-one" =>
                Math.Max(1, (int)((long)current * targetMax / sourceMax)),
            "preserve-current-capped-to-target-maximum" => current,
            _ when health.FlatHealthGain != 0 => current + health.FlatHealthGain,
            _ => current,
        };
        return Math.Clamp(mapped, 1, targetMax);
    }

    /// <summary>
    /// Health this life would come back with after going out and returning —
    /// the round trip an unlimited anchor/mobilize cycle actually costs. A
    /// proportional map with a floor is lossless only at the ends of the scale:
    /// a full body cycles for free (4/4 to 7/7 and back), and every partial
    /// value pays the floor once per direction. So "may I cycle?" is one
    /// subtraction, not a belief.
    /// </summary>
    public int HealthRoundTrip(
        GenericActorRulesContract.FormTransition out_,
        GenericActorRulesContract.FormTransition back,
        int current) =>
        HealthThrough(back, HealthThrough(out_, current));

    /// <summary>
    /// Ticks a destroyed body of one slot stays off the board, and whether it
    /// even returns by itself. This is the price tag on a kill: a body worth one
    /// objective weight that is gone for N ticks is N points of capture progress
    /// the owner never collects, which is the only currency a turret earns in.
    /// </summary>
    public (int DelayTicks, bool ReturnsByItself) Absence(int teamId, int unitId)
    {
        if (!_slots.TryGetValue(
                (teamId, unitId),
                out GenericActorResolvedMatchContract.LifecycleAssignment? slot)
            || !_lifecycleProfiles.TryGetValue(
                slot.LifecycleProfileId,
                out GenericActorRulesContract.LifecycleProfile? profile))
        {
            return (0, true);
        }
        return (
            profile.DelayTicks,
            profile.AutomaticReturnFormId is not null);
    }

    /// <summary>
    /// Bodies a team can ever field at once, counted from its own declared unit
    /// slots. This is THE BODY CURVE, and it decides how much relief the fortify
    /// gate is allowed to demand: a class that fields four slots against our
    /// three will out-weigh us in two long windows however we play, and a gate
    /// that insists on two spare bodies before it will pick up the faster gun
    /// simply never fires it in the cell where it is most needed.
    /// </summary>
    public int SlotCount(int teamId)
    {
        int count = 0;
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment slot
                 in Raw.LifecycleAssignments)
        {
            if (slot.TeamId == teamId)
                count++;
        }
        return count;
    }

    /// <summary>Largest slot count fielded by any team that is not ours.</summary>
    public int OpposingSlotCount()
    {
        int most = 0;
        var seen = new HashSet<int>();
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment slot
                 in Raw.LifecycleAssignments)
        {
            if (slot.TeamId != TeamId && seen.Add(slot.TeamId))
                most = Math.Max(most, SlotCount(slot.TeamId));
        }
        return most;
    }

    /// <summary>
    /// Ticks until this team's body count can next grow — the relief clock. A
    /// dormant slot's declared unlock is the honest answer; a slot that must be
    /// fabricated explicitly may never arrive at all, so it is not relief.
    /// </summary>
    public int TicksToRelief(
        int teamId,
        int tick,
        IEnumerable<GenericActorContext.ObservedUnitSlot> observed)
    {
        bool canFabricate = !Raw.Rules.FabricationTransitions.IsEmpty;
        int best = int.MaxValue;
        foreach (GenericActorContext.ObservedUnitSlot slot in observed)
        {
            if (slot.TeamId != teamId)
                continue;
            int? due = slot.State switch
            {
                GenericActorContext.UnitSlotState.AvailabilityPending pending =>
                    pending.DueTick,
                GenericActorContext.UnitSlotState.AutomaticReturnPending returning
                    => returning.DueTick,
                GenericActorContext.UnitSlotState.LifecyclePending queued =>
                    queued.DueTick,
                GenericActorContext.UnitSlotState.Ready when canFabricate => tick,
                _ => null,
            };
            if (due is int arrival)
                best = Math.Min(best, Math.Max(arrival - tick, 0));
        }
        return best;
    }

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
