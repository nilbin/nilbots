using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// A contract-only view of the facts the FORTRESS ROTATOR doctrine needs.
/// Every value is derived from the resolved match contract delivered to
/// StartLife: no form ID, action ID, action code, map coordinate, unlock tick,
/// team ID, or transition route is assumed to exist. Whatever is missing simply
/// yields an empty collection or <see langword="null"/>, and the policy above
/// degrades to the capabilities that are actually declared.
/// </summary>
internal sealed class ContractLens
{
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.ActionDefinition>
        _actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.FormTransition>
        _anchorRoutes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.FormTransition>
        _reverseRoutes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _staticForms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movement = new(StringComparer.Ordinal);

    public ContractLens(GenericActorMatchStart start)
    {
        Contract = start.Contract;
        Self = start.ActorId;
        ParticipantId = start.ParticipantId;
        Map = start.Contract.Map;

        GenericActorRulesContract rules = start.Contract.Rules;
        foreach (GenericActorRulesContract.Form form in rules.Forms)
            _forms[form.Id] = form;
        foreach (GenericActorRulesContract.AttackProfile attack
                 in rules.AttackProfiles)
            _attacks[attack.Id] = attack;
        foreach (GenericActorRulesContract.ActionDefinition action
                 in rules.Actions)
            _actions[action.Id] = action;
        foreach (GenericActorRulesContract.MovementProfile profile
                 in rules.MovementProfiles)
            _movement[profile.Id] = profile;

        // "Static" is a derived property, not a name: a form with no movement
        // action in its own mask cannot reposition, whatever it is called.
        foreach (GenericActorRulesContract.Form form in rules.Forms)
        {
            bool mobile = false;
            foreach (string actionId in form.AllowedActionIds)
            {
                if (_actions.TryGetValue(actionId, out var definition)
                    && definition.Kind
                        == GenericActorRulesContract.ActionKind.Movement)
                {
                    mobile = true;
                    break;
                }
            }
            if (!mobile)
                _staticForms.Add(form.Id);
        }

        // Anchor = mobile -> static; Mobilize = static -> mobile. Both are read
        // from the same-life transition catalog, so a contract that offers only
        // one of them (or neither) is handled without special cases.
        foreach (GenericActorRulesContract.SameLifeTransition transition
                 in rules.SameLifeTransitions)
        {
            if (transition is not GenericActorRulesContract.FormTransition form)
                continue;
            bool sourceStatic = _staticForms.Contains(form.SourceFormId);
            bool targetStatic = _staticForms.Contains(form.TargetFormId);
            if (!sourceStatic && targetStatic)
                _anchorRoutes.TryAdd(form.SourceFormId, form);
            else if (sourceStatic && !targetStatic)
                _reverseRoutes.TryAdd(form.SourceFormId, form);
        }

        MaxTicks = rules.Limits.MaxTicks;

        if (rules.GameMode is GenericActorRulesContract.FrontlineGameMode mode)
        {
            CaptureThreshold = mode.Capture.Threshold;
            RedeployPauseTicks = mode.Capture.RedeployPauseTicks;
            PositionCount = mode.FrontlinePositionCount;
        }

        var objectives = new List<Position[]>();
        if (start.Contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding binding)
        {
            foreach (string regionId in binding.OrderedObjectiveRegionIds)
            {
                GenericActorMapContract.Region? region = null;
                foreach (GenericActorMapContract.Region candidate in Map.Regions)
                {
                    if (string.Equals(
                            candidate.RegionId,
                            regionId,
                            StringComparison.Ordinal))
                    {
                        region = candidate;
                        break;
                    }
                }
                objectives.Add(region is null ? [] : [.. region.Tiles]);
            }
            foreach (GenericActorResolvedMatchContract.FrontlineTeamAdvance
                     advance in binding.TeamAdvances)
            {
                if (advance.TeamId == Self.TeamId)
                    AdvanceDelta = advance.ObjectiveIndexDelta;
            }
        }
        Objectives = [.. objectives];

        foreach (GenericActorMapContract.TileTag tag in Map.TileTags)
        {
            HashSet<Position>? sink = tag.Kind switch
            {
                GenericActorMapContract.TileTagKind
                    .TransitionPlacementForbidden => TransitionForbidden,
                GenericActorMapContract.TileTagKind.SpawnProtected =>
                    SpawnProtected,
                _ => null,
            };
            if (sink is null)
                continue;
            foreach (Position tile in tag.Tiles)
                sink.Add(tile);
        }

        foreach (GenericActorResolvedMatchContract.InitialLifeDeployment life
                 in Contract.InitialDeployment.Lives)
        {
            if (life.TeamId != Self.TeamId)
                continue;
            foreach (GenericActorResolvedMatchContract.InitialSpawn spawn
                     in Contract.InitialDeployment.Spawns)
            {
                if (string.Equals(
                        spawn.SpawnId,
                        life.SpawnId,
                        StringComparison.Ordinal))
                {
                    HomeAnchor ??= spawn.Position;
                }
            }
        }

        int primeUnit = int.MaxValue;
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Contract.LifecycleAssignments)
        {
            if (assignment.TeamId != Self.TeamId)
                continue;
            if (assignment.InitialAvailability
                == GenericActorResolvedMatchContract.InitialAvailability
                    .ActiveAtTickZero
                && assignment.UnitId < primeUnit)
            {
                primeUnit = assignment.UnitId;
            }
        }
        PrimeUnitId = primeUnit == int.MaxValue ? -1 : primeUnit;

        int longestReach = 0;
        int fastestCadence = int.MaxValue;
        int heaviestHit = 1;
        foreach (GenericActorRulesContract.AttackProfile attack
                 in rules.AttackProfiles)
        {
            longestReach = Math.Max(longestReach, attack.Projectile.MaxTravelTiles);
            fastestCadence = Math.Min(
                fastestCadence,
                Math.Max(1, attack.Projectile.TicksPerAdvance));
            heaviestHit = Math.Max(heaviestHit, attack.Projectile.DamagePerHit);
        }
        LongestKnownReach = longestReach;
        FastestProjectileCadence =
            fastestCadence == int.MaxValue ? 1 : fastestCadence;
        HeaviestHit = heaviestHit;
    }

    public GenericActorResolvedMatchContract Contract { get; }
    public GenericActorMapContract Map { get; }
    public ActorIdentity Self { get; }
    public int ParticipantId { get; }
    public int TeamId => Self.TeamId;
    public int MaxTicks { get; }
    public int CaptureThreshold { get; }
    public int RedeployPauseTicks { get; }
    public int PositionCount { get; }
    public int AdvanceDelta { get; }
    public int PrimeUnitId { get; }
    public int LongestKnownReach { get; }
    /// <summary>
    /// Shortest declared ticks-per-advance among all attack profiles. Observed
    /// projectiles report tiles per advance but not their owner's cadence, so
    /// the fastest declared one is the safe assumption.
    /// </summary>
    public int FastestProjectileCadence { get; }
    /// <summary>
    /// Heaviest declared damage per hit. An observed projectile does not carry
    /// its own damage, so the worst declared one is what a body about to eat a
    /// bolt has to budget for.
    /// </summary>
    public int HeaviestHit { get; }
    public Position? HomeAnchor { get; }
    public ImmutableArray<Position[]> Objectives { get; }
    public HashSet<Position> TransitionForbidden { get; } = [];
    public HashSet<Position> SpawnProtected { get; } = [];

    public GenericActorRulesContract.Form? Form(string? formId) =>
        formId is not null && _forms.TryGetValue(formId, out var form)
            ? form
            : null;

    public bool IsStatic(string? formId) =>
        formId is not null && _staticForms.Contains(formId);

    /// <summary>
    /// How this form's declared movement profile couples facing to a step.
    /// The field is optional in the canonical bytes, so an absent profile or an
    /// absent field both mean the inert <c>PreserveFacing</c> value; every
    /// arithmetic that depends on turning cost goes through this one reader.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling(
        string? formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && _movement.TryGetValue(form.MovementProfileId, out var profile)
                ? profile.FacingCoupling
                : GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>
    /// The shortest ticks in which a body of this form could hold a tile long
    /// enough for the objective on it to be worth anything: one full capture
    /// window plus the pause the mode imposes after an advance. This is the
    /// unit the fortress doctrine prices tenure in, and every term is declared.
    /// </summary>
    public int CaptureWindowTicks =>
        Math.Max(1, CaptureThreshold) + Math.Max(0, RedeployPauseTicks);

    public GenericActorRulesContract.AttackProfile? Attack(
        GenericActorRulesContract.Form? form) =>
        form?.AttackProfileId is string id
        && _attacks.TryGetValue(id, out var attack)
            ? attack
            : null;

    public int Reach(string? formId)
    {
        GenericActorRulesContract.AttackProfile? attack = Attack(Form(formId));
        return attack?.Projectile.MaxTravelTiles ?? LongestKnownReach;
    }

    public GenericActorRulesContract.FormTransition? AnchorRoute(string? formId) =>
        formId is not null && _anchorRoutes.TryGetValue(formId, out var route)
            ? route
            : null;

    public GenericActorRulesContract.FormTransition? ReverseRoute(string? formId) =>
        formId is not null && _reverseRoutes.TryGetValue(formId, out var route)
            ? route
            : null;

    public GenericActorRulesContract.ActionKind? KindOf(string actionId) =>
        _actions.TryGetValue(actionId, out var action) ? action.Kind : null;

    /// <summary>
    /// Currently available legality entries whose catalog kind matches, in
    /// canonical action-ID order. Numeric codes always come from the mask.
    /// </summary>
    public List<GenericActorActionLegality> Available(
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        var matches = new List<GenericActorActionLegality>();
        foreach (GenericActorActionLegality legality in context.ActionLegalities)
        {
            if (legality.Available && KindOf(legality.ActionId) == kind)
                matches.Add(legality);
        }
        matches.Sort(static (left, right) =>
            string.CompareOrdinal(left.ActionId, right.ActionId));
        return matches;
    }

    public Position[] ObjectiveTiles(int index) =>
        index >= 0 && index < Objectives.Length ? Objectives[index] : [];

    /// <summary>
    /// The objective index this team is trying to reach next. Used to know
    /// where the front is about to rotate to, not to pre-commit to it.
    /// </summary>
    public int NextObjectiveIndex(int activeIndex) =>
        Math.Clamp(activeIndex + AdvanceDelta, 0, Math.Max(0, Objectives.Length - 1));

    /// <summary>
    /// Fabrication source tiles for the given form, resolved through the
    /// transition's declared region role and this participant's role binding.
    /// Empty when the contract declares no fabrication for that form.
    /// </summary>
    public HashSet<Position> FabricationSourceTiles(string? formId)
    {
        var tiles = new HashSet<Position>();
        if (formId is null)
            return tiles;
        foreach (GenericActorRulesContract.FabricationTransition transition
                 in Contract.Rules.FabricationTransitions)
        {
            if (transition is not GenericActorRulesContract
                    .BoundedChildFabricationTransition bounded
                || !bounded.SourceFormIds.Contains(formId))
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
                foreach (GenericActorMapContract.Region region in Map.Regions)
                {
                    if (!string.Equals(
                            region.RegionId,
                            assignment.MapRegionId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (Position tile in region.Tiles)
                        tiles.Add(tile);
                }
            }
        }
        return tiles;
    }
}
