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
    private readonly ArenaBasics.CaptureRules? _captureRules;

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

        OwnMaxRange = MaxRange(OwnFormIds, mobileOnly: false);
        OwnCanBend = AnyBend(OwnFormIds);
        OpposingMobileRange = MaxRange(OpposingFormIds, mobileOnly: true);
        OpposingAnyRange = MaxRange(OpposingFormIds, mobileOnly: false);
        OpposingMobileVision = MaxVision(OpposingFormIds, mobileOnly: true);
        OpposingCanBend = AnyBend(OpposingFormIds);
        OpposingMaxHealth = OpposingFormIds
            .Select(id => _forms.TryGetValue(id, out var form) ? form.MaxHealth : 1)
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

    public bool OpposingCanBend { get; }

    public int OpposingMaxHealth { get; }

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

    public GenericActorRulesContract.Form? Form(string formId) =>
        _forms.TryGetValue(formId, out var form) ? form : null;

    public GenericActorRulesContract.AttackProfile? Attack(string formId)
    {
        if (!_forms.TryGetValue(formId, out var form)
            || form.AttackProfileId is not string profileId)
        {
            return null;
        }
        return _attacks.TryGetValue(profileId, out var attack) ? attack : null;
    }

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
        int erode = (progress + gain - 1) / gain;
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
    /// Splits the form catalog into the chassis my slots can ever wear and the
    /// chassis the other team can wear, closing over declared transition routes.
    /// A mirror leaves the opposing set empty, so it falls back to the whole
    /// catalog: the opponent is exactly as dangerous as I am.
    /// </summary>
    private (ImmutableArray<string> Own, ImmutableArray<string> Opposing)
        ResolveFormOwnership()
    {
        var own = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in Contract.LifecycleAssignments)
        {
            if (assignment.TeamId != TeamId)
                continue;
            foreach (string formId in assignment.AllowedFormIds)
                own.Add(formId);
            foreach (var profile in Contract.Rules.Lifecycle.Profiles)
            {
                if (string.Equals(
                        profile.ProfileId,
                        assignment.LifecycleProfileId,
                        StringComparison.Ordinal)
                    && profile.AutomaticReturnFormId is string returnForm)
                {
                    own.Add(returnForm);
                }
            }
        }
        foreach (var life in Contract.InitialDeployment.Lives)
        {
            if (life.TeamId == TeamId)
                own.Add(life.FormId);
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
                    && own.Contains(form.SourceFormId))
                {
                    grew |= own.Add(form.TargetFormId);
                }
            }
            foreach (var transition in Contract.Rules.FabricationTransitions)
            {
                if (transition
                        is GenericActorRulesContract.BoundedChildFabricationTransition
                            fabrication
                    && fabrication.SourceFormIds.Any(own.Contains))
                {
                    grew |= own.Add(fabrication.OutputFormId);
                }
            }
        }

        var opposing = Contract.Rules.Forms
            .Select(form => form.Id)
            .Where(id => !own.Contains(id))
            .ToImmutableArray();
        if (opposing.IsEmpty)
        {
            opposing = Contract.Rules.Forms
                .Select(form => form.Id)
                .ToImmutableArray();
        }
        return (own.Order(StringComparer.Ordinal).ToImmutableArray(), opposing);
    }

    private int MaxRange(ImmutableArray<string> formIds, bool mobileOnly)
    {
        int best = 0;
        foreach (string formId in formIds)
        {
            if (!_forms.TryGetValue(formId, out var form))
                continue;
            if (mobileOnly && form.ObjectiveWeight <= 0)
                continue;
            if (Attack(formId) is { } attack)
                best = Math.Max(best, attack.Projectile.MaxTravelTiles);
        }
        return best;
    }

    private int MaxVision(ImmutableArray<string> formIds, bool mobileOnly)
    {
        int best = 0;
        foreach (string formId in formIds)
        {
            if (!_forms.TryGetValue(formId, out var form))
                continue;
            if (mobileOnly && form.ObjectiveWeight <= 0)
                continue;
            if (_visions.TryGetValue(form.VisionProfileId, out var vision))
                best = Math.Max(best, vision.Range);
        }
        return best;
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

    private int ResolveForkReach()
    {
        int best = 0;
        foreach (string formId in OwnFormIds)
        {
            if (Attack(formId) is not { } attack)
                continue;
            var program = attack.ShotProgram;
            int reach = program.Enabled && program.MaxBendCount > 0
                ? Math.Min(
                    attack.Projectile.MaxTravelTiles,
                    program.MaxBendAfterTiles + 1)
                : Math.Max(2, attack.Projectile.MaxTravelTiles / 2);
            best = Math.Max(best, reach);
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
