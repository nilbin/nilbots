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
        _guardRoutes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.FormTransition>
        _volleyRoutes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.FormTransition>
        _reverseRoutes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _staticForms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movement = new(StringComparer.Ordinal);
    private readonly Dictionary<
        GenericActorMapContract.TileTagKind,
        HashSet<Position>> _tagged = [];
    private readonly Dictionary<string, int> _rebuildDelay =
        new(StringComparer.Ordinal);

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

        // WAVE 4, REPAIR THAT WAS ALMOST A LOST MATCH. Revision 3 classified a
        // same-life route by ONE derived property — "the target form has no
        // movement action, so it is a fortress" — and that was exactly right
        // for as long as the only immobile form in the catalog was the turret.
        // A skill kit adds a SECOND immobile form which is not a fortress at
        // all: the guard stance keeps objective weight, carries no gun, cannot
        // rotate, and spends a declared budget. Under the old derivation both
        // routes landed in the same dictionary and `TryAdd` picked whichever
        // the catalog happened to list first, so the doctrine's one-use
        // "fortify" route was decided by collection order.
        //
        // So routes are now classified by what the TARGET FORM DECLARES, which
        // is the only thing that generalises to a stance nobody has invented
        // yet:
        //
        //   fortified — objective weight zero. This is the turret bargain in
        //               its contract form: the body leaves every capture and
        //               contest count. It is the property the doctrine prices,
        //               not the name and not the immobility.
        //   guarded   — declares a projectile guard. Ground-keeping armour:
        //               weight is retained, so it is the opposite trade.
        //   volley    — the target's attack profile launches more than one
        //               projectile per action.
        //
        // A form can in principle be several of these; each dictionary is
        // keyed independently so no classification steals another's route.
        foreach (GenericActorRulesContract.SameLifeTransition transition
                 in rules.SameLifeTransitions)
        {
            if (transition is not GenericActorRulesContract.FormTransition form)
                continue;
            GenericActorRulesContract.Form? target = Form(form.TargetFormId);
            GenericActorRulesContract.Form? source = Form(form.SourceFormId);
            if (target is null || source is null)
                continue;

            bool targetFortified = target.ObjectiveWeight <= 0;
            bool targetGuarded = target.ProjectileGuard
                != GenericActorRulesContract.FormProjectileGuard.None;
            bool targetVolley = Attack(target)?.Volley is not null;
            bool sourceStance = source.ObjectiveWeight <= 0
                || source.ProjectileGuard
                    != GenericActorRulesContract.FormProjectileGuard.None
                || Attack(source)?.Volley is not null;

            if (targetFortified && !sourceStance)
                _anchorRoutes.TryAdd(form.SourceFormId, form);
            if (targetGuarded && !sourceStance)
                _guardRoutes.TryAdd(form.SourceFormId, form);
            if (targetVolley && !sourceStance)
                _volleyRoutes.TryAdd(form.SourceFormId, form);
            if (sourceStance && !targetFortified && !targetGuarded
                && !targetVolley)
            {
                _reverseRoutes.TryAdd(form.SourceFormId, form);
            }
        }

        MaxTicks = rules.Limits.MaxTicks;

        if (rules.GameMode is GenericActorRulesContract.FrontlineGameMode mode)
        {
            CaptureThreshold = mode.Capture.Threshold;
            RedeployPauseTicks = mode.Capture.RedeployPauseTicks;
            PositionCount = mode.FrontlinePositionCount;
        }

        // The structural counterweights. Every one of them is a capture- or
        // lifecycle-policy field that leaves the observation schema, the action
        // catalog, and every class stat untouched — so these four reads are the
        // entire difference between the arms, and a doctrine that skips them
        // simply plays the wrong game without ever seeing an error.
        ArenaBasics.CaptureRules? capture = ArenaBasics.Capture(Contract);
        HoldTicks = capture?.HoldTicks ?? 0;
        SurplusWeightScalesGain = capture?.SurplusWeightScalesGain ?? false;
        OnlyEnemySolePresenceDecays =
            capture?.OnlyEnemySolePresenceDecays ?? false;
        RalliesForward = ArenaBasics.ArrivalsRallyForward(Contract);

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

        // Every tag kind the map declares, kept by kind rather than folded into
        // two named sets. WAVE 5: this is the difference between a doctrine that
        // reads the rules and one that remembers them. The map still publishes
        // 112 `transition-placement-forbidden` tiles on the arm this revision
        // was written for — and every same-life route's own
        // `placement.forbiddenTileTags` is EMPTY, so not one of those tiles
        // forbids anything. The tag is the map's vocabulary; the ROUTE decides
        // which words bind. Revision 4 consulted the map, so it declined every
        // stance and every root on the whole scoring surface and the central
        // corridor on a contract that permitted all of them.
        foreach (GenericActorMapContract.TileTag tag in Map.TileTags)
        {
            if (!_tagged.TryGetValue(tag.Kind, out HashSet<Position>? sink))
            {
                sink = [];
                _tagged[tag.Kind] = sink;
            }
            foreach (Position tile in tag.Tiles)
                sink.Add(tile);

            HashSet<Position>? named = tag.Kind switch
            {
                GenericActorMapContract.TileTagKind
                    .TransitionPlacementForbidden => TransitionForbidden,
                GenericActorMapContract.TileTagKind.SpawnProtected =>
                    SpawnProtected,
                _ => null,
            };
            if (named is null)
                continue;
            foreach (Position tile in tag.Tiles)
                named.Add(tile);
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

        // Rebuild economies are DECLARED, and the declaration moved. A tuning
        // variant of somebody else's skill can change how fast the other side's
        // bodies come back without changing one action or form — so "how
        // renewable are those numbers" is a lifecycle-profile read, per slot,
        // for both sides. Never the unlock ticks either: they are on the
        // assignment (and the observation reports the live due tick), which is
        // why nothing here counts in 120s or 60s.
        foreach (GenericActorRulesContract.LifecycleProfile profile
                 in rules.Lifecycle.Profiles)
        {
            _rebuildDelay[profile.ProfileId] = profile.DelayTicks;
        }

        int primeUnit = int.MaxValue;
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Contract.LifecycleAssignments)
        {
            bool renewable = assignment.InitialAvailability
                != GenericActorResolvedMatchContract.InitialAvailability
                    .ActiveAtTickZero
                && _rebuildDelay.TryGetValue(
                    assignment.LifecycleProfileId,
                    out int delay);
            if (assignment.TeamId != Self.TeamId)
            {
                if (renewable)
                {
                    EnemyRebuildTicks = Math.Min(
                        EnemyRebuildTicks,
                        _rebuildDelay[assignment.LifecycleProfileId]);
                }
                continue;
            }
            if (renewable)
            {
                OwnRebuildTicks = Math.Min(
                    OwnRebuildTicks,
                    _rebuildDelay[assignment.LifecycleProfileId]);
            }
            if (assignment.InitialAvailability
                == GenericActorResolvedMatchContract.InitialAvailability
                    .ActiveAtTickZero
                && assignment.UnitId < primeUnit)
            {
                primeUnit = assignment.UnitId;
            }
        }
        PrimeUnitId = primeUnit == int.MaxValue ? -1 : primeUnit;

        // NEVER HARD-CODE THREE SLOTS. A skill arm may give one side five and
        // leave the other on three, so the two counts are separate facts and
        // both come from the topology's own slot list. Revision 3 never used a
        // slot count at all, which is why it never noticed it would have been
        // wrong.
        int ownSlots = 0;
        int enemySlots = 0;
        foreach (PublicUnitSlot slot in Contract.Topology.UnitSlots)
        {
            if (slot.TeamId == Self.TeamId)
                ownSlots++;
            else
                enemySlots++;
        }
        OwnSlotCount = ownSlots;
        EnemySlotCount = enemySlots;

        // Both declared classes are public in the topology, and the ONLY
        // correct way to read them: a form-ID prefix is a naming convention,
        // not a contract field, and the class is published on the team, the
        // participant, and every visible body.
        foreach (PublicScoringTeam team in Contract.Topology.Teams)
        {
            if (team.TeamId == Self.TeamId)
                OwnClassId = team.ClassId;
            else
                EnemyClassId ??= team.ClassId;
        }

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

    /// <summary>
    /// Shortest declared rebuild delay on this team's renewable slots, or
    /// <see cref="int.MaxValue"/> when nothing renews. Read from the lifecycle
    /// profile the slot is assigned, never from a number in a table: a tuning
    /// variant can change this without touching an action or a form.
    /// </summary>
    public int OwnRebuildTicks { get; private set; } = int.MaxValue;

    /// <summary>
    /// The same for the opposition. Together with <see cref="EnemySlotCount"/>
    /// this is what "numbers" means as a declared fact rather than a class name:
    /// more slots that come back faster is a side that can afford to walk into a
    /// gun, and an immobile stance in front of it is the thing being afforded.
    /// </summary>
    public int EnemyRebuildTicks { get; private set; } = int.MaxValue;

    /// <summary>Stable unit slots this team actually has, from the topology.</summary>
    public int OwnSlotCount { get; }

    /// <summary>
    /// Stable unit slots the opposition has. Asymmetric slot arms exist, so the
    /// two counts are read separately and neither is assumed to be three.
    /// </summary>
    public int EnemySlotCount { get; }

    /// <summary>Declared chassis class, or null on a classless contract.</summary>
    public string? OwnClassId { get; }

    /// <summary>The opposition's declared chassis class, if any.</summary>
    public string? EnemyClassId { get; }

    public ImmutableArray<Position[]> Objectives { get; }
    public HashSet<Position> TransitionForbidden { get; } = [];
    public HashSet<Position> SpawnProtected { get; } = [];

    /// <summary>
    /// Ticks a completed advance is protected against being pushed back; zero
    /// when the capture definition declares no hold and the front can come
    /// straight back.
    /// </summary>
    public int HoldTicks { get; }

    /// <summary>
    /// True when net objective weight scales capture pressure, so a second body
    /// on the surface is worth more than the first body's presence — and a body
    /// converted into a zero-weight gun is worth measurably less than it is
    /// under binary control, where one body nulls any number.
    /// </summary>
    public bool SurplusWeightScalesGain { get; }

    /// <summary>
    /// True when only an enemy standing alone erodes a claim, so leaving an
    /// objective is cheap and contesting one is a full stop.
    /// </summary>
    public bool OnlyEnemySolePresenceDecays { get; }

    /// <summary>
    /// True when automatic returns and activations are placed by the objective
    /// chain rather than at the slot's spawn anchor.
    /// </summary>
    public bool RalliesForward { get; }

    /// <summary>
    /// Where this slot's next automatic arrival is expected: the own-side
    /// chain-adjacent objective when the contract rallies arrivals forward,
    /// otherwise the slot's declared spawn anchor.
    /// </summary>
    public Position[] ArrivalTiles(int unitId, int activeIndex) =>
        ArenaBasics.ExpectedArrivalTiles(Contract, TeamId, unitId, activeIndex);

    /// <summary>
    /// True when dying puts this slot materially closer to the fight than its
    /// authored spawn would — a geometric comparison against the chain, not a
    /// policy name, so it stays right on a contract that rallies arrivals to
    /// somewhere unhelpful. When it holds, a body is a renewable asset and
    /// ground is worth trading health for; when it does not, a death is a long
    /// walk and the body is the scarce thing.
    /// </summary>
    public bool ForwardReturn(int unitId, int activeIndex)
    {
        Position[] active = ObjectiveTiles(activeIndex);
        if (active.Length == 0 || !RalliesForward)
            return false;
        Position[] arrival = ArrivalTiles(unitId, activeIndex);
        if (arrival.Length == 0)
            return false;
        int forward = int.MaxValue;
        foreach (Position tile in arrival)
        {
            forward = Math.Min(
                forward,
                ArenaGeometry.NearestDistance(tile, active));
        }
        int home = HomeAnchor is Position anchor
            ? ArenaGeometry.NearestDistance(anchor, active)
            : forward;
        return forward < home;
    }

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

    /// <summary>
    /// The route from this form into a stance that declares a projectile guard,
    /// or null when the contract declares none for it. The guard trade is the
    /// opposite of the fortress trade: weight is kept, the gun is given up.
    /// </summary>
    public GenericActorRulesContract.FormTransition? GuardRoute(string? formId) =>
        formId is not null && _guardRoutes.TryGetValue(formId, out var route)
            ? route
            : null;

    /// <summary>
    /// The route from this form into a stance whose gun launches more than one
    /// projectile per action. Null on every chassis that owns no such skill —
    /// which is most of them, and this doctrine's own.
    /// </summary>
    public GenericActorRulesContract.FormTransition? VolleyRoute(string? formId) =>
        formId is not null && _volleyRoutes.TryGetValue(formId, out var route)
            ? route
            : null;

    public GenericActorRulesContract.FormTransition? ReverseRoute(string? formId) =>
        formId is not null && _reverseRoutes.TryGetValue(formId, out var route)
            ? route
            : null;

    /// <summary>
    /// Is this tile a legal place for that route to COMPLETE?
    ///
    /// <para>THE READ REVISION 4 DID NOT MAKE, and the reason it declined every
    /// stance and every root on a third of the walkable map. A route declares its
    /// own placement legality — required tags, forbidden tags — and the map
    /// declares tag vocabulary. Revision 4 asked the map: "is this tile tagged
    /// transition-placement-forbidden?" On the arm this revision is written for
    /// the map still tags 112 tiles that way and every route's
    /// <c>forbiddenTileTags</c> is EMPTY, so the tag binds nothing: the whole
    /// scoring surface and the central corridor are legal. Asking the route is
    /// also right on every stricter arm, where the tag reappears in the route's
    /// own list and this returns exactly what revision 4 returned.</para>
    /// </summary>
    public bool PlacementAllows(
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

    /// <summary>
    /// True when this route can be undone by the same life: the declaration says
    /// so and a route back actually exists.
    ///
    /// <para>The field reads backwards on purpose — <c>irreversibleForLife</c>
    /// false means reversible — and it is the single most consequential boolean
    /// in the contract for this class. Revision 4 was authored against a turret
    /// that could be left exactly once per life, so it priced a root as a SALE:
    /// the whole tenure gate exists to make sure a body only gives its objective
    /// weight away when somebody else's presence can convert the gun into
    /// territory before the life ends. When the route is reversible the root is a
    /// RENTAL, the price is two windups instead of a life, and a gate built for
    /// a sale is refusing a deal that is no longer being offered.</para>
    /// </summary>
    public bool Reversible(GenericActorRulesContract.FormTransition? route) =>
        route is not null
        && !route.IrreversibleForLife
        && ReverseRoute(route.TargetFormId) is not null;

    /// <summary>
    /// Health this life would have after completing that route, from the route's
    /// own declared transfer policy and the two forms' declared maxima.
    ///
    /// <para>Three policies exist in the wild and the doctrine must price all of
    /// them, because they behave in opposite directions. <c>preserve-current</c>
    /// keeps the number and clamps it. A flat gain heals on entry. A
    /// <c>preserve-ratio</c> policy keeps the FRACTION, which on a route into a
    /// tougher form raises absolute health and on the way back lowers it — and
    /// the declared floor of one means a transform never kills.</para>
    /// </summary>
    public int HealthAfter(
        GenericActorRulesContract.FormTransition route,
        int currentHealth)
    {
        int sourceMax = Math.Max(1, Form(route.SourceFormId)?.MaxHealth ?? 1);
        int targetMax = Math.Max(1, Form(route.TargetFormId)?.MaxHealth ?? 1);
        int health = Math.Max(0, currentHealth);
        GenericActorRulesContract.SameLifeHealth policy = route.Health;

        int mapped;
        if (policy.Policy.Contains("ratio", StringComparison.Ordinal))
        {
            mapped = (int)((long)health * targetMax / sourceMax);
            if (policy.PreserveRatioFormula.Contains(
                    "minimum-one",
                    StringComparison.Ordinal)
                || policy.Policy.Contains(
                    "minimum-one",
                    StringComparison.Ordinal))
            {
                mapped = Math.Max(1, mapped);
            }
        }
        else if (policy.FlatHealthGain != 0)
        {
            mapped = health + policy.FlatHealthGain;
        }
        else
        {
            mapped = health;
        }
        return Math.Clamp(mapped, 0, targetMax);
    }

    /// <summary>
    /// Health lost by going out and coming straight back at
    /// <paramref name="currentHealth"/>. Zero is a free cycle; anything else is
    /// the price of renting a form at less than full health, and it is charged
    /// EVERY round trip, so a doctrine that cycles on a whim grinds itself down.
    /// Null when there is no way back at all.
    /// </summary>
    public int? RoundTripCost(
        GenericActorRulesContract.FormTransition? route,
        int currentHealth)
    {
        if (route is null)
            return null;
        GenericActorRulesContract.FormTransition? back =
            ReverseRoute(route.TargetFormId);
        if (back is null)
            return null;
        int outbound = HealthAfter(route, currentHealth);
        return Math.Max(0, currentHealth - HealthAfter(back, outbound));
    }

    /// <summary>
    /// True when this form declares a projectile guard: a hostile bolt arriving
    /// inside its facing quadrant dies on the arc and a NEW bolt launches from
    /// the guard's tile along the exactly reversed heading, owned by the
    /// guard's team. Two consequences the doctrine prices separately — poking
    /// one head-on shoots you, and a guard standing on the scoring surface is
    /// objective weight that frontal fire cannot remove.
    /// </summary>
    public bool IsGuarded(string? formId) =>
        Form(formId)?.ProjectileGuard
            is GenericActorRulesContract.FormProjectileGuard
                .FacingQuadrantContactsDeflected;

    /// <summary>
    /// True when a form has left every capture and contest count — the turret
    /// bargain stated as the contract states it. Read this rather than
    /// immobility: a guard stance is immobile and still holds ground.
    /// </summary>
    public bool IsFortified(string? formId) =>
        Form(formId) is { ObjectiveWeight: <= 0 };

    /// <summary>Whether this form declares a gun at all.</summary>
    public bool IsArmed(string? formId) => Attack(Form(formId)) is not null;

    /// <summary>
    /// Projectiles one accepted attack launches from this form. One on every
    /// ordinary gun; canonical contracts omit the volley field entirely for
    /// that case, so an absent field is a real answer.
    /// </summary>
    public int ProjectilesPerAttack(string? formId) =>
        Attack(Form(formId))?.ProjectilesPerAttack ?? 1;

    /// <summary>
    /// The declared budget that ends a stance without an action, or null when
    /// the engine never starts this route by itself. Canonical contracts omit
    /// the property on every route with no budget, so absent means "this
    /// stance runs until I leave it".
    /// </summary>
    public GenericActorRulesContract.AutomaticReturnTrigger? ReturnBudget(
        string? formId) =>
        ReverseRoute(formId)?.AutomaticReturn;

    /// <summary>
    /// Windup declared by the route out of this stance, plus the windup back
    /// in: the punish window a broken stance pays, and the price of one cycle.
    /// Both come from the routes, never from the two numbers in a table.
    /// </summary>
    public int CycleCost(string? stanceFormId, string? mobileFormId)
    {
        GenericActorRulesContract.FormTransition? exit =
            ReverseRoute(stanceFormId);
        GenericActorRulesContract.FormTransition? entry =
            GuardRoute(mobileFormId) ?? VolleyRoute(mobileFormId);
        return Math.Max(1, exit?.Windup.DurationTicks ?? 1)
            + Math.Max(1, entry?.Windup.DurationTicks ?? 1);
    }

    /// <summary>
    /// Idle ticks between two shots from this form's declared gun. The gun's
    /// cadence is the doctrine's spare-time budget: a stance whose entry and
    /// exit windups fit inside it costs no shots at all.
    /// </summary>
    public int FireIdleTicks(string? formId) =>
        Math.Max(0, Attack(Form(formId))?.CooldownTicks ?? 0);

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
