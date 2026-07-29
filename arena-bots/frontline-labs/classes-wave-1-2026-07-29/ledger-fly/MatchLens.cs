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
                if (isBank
                    && home is null
                    && assignment.AssignedRespawnSpawnId is string spawnId)
                {
                    home = SpawnPosition(spawnId);
                }
            }
            else if (isBank)
            {
                _enemyBankUnits.Add(assignment.UnitId);
            }
        }

        home ??= Contract.InitialDeployment.Lives
            .Where(life => life.TeamId == TeamId)
            .Select(life => SpawnPosition(life.SpawnId))
            .FirstOrDefault(position => position is not null);
        HomeAnchor = home ?? new Position(Width / 2, Height / 2);
        IsBankSlot = _bankSlotByUnit.TryGetValue(UnitId, out bool bank) && bank;
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
