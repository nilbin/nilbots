using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Everything LedgerFly needs to know about the match that never changes,
/// resolved once from <see cref="GenericActorMatchStart.Contract"/>. Nothing in
/// here is hard-coded: unlock ticks, fabrication routes, placement offsets,
/// tile tags, form stats, and the ranking channel are all read from the
/// delivered contract, so the same source runs on the class arm, the base
/// duel-depth arm, and the automatic-companion arm.
/// </summary>
internal sealed class MatchLens
{
    private readonly bool[] _wall;
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms =
        new(StringComparer.Ordinal);
    private readonly
        Dictionary<string, GenericActorRulesContract.AttackProfile> _attacks =
            new(StringComparer.Ordinal);
    private readonly HashSet<Position> _spawnProtected = [];
    private readonly HashSet<Position> _transitionForbidden = [];
    private readonly Dictionary<int, bool> _bankSlotByUnit = [];
    private readonly HashSet<int> _enemyBankUnits = [];
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movement = new(StringComparer.Ordinal);
    private readonly List<int?> _enemyUnlockTicks = [];
    private readonly
        Dictionary<string, GenericActorRulesContract.LifecycleProfile> _profiles =
            new(StringComparer.Ordinal);
    private readonly List<SlotRole> _ownSlots = [];
    private readonly List<SlotRole> _enemySlots = [];

    public MatchLens(GenericActorMatchStart start)
    {
        Contract = start.Contract;
        TeamId = start.ActorId.TeamId;
        UnitId = start.ActorId.UnitId;
        ParticipantId = start.ParticipantId;

        GenericActorMapContract map = Contract.Map;
        Width = map.Width;
        Height = map.Height;
        _wall = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            string row = y < map.TileRows.Length ? map.TileRows[y] : string.Empty;
            for (int x = 0; x < Width; x++)
                _wall[(y * Width) + x] = x >= row.Length || row[x] == '#';
        }

        foreach (GenericActorMapContract.TileTag tag in map.TileTags)
        {
            HashSet<Position> sink = tag.Kind switch
            {
                GenericActorMapContract.TileTagKind.SpawnProtected =>
                    _spawnProtected,
                _ => _transitionForbidden,
            };
            foreach (Position tile in tag.Tiles)
                sink.Add(tile);
        }

        foreach (GenericActorRulesContract.Form form in Contract.Rules.Forms)
            _forms[form.Id] = form;
        foreach (GenericActorRulesContract.AttackProfile profile
                 in Contract.Rules.AttackProfiles)
        {
            _attacks[profile.Id] = profile;
        }
        foreach (GenericActorRulesContract.MovementProfile profile
                 in Contract.Rules.MovementProfiles)
        {
            _movement[profile.Id] = profile;
        }
        foreach (GenericActorRulesContract.LifecycleProfile profile
                 in Contract.Rules.Lifecycle.Profiles)
        {
            _profiles[profile.ProfileId] = profile;
        }

        if (Contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding frontline)
        {
            ObjectiveTiles = frontline.OrderedObjectiveRegionIds
                .Select(RegionTiles)
                .ToArray();
            AdvanceDelta = frontline.TeamAdvances
                .Where(advance => advance.TeamId == TeamId)
                .Select(advance => advance.ObjectiveIndexDelta)
                .DefaultIfEmpty(1)
                .First();
        }
        else
        {
            ObjectiveTiles = [];
            AdvanceDelta = 1;
        }

        MaxTicks = Contract.Rules.Limits.MaxTicks;
        CaptureThreshold =
            Contract.Rules.GameMode
                is GenericActorRulesContract.FrontlineGameMode frontlineMode
                ? frontlineMode.Capture.Threshold
                : 15;
        RankingChannel = Contract.Rules.GameMode.Victory.TimeoutRanking
            .Select(ranking => ranking.Channel)
            .Concat(Contract.Rules.GameMode.ScoreCatalog
                .Select(channel => channel.Channel))
            .DefaultIfEmpty("territorial-progress")
            .First();
        AlliedProjectilesPassAllies = string.Equals(
            Contract.Rules.Collisions.AlliedProjectileContact,
            "pass-through",
            StringComparison.Ordinal);

        var respawnProfiles = Contract.Rules.Lifecycle.Profiles
            .Where(profile => profile.AutomaticReturnFormId is not null)
            .Select(profile => profile.ProfileId)
            .ToHashSet(StringComparer.Ordinal);
        Position? home = null;
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Contract.LifecycleAssignments)
        {
            bool isBank = assignment.AssignedRespawnSpawnId is not null
                || respawnProfiles.Contains(assignment.LifecycleProfileId);
            if (assignment.TeamId == TeamId)
            {
                _bankSlotByUnit[assignment.UnitId] = isBank;
                _ownSlots.Add(
                    new SlotRole(
                        assignment.UnitId,
                        isBank,
                        assignment.UnlockTick,
                        _profiles.TryGetValue(
                            assignment.LifecycleProfileId,
                            out GenericActorRulesContract.LifecycleProfile? own)
                            ? own.DelayTicks
                            : 0));
                if (isBank && BankUnitId < 0)
                {
                    BankUnitId = assignment.UnitId;
                    BankFormId = assignment.AllowedFormIds.Length > 0
                        ? assignment.AllowedFormIds[0]
                        : BankFormId;
                }
                if (isBank
                    && home is null
                    && assignment.AssignedRespawnSpawnId is string spawnId)
                {
                    home = SpawnPosition(spawnId);
                }
            }
            else
            {
                _enemyUnlockTicks.Add(assignment.UnlockTick);
                if (isBank)
                    _enemyBankUnits.Add(assignment.UnitId);
                _enemySlots.Add(
                    new SlotRole(
                        assignment.UnitId,
                        isBank,
                        assignment.UnlockTick,
                        _profiles.TryGetValue(
                            assignment.LifecycleProfileId,
                            out GenericActorRulesContract.LifecycleProfile? theirs)
                            ? theirs.DelayTicks
                            : 0));
            }
        }
        _ownSlots.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
        _enemySlots.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));

        home ??= Contract.InitialDeployment.Lives
            .Where(life => life.TeamId == TeamId)
            .Select(life => SpawnPosition(life.SpawnId))
            .FirstOrDefault(position => position is not null);
        HomeAnchor = home ?? new Position(Width / 2, Height / 2);
        IsBankSlot = _bankSlotByUnit.TryGetValue(UnitId, out bool bank) && bank;

        var ownFormIds = Contract.LifecycleAssignments
            .Where(assignment => assignment.TeamId == TeamId)
            .SelectMany(assignment => assignment.AllowedFormIds)
            .ToHashSet(StringComparer.Ordinal);
        if (BankFormId.Length == 0)
        {
            BankFormId = ownFormIds
                .Order(StringComparer.Ordinal)
                .FirstOrDefault(string.Empty);
        }
        int reach = 0;
        int hit = 0;
        foreach (GenericActorRulesContract.Form form in Contract.Rules.Forms)
        {
            if (ownFormIds.Contains(form.Id))
                continue;
            GenericActorRulesContract.AttackProfile? profile = Attack(form.Id);
            if (profile is not null)
            {
                reach = Math.Max(reach, profile.Projectile.MaxTravelTiles);
                // The biggest single contact any opposing form declares. On a
                // salvo arm one of those forms is a fan whose bolts cost twice
                // what a mobile bolt costs, and this chassis's prime does not
                // have twice the health — so the number is read once here and
                // priced everywhere, rather than assumed to be one.
                hit = Math.Max(hit, profile.Projectile.DamagePerHit);
            }
        }
        LongestEnemyHit = Math.Max(1, hit);
        // Class is a DECLARED topology fact on both sides, not something to
        // recover from a form-ID prefix. The template's ClassOf still splits
        // `<class>-<role>`, which is the pinned naming convention and not the
        // contract; the typed field is authoritative and survives a chassis
        // whose forms are named anything at all.
        OwnClassId = ClassOfTeam(TeamId);
        foreach (PublicScoringTeam team in Contract.Topology.Teams)
        {
            if (team.TeamId == TeamId)
                continue;
            EnemyClassId = team.ClassId ?? EnemyClassId;
            break;
        }
        EnemyClassLabel = EnemyClassId ?? "the other side";

        LongestEnemyReach = reach > 0
            ? reach
            : Contract.Rules.AttackProfiles
                .Select(profile => profile.Projectile.MaxTravelTiles)
                .DefaultIfEmpty(8)
                .Max();

        // The three pendulum facts, all read rather than assumed. None of them
        // changes the observation schema, so a bot that does not read them
        // simply plays the wrong game on those arms: it prices every capture as
        // an advance, treats a second body on the objective as dead weight, and
        // believes it will walk home after dying.
        Capture = ArenaBasics.Capture(Contract);
        RallyForward = ArenaBasics.ArrivalsRallyForward(Contract);
        AdvanceHeading = ArenaBasics.AdvanceDirection(Contract, TeamId);

        // Whether the bodies this team can field can answer a contact that is
        // NOT on their movement lane. Under a facing-locked profile the facing
        // is the movement lane, so a gun that reaches 45 degrees off it without
        // spending a rotation is the difference between a front that trades and
        // a front that walks into fire with its shot pointing the wrong way.
        // TWO declared envelopes now grant that, and either is enough: a bend
        // (curve onto the diagonal after a tile or two) or an initial aim offset
        // (launch straight down the diagonal, zero bends). Wave 4 could only
        // read the first, which is why this is a reading and not a rename.
        foreach (string formId in ownFormIds)
        {
            GenericActorRulesContract.AttackProfile? profile = Attack(formId);
            if (profile is null)
                continue;
            GenericActorRulesContract.ShotProgramDefinition program =
                profile.ShotProgram;
            if (!program.Enabled)
                continue;
            bool bends = program.MaxBendCount > 0
                && program.MaxBendAfterTiles > 0;
            bool aims = program.MinInitialAimSteps < 0
                || program.MaxInitialAimSteps > 0;
            if (bends || aims)
            {
                OffAxisEnvelope = true;
                break;
            }
        }

        // The unit of account, restated for a contract whose bodies do not cost
        // what they used to. One capture is THRESHOLD progress bought at GAIN
        // per tick of sole presence, so a capture is priced in ticks, and every
        // slot's own lifecycle profile prices a body in the same currency: the
        // ticks the slot spends unavailable after the body in it dies. When a
        // body costs more ticks than a capture buys, a trade that used to be
        // profitable is not - and that comparison is entirely contract data.
        int gain = Math.Max(1, Capture?.GainPerSoleTeamTick ?? 1);
        ConversionTicks = Math.Max(
            1,
            ((Capture?.Threshold ?? CaptureThreshold) + gain - 1) / gain);
        OwnReplacementTicks = ReplacementTicks(UnitId);
        PipelineStallTicks = _ownSlots
            .Where(slot => !slot.IsBank)
            .Sum(slot => slot.RebuildDelayTicks);

        foreach (GenericActorRulesContract.ActionDefinition action
                 in Contract.Rules.Actions)
        {
            if (action.Kind == GenericActorRulesContract.ActionKind.Movement)
                MovementActionIds.Add(action.Id);
        }

        // THE CHANNEL, read rather than assumed. Everything below is inert on a
        // contract that does not channel: the cap falls back to 1, the erosion
        // multiplier to 1, and the interrupt to nothing, which is exactly the
        // arithmetic every earlier revision played.
        if (Contract.Rules.GameMode
            is GenericActorRulesContract.FrontlineGameMode gameMode)
        {
            GenericActorRulesContract.FrontlineCapture capture =
                gameMode.Capture;
            Channels = capture.ControlPolicy.Contains(
                "stationary-claim-weight",
                StringComparison.Ordinal);
            StationaryCap = capture.StationaryGainMultiplierCap > 0
                ? capture.StationaryGainMultiplierCap
                : 1;
            ErosionMultiplier = capture.OpposingErosionMultiplier > 0
                ? capture.OpposingErosionMultiplier
                : 1;
            if (capture.ClaimInterrupt is
                GenericActorRulesContract.FrontlineClaimInterrupt interrupt)
            {
                RevertPerDamage = interrupt.RevertPerDamagePoint;
                InterruptOnObjective = interrupt.Scope.Contains(
                    "on-active-objective-region",
                    StringComparison.Ordinal);
                WholeRunRevert = interrupt.Granularity.Contains(
                    "whole-run",
                    StringComparison.Ordinal);
            }
            Economy = gameMode.ScrapEconomy;
        }
        else
        {
            StationaryCap = 1;
            ErosionMultiplier = 1;
        }

        // THE STORE. The verb only exists on one purchase mode; on the control
        // level the bank buys by itself and a purchase routine is dead code
        // that would cost a body its tick, so it is switched off by reading.
        if (Economy is GenericActorRulesContract.FrontlineScrapEconomy economy)
        {
            BuysByHand = string.Equals(
                economy.PurchaseMode,
                "invest-action",
                StringComparison.Ordinal);
            int index = TeamId;
            if (index >= 0 && index < economy.BankRegionIds.Length)
                BankTiles = RegionTiles(economy.BankRegionIds[index]).ToHashSet();
            foreach (GenericActorRulesContract.ScrapVeinSite site
                     in economy.VeinSites)
            {
                _veins.Add(new Position(site.X, site.Y));
            }
        }
    }

    private readonly List<Position> _veins = [];

    /// <summary>
    /// Stable IDs of every action the contract classes as movement. "Did that
    /// body change tile" is answered against this set rather than against the
    /// string <c>move</c>, so a contract that renames or adds a movement verb
    /// still answers correctly.
    /// </summary>
    public HashSet<string> MovementActionIds { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Whether a capture on this contract is a CHANNEL: claim weight counts
    /// only bodies whose tile did not change this tick, denial weight counts
    /// all of them, and surplus scales the gain. False restores the binary or
    /// net-weight policies every earlier revision was written against.
    /// </summary>
    public bool Channels { get; }

    /// <summary>
    /// Largest gain multiplier stationary surplus can buy. One on a contract
    /// that does not scale, which makes every "is another body worth sending"
    /// test below answer no without a special case.
    /// </summary>
    public int StationaryCap { get; }

    /// <summary>How much faster an opposing claim erodes than a fresh one builds.</summary>
    public int ErosionMultiplier { get; }

    /// <summary>Progress a controlling body loses per point of health removed.</summary>
    public int RevertPerDamage { get; }

    /// <summary>Whether the interrupt is scoped to the active objective region.</summary>
    public bool InterruptOnObjective { get; }

    /// <summary>Whether one hit reverts the controller's whole run.</summary>
    public bool WholeRunRevert { get; }

    /// <summary>The declared economy, or null when the ruleset has none.</summary>
    public GenericActorRulesContract.FrontlineScrapEconomy? Economy { get; }

    /// <summary>Whether spending is a player verb rather than an automatic clock.</summary>
    public bool BuysByHand { get; }

    /// <summary>Tiles where a load banks itself at the end of a tick.</summary>
    public HashSet<Position> BankTiles { get; } = [];

    /// <summary>Declared deposit addresses, in contract order.</summary>
    public IReadOnlyList<Position> Veins => _veins;

    /// <summary>Biggest single contact any opposing form declares.</summary>
    public int LongestEnemyHit { get; } = 1;

    /// <summary>
    /// Our economy anchor's unit slot — the one the contract returns
    /// automatically and, on this chassis, the only one carrying a fabrication
    /// route. Minus one when the roster declares no such slot.
    /// </summary>
    public int BankUnitId { get; private set; } = -1;

    /// <summary>The form that slot's lives occupy.</summary>
    public string BankFormId { get; private set; } = string.Empty;

    /// <summary>Declared sight range of a form; zero when it has no sensor.</summary>
    public int VisionRange(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        if (form?.VisionProfileId is not string id)
            return 0;
        foreach (GenericActorRulesContract.VisionProfile profile
                 in Contract.Rules.VisionProfiles)
        {
            if (string.Equals(profile.Id, id, StringComparison.Ordinal))
                return profile.Range;
        }
        return 0;
    }

    /// <summary>Frozen contract delivered to this life.</summary>
    public GenericActorResolvedMatchContract Contract { get; }
    /// <summary>Scoring team this life fights for.</summary>
    public int TeamId { get; }
    /// <summary>Stable unit slot this life occupies.</summary>
    public int UnitId { get; }
    /// <summary>Submitted participant controlling this life.</summary>
    public int ParticipantId { get; }
    /// <summary>Map width in tiles.</summary>
    public int Width { get; }
    /// <summary>Map height in tiles.</summary>
    public int Height { get; }
    /// <summary>Objective tiles per ordered Frontline position.</summary>
    public Position[][] ObjectiveTiles { get; }
    /// <summary>Signed objective-index delta for one of our advances.</summary>
    public int AdvanceDelta { get; }
    /// <summary>Tile our returning bank body deploys onto.</summary>
    public Position HomeAnchor { get; }
    /// <summary>Whether this life sits in the slot that returns automatically.</summary>
    public bool IsBankSlot { get; }
    /// <summary>Declared tick cap of the match.</summary>
    public int MaxTicks { get; }
    /// <summary>Progress required to complete one capture.</summary>
    public int CaptureThreshold { get; }
    /// <summary>Score channel that decides a timeout ranking.</summary>
    public string RankingChannel { get; }
    /// <summary>Whether allied bodies do not stop our own projectiles.</summary>
    public bool AlliedProjectilesPassAllies { get; }
    /// <summary>Longest projectile travel any form we do not own declares.</summary>
    public int LongestEnemyReach { get; }

    /// <summary>
    /// Capture policy values this contract plays by: threshold, gain, decay,
    /// redeploy pause, the ratchet hold length (null when no hold is declared),
    /// whether surplus objective weight scales capture pressure, and whether
    /// only an enemy standing alone erodes a claim.
    /// </summary>
    public ArenaBasics.CaptureRules? Capture { get; }

    /// <summary>
    /// Whether automatic returns and activations land on our own-side
    /// chain-adjacent objective instead of at the slot's spawn anchor. It is the
    /// difference between a death costing a walk across the map and costing only
    /// the return clock, which is most of what the bank's caution is priced on.
    /// </summary>
    public bool RallyForward { get; }

    /// <summary>Map heading this team advances along, derived from the chain.</summary>
    public Direction? AdvanceHeading { get; }

    /// <summary>Declared chassis class of this team, or null on a classless contract.</summary>
    public string? OwnClassId { get; }

    /// <summary>
    /// Whether any form this team may field can put a bolt 45 degrees off its
    /// own facing without spending a rotation — through a declared bend, a
    /// declared initial aim offset, or both. It is the difference between a
    /// front body that can answer an off-lane contact this tick and one that
    /// must turn first, which is the whole reason the bank can or cannot afford
    /// to sit out an exchange.
    /// </summary>
    public bool OffAxisEnvelope { get; }

    /// <summary>
    /// Ticks of sole presence one capture costs: the declared threshold divided
    /// by the declared gain per tick, rounded up. This is the exchange rate
    /// between TIME and GROUND, and it is what every body price below is
    /// measured against.
    /// </summary>
    public int ConversionTicks { get; }

    /// <summary>
    /// What losing THIS body costs in ticks, read from its own slot's lifecycle
    /// profile: a rebuild clock for a slot that only refills through an explicit
    /// fabrication, an automatic-return clock for the bank. Compared against
    /// <see cref="ConversionTicks"/> it says whether this body is dearer than
    /// the ground it is standing on.
    /// </summary>
    public int OwnReplacementTicks { get; }

    /// <summary>
    /// Total ticks of slot downtime the bank's own death sets running: the sum
    /// of the rebuild clocks of every slot it feeds by hand, because none of
    /// them refills while the bank is dead or walking back to a source tile.
    /// </summary>
    public int PipelineStallTicks { get; }

    /// <summary>
    /// What this body pays, in tile score, for standing where a bolt is going.
    /// It is <see cref="OwnReplacementTicks"/> expressed against
    /// <see cref="ConversionTicks"/>: a body that costs exactly one capture to
    /// replace prices exposure at the flat 12 that revision 4 measured, a body
    /// on a slower clock prices it higher, and a body the contract refills
    /// cheaply prices it lower. Nothing here is tuned per arm — the ratio is
    /// two declared numbers.
    /// </summary>
    public int ThreatPremium =>
        8 + (4 * OwnReplacementTicks / Math.Max(1, ConversionTicks));

    /// <summary>Declared chassis class of the opposition, or null when absent.</summary>
    public string? EnemyClassId { get; private set; }

    /// <summary>
    /// Stable unit slots this team owns, in unit order, with the two facts that
    /// decide what a body is worth: when the slot unlocks and how long the slot
    /// takes to become available again after the body in it dies. Both come from
    /// the slot's own lifecycle assignment and profile, never from a count.
    /// </summary>
    public IReadOnlyList<SlotRole> OwnSlots => _ownSlots;

    /// <summary>
    /// How many of this team's slots the bank has to feed by hand: slots whose
    /// lifecycle profile declares no automatic return, so a body only ever
    /// appears in them through an explicit fabrication the bank pays a combat
    /// action for. This is the bank's THROUGHPUT, and it is the number that
    /// decides how much its own survival is worth — a bank that owns four
    /// pipelines is worth more alive than a bank that owns two, whatever the
    /// arrival policy does to the cost of dying.
    /// </summary>
    public int Pipelines => _ownSlots.Count(slot => !slot.IsBank);

    /// <summary>
    /// Ticks one of our own slots spends unavailable after its body dies, from
    /// that slot's own lifecycle profile. Slots of one team are NOT
    /// interchangeable currency on a tuned roster: a late slot can declare a
    /// slower clock than an early one, and the bank declares an automatic
    /// return instead of a rebuild.
    /// </summary>
    public int ReplacementTicks(int unitId)
    {
        foreach (SlotRole slot in _ownSlots)
        {
            if (slot.UnitId == unitId)
                return slot.RebuildDelayTicks;
        }
        return 0;
    }

    /// <summary>
    /// The same price on the other side of the books: ticks an opposing slot
    /// spends unavailable after we kill the body in it. Enemy slot STATE is
    /// never delivered, but the enemy's lifecycle assignments and profiles are
    /// public in the contract, so what a kill is worth is a read rather than a
    /// guess — and it is how this doctrine chooses between two bodies it can
    /// equally reach.
    /// </summary>
    public int EnemyReplacementTicks(int unitId)
    {
        foreach (SlotRole slot in _enemySlots)
        {
            if (slot.UnitId == unitId)
                return slot.RebuildDelayTicks;
        }
        return 0;
    }

    /// <summary>
    /// One stable slot's declared role.
    /// </summary>
    /// <param name="UnitId">Stable unit identifier.</param>
    /// <param name="IsBank">Whether the slot returns automatically.</param>
    /// <param name="UnlockTick">Declared unlock tick, or null when never dormant.</param>
    /// <param name="RebuildDelayTicks">
    /// Ticks the slot spends unavailable after its body dies. Late five-slot
    /// children declare a slower clock than early ones, so two slots of the
    /// same team are not interchangeable currency.
    /// </param>
    public sealed record SlotRole(
        int UnitId,
        bool IsBank,
        int? UnlockTick,
        int RebuildDelayTicks);

    /// <summary>Declared class of any team, straight from the topology.</summary>
    public string? ClassOfTeam(int teamId)
    {
        foreach (PublicScoringTeam team in Contract.Topology.Teams)
        {
            if (team.TeamId == teamId)
                return team.ClassId;
        }
        foreach (PublicParticipant participant in Contract.Topology.Participants)
        {
            if (participant.TeamId == teamId)
                return participant.ClassId;
        }
        return null;
    }

    /// <summary>
    /// Multi-projectile launch shape of a form's gun, or null when one attack
    /// means one bolt. Canonical contracts omit the field on every ordinary gun,
    /// so absent is the answer rather than a gap: a fan is public data, and a
    /// bot standing in one has read the contract and chosen to.
    /// </summary>
    public GenericActorRulesContract.AttackVolley? Volley(string formId) =>
        Attack(formId)?.Volley;

    /// <summary>
    /// Whether a form deflects hostile bolts that arrive inside its facing
    /// quadrant, launching a team-flipped return along the reversed heading.
    /// Absent on every unguarded form, so poking a face is a decision with a
    /// readable price rather than a surprise.
    /// </summary>
    public bool Guards(string formId) =>
        Form(formId)?.ProjectileGuard
            == GenericActorRulesContract.FormProjectileGuard
                .FacingQuadrantContactsDeflected;

    /// <summary>
    /// Same-life routes out of <paramref name="formId"/> that lead into a
    /// stance — a form that keeps objective weight and adds either a fan or a
    /// guard. Routes into a zero-weight form are deliberately NOT stances: a
    /// body with no objective weight has been deleted from the ledger, and this
    /// doctrine does not pay that price for durability.
    /// </summary>
    public List<StanceRoute> StanceRoutes(string formId)
    {
        var routes = new List<StanceRoute>();
        foreach (GenericActorRulesContract.SameLifeTransition transition
                 in Contract.Rules.SameLifeTransitions)
        {
            if (transition
                is not GenericActorRulesContract.FormTransition form)
            {
                continue;
            }
            if (!string.Equals(
                    form.SourceFormId,
                    formId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            GenericActorRulesContract.Form? target = Form(form.TargetFormId);
            if (target is null || target.ObjectiveWeight <= 0)
                continue;
            bool fan = Volley(form.TargetFormId) is not null;
            bool guard = Guards(form.TargetFormId);
            if (!fan && !guard)
                continue;
            routes.Add(
                new StanceRoute(
                    form.TransitionId,
                    form.ActionId,
                    form.TargetFormId,
                    form.Windup.DurationTicks,
                    fan,
                    guard,
                    form.AutomaticReturn?.Threshold,
                    form.IrreversibleForLife));
        }
        routes.Sort((left, right) =>
            string.CompareOrdinal(left.TargetFormId, right.TargetFormId));
        return routes;
    }

    /// <summary>
    /// The parameterless route back out of <paramref name="formId"/>: the same
    /// mobilize the engine fires for us when the stance budget runs out, and the
    /// one we spend ourselves to leave early. Null when the form has no way back.
    /// </summary>
    public GenericActorRulesContract.FormTransition? ReturnRoute(string formId)
    {
        GenericActorRulesContract.FormTransition? best = null;
        foreach (GenericActorRulesContract.SameLifeTransition transition
                 in Contract.Rules.SameLifeTransitions)
        {
            if (transition
                is not GenericActorRulesContract.FormTransition form)
            {
                continue;
            }
            if (!string.Equals(
                    form.SourceFormId,
                    formId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            GenericActorRulesContract.Form? target = Form(form.TargetFormId);
            if (target is null
                || Volley(form.TargetFormId) is not null
                || Guards(form.TargetFormId))
            {
                continue;
            }
            if (best is null
                || string.CompareOrdinal(
                    form.TargetFormId,
                    best.TargetFormId) < 0)
            {
                best = form;
            }
        }
        return best;
    }

    /// <summary>
    /// One route into a weight-preserving stance, reduced to what the doctrine
    /// decides with.
    /// </summary>
    /// <param name="TransitionId">Stable transition identifier.</param>
    /// <param name="ActionId">Action that requests the change.</param>
    /// <param name="TargetFormId">Stance form entered at completion.</param>
    /// <param name="WindupTicks">Ticks of committed, Wait-only windup.</param>
    /// <param name="Fan">Whether the stance's gun fires more than one bolt.</param>
    /// <param name="Guard">Whether the stance deflects bolts on its arc.</param>
    /// <param name="BudgetThreshold">
    /// Count at which the engine returns us by itself, or null when the stance
    /// declares no budget and we own the exit entirely.
    /// </param>
    /// <param name="Irreversible">Whether entering costs the life its way back.</param>
    public sealed record StanceRoute(
        string TransitionId,
        string ActionId,
        string TargetFormId,
        int WindupTicks,
        bool Fan,
        bool Guard,
        int? BudgetThreshold,
        bool Irreversible);

    /// <summary>
    /// Where our own next automatic arrival is expected: the own-side
    /// chain-adjacent objective under a forward rally, otherwise the slot's
    /// declared spawn anchor. Used to price a death and to keep the bank's
    /// staging band anchored on something that moves with the front, because
    /// a spawn anchor is simply the wrong tile on a rallying contract.
    /// </summary>
    public Position RearAnchor(GenericActorContext context)
    {
        Position[] tiles = ArenaBasics.ExpectedArrivalTiles(Contract, context);
        return tiles.Length == 0 ? HomeAnchor : Centroid(tiles);
    }

    /// <summary>Integer centroid of a tile set; the caller guarantees it is non-empty.</summary>
    public static Position Centroid(IReadOnlyList<Position> tiles)
    {
        int x = 0;
        int y = 0;
        foreach (Position tile in tiles)
        {
            x += tile.X;
            y += tile.Y;
        }
        return new Position(x / tiles.Count, y / tiles.Count);
    }

    /// <summary>
    /// How far a tile sits along this team's advance axis. Larger is deeper
    /// into opposing ground. Chain-derived, so it stays correct on a contract
    /// that puts a returning body in front of its own spawn.
    /// </summary>
    public int Forwardness(Position tile) =>
        AdvanceHeading switch
        {
            Direction.East => tile.X,
            Direction.West => -tile.X,
            Direction.South => tile.Y,
            Direction.North => -tile.Y,
            _ => 0,
        };

    /// <summary>
    /// Chassis label of the opposing team when the contract carries class
    /// chassis, otherwise a neutral word. It is used for replay-readable debug
    /// text only: every decision above is conditioned on declared stats and
    /// routes, which generalize to classes that do not exist yet.
    /// </summary>
    public string EnemyClassLabel { get; private set; } = "the other side";

    /// <summary>True when the tile is outside the map or a wall.</summary>
    public bool IsWall(Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= Width
        || position.Y >= Height
        || _wall[(position.Y * Width) + position.X];

    /// <summary>True when the tile is inside the map and walkable.</summary>
    public bool IsOpen(Position position) => !IsWall(position);

    /// <summary>Form catalog entry, or null when the form is unknown.</summary>
    public GenericActorRulesContract.Form? Form(string formId) =>
        _forms.TryGetValue(formId, out GenericActorRulesContract.Form? form)
            ? form
            : null;

    /// <summary>Attack profile of a form, or null when it cannot attack.</summary>
    public GenericActorRulesContract.AttackProfile? Attack(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form?.AttackProfileId is string id
            && _attacks.TryGetValue(
                id,
                out GenericActorRulesContract.AttackProfile? profile)
            ? profile
            : null;
    }

    /// <summary>Maximum health declared for a form; 3 when unknown.</summary>
    public int MaxHealth(string formId) => Form(formId)?.MaxHealth ?? 3;

    /// <summary>
    /// Whether a body in this form counts toward capturing and contesting at
    /// all. A zero-weight form is a body that has left the ledger: it holds no
    /// ground, and a contract may well let it come back and fortify again as
    /// often as it likes. Read from the form, so it is one fact for our own
    /// bodies and for theirs.
    /// </summary>
    public bool Scores(string formId) => (Form(formId)?.ObjectiveWeight ?? 1) > 0;

    /// <summary>Whether an enemy unit slot is the opposing economy anchor.</summary>
    public bool IsEnemyBankUnit(int unitId) => _enemyBankUnits.Contains(unitId);

    /// <summary>Whether one of our own unit slots is the economy anchor.</summary>
    public bool IsAlliedBankUnit(int unitId) =>
        _bankSlotByUnit.TryGetValue(unitId, out bool bank) && bank;

    /// <summary>
    /// How a movement action treats this form's facing. The field is optional in
    /// the canonical contract and absent means <c>PreserveFacing</c>, so the
    /// value is resolved through the form's declared movement profile rather
    /// than assumed per arm.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling(
        string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && _movement.TryGetValue(
                form.MovementProfileId,
                out GenericActorRulesContract.MovementProfile? profile)
            ? profile.FacingCoupling
            : GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>
    /// How many opposing unit slots the contract says could already be carrying
    /// a body at <paramref name="tick"/>. Enemy slots are never delivered in
    /// <c>TeamUnits</c> and a facing-quadrant sensor sees at most a fraction of
    /// what they field, so solvency is denominated in this declared capacity
    /// rather than in the bodies currently inside our vision cone.
    /// </summary>
    public int EnemySlotCapacity(int tick)
    {
        int capacity = 0;
        foreach (int? unlock in _enemyUnlockTicks)
        {
            if (unlock is null || unlock.Value <= tick)
                capacity++;
        }
        return Math.Max(1, capacity);
    }

    /// <summary>Tiles of the currently active objective position.</summary>
    public Position[] ActiveObjective(GenericActorContext context)
    {
        if (context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode
            || mode.ActivePositionIndex < 0
            || mode.ActivePositionIndex >= ObjectiveTiles.Length)
        {
            return [];
        }
        return ObjectiveTiles[mode.ActivePositionIndex];
    }

    /// <summary>
    /// Fabrication route usable from <paramref name="formId"/>, or null when the
    /// contract gives this form no explicit forward fabrication.
    /// </summary>
    public FabricationRoute? FabricationFor(string formId)
    {
        foreach (GenericActorRulesContract.FabricationTransition transition
                 in Contract.Rules.FabricationTransitions)
        {
            if (transition
                is not GenericActorRulesContract.BoundedChildFabricationTransition
                    bounded)
            {
                continue;
            }
            if (!bounded.SourceFormIds.Contains(formId, StringComparer.Ordinal))
                continue;

            HashSet<Position> source = RoleTiles(bounded.SourceRegionRoleId);
            Filter(source, bounded.RequiredSourceTileTags, required: true);
            HashSet<Position> output = RoleTiles(bounded.OutputRegionRoleId);
            Filter(output, bounded.RequiredOutputTileTags, required: true);
            Filter(output, bounded.ForbiddenOutputTileTags, required: false);
            return new FabricationRoute(
                bounded.ActionId,
                bounded.CandidateOffsets,
                source,
                output);
        }
        return null;
    }

    /// <summary>Signed timeout-ranking score currently credited to a team.</summary>
    public long Score(GenericActorContext context, int teamId)
    {
        foreach (GenericActorContext.TeamScoreState team
                 in context.Scoreboard.Teams)
        {
            if (team.TeamId != teamId)
                continue;
            foreach (GenericActorContext.ScoreValue score in team.Scores)
            {
                if (string.Equals(
                        score.Channel,
                        RankingChannel,
                        StringComparison.Ordinal))
                {
                    return score.Value;
                }
            }
        }
        return 0;
    }

    private HashSet<Position> RoleTiles(string regionRoleId)
    {
        foreach (GenericActorResolvedMatchContract.ParticipantRegionAssignment
                     assignment in Contract.ParticipantRegionAssignments)
        {
            if (assignment.ParticipantId == ParticipantId
                && string.Equals(
                    assignment.RegionRoleId,
                    regionRoleId,
                    StringComparison.Ordinal))
            {
                return RegionTiles(assignment.MapRegionId).ToHashSet();
            }
        }
        return [];
    }

    private void Filter(
        HashSet<Position> tiles,
        ImmutableArray<GenericActorMapContract.TileTagKind> kinds,
        bool required)
    {
        foreach (GenericActorMapContract.TileTagKind kind in kinds)
        {
            HashSet<Position> tagged =
                kind == GenericActorMapContract.TileTagKind.SpawnProtected
                    ? _spawnProtected
                    : _transitionForbidden;
            if (required)
                tiles.IntersectWith(tagged);
            else
                tiles.ExceptWith(tagged);
        }
    }

    private Position[] RegionTiles(string regionId)
    {
        foreach (GenericActorMapContract.Region region in Contract.Map.Regions)
        {
            if (string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
            {
                return region.Tiles.ToArray();
            }
        }
        return [];
    }

    private Position? SpawnPosition(string spawnId)
    {
        foreach (GenericActorMapContract.SpawnAnchor anchor
                 in Contract.Map.SpawnAnchors)
        {
            if (string.Equals(anchor.SpawnId, spawnId, StringComparison.Ordinal))
                return anchor.Position;
        }
        foreach (GenericActorResolvedMatchContract.InitialSpawn spawn
                 in Contract.InitialDeployment.Spawns)
        {
            if (string.Equals(spawn.SpawnId, spawnId, StringComparison.Ordinal))
                return spawn.Position;
        }
        return null;
    }
}
