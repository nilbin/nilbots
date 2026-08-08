using System.Collections.Immutable;
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
    private readonly Dictionary<string, GenericActorRulesContract.VisionProfile>
        _visionProfiles = new(StringComparer.Ordinal);
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
        foreach (GenericActorRulesContract.VisionProfile profile
                 in contract.Rules.VisionProfiles)
        {
            _visionProfiles[profile.Id] = profile;
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
        BoltsStopOnFirstEnemy =
            contract.Rules.Collisions.ProjectilesStopOnFirstEnemyActor;
        Width = contract.Map.Width;
        Height = contract.Map.Height;
        _chokes = FindChokes();

        foreach (GenericActorMapContract.Region region in contract.Map.Regions)
            _regions[region.RegionId] = region.Tiles.ToArray();

        GenericActorRulesContract.FrontlineGameMode? frontline =
            contract.Rules.GameMode
                as GenericActorRulesContract.FrontlineGameMode;
        Channel = ReadChannel(frontline);
        Economy = ReadEconomy(frontline);
        Bank = Economy is null ? [] : BankTiles(Economy, teamId);
    }

    private readonly Dictionary<string, Position[]> _regions =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The capture arithmetic THIS ruleset plays, read straight off
    /// <c>gameMode.capture</c> rather than through a helper that predates the
    /// channel.
    ///
    /// <para>This is the wave-8 repair that everything else rests on. The
    /// scaffold's <c>ArenaBasics.Capture</c> decides whether surplus scales
    /// gain by looking for the string
    /// <c>net-positive-objective-weight-difference</c> in the control policy —
    /// true of the keel's contest-majority policy, and FALSE of the channel's
    /// <c>stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-…</c>,
    /// which scales harder than either. A doctrine that keeps trading in the
    /// helper's answer prices every push on the wrong curve and never notices,
    /// because both answers are plausible integers.</para>
    ///
    /// <para>Every field is inert-omitted when the mechanic is absent, so the
    /// flags below are presence tests rather than arm names: a ruleset with no
    /// cap, no erosion multiple and no interrupt block is simply the old game,
    /// and the same code plays it.</para>
    /// </summary>
    /// <param name="Threshold">Progress one capture costs.</param>
    /// <param name="GainPerTick">Base progress per unit of pressure.</param>
    /// <param name="StationaryCap">
    /// Ceiling on the stationary-surplus multiplier, or 0 when the contract
    /// declares none. A cap is the fact that makes a THIRD body on the point
    /// worth exactly nothing, which is where most of this wave's play comes
    /// from.
    /// </param>
    /// <param name="ErosionMultiple">
    /// How many times faster a standing enemy claim erodes than a fresh claim
    /// builds, or 0 when the contract declares no separate erosion path.
    /// </param>
    /// <param name="RevertPerDamage">
    /// Progress the controller loses per point of health removed from one of
    /// its bodies standing on the objective, or 0 when no interrupt exists.
    /// </param>
    /// <param name="StationaryClaim">
    /// True when only bodies that did not change tile this tick count toward
    /// a claim. Derived from the control policy naming a stationary claim
    /// weight; false leaves every rule below inert.
    /// </param>
    /// <param name="RedeployPause">Ticks of pause after an advance.</param>
    /// <param name="HoldTicks">Ratchet hold duration, or 0.</param>
    public sealed record ChannelRules(
        int Threshold,
        int GainPerTick,
        int StationaryCap,
        int ErosionMultiple,
        int RevertPerDamage,
        bool StationaryClaim,
        int RedeployPause,
        int HoldTicks);

    /// <summary>
    /// The battlefield economy, or null when the ruleset declares none.
    /// Prices, schedule and ladder are all static contract data; the bank,
    /// the tiers and the live piles are observation state.
    /// </summary>
    /// <param name="Sites">Declared deposit addresses.</param>
    /// <param name="FirstTick">First scheduled deposit tick.</param>
    /// <param name="IntervalTicks">Ticks between deposits.</param>
    /// <param name="LastTick">Last scheduled deposit tick.</param>
    /// <param name="Amount">Scrap in one deposit.</param>
    /// <param name="Assay">Banked instantly on stepping onto a pile.</param>
    /// <param name="CarryCapacity">Most one body may carry.</param>
    /// <param name="PileLifetime">Ticks a pile survives.</param>
    /// <param name="MaxTotalTiers">Team-wide ceiling on tiers.</param>
    /// <param name="BuyableByAction">
    /// True when the <c>invest</c> verb exists. The control arm buys by
    /// itself and declares no verb, so a purchase routine must be skipped
    /// rather than merely unused.
    /// </param>
    /// <param name="Tracks">The ladder, in declared order.</param>
    public sealed record ScrapRules(
        Position[] Sites,
        int FirstTick,
        int IntervalTicks,
        int LastTick,
        int Amount,
        int Assay,
        int CarryCapacity,
        int PileLifetime,
        int MaxTotalTiers,
        bool BuyableByAction,
        GenericActorRulesContract.ScrapUpgradeTrack[] Tracks);

    /// <summary>The capture arithmetic in force, never null.</summary>
    public ChannelRules Channel { get; }
    /// <summary>The declared economy, or null when the ruleset has none.</summary>
    public ScrapRules? Economy { get; }
    /// <summary>Tiles on which one of our loads banks itself.</summary>
    public Position[] Bank { get; }
    /// <summary>Whether an enemy body physically stops a bolt — the screen.</summary>
    public bool BoltsStopOnFirstEnemy { get; }

    private static ChannelRules ReadChannel(
        GenericActorRulesContract.FrontlineGameMode? frontline)
    {
        if (frontline is null)
            return new ChannelRules(1, 1, 0, 0, 0, false, 0, 0);
        GenericActorRulesContract.FrontlineCapture capture = frontline.Capture;
        return new ChannelRules(
            Math.Max(capture.Threshold, 1),
            Math.Max(capture.GainPerSoleTeamTick, 1),
            Math.Max(capture.StationaryGainMultiplierCap, 0),
            Math.Max(capture.OpposingErosionMultiplier, 0),
            capture.ClaimInterrupt?.RevertPerDamagePoint ?? 0,
            capture.ControlPolicy.Contains(
                "stationary-claim-weight",
                StringComparison.Ordinal),
            Math.Max(capture.RedeployPauseTicks, 0),
            Math.Max(capture.RatchetHoldTicks, 0));
    }

    private static ScrapRules? ReadEconomy(
        GenericActorRulesContract.FrontlineGameMode? frontline)
    {
        if (frontline?.ScrapEconomy is not
            GenericActorRulesContract.FrontlineScrapEconomy economy)
        {
            return null;
        }
        var sites = new Position[economy.VeinSites.Length];
        for (int index = 0; index < sites.Length; index++)
        {
            sites[index] = new Position(
                economy.VeinSites[index].X,
                economy.VeinSites[index].Y);
        }
        return new ScrapRules(
            sites,
            economy.VeinFirstSpawnTick,
            Math.Max(economy.VeinSpawnIntervalTicks, 1),
            economy.VeinLastSpawnTick,
            economy.VeinAmount,
            economy.AssayAmount,
            Math.Max(economy.CarryCapacity, 0),
            Math.Max(economy.PileLifetimeTicks, 0),
            Math.Max(economy.MaxTotalTiers, 0),
            economy.PurchaseMode.Contains(
                "invest-action",
                StringComparison.Ordinal),
            economy.Tracks.ToArray());
    }

    private Position[] BankTiles(ScrapRules economy, int teamId)
    {
        if (Raw.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline
            || frontline.ScrapEconomy is not
                GenericActorRulesContract.FrontlineScrapEconomy declared)
        {
            return [];
        }
        // The bank regions are indexed BY TEAM ID, which is the one place in
        // this contract where an array position is an identity — so it is read
        // as such and bounds-checked rather than assumed to be ours.
        if (teamId < 0 || teamId >= declared.BankRegionIds.Length)
            return [];
        return _regions.TryGetValue(
            declared.BankRegionIds[teamId],
            out Position[]? tiles)
            ? tiles
            : [];
    }

    /// <summary>Tiles of one declared map region, or empty.</summary>
    public Position[] Region(string regionId) =>
        _regions.TryGetValue(regionId, out Position[]? tiles) ? tiles : [];

    /// <summary>
    /// Whether a slot-scoped route clock is currently holding one route shut
    /// for a body. Read from the published clocks rather than inferred from
    /// our own completion history: the clock survives a death, and a life born
    /// inside the window has no history to infer from.
    /// </summary>
    public static bool RouteHeld(
        ImmutableArray<GenericActorContext.ObservedRouteCooldown> clocks,
        string transitionId,
        int tick)
    {
        foreach (GenericActorContext.ObservedRouteCooldown clock in clocks)
        {
            if (string.Equals(
                    clock.TransitionId,
                    transitionId,
                    StringComparison.Ordinal))
            {
                return tick < clock.ReadyAtTick;
            }
        }
        return false;
    }

    private readonly HashSet<Position> _chokes;

    /// <summary>
    /// One-tile corridors, computed once from the map the contract delivers.
    ///
    /// <para>A choke is an open tile whose open cardinal neighbours are a single
    /// OPPOSITE pair, or a dead end. That is exactly the shape in which one body
    /// standing still is another body's whole detour, and on this map it picks out
    /// the four mouths of the central corridor — the highest-traffic ground in the
    /// game and the ground the owner watched a gate body sit in while its relief
    /// walked the long way round.</para>
    ///
    /// <para>Cardinals only, deliberately: bodies move one cardinal step per
    /// tick, so a diagonal gap is not a way past a body even where a bolt fits
    /// through it.</para>
    /// </summary>
    private HashSet<Position> FindChokes()
    {
        var chokes = new HashSet<Position>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var tile = new Position(x, y);
                if (IsWall(tile))
                    continue;
                bool north = !IsWall(tile.Offset(0, -1));
                bool south = !IsWall(tile.Offset(0, 1));
                bool east = !IsWall(tile.Offset(1, 0));
                bool west = !IsWall(tile.Offset(-1, 0));
                int open = (north ? 1 : 0) + (south ? 1 : 0)
                    + (east ? 1 : 0) + (west ? 1 : 0);
                if (open <= 1
                    || (open == 2 && ((north && south) || (east && west))))
                {
                    chokes.Add(tile);
                }
            }
        }
        return chokes;
    }

    /// <summary>Whether a tile is a one-tile corridor a body can plug.</summary>
    public bool IsChoke(Position tile) => _chokes.Contains(tile);

    /// <summary>
    /// The cardinal our own advance points along, derived from the objective
    /// chain and our signed index delta rather than from the team ID — the two
    /// sides' geometry is an exact reflection, so a rule written on the delta
    /// works from both.
    /// </summary>
    public Direction? AdvanceFacing()
    {
        int delta = AdvanceDelta(TeamId);
        if (delta == 0 || _objectives.Length < 2)
            return null;
        Position[] first = _objectives[0];
        Position[] last = _objectives[^1];
        if (first.Length == 0 || last.Length == 0)
            return null;
        int dx = (last[0].X - first[0].X) * Math.Sign(delta);
        int dy = (last[0].Y - first[0].Y) * Math.Sign(delta);
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
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

    /// <summary>
    /// A form's DECLARED sight range. The store's optic tier is added on top
    /// of this number at the point of use, exactly as the contract says: the
    /// base is here and the step is in the observation, and both are published
    /// so nothing already read becomes a lie.
    /// </summary>
    public int Sight(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && _visionProfiles.TryGetValue(
                form.VisionProfileId,
                out GenericActorRulesContract.VisionProfile? profile)
            ? profile.Range
            : 0;
    }

    /// <summary>
    /// The longest gun any team that is not ours is entitled to field, taken
    /// from the forms their own slots declare rather than from the bodies we
    /// happen to have seen. It is the number the edge track is bought against:
    /// a class that outranges us wins every exchange it chooses to start.
    /// </summary>
    public int LongestOpposingTravel()
    {
        int longest = 0;
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment slot
                 in Raw.LifecycleAssignments)
        {
            if (slot.TeamId == TeamId)
                continue;
            foreach (string formId in slot.AllowedFormIds)
            {
                GenericActorRulesContract.AttackProfile? gun = Attack(formId);
                if (gun is not null)
                {
                    longest = Math.Max(
                        longest,
                        gun.Projectile.MaxTravelTiles);
                }
            }
        }
        return longest;
    }

    /// <summary>
    /// The tier one declared effect currently stands at for a team, resolved
    /// positionally against the contract's declared track order — which is the
    /// one place the economy uses array position as an index, and it says so.
    /// </summary>
    public int Tier(
        GenericActorContext context,
        int teamId,
        string effectFragment)
    {
        if (Economy is not ScrapRules economy
            || context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return 0;
        }
        foreach (GenericActorContext.ScrapTeamState team in mode.ScrapTeams)
        {
            if (team.TeamId != teamId)
                continue;
            int total = 0;
            for (int index = 0;
                 index < economy.Tracks.Length && index < team.TierLevels.Length;
                 index++)
            {
                if (economy.Tracks[index].Effect.Contains(
                        effectFragment,
                        StringComparison.Ordinal))
                {
                    total += team.TierLevels[index]
                        * economy.Tracks[index].PerTierMagnitude;
                }
            }
            return total;
        }
        return 0;
    }

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
