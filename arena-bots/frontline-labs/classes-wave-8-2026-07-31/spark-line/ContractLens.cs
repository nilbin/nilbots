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
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movement = new(StringComparer.Ordinal);
    private readonly bool[] _wall;
    private readonly bool[] _closedToMe;
    private readonly int[] _exposure;
    private readonly int[]? _sourceField;
    private readonly int[] _chokeRun;

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
        foreach (GenericActorRulesContract.MovementProfile profile
                 in Safe(rules.MovementProfiles))
        {
            _movement[profile.Id] = profile;
        }

        // The widest declared gun in the catalog, whoever owns it. Threat
        // geometry has to assume the longest reach the ruleset publishes,
        // not the reach of the chassis I happen to be driving.
        foreach (GenericActorRulesContract.AttackProfile attack
                 in Safe(rules.AttackProfiles))
        {
            WidestAttackTiles = Math.Max(
                WidestAttackTiles,
                attack.Projectile.MaxTravelTiles);
            HeaviestDeclaredDamage = Math.Max(
                HeaviestDeclaredDamage,
                attack.Projectile.DamagePerHit);
            StrictCorners |= attack.Projectile.DiagonalCornersMustBeClear;
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
        _exposure = ResolveExposure();
        _chokeRun = ResolveChokeRuns();

        // What a push is worth, and where a death puts me. Both are policy
        // decisions this contract publishes and neither changes the
        // observation schema, so the only way to tell one structural arm from
        // another is to read them. Everything downstream prices from these.
        Capture = ArenaBasics.Capture(Contract);
        ArrivalsRallyForward = ArenaBasics.ArrivalsRallyForward(Contract);
        _sourceField = FabricationSourceTiles.Count == 0
            ? null
            : Tactics.DistanceField(this, FabricationSourceTiles);

        // The kit's two threat shapes, by form. Both are fields the class-skill
        // arm adds and canonical contracts omit when inert, so reading them is
        // how one artifact plays the kit-off and kit-on cells without a flag:
        // an absent guard and an absent volley collapse every skill-aware
        // branch downstream into revision 3's behaviour.
        ResolveGuardsAndFans();

        // Wave 8. Two arms rewrote what the front IS and what a body is worth,
        // and both are absent-means-inert contract blocks: the CHANNEL on the
        // capture definition, and the ECONOMY on the mode. Everything the
        // policy does with either is gated on these reads, so the same artifact
        // plays the cells that carry them and the cells that do not.
        ResolveChannel();
        ResolveEconomy();
        _bankField = BankTiles.Count == 0
            ? null
            : Tactics.DistanceField(this, BankTiles);
    }

    // ------------------------------------------------------------------
    // Wave 8 — the channel
    // ------------------------------------------------------------------

    /// <summary>
    /// True when capture is a CHANNEL: claim weight counts only bodies whose
    /// tile did not change this tick, denial weight counts all of them, and
    /// gain is the capped surplus of the one over the other. Recognised from
    /// the declared control policy, never from a ruleset name — and false on
    /// every contract that does not declare it, where every branch below
    /// collapses to the wave-6 arithmetic.
    /// </summary>
    public bool CaptureIsChannel { get; private set; }

    /// <summary>
    /// The ceiling on the gain multiplier stationary surplus can buy. One when
    /// the contract declares no cap, which is also the value that makes a
    /// non-channel contract read like binary control.
    /// </summary>
    public int StationaryGainCap { get; private set; } = 1;

    /// <summary>How many times faster an opposing claim erodes than a fresh one builds.</summary>
    public int ErosionMultiplier { get; private set; } = 1;

    /// <summary>
    /// Progress reverted per point of damage taken by a body of the
    /// CONTROLLING team standing on the active objective. Zero when the
    /// contract declares no interrupt, and the switch that turns the whole
    /// "stillness is a purchase" branch off.
    /// </summary>
    public int RevertPerDamagePoint { get; private set; }

    private void ResolveChannel()
    {
        if (Contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline)
        {
            return;
        }
        GenericActorRulesContract.FrontlineCapture capture = frontline.Capture;
        CaptureIsChannel = capture.ControlPolicy.Contains(
            "stationary-claim-weight",
            StringComparison.Ordinal);
        StationaryGainCap = capture.StationaryGainMultiplierCap > 0
            ? capture.StationaryGainMultiplierCap
            : 1;
        ErosionMultiplier = capture.OpposingErosionMultiplier > 0
            ? capture.OpposingErosionMultiplier
            : 1;
        RevertPerDamagePoint =
            capture.ClaimInterrupt?.RevertPerDamagePoint ?? 0;
    }

    // ------------------------------------------------------------------
    // Wave 8 — the economy
    // ------------------------------------------------------------------

    private readonly int[]? _bankField;

    /// <summary>The declared battlefield economy, or null when there is none.</summary>
    public GenericActorRulesContract.FrontlineScrapEconomy? Scrap
    {
        get;
        private set;
    }

    /// <summary>
    /// True when a live body spends its action on the store's verb. False both
    /// where there is no economy at all and on the control arm, where the bank
    /// buys by itself and the verb is not in the catalog — read it rather than
    /// looking the action up, because an absent verb and an automatic bank are
    /// different facts with the same missing action.
    /// </summary>
    public bool InvestIsAnAction { get; private set; }

    /// <summary>Tiles a carried load banks on, empty when nothing banks.</summary>
    public HashSet<Position> BankTiles { get; } = [];

    /// <summary>Declared deposit addresses, in declared order.</summary>
    public Position[] VeinTiles { get; private set; } = [];

    /// <summary>Most scrap one body may carry, zero when there is no economy.</summary>
    public int CarryCapacity { get; private set; }

    /// <summary>
    /// The unit slot the upgrade ladder applies to, or -1 when nothing does.
    /// Read from the declared scope and the slot's own lifecycle assignment,
    /// so a ruleset that upgrades every slot resolves to the frailest one and
    /// the purchase order below is unchanged.
    /// </summary>
    public int UpgradedSlotUnitId { get; private set; } = -1;

    /// <summary>
    /// Declared maximum health of the frailest form the upgraded slot may
    /// host, BEFORE any tier. Compared against the heaviest gun on the board,
    /// this is the whole argument for the health track: a body that dies to
    /// one contact buys a life with the first ten scrap it banks.
    /// </summary>
    public int UpgradedSlotBaseHealth { get; private set; }

    /// <summary>Declared upgrade tracks, in the order their tiers are published.</summary>
    public ImmutableArray<GenericActorRulesContract.ScrapUpgradeTrack> Tracks
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// Tiles this body must walk from <paramref name="from"/> to bank a load,
    /// or 0 when nothing banks. Map geometry, so a courier prices its trip
    /// home before it picks anything up.
    /// </summary>
    public int WalkToBank(Position from) =>
        _bankField is null
            ? 0
            : Math.Min(
                Tactics.Unreachable,
                Tactics.DistanceAt(this, _bankField, from));

    /// <summary>
    /// The effect policy of one declared track, or null when the track is not
    /// declared. Every purchase decision keys off this rather than off the
    /// track's ID, so a ruleset that renames its tracks still buys correctly.
    /// </summary>
    public string? EffectOf(string trackId)
    {
        foreach (GenericActorRulesContract.ScrapUpgradeTrack track in Tracks)
        {
            if (string.Equals(track.TrackId, trackId, StringComparison.Ordinal))
                return track.Effect;
        }
        return null;
    }

    private void ResolveEconomy()
    {
        if (Contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline)
        {
            return;
        }
        if (frontline.ScrapEconomy is not
            GenericActorRulesContract.FrontlineScrapEconomy economy)
        {
            return;
        }

        Scrap = economy;
        Tracks = economy.Tracks;
        CarryCapacity = economy.CarryCapacity;
        InvestIsAnAction = economy.PurchaseMode.Contains(
            "invest-action",
            StringComparison.Ordinal);

        // Which slot the ladder actually upgrades, and how much body it has to
        // work with. The scope is a declared policy ID, so a contract that
        // upgrades everything leaves this at the frailest form my slots may
        // host and the purchase order is unchanged.
        UpgradedSlotUnitId = -1;
        UpgradedSlotBaseHealth = int.MaxValue;
        bool primeOnly = economy.UpgradeScope.Contains(
            "prime-slot",
            StringComparison.Ordinal);
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Safe(Contract.LifecycleAssignments))
        {
            if (assignment.TeamId != TeamId)
                continue;
            if (primeOnly
                && assignment.InitialAvailability
                    != GenericActorResolvedMatchContract.InitialAvailability
                        .ActiveAtTickZero)
            {
                continue;
            }
            foreach (string formId in assignment.AllowedFormIds)
            {
                GenericActorRulesContract.Form? form = Form(formId);
                if (form is null)
                    continue;
                if (form.MaxHealth < UpgradedSlotBaseHealth)
                {
                    UpgradedSlotBaseHealth = form.MaxHealth;
                    UpgradedSlotUnitId = assignment.UnitId;
                }
            }
        }
        if (UpgradedSlotBaseHealth == int.MaxValue)
            UpgradedSlotBaseHealth = 0;

        var veins = new List<Position>();
        foreach (GenericActorRulesContract.ScrapVeinSite site in economy.VeinSites)
        {
            var tile = new Position(site.X, site.Y);
            if (InBounds(tile) && !IsWall(tile))
                veins.Add(tile);
        }
        VeinTiles = [.. veins];

        // Bank regions are indexed by team ID, which is a POSITION in a
        // declared array and therefore the one place this contract does use
        // ordinal identity — the field's own documentation says so. Resolve it
        // defensively: an index this team does not have means nothing banks.
        if (TeamId >= 0 && TeamId < economy.BankRegionIds.Length)
        {
            string regionId = economy.BankRegionIds[TeamId];
            foreach (GenericActorMapContract.Region region
                     in Safe(Contract.Map.Regions))
            {
                if (!string.Equals(
                        region.RegionId,
                        regionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (Position tile in region.Tiles)
                {
                    if (InBounds(tile) && !IsWall(tile))
                        BankTiles.Add(tile);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Wave 8 — what a body can put on a tile
    // ------------------------------------------------------------------

    /// <summary>
    /// The heaviest single contact a body in <paramref name="formId"/> can
    /// deliver, counting every form ONE same-life route away — because a
    /// stance whose windup is a tick is a gun that already exists. Returns the
    /// damage, the travel that gun declares, and whether it is straight-only.
    ///
    /// <para>This is the salvo read, stated as a stat rather than as a class:
    /// a fan bolt deals two and a two-health prime dies to one of them, so the
    /// tiles that gun can reach are lethal ground whether or not the stance is
    /// up yet. Nothing here names a skill, a class, or a form — a chassis that
    /// gains a heavier gun tomorrow is priced by the same three numbers.</para>
    /// </summary>
    public (int Damage, int Travel, bool StraightOnly) HeaviestStrike(
        string formId)
    {
        (int Damage, int Travel, bool StraightOnly) best = (0, 0, true);
        Consider(formId, ref best);
        foreach (GenericActorRulesContract.FormTransition route
                 in RoutesFrom(formId))
        {
            Consider(route.TargetFormId, ref best);
        }
        return best;

        void Consider(
            string candidate,
            ref (int Damage, int Travel, bool StraightOnly) carry)
        {
            GenericActorRulesContract.AttackProfile? attack =
                AttackFor(candidate);
            if (attack is null)
                return;
            int damage = attack.Projectile.DamagePerHit;
            if (damage <= carry.Damage)
                return;
            bool straight = !(attack.ShotProgram?.Enabled ?? false);
            carry = (damage, attack.Projectile.MaxTravelTiles, straight);
        }
    }

    /// <summary>Forms that deflect contacts inside their facing quadrant.</summary>
    private readonly HashSet<string> _guardForms = new(StringComparer.Ordinal);

    /// <summary>Projectiles one attack launches, by form.</summary>
    private readonly Dictionary<string, int> _fanByForm = new(StringComparer.Ordinal);

    /// <summary>
    /// True when any form in the catalog declares a projectile guard. False on
    /// every contract without the shell skill, and the switch that turns the
    /// whole reflection model off.
    /// </summary>
    public bool AnyGuardForms => _guardForms.Count > 0;

    /// <summary>
    /// The widest simultaneous heading fan any attack profile declares. One on
    /// every contract without a volley — and the separation two of my bodies
    /// need before a single cast can no longer cover both.
    /// </summary>
    public int WidestFan { get; private set; } = 1;

    /// <summary>
    /// True when the named form deflects bolts arriving in its facing
    /// quadrant. Poking one head-on returns the bolt to the shooter, so a
    /// straight line into a guard's face is a line onto myself.
    /// </summary>
    public bool IsGuard(string formId) => _guardForms.Contains(formId);

    /// <summary>Projectiles one attack from the named form launches.</summary>
    public int FanOf(string formId) =>
        _fanByForm.TryGetValue(formId, out int count) ? count : 1;

    /// <summary>
    /// Same-life routes out of a form, with their windups and stance budgets.
    /// A stance is an ordinary route — read it rather than naming it.
    /// </summary>
    public IEnumerable<GenericActorRulesContract.FormTransition> RoutesFrom(
        string formId)
    {
        foreach (GenericActorRulesContract.SameLifeTransition transition
                 in Safe(Contract.Rules.SameLifeTransitions))
        {
            if (transition
                    is GenericActorRulesContract.FormTransition route
                && string.Equals(
                    route.SourceFormId,
                    formId,
                    StringComparison.Ordinal))
            {
                yield return route;
            }
        }
    }

    /// <summary>
    /// Cheapest windup any same-life route spends to REACH a form. With the
    /// windup of the route back out, this is the contract's own statement of
    /// what leaving and re-entering a stance costs — the punish window a broken
    /// shell pays, and therefore the minimum a stance is worth holding for
    /// rather than flapping in and out of.
    /// </summary>
    public int WindupInto(string formId)
    {
        int best = int.MaxValue;
        foreach (GenericActorRulesContract.SameLifeTransition transition
                 in Safe(Contract.Rules.SameLifeTransitions))
        {
            if (transition is GenericActorRulesContract.FormTransition route
                && string.Equals(
                    route.TargetFormId,
                    formId,
                    StringComparison.Ordinal))
            {
                best = Math.Min(best, Math.Max(1, route.Windup.DurationTicks));
            }
        }
        return best == int.MaxValue ? 1 : best;
    }

    /// <summary>
    /// True when the capture definition only erodes a claim under enemy sole
    /// presence. Then matching an opponent's weight on the objective is a full
    /// stop rather than a slow bleed, and leaving is cheap.
    /// </summary>
    public bool OnlyEnemySoleDecays =>
        Capture?.OnlyEnemySolePresenceDecays ?? false;

    private void ResolveGuardsAndFans()
    {
        foreach (GenericActorRulesContract.Form form in Safe(Contract.Rules.Forms))
        {
            if (form.ProjectileGuard
                != GenericActorRulesContract.FormProjectileGuard.None)
            {
                _guardForms.Add(form.Id);
            }

            GenericActorRulesContract.AttackProfile? attack = AttackFor(form.Id);
            int fan = attack?.ProjectilesPerAttack ?? 1;
            _fanByForm[form.Id] = fan;
            WidestFan = Math.Max(WidestFan, fan);
        }
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
    /// <summary>Longest projectile travel any declared attack profile allows.</summary>
    public int WidestAttackTiles { get; private set; }

    /// <summary>
    /// Heaviest single contact any declared attack profile delivers, whoever
    /// owns it. Compared against a body's CURRENT health it answers the only
    /// question that matters to a two-health chassis on a board carrying a
    /// two-damage fan: does one bolt end this life, or merely cost it?
    /// </summary>
    public int HeaviestDeclaredDamage { get; private set; }
    /// <summary>True when any declared projectile requires clear diagonal corners.</summary>
    public bool StrictCorners { get; private set; }

    /// <summary>
    /// What one push costs and what protects it. Null when the mode declares
    /// no capture at all. <see cref="ArenaBasics.CaptureRules.HoldTicks"/> is
    /// null when the contract declares no hold — an absent hold means a
    /// completed capture always advances, so nothing below engages.
    /// </summary>
    public ArenaBasics.CaptureRules? Capture { get; }

    /// <summary>
    /// True when automatic returns and activations land on the own-side
    /// chain-adjacent objective instead of the slot's spawn anchor. For a
    /// chassis whose fabrication is bound to a home region this is a cost, not
    /// a convenience: the body comes back near the fight and away from its
    /// workbench.
    /// </summary>
    public bool ArrivalsRallyForward { get; }

    /// <summary>
    /// Tiles a body must walk from <paramref name="from"/> back to the
    /// fabrication source region, or 0 when nothing binds fabrication to a
    /// region. Under a forward-rally contract this is the real price of losing
    /// a fabricating body — it comes back near the fight and away from its
    /// workbench — and it is derived from map geometry rather than assumed.
    /// </summary>
    public int WalkToSource(Position from) =>
        _sourceField is null
            ? 0
            : Math.Min(
                Tactics.Unreachable,
                Tactics.DistanceAt(this, _sourceField, from));

    /// <summary>Progress required for one capture, or 0 when unknown.</summary>
    public int CaptureThreshold => Capture?.Threshold ?? 0;

    /// <summary>
    /// True when net objective weight scales capture pressure, so a second
    /// weighted body on the ground is a second unit of gain per tick rather
    /// than a redundant one.
    /// </summary>
    public bool SurplusWeightScalesGain =>
        Capture?.SurplusWeightScalesGain ?? false;

    /// <summary>
    /// Facing coupling declared by the movement profile a form references.
    /// A contract that publishes no coupling means the inert
    /// <see cref="GenericActorRulesContract.MovementFacingCoupling.PreserveFacing"/>.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling CouplingFor(
        string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        if (form is null
            || !_movement.TryGetValue(
                form.MovementProfileId,
                out GenericActorRulesContract.MovementProfile? profile))
        {
            return GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
        }
        return profile.FacingCoupling;
    }

    /// <summary>
    /// How many distinct firing lines the map geometry allows onto a tile,
    /// given the widest gun the ruleset declares. It is a static property of
    /// walls and range, so an occupier can choose the least sniped tile of an
    /// objective without ever seeing the shooter.
    /// </summary>
    public int ExposureAt(Position position) =>
        InBounds(position) ? _exposure[(position.Y * Width) + position.X] : int.MaxValue;

    /// <summary>
    /// Which one-tile corridor RUN a tile belongs to, or zero when the tile is
    /// not a choke. A choke is a walkable tile whose walkable cardinal
    /// neighbours number at most one, or exactly two that are mutually
    /// opposite: a tile two bodies cannot pass abreast, because the movement
    /// rules refuse same-destination moves, swaps, and following a vacated
    /// actor. Runs are the connected components of those tiles, so "is my
    /// sibling already committed to this corridor" is one integer comparison.
    ///
    /// <para>It is pure map geometry — walls only — so it is computed once per
    /// life and is identical for every body of mine. That is what lets four
    /// independent lives apply one precedence rule to a corridor without any
    /// shared memory: they all label the map the same way.</para>
    /// </summary>
    public int ChokeRunAt(Position position) =>
        InBounds(position) ? _chokeRun[(position.Y * Width) + position.X] : 0;

    /// <summary>Number of one-tile corridor runs the map contains.</summary>
    public int ChokeRunCount { get; private set; }

    /// <summary>
    /// Cardinal walk distance to a single tile, cached for the whole life.
    /// Objective regions are fixed tile sets, so the field to one of their
    /// tiles never changes and the bearing assignment can afford to be exact
    /// rather than Chebyshev-approximate — which matters, because an
    /// approximation that ignores walls assigns two bodies to targets whose
    /// real routes cross around a block.
    /// </summary>
    public int[] FieldToTile(Position tile)
    {
        if (_tileFields.TryGetValue(tile, out int[]? cached))
            return cached;
        int[] field = Tactics.DistanceField(this, [tile]);
        _tileFields[tile] = field;
        return field;
    }

    private readonly Dictionary<Position, int[]> _tileFields = [];

    private int[] ResolveChokeRuns()
    {
        int[] run = new int[Width * Height];
        var stack = new Stack<Position>();
        int next = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var seed = new Position(x, y);
                if (run[(y * Width) + x] != 0 || !IsChokeTile(seed))
                    continue;
                next++;
                stack.Push(seed);
                while (stack.Count > 0)
                {
                    Position current = stack.Pop();
                    int index = (current.Y * Width) + current.X;
                    if (run[index] != 0)
                        continue;
                    run[index] = next;
                    foreach (Direction direction in Tactics.Cardinals)
                    {
                        (int dx, int dy) = direction.Vector();
                        Position neighbour = current.Offset(dx, dy);
                        if (!IsChokeTile(neighbour))
                            continue;
                        if (run[(neighbour.Y * Width) + neighbour.X] == 0)
                            stack.Push(neighbour);
                    }
                }
            }
        }
        ChokeRunCount = next;
        return run;
    }

    private bool IsChokeTile(Position tile)
    {
        if (IsWall(tile))
            return false;
        int open = 0;
        bool north = false;
        bool south = false;
        bool east = false;
        bool west = false;
        foreach (Direction direction in Tactics.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            if (IsWall(tile.Offset(dx, dy)))
                continue;
            open++;
            north |= direction == Direction.North;
            south |= direction == Direction.South;
            east |= direction == Direction.East;
            west |= direction == Direction.West;
        }
        if (open <= 1)
            return true;
        // Exactly two open neighbours that face each other: a straight
        // one-tile corridor. Two open neighbours at a right angle is a corner,
        // which one body can turn in while another waits beside it.
        return open == 2 && ((north && south) || (east && west));
    }

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

    /// <summary>
    /// Counts, for every tile, the number of (origin, heading) firing lines
    /// that could put a projectile on it. Walls and the declared corner rule
    /// terminate a line exactly as they terminate a real bolt.
    /// </summary>
    private int[] ResolveExposure()
    {
        var exposure = new int[Width * Height];
        int reach = Math.Max(1, WidestAttackTiles);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var origin = new Position(x, y);
                if (IsWall(origin))
                    continue;
                foreach (ProjectileHeading heading in Headings)
                {
                    (int dx, int dy) = heading.Vector();
                    Position cursor = origin;
                    for (int step = 0; step < reach; step++)
                    {
                        Position next = cursor.Offset(dx, dy);
                        if (IsWall(next))
                            break;
                        if (StrictCorners
                            && dx != 0
                            && dy != 0
                            && (IsWall(cursor.Offset(dx, 0))
                                || IsWall(cursor.Offset(0, dy))))
                        {
                            break;
                        }
                        cursor = next;
                        exposure[(cursor.Y * Width) + cursor.X]++;
                    }
                }
            }
        }
        return exposure;
    }

    private static readonly ProjectileHeading[] Headings =
    [
        ProjectileHeading.North,
        ProjectileHeading.NorthEast,
        ProjectileHeading.East,
        ProjectileHeading.SouthEast,
        ProjectileHeading.South,
        ProjectileHeading.SouthWest,
        ProjectileHeading.West,
        ProjectileHeading.NorthWest,
    ];

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
