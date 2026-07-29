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
    private readonly Dictionary<string, Position[]> _regionTiles;

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
        _regionTiles = Contract.Map.Regions
            .ToDictionary(
                region => region.RegionId,
                region => region.Tiles.ToArray(),
                StringComparer.Ordinal);

        ObjectiveTiles = ResolveObjectiveTiles();
        IndexDelta = ResolveIndexDelta();
        MaxTicks = Contract.Rules.Limits.MaxTicks;

        (Forward, Lateral) = ResolveAxes();
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

    /// <summary>Ticks of uncontested presence one capture costs at this tick.</summary>
    public int TicksToCapture(int tick)
    {
        if (Capture is not { } capture)
            return 20;
        int gain = Math.Max(1, capture.GainPhaseAtTick(tick).GainPerSoleTeamTick);
        return ((capture.Threshold + gain - 1) / gain) + capture.RedeployPauseTicks;
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
