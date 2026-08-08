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
            }
        }
        _ownSlots.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));

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
        int reach = 0;
        foreach (GenericActorRulesContract.Form form in Contract.Rules.Forms)
        {
            if (ownFormIds.Contains(form.Id))
                continue;
            GenericActorRulesContract.AttackProfile? profile = Attack(form.Id);
            if (profile is not null)
                reach = Math.Max(reach, profile.Projectile.MaxTravelTiles);
        }
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

        // Whether the bodies this team can field are armed with a curve. Under a
        // facing-locked profile the facing IS the movement lane, so a gun that
        // may bend once reaches a body 45 degrees off that lane without paying
        // the rotation that would also cancel the step - the difference between
        // a front that trades and a front that walks into fire with its shot
        // pointing the wrong way. Read from every form the roster may host, so
        // it is a team fact rather than this life's current form.
        foreach (string formId in ownFormIds)
        {
            GenericActorRulesContract.AttackProfile? profile = Attack(formId);
            if (profile is null)
                continue;
            GenericActorRulesContract.ShotProgramDefinition program =
                profile.ShotProgram;
            if (program.Enabled
                && program.MaxBendCount > 0
                && program.MaxBendAfterTiles > 0)
            {
                BendEnvelope = true;
                break;
            }
        }
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
    /// Whether any form this team may field declares a usable one-bend envelope
    /// on its mobile gun. It is the difference between a front body that can
    /// answer an off-lane contact this tick and one that must spend a rotation
    /// first, which is the whole reason the bank can or cannot afford to sit out
    /// an exchange.
    /// </summary>
    public bool BendEnvelope { get; }

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
