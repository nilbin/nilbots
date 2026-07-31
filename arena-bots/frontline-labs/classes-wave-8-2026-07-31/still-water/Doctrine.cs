using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Everything Still Water believes about the match, resolved once per life from
/// <see cref="GenericActorResolvedMatchContract"/>. No identifier, count, tick,
/// range, or coordinate below is written into the source: the front axis comes
/// from the mode/map binding, the capture arithmetic from the mode definition,
/// and both chassis profiles from the form/attack catalogs.
/// </summary>
internal sealed class Doctrine
{
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms;
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks;
    private readonly Dictionary<string, GenericActorRulesContract.VisionProfile>
        _visions;
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movements;
    private readonly Dictionary<string, Position[]> _regionTiles;
    private readonly Dictionary<string, GenericActorRulesContract.ActionKind>
        _actionKinds;
    private readonly Dictionary<GenericActorMapContract.TileTagKind,
        HashSet<Position>> _tagged;
    private readonly Dictionary<(int TeamId, int UnitId), SlotEconomy> _slots;
    private readonly ArenaBasics.CaptureRules? _captureRules;
    private HashSet<string> _creationForms = [];

    public Doctrine(GenericActorMatchStart start)
    {
        Contract = start.Contract;
        TeamId = start.ActorId.TeamId;
        UnitId = start.ActorId.UnitId;
        Field = new Field(Contract.Map);

        _forms = Contract.Rules.Forms
            .ToDictionary(form => form.Id, StringComparer.Ordinal);
        _attacks = Contract.Rules.AttackProfiles
            .ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        _visions = Contract.Rules.VisionProfiles
            .ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        _movements = Contract.Rules.MovementProfiles
            .ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        _regionTiles = Contract.Map.Regions
            .ToDictionary(
                region => region.RegionId,
                region => region.Tiles.ToArray(),
                StringComparer.Ordinal);
        _actionKinds = Contract.Rules.Actions
            .ToDictionary(
                action => action.Id,
                action => action.Kind,
                StringComparer.Ordinal);

        // Tile tags, indexed BY KIND rather than flattened into one refusal set.
        // Wave 4 collapsed them the moment it read them, which quietly welded a
        // map fact to a rules fact: it asked "is this tile tagged
        // transition-placement-forbidden?" and treated the answer as "may a
        // stance rise here?". Those are two different questions, and a ground arm
        // that empties a route's own forbidden-tag list answers them differently
        // — the tag stays on 112 tiles of this map while no route refuses any of
        // them. The tags are indexed here; only a ROUTE decides what they mean.
        _tagged = [];
        foreach (var tag in Contract.Map.TileTags)
        {
            if (!_tagged.TryGetValue(tag.Kind, out HashSet<Position>? tiles))
            {
                tiles = [];
                _tagged[tag.Kind] = tiles;
            }
            foreach (Position tile in tag.Tiles)
                tiles.Add(tile);
        }

        _slots = ResolveSlotEconomy();

        ObjectiveTiles = ResolveObjectiveTiles();
        IndexDelta = ResolveIndexDelta();
        MaxTicks = Contract.Rules.Limits.MaxTicks;

        // The structural counterweights are policy IDs and one optional
        // integer, none of which changes the observation schema. Reading them
        // is the only way to tell a mean-reverting frontline from a ratcheted
        // one, and the scaffold already knows how to spell each policy.
        _captureRules = ArenaBasics.Capture(Contract);
        RallyForward = ArenaBasics.ArrivalsRallyForward(Contract);

        (Forward, Lateral) = ResolveAxes();
        EnemyApproachOrder =
        [
            ToDirection(-Forward.Dx, -Forward.Dy),
            ToDirection(Lateral.Dx, Lateral.Dy),
            ToDirection(-Lateral.Dx, -Lateral.Dy),
            ToDirection(Forward.Dx, Forward.Dy),
        ];
        (OwnFormIds, OpposingFormIds) = ResolveFormOwnership();
        _creationForms = ResolveCreationForms();
        Stances = new StanceBook(
            Contract,
            Form,
            AttackProfile,
            ActionKind,
            ActionTakesFormTarget,
            ActionIsParameterless,
            IsCreationForm);

        // Both teams' classes are declared, and both are public. Read them;
        // never recover a class by splitting a form ID. The doctrine still
        // conditions on stats and routes — the class is used to recognise a
        // mirror and to label decisions in the replay.
        OwnClassId = ClassOfTeam(TeamId);
        OpposingClassId = Contract.Topology.Teams
            .Where(team => team.TeamId != TeamId)
            .Select(team => team.ClassId)
            .FirstOrDefault(id => id is not null)
            ?? Contract.Topology.Participants
                .Where(participant => participant.TeamId != TeamId)
                .Select(participant => participant.ClassId)
                .FirstOrDefault(id => id is not null);
        Mirror = OwnClassId is not null
            && string.Equals(OwnClassId, OpposingClassId, StringComparison.Ordinal);

        OwnSlotCount = Contract.Topology.UnitSlots
            .Count(slot => slot.TeamId == TeamId);
        EnemySlotCount = Contract.Topology.UnitSlots
            .Count(slot => slot.TeamId != TeamId);

        OwnMaxRange = MaxRange(OwnFormIds, mobileOnly: false);
        OwnCanBend = AnyBend(OwnFormIds);
        OpposingMobileRange = MaxRange(OpposingFormIds, mobileOnly: true);
        OpposingAnyRange = MaxRange(OpposingFormIds, mobileOnly: false);
        OpposingMobileVision = MaxVision(OpposingFormIds, mobileOnly: true);
        OwnMobileVision = MaxVision(OwnFormIds, mobileOnly: true);
        OpposingCanBend = AnyBend(OpposingFormIds);
        OpposingMaxHealth = OpposingFormIds
            .Select(id => _forms.TryGetValue(id, out var form) ? form.MaxHealth : 1)
            .DefaultIfEmpty(1)
            .Max();
        OpposingWorstBolt = OpposingFormIds
            .Select(id => Attack(id)?.Projectile.DamagePerHit ?? 1)
            .DefaultIfEmpty(1)
            .Max();

        ForkReach = ResolveForkReach();
        StandBand = ResolveStandBand();

        UnitRank = Contract.Topology.UnitSlots
            .Where(slot => slot.TeamId == TeamId)
            .Select(slot => slot.UnitId)
            .OrderBy(unitId => unitId)
            .ToList()
            .IndexOf(UnitId);
        if (UnitRank < 0)
            UnitRank = 0;
        LateralBias = (((UnitRank + 1) / 2) * 2)
            * ((UnitRank % 2) == 1 ? -1 : 1);
    }

    public GenericActorResolvedMatchContract Contract { get; }

    public Field Field { get; }

    public int TeamId { get; }

    public int UnitId { get; }

    /// <summary>Ordered objective regions, resolved through the mode binding.</summary>
    public ImmutableArray<Position[]> ObjectiveTiles { get; }

    /// <summary>Signed step through objective indices that advances my team.</summary>
    public int IndexDelta { get; }

    public int MaxTicks { get; }

    /// <summary>Map-space cardinal vector pointing at the enemy base.</summary>
    public (int Dx, int Dy) Forward { get; }

    /// <summary>Map-space cardinal vector perpendicular to <see cref="Forward"/>.</summary>
    public (int Dx, int Dy) Lateral { get; }

    /// <summary>
    /// Search order used when predicting where an enemy body walks next. It is
    /// derived from the contract's own front axis — their advance first, ours
    /// last — rather than from an absolute compass, so the prediction carries no
    /// systematic side bias on a mirror-symmetric map.
    /// </summary>
    public Direction[] EnemyApproachOrder { get; }

    public ImmutableArray<string> OwnFormIds { get; }

    public ImmutableArray<string> OpposingFormIds { get; }

    public int OwnMaxRange { get; }

    public bool OwnCanBend { get; }

    public int OpposingMobileRange { get; }

    public int OpposingAnyRange { get; }

    public int OpposingMobileVision { get; }

    /// <summary>
    /// How far this chassis SEES while it can still walk. A doctrine that only
    /// fires at bodies a prediction names is bounded by this rather than by its
    /// gun, which is what makes the gap between the two a purchasable thing.
    /// </summary>
    public int OwnMobileVision { get; }

    public bool OpposingCanBend { get; }

    public int OpposingMaxHealth { get; }

    /// <summary>
    /// Worst damage any single bolt in the opposing catalog declares. Health is
    /// spent in hits, not in points, so this is the denominator every
    /// survivability question divides by.
    /// </summary>
    public int OpposingWorstBolt { get; }

    /// <summary>
    /// The range at which a single programmed bend covers the widest contiguous
    /// lateral band. For a one-bend envelope that is one tile past the latest
    /// legal bend point.
    /// </summary>
    public int ForkReach { get; }

    /// <summary>Preferred engagement distance: the still water this bot holds.</summary>
    public int StandBand { get; }

    /// <summary>Position of this unit slot inside its team's stable ordering.</summary>
    public int UnitRank { get; }

    /// <summary>Lateral station offset that keeps allied bodies off one line.</summary>
    public int LateralBias { get; }

    /// <summary>Reversible stances this contract declares, indexed by source form.</summary>
    public StanceBook Stances { get; }

    /// <summary>This team's declared chassis class, or null on a classless contract.</summary>
    public string? OwnClassId { get; }

    /// <summary>The opposing team's declared chassis class, if any.</summary>
    public string? OpposingClassId { get; }

    /// <summary>Whether both sides declare the same class.</summary>
    public bool Mirror { get; }

    /// <summary>Stable slots this team owns — never assumed to be three.</summary>
    public int OwnSlotCount { get; }

    /// <summary>
    /// Stable slots the opposition owns. An asymmetric-slot arm gives one side
    /// more bodies, which is a reason to expect pressure from more bearings at
    /// once rather than a reason to match it.
    /// </summary>
    public int EnemySlotCount { get; }

    public GenericActorRulesContract.Form? Form(string formId) =>
        _forms.TryGetValue(formId, out var form) ? form : null;

    public GenericActorRulesContract.AttackProfile? AttackProfile(string profileId) =>
        _attacks.TryGetValue(profileId, out var attack) ? attack : null;

    public GenericActorRulesContract.ActionKind? ActionKind(string actionId) =>
        _actionKinds.TryGetValue(actionId, out var kind) ? kind : null;

    /// <summary>Whether an action's declared parameter schema names a form target.</summary>
    public bool ActionTakesFormTarget(string actionId)
    {
        foreach (var action in Contract.Rules.Actions)
        {
            if (!string.Equals(action.Id, actionId, StringComparison.Ordinal))
                continue;
            foreach (var parameter in action.ParameterKinds)
            {
                if (parameter
                    == GenericActorRulesContract.ActionParameterKind.FormTarget)
                {
                    return true;
                }
            }
            return false;
        }
        return false;
    }

    /// <summary>Whether an action declares no parameters at all.</summary>
    public bool ActionIsParameterless(string actionId)
    {
        foreach (var action in Contract.Rules.Actions)
        {
            if (string.Equals(action.Id, actionId, StringComparison.Ordinal))
                return action.ParameterKinds.IsEmpty;
        }
        return false;
    }

    /// <summary>
    /// Whether a life is ever created already wearing this form — the initial
    /// deployment, an automatic return, or a fabrication output. A stance is
    /// something a life goes into, never something it starts as.
    /// </summary>
    public bool IsCreationForm(string formId) => _creationForms.Contains(formId);

    private HashSet<string> ResolveCreationForms()
    {
        var forms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var life in Contract.InitialDeployment.Lives)
            forms.Add(life.FormId);
        foreach (var profile in Contract.Rules.Lifecycle.Profiles)
        {
            if (profile.AutomaticReturnFormId is string returnForm)
                forms.Add(returnForm);
        }
        foreach (var transition in Contract.Rules.FabricationTransitions)
        {
            if (transition
                is GenericActorRulesContract.BoundedChildFabricationTransition
                    fabrication)
            {
                forms.Add(fabrication.OutputFormId);
            }
        }
        foreach (var transition in Contract.Rules.ReplicationTransitions)
        {
            if (transition
                is GenericActorRulesContract.SplitReplicationTransition
                    replication)
            {
                forms.Add(replication.OutputFormId);
            }
        }
        return forms;
    }

    public GenericActorRulesContract.AttackProfile? Attack(string formId)
    {
        if (!_forms.TryGetValue(formId, out var form)
            || form.AttackProfileId is not string profileId)
        {
            return null;
        }
        return _attacks.TryGetValue(profileId, out var attack) ? attack : null;
    }

    /// <summary>
    /// Whether a form's declared action mask contains an action of this kind.
    /// This is the only honest way to ask "can that body still walk?" — a
    /// stance and a turret both keep objective weight, and one of them cannot
    /// move, so weight is the wrong proxy.
    /// </summary>
    public bool FormAllows(
        string formId,
        GenericActorRulesContract.ActionKind kind)
    {
        if (!_forms.TryGetValue(formId, out var form))
            return true;
        foreach (string actionId in form.AllowedActionIds)
        {
            if (ActionKind(actionId) == kind)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the form deflects hostile bolts that arrive inside its facing
    /// quadrant. Poking such a form head-on returns the bolt, team-flipped, at
    /// the shooter; the field is absent on every unguarded form.
    /// </summary>
    public bool Guarded(string formId) =>
        Form(formId)?.ProjectileGuard
            == GenericActorRulesContract.FormProjectileGuard
                .FacingQuadrantContactsDeflected;

    /// <summary>Bolts one accepted attack from this form launches.</summary>
    public int BoltsPerAttack(string formId) =>
        Attack(formId)?.ProjectilesPerAttack ?? 1;

    /// <summary>
    /// Whether THIS route's declared placement rules admit the tile. The route
    /// names the tag kinds it requires and the tag kinds it refuses, and the map
    /// names which tiles carry each kind; nothing else is a placement rule.
    ///
    /// <para>An empty forbidden list means the route is legal everywhere it is
    /// otherwise offered — which is exactly what an open-ground arm publishes,
    /// and it is the difference between a stance that may only rise on the
    /// shoulder and one that may rise on the point it is defending. Asking the
    /// map instead of the route gets this backwards for free: the tag is still
    /// there, so a bot that reads the tag refuses ground the rules have just
    /// handed it and never notices.</para>
    /// </summary>
    public bool PlacementAdmits(
        GenericActorRulesContract.SameLifePlacement placement,
        Position tile)
    {
        foreach (var kind in placement.ForbiddenTileTags)
        {
            if (_tagged.TryGetValue(kind, out HashSet<Position>? forbidden)
                && forbidden.Contains(tile))
            {
                return false;
            }
        }
        foreach (var kind in placement.RequiredTileTags)
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
    /// What destroying a body actually buys, priced from its own stable slot's
    /// declared lifecycle rather than from a remembered number.
    /// </summary>
    /// <param name="AbsenceTicks">Declared delay before the slot can return.</param>
    /// <param name="NeedsExplicitRefield">
    /// Whether the slot's destruction policy hands the body back automatically or
    /// makes its owner spend a combat action to rebuild it.
    /// </param>
    internal readonly record struct SlotEconomy(
        int AbsenceTicks,
        bool NeedsExplicitRefield);

    /// <summary>
    /// The cheapest return any slot on this contract declares. It is the unit the
    /// farming rule prices every other body against, so "worth more than a Prime"
    /// is a comparison the contract makes rather than one this code asserts.
    /// </summary>
    public int CheapestReturnTicks { get; private set; } = 1;

    /// <summary>
    /// How long the board stays one body lighter if this actor dies now, and
    /// whether its owner also has to spend an action putting it back. A patient
    /// doctrine converts damage into TIME, so this is the exchange rate.
    /// </summary>
    internal SlotEconomy Economy(ActorIdentity actorId) =>
        _slots.TryGetValue((actorId.TeamId, actorId.UnitId), out SlotEconomy slot)
            ? slot
            : new SlotEconomy(CheapestReturnTicks, false);

    private Dictionary<(int, int), SlotEconomy> ResolveSlotEconomy()
    {
        var profiles = new Dictionary<string, GenericActorRulesContract.LifecycleProfile>(
            StringComparer.Ordinal);
        foreach (var profile in Contract.Rules.Lifecycle.Profiles)
            profiles[profile.ProfileId] = profile;

        var slots = new Dictionary<(int, int), SlotEconomy>();
        int cheapest = int.MaxValue;
        foreach (var assignment in Contract.LifecycleAssignments)
        {
            if (!profiles.TryGetValue(
                    assignment.LifecycleProfileId,
                    out var profile))
            {
                continue;
            }
            int delay = Math.Max(0, profile.DelayTicks);
            // A slot the engine refills by itself costs its owner nothing but the
            // clock. A slot whose declared policy is "ready for explicit
            // fabrication" costs the clock AND a combat action from a body that
            // has to be standing in its own fabrication region to spend it — so
            // the same kill removes more from that side's tempo. Both facts are
            // read off the policy string, never inferred from a class name.
            bool explicitRefield = profile.DestructionPolicy.Contains(
                "explicit",
                StringComparison.Ordinal);
            slots[(assignment.TeamId, assignment.UnitId)] =
                new SlotEconomy(delay, explicitRefield);
            if (delay > 0)
                cheapest = Math.Min(cheapest, delay);
        }
        CheapestReturnTicks = cheapest == int.MaxValue ? 1 : cheapest;
        return slots;
    }

    /// <summary>
    /// Whether the declared windup of a transition permits nothing but waiting.
    /// Read from the route rather than assumed: a body inside such a windup is
    /// immobile, unarmed, and cancellable by lethal damage.
    /// </summary>
    public bool WindupIsWaitOnly(string transitionId)
    {
        foreach (var transition in Contract.Rules.SameLifeTransitions)
        {
            if (transition
                    is GenericActorRulesContract.FormTransition form
                && string.Equals(
                    form.TransitionId,
                    transitionId,
                    StringComparison.Ordinal))
            {
                return form.Windup.PendingAction.Contains(
                    "wait-only",
                    StringComparison.Ordinal);
            }
        }
        return false;
    }

    /// <summary>Whether lethal damage during that windup cancels the change.</summary>
    public bool WindupCancelsOnLethal(string transitionId)
    {
        foreach (var transition in Contract.Rules.SameLifeTransitions)
        {
            if (transition
                    is GenericActorRulesContract.FormTransition form
                && string.Equals(
                    form.TransitionId,
                    transitionId,
                    StringComparison.Ordinal))
            {
                return form.Windup.LethalDamage.Contains(
                    "cancel",
                    StringComparison.Ordinal);
            }
        }
        return false;
    }

    private string? ClassOfTeam(int teamId) =>
        Contract.Topology.Teams
            .FirstOrDefault(team => team.TeamId == teamId)
            ?.ClassId
        ?? Contract.Topology.Participants
            .Where(participant => participant.TeamId == teamId)
            .Select(participant => participant.ClassId)
            .FirstOrDefault(id => id is not null);

    public GenericActorRulesContract.FrontlineCapture? Capture =>
        Contract.Rules.GameMode
            is GenericActorRulesContract.FrontlineGameMode frontline
            ? frontline.Capture
            : null;

    /// <summary>
    /// How long a completed advance is locked against being pushed back, or
    /// <see langword="null"/> when this capture definition declares no hold and
    /// the front can come straight back. A declared hold changes what a push is
    /// worth rather than what it costs: a capture completed inside the holder's
    /// window is spent, so the same presence buys a position or buys nothing.
    /// </summary>
    public int? HoldTicks => _captureRules?.HoldTicks;

    /// <summary>
    /// Whether net objective weight scales capture pressure. When it does, a
    /// second body on the point is pressure rather than decoration and a lone
    /// body no longer nulls two; when it does not, control is binary and
    /// reinforcing a contested point buys only survivability.
    /// </summary>
    public bool WeightScales => _captureRules?.SurplusWeightScalesGain ?? false;

    /// <summary>
    /// Whether only an enemy standing alone erodes a claim, so that empty and
    /// contested ticks preserve it. Leaving the point is then cheap and merely
    /// contesting one is a full stop rather than a slow bleed.
    /// </summary>
    public bool SoleDecayOnly =>
        _captureRules?.OnlyEnemySolePresenceDecays ?? false;

    /// <summary>
    /// Whether automatic returns and activations land on this team's own side
    /// of the active objective instead of at the slot's spawn anchor. It is the
    /// single fact that decides what a death costs positionally: a body that
    /// reappears beside the fight is worth trading, and one that reappears at
    /// home is not.
    /// </summary>
    public bool RallyForward { get; }

    /// <summary>Ticks of uncontested presence one capture costs at this tick.</summary>
    public int TicksToCapture(int tick)
    {
        if (Capture is not { } capture)
            return 20;
        int gain = Math.Max(1, capture.GainPhaseAtTick(tick).GainPerSoleTeamTick);
        return ((capture.Threshold + gain - 1) / gain) + capture.RedeployPauseTicks;
    }

    /// <summary>
    /// Ticks of presence a capture itself costs, without the redeploy pause that
    /// only matters if another position is going to be contested afterwards.
    /// </summary>
    public int CaptureTicks(int tick)
    {
        if (Capture is not { } capture)
            return 15;
        int gain = Math.Max(1, capture.GainPhaseAtTick(tick).GainPerSoleTeamTick);
        return (capture.Threshold + gain - 1) / gain;
    }

    /// <summary>
    /// Ticks of presence needed to take an opposing claim of
    /// <paramref name="progress"/> back to neutral. Sole presence erodes at the
    /// declared gain rate; a merely contested point falls at the declared decay
    /// clock instead. Which one applies depends on whether the other side keeps
    /// a body on the tile, so the ledger budgets the slower of the two.
    /// </summary>
    public int TicksToNeutralise(int progress, int tick)
    {
        if (progress <= 0)
            return 0;
        if (Capture is not { } capture)
            return progress * 2;
        int gain = Math.Max(1, capture.GainPhaseAtTick(tick).GainPerSoleTeamTick);
        int amount = Math.Max(1, capture.DecayAmount);
        int interval = Math.Max(1, capture.DecayIntervalTicks);
        // RULE C6. Where the ruleset declares that an opposing claim erodes at a
        // MULTIPLE of the rate a fresh one builds, sole presence takes ground
        // back that many times faster. The field is inert-omitted everywhere
        // else, so this reverts to the plain rate by construction.
        int rate = gain;
        if (ChannelRules.ErosionClock && capture.OpposingErosionMultiplier > 0)
            rate = gain * capture.OpposingErosionMultiplier;
        int erode = (progress + rate - 1) / rate;
        // When the declared decay clock only runs against an enemy standing
        // alone, a contested point does not bleed at all: the claim has to be
        // eroded by sole presence, and budgeting the decay path would promise
        // a neutralisation that never arrives.
        if (SoleDecayOnly)
            return erode;
        int decay = ((progress + amount - 1) / amount) * interval;
        return Math.Max(erode, decay);
    }

    /// <summary>
    /// How this form's movement action treats facing, read from the declared
    /// movement profile. An arm that couples the two turns every retreat into a
    /// commitment; an absent field means the inert preserve-facing default.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling(string formId)
    {
        if (_forms.TryGetValue(formId, out var form)
            && _movements.TryGetValue(form.MovementProfileId, out var profile))
        {
            return profile.FacingCoupling;
        }
        return GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>Whether an ordered objective position exists at this index.</summary>
    public bool HasPosition(int index) =>
        index >= 0 && index < ObjectiveTiles.Length;

    public Position[] TilesAt(int index) =>
        index >= 0 && index < ObjectiveTiles.Length
            ? ObjectiveTiles[index]
            : [];

    /// <summary>
    /// The objective tile my team reaches first: the reference point for a
    /// station that covers the point without standing on it.
    /// </summary>
    public Position NearEdge(int index)
    {
        Position[] tiles = TilesAt(index);
        if (tiles.Length == 0)
            return new Position(Field.Width / 2, Field.Height / 2);

        Position best = tiles[0];
        int bestScore = Project(best);
        foreach (Position tile in tiles)
        {
            int score = Project(tile);
            if (score < bestScore || (score == bestScore && Prefer(tile, best)))
            {
                best = tile;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>Signed distance along the advance axis; larger is deeper in enemy ground.</summary>
    public int Project(Position position) =>
        (position.X * Forward.Dx) + (position.Y * Forward.Dy);

    private static Direction ToDirection(int dx, int dy) =>
        Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;

    private static bool Prefer(Position candidate, Position current) =>
        candidate.Y != current.Y
            ? candidate.Y < current.Y
            : candidate.X < current.X;

    private ImmutableArray<Position[]> ResolveObjectiveTiles()
    {
        if (Contract.ModeMapBinding
            is not GenericActorResolvedMatchContract.FrontlineModeMapBinding binding)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<Position[]>();
        foreach (string regionId in binding.OrderedObjectiveRegionIds)
        {
            builder.Add(
                _regionTiles.TryGetValue(regionId, out Position[]? tiles)
                    ? tiles
                    : []);
        }
        return builder.ToImmutable();
    }

    private int ResolveIndexDelta()
    {
        if (Contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding binding)
        {
            foreach (var advance in binding.TeamAdvances)
            {
                if (advance.TeamId == TeamId)
                    return advance.ObjectiveIndexDelta == 0
                        ? 1
                        : advance.ObjectiveIndexDelta;
            }
        }
        return 1;
    }

    private ((int, int) Forward, (int, int) Lateral) ResolveAxes()
    {
        int dx = 0;
        int dy = 0;
        if (ObjectiveTiles.Length >= 2)
        {
            Position first = Centroid(ObjectiveTiles[0]);
            Position last = Centroid(ObjectiveTiles[^1]);
            dx = last.X - first.X;
            dy = last.Y - first.Y;
        }
        if (dx == 0 && dy == 0)
        {
            foreach (var life in Contract.InitialDeployment.Lives)
            {
                var spawn = Contract.InitialDeployment.Spawns
                    .FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.SpawnId,
                            life.SpawnId,
                            StringComparison.Ordinal));
                if (spawn is null)
                    continue;
                int sign = life.TeamId == TeamId ? -1 : 1;
                dx += sign * spawn.Position.X;
                dy += sign * spawn.Position.Y;
            }
        }

        if (Math.Abs(dx) >= Math.Abs(dy))
            dy = 0;
        else
            dx = 0;
        int fx = Math.Sign(dx);
        int fy = Math.Sign(dy);
        if (fx == 0 && fy == 0)
            fx = 1;
        if (IndexDelta < 0)
        {
            fx = -fx;
            fy = -fy;
        }
        return ((fx, fy), (-fy, fx));
    }

    private static Position Centroid(Position[] tiles)
    {
        if (tiles.Length == 0)
            return new Position(0, 0);
        int sumX = 0;
        int sumY = 0;
        foreach (Position tile in tiles)
        {
            sumX += tile.X;
            sumY += tile.Y;
        }
        return new Position(sumX / tiles.Length, sumY / tiles.Length);
    }

    /// <summary>
    /// The chassis each side can ever wear. Both teams' lifecycle assignments
    /// are in the contract, so each set is READ from its owner's declared
    /// allowed forms and then closed over the declared routes out of them.
    /// <para>
    /// Revision 3 derived the opposing set by subtracting its own from the
    /// catalog, which is wrong twice over: on a mirror the remainder is empty
    /// and the code fell back to "the whole catalog", and on an asymmetric arm
    /// a form both sides can wear would have been credited to neither. Asking
    /// each team's own assignments is both simpler and exact, and it is the
    /// difference between knowing the opposition has a deflecting stance and
    /// guessing that someone might.
    /// </para>
    /// </summary>
    private (ImmutableArray<string> Own, ImmutableArray<string> Opposing)
        ResolveFormOwnership()
    {
        HashSet<string> own = TeamForms(id => id == TeamId);
        HashSet<string> opposing = TeamForms(id => id != TeamId);
        if (opposing.Count == 0)
        {
            // No declared opposition (a solo contract, or one that hides the
            // other side): assume they are exactly as dangerous as we are.
            opposing = own.Count > 0
                ? own
                : Contract.Rules.Forms
                    .Select(form => form.Id)
                    .ToHashSet(StringComparer.Ordinal);
        }
        return (
            own.Order(StringComparer.Ordinal).ToImmutableArray(),
            opposing.Order(StringComparer.Ordinal).ToImmutableArray());
    }

    private HashSet<string> TeamForms(Func<int, bool> matches)
    {
        var forms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in Contract.LifecycleAssignments)
        {
            if (!matches(assignment.TeamId))
                continue;
            foreach (string formId in assignment.AllowedFormIds)
                forms.Add(formId);
            foreach (var profile in Contract.Rules.Lifecycle.Profiles)
            {
                if (string.Equals(
                        profile.ProfileId,
                        assignment.LifecycleProfileId,
                        StringComparison.Ordinal)
                    && profile.AutomaticReturnFormId is string returnForm)
                {
                    forms.Add(returnForm);
                }
            }
        }
        foreach (var life in Contract.InitialDeployment.Lives)
        {
            if (matches(life.TeamId))
                forms.Add(life.FormId);
        }

        bool grew = true;
        int guard = 0;
        while (grew && guard++ < 16)
        {
            grew = false;
            foreach (var transition in Contract.Rules.SameLifeTransitions)
            {
                if (transition
                        is GenericActorRulesContract.FormTransition form
                    && forms.Contains(form.SourceFormId))
                {
                    grew |= forms.Add(form.TargetFormId);
                }
            }
            foreach (var transition in Contract.Rules.FabricationTransitions)
            {
                if (transition
                        is GenericActorRulesContract.BoundedChildFabricationTransition
                            fabrication
                    && fabrication.SourceFormIds.Any(forms.Contains))
                {
                    grew |= forms.Add(fabrication.OutputFormId);
                }
            }
        }
        return forms;
    }

    /// <summary>
    /// Longest gun among a set of forms. "Mobile" means the form can still take a
    /// step — either its own action mask offers movement, or it can get back to a
    /// form that does through a route the contract declares reversible.
    ///
    /// <para>That second clause is not decoration. A fortified form whose exit is
    /// one-way is a body that has stopped following you for the rest of its life,
    /// and its gun does not set the standoff band. A fortified form that may
    /// mobilize as often as it likes is a body that walks, digs in, shoots, and
    /// walks again — so its gun is part of the reach you have to stand outside
    /// of, and when that reach exceeds your own the out-range bargain is simply
    /// not on offer and the doctrine falls back to the fork band. Which of the
    /// two it is comes from <c>irreversibleForLife</c> on the exit route, read
    /// rather than assumed: the same class was once the first kind and is now the
    /// second, and nothing in the form or the gun changed when it moved.</para>
    /// </summary>
    private int MaxRange(ImmutableArray<string> formIds, bool mobileOnly)
    {
        int best = 0;
        foreach (string formId in formIds)
        {
            if (!_forms.TryGetValue(formId, out _))
                continue;
            if (mobileOnly && !CanStillWalk(formId))
                continue;
            if (Attack(formId) is { } attack)
                best = Math.Max(best, attack.Projectile.MaxTravelTiles);
        }
        return best;
    }

    /// <summary>
    /// Whether a body wearing this form can take a step now or get back to a form
    /// that can. Bounded by the catalog size, and only reversible routes count.
    /// </summary>
    private bool CanStillWalk(string formId)
    {
        if (FormAllows(formId, GenericActorRulesContract.ActionKind.Movement))
            return true;

        var reached = new HashSet<string>(StringComparer.Ordinal) { formId };
        bool grew = true;
        int guard = 0;
        while (grew && guard++ < 8)
        {
            grew = false;
            foreach (var transition in Contract.Rules.SameLifeTransitions)
            {
                if (transition
                        is not GenericActorRulesContract.FormTransition route
                    || route.IrreversibleForLife
                    || !reached.Contains(route.SourceFormId))
                {
                    continue;
                }
                if (FormAllows(
                        route.TargetFormId,
                        GenericActorRulesContract.ActionKind.Movement))
                {
                    return true;
                }
                grew |= reached.Add(route.TargetFormId);
            }
        }
        return false;
    }

    private int MaxVision(ImmutableArray<string> formIds, bool mobileOnly)
    {
        int best = 0;
        foreach (string formId in formIds)
        {
            if (!_forms.TryGetValue(formId, out var form))
                continue;
            if (mobileOnly && !CanStillWalk(formId))
                continue;
            if (_visions.TryGetValue(form.VisionProfileId, out var vision))
                best = Math.Max(best, vision.Range);
        }
        return best;
    }

    /// <summary>Most bolts any opposing form launches with one attack.</summary>
    public int OpposingFanBolts
    {
        get
        {
            int best = 1;
            foreach (string formId in OpposingFormIds)
                best = Math.Max(best, BoltsPerAttack(formId));
            return best;
        }
    }

    /// <summary>Whether any opposing form can deflect bolts back at us.</summary>
    public bool OpposingCanDeflect
    {
        get
        {
            foreach (string formId in OpposingFormIds)
            {
                if (Guarded(formId))
                    return true;
            }
            return false;
        }
    }

    private bool AnyBend(ImmutableArray<string> formIds)
    {
        foreach (string formId in formIds)
        {
            if (Attack(formId) is { } attack
                && attack.ShotProgram.Enabled
                && attack.ShotProgram.MaxBendCount > 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The forward distance at which this chassis covers the widest contiguous
    /// lateral band — Still Water's whole positional argument, and a number that
    /// MOVED when launch offsets came back.
    ///
    /// <para>Without offsets the band is the fork's: a target at forward
    /// <c>f</c> and lateral <c>g</c> needs a bend after <c>f - |g|</c> tiles, so
    /// the band is widest one tile past the latest legal bend and collapses
    /// beyond it. With offsets the hook exists — launch 45 degrees off, then bend
    /// the same way — and the hook is legal only while <c>f</c> itself is inside
    /// the bend window. So the widest band is no longer past the window: it is at
    /// the DEEPEST tile still inside it, where the lane, the diagonal, the fork,
    /// the flatten and the hook are all available at once and the coverable band
    /// runs the full travel range to either side. One tile further out the hook
    /// is gone and the band narrows to the fork's.</para>
    /// </summary>
    private int ResolveForkReach()
    {
        int best = 0;
        foreach (string formId in OwnFormIds)
        {
            if (Attack(formId) is not { } attack)
                continue;
            var program = attack.ShotProgram;
            if (!program.Enabled || program.MaxBendCount <= 0)
            {
                best = Math.Max(
                    best,
                    Math.Max(2, attack.Projectile.MaxTravelTiles / 2));
                continue;
            }
            bool hooks = program.MinInitialAimSteps < 0
                || program.MaxInitialAimSteps > 0;
            int reach = Math.Min(
                attack.Projectile.MaxTravelTiles,
                hooks ? program.MaxBendAfterTiles : program.MaxBendAfterTiles + 1);
            best = Math.Max(best, Math.Max(2, reach));
        }
        return best <= 0 ? 3 : best;
    }

    /// <summary>
    /// Still Water's core positional constant. If the opposing chassis can be
    /// out-ranged with a tile to spare, stand exactly outside its reach.
    /// Otherwise stand where one bend covers the widest lateral band.
    /// </summary>
    private int ResolveStandBand()
    {
        int outrange = OpposingMobileRange + 1;
        int ceiling = Math.Max(2, OwnMaxRange - 1);
        return outrange > ForkReach && outrange <= ceiling
            ? outrange
            : Math.Min(ForkReach, ceiling);
    }
}
