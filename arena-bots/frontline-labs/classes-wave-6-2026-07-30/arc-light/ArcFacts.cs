using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Everything arc-light needs to know that never changes during a life, read
/// once from the resolved contract. Nothing in here is a literal from a rule
/// card: the skill routes, the stance budgets, the aim envelope, the facing
/// coupling, the capture policy, the route's OWN placement legality, and which
/// enemy forms can create more bodies are all discovered, so one artifact plays
/// the kit-off and kit-on cells, both bend envelopes, both ground arms, and the
/// classless qualification profile without branching on an arm name.
/// </summary>
internal sealed class ArcFacts
{
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms;
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks;
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movement;
    private readonly Dictionary<
        GenericActorMapContract.TileTagKind,
        HashSet<Position>> _tagged;
    private readonly Dictionary<string, HashSet<Position>> _routeForbidden = [];
    private readonly Dictionary<string, HashSet<Position>> _routeRequired = [];
    private readonly bool[] _walls;
    private readonly int[] _chokeRun;

    public ArcFacts(
        GenericActorResolvedMatchContract contract,
        int teamId,
        int participantId)
    {
        Contract = contract;
        TeamId = teamId;
        ParticipantId = participantId;
        _forms = contract.Rules.Forms
            .ToDictionary(form => form.Id, StringComparer.Ordinal);
        _attacks = contract.Rules.AttackProfiles
            .ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        _movement = contract.Rules.MovementProfiles
            .ToDictionary(profile => profile.Id, StringComparer.Ordinal);

        Width = contract.Map.Width;
        Height = contract.Map.Height;
        _walls = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            string row = contract.Map.TileRows[y];
            for (int x = 0; x < Width; x++)
                _walls[y * Width + x] = row[x] == '#';
        }

        _chokeRun = BuildChokeRuns();

        Capture = ArenaBasics.Capture(contract);
        ObjectiveCount = contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding binding
                ? binding.OrderedObjectiveRegionIds.Length
                : 0;
        AdvanceDelta = contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding chain
            ? chain.TeamAdvances
                .FirstOrDefault(entry => entry.TeamId == teamId)
                ?.ObjectiveIndexDelta
                ?? 0
            : 0;
        RalliesForward = ArenaBasics.ArrivalsRallyForward(contract);

        _tagged = [];
        foreach (GenericActorMapContract.TileTag tag in contract.Map.TileTags)
        {
            if (!_tagged.TryGetValue(tag.Kind, out HashSet<Position>? tiles))
            {
                tiles = [];
                _tagged[tag.Kind] = tiles;
            }
            foreach (Position tile in tag.Tiles)
                tiles.Add(tile);
        }
        SpawnProtected = Tagged(
            GenericActorMapContract.TileTagKind.SpawnProtected);

        AlliedBoltsPass = contract.Rules.Collisions.AlliedProjectileContact
            .Contains("pass-through", StringComparison.Ordinal);

        Routes = contract.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .ToImmutableArray();
        ObjectiveRegionIds = contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding bind
                ? bind.OrderedObjectiveRegionIds
                : [];
        ObjectiveTilesByIndex = Enumerable.Range(0, Math.Max(0, ObjectiveCount))
            .Select(index => ArenaBasics.ObjectiveTiles(contract, index))
            .ToImmutableArray();

        // Forms that can put ANOTHER body on the board. On a swarm chassis this
        // is the whole supply line: children cannot fabricate, so the body that
        // can is the only one whose death stops the flow. Read from the
        // fabrication and replication catalogs, never from a form name, so it
        // is equally correct for a class that does not exist yet.
        SupplyForms = contract.Rules.FabricationTransitions
            .OfType<GenericActorRulesContract.BoundedChildFabricationTransition>()
            .SelectMany(transition => transition.SourceFormIds)
            .Concat(
                contract.Rules.ReplicationTransitions
                    .OfType<GenericActorRulesContract
                        .SplitReplicationTransition>()
                    .SelectMany(transition => transition.SourceFormIds))
            .ToHashSet(StringComparer.Ordinal);

        // A protected pad blocks OPPOSING ground entry, so the pads that are
        // not this team's own are impassable terrain. Wave 4 derived "own" from
        // the participant's region assignments, which is wrong the moment a
        // ruleset assigns a fabrication role to the whole map: every pad then
        // looks like ours and the router plans routes through the enemy's.
        // The durable derivation is the SPAWN ANCHOR this team's own lifecycle
        // slots are assigned to.
        HashSet<Position> ownAnchors = contract.LifecycleAssignments
            .Where(assignment => assignment.TeamId == teamId)
            .Select(assignment => assignment.AssignedRespawnSpawnId)
            .Concat(
                contract.InitialDeployment.Lives
                    .Where(life => life.TeamId == teamId)
                    .Select(life => life.SpawnId))
            .OfType<string>()
            .Select(spawnId => contract.Map.SpawnAnchors
                .FirstOrDefault(anchor => string.Equals(
                    anchor.SpawnId,
                    spawnId,
                    StringComparison.Ordinal)))
            .OfType<GenericActorMapContract.SpawnAnchor>()
            .Select(anchor => anchor.Position)
            .ToHashSet();
        OwnPadTiles = [];
        ForeignPadTiles = [];
        foreach (HashSet<Position> pad in ProtectedPads(contract))
        {
            bool mine = pad.Overlaps(ownAnchors);
            foreach (Position tile in pad)
            {
                if (mine)
                    OwnPadTiles.Add(tile);
                else
                    ForeignPadTiles.Add(tile);
            }
        }
        // Anything tagged protected that no pad region claimed is classified by
        // which side's anchors it sits closest to, so a map with tags and no
        // pad regions still produces the right impassable set.
        foreach (Position tile in SpawnProtected)
        {
            if (OwnPadTiles.Contains(tile) || ForeignPadTiles.Contains(tile))
                continue;
            int own = ownAnchors.Count == 0
                ? int.MaxValue
                : ownAnchors.Min(anchor => tile.ChebyshevDistance(anchor));
            IEnumerable<Position> others = contract.Map.SpawnAnchors
                .Select(anchor => anchor.Position)
                .Where(position => !ownAnchors.Contains(position));
            int foreign = others.Any()
                ? others.Min(anchor => tile.ChebyshevDistance(anchor))
                : int.MaxValue;
            if (foreign < own)
                ForeignPadTiles.Add(tile);
            else
                OwnPadTiles.Add(tile);
        }
    }

    public GenericActorResolvedMatchContract Contract { get; }
    public int TeamId { get; }
    public int ParticipantId { get; }
    public HashSet<Position> OwnPadTiles { get; }
    public HashSet<Position> ForeignPadTiles { get; }
    public int Width { get; }
    public int Height { get; }
    public ArenaBasics.CaptureRules? Capture { get; }
    public int ObjectiveCount { get; }
    public int AdvanceDelta { get; }
    public bool RalliesForward { get; }
    public HashSet<Position> SpawnProtected { get; }
    public bool AlliedBoltsPass { get; }
    public ImmutableArray<GenericActorRulesContract.FormTransition> Routes { get; }
    public ImmutableArray<string> ObjectiveRegionIds { get; }
    public ImmutableArray<Position[]> ObjectiveTilesByIndex { get; }

    /// <summary>
    /// Forms from which a body can create another body. An enemy standing in
    /// one of these is the opposition's supply line, not just a target.
    /// </summary>
    public HashSet<string> SupplyForms { get; }

    private HashSet<Position> Tagged(
        GenericActorMapContract.TileTagKind kind) =>
        _tagged.TryGetValue(kind, out HashSet<Position>? tiles) ? tiles : [];

    /// <summary>
    /// Tile sets that are entirely spawn-protected: the home pads, taken as
    /// map regions so each one can be attributed to a side as a unit.
    /// </summary>
    private static IEnumerable<HashSet<Position>> ProtectedPads(
        GenericActorResolvedMatchContract contract)
    {
        HashSet<Position> protectedTiles = contract.Map.TileTags
            .Where(tag =>
                tag.Kind == GenericActorMapContract.TileTagKind.SpawnProtected)
            .SelectMany(tag => tag.Tiles)
            .ToHashSet();
        if (protectedTiles.Count == 0)
            yield break;
        foreach (GenericActorMapContract.Region region in contract.Map.Regions)
        {
            if (region.Tiles.Length == 0)
                continue;
            if (region.Tiles.All(protectedTiles.Contains))
                yield return region.Tiles.ToHashSet();
        }
    }

    public bool InBounds(Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < Width
        && position.Y < Height;

    public bool IsWall(Position position) =>
        !InBounds(position) || _walls[position.Y * Width + position.X];

    public bool Open(Position position) => !IsWall(position);

    /// <summary>
    /// Terrain this team's ground bodies cannot walk into: walls plus the
    /// opposing spawn-protected pad. Projectiles are NOT stopped by a protected
    /// pad, so bolt geometry uses <see cref="IsWall"/> and only routing uses
    /// this.
    /// </summary>
    public bool Impassable(Position position) =>
        IsWall(position) || ForeignPadTiles.Contains(position);

    public GenericActorRulesContract.Form? Form(string formId) =>
        _forms.TryGetValue(formId, out GenericActorRulesContract.Form? form)
            ? form
            : null;

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

    public int ObjectiveWeight(string formId) => Form(formId)?.ObjectiveWeight ?? 0;

    public int MaxHealth(string formId) => Form(formId)?.MaxHealth ?? 1;

    /// <summary>Ticks between two accepted attacks from this form's gun.</summary>
    public int Cadence(string formId) =>
        Math.Max(1, Attack(formId)?.CooldownTicks ?? 1);

    /// <summary>
    /// The declared initial-aim envelope of a form's gun in 45-degree sectors.
    /// An arm that restores the offsets publishes -1..+1 here and an arm that
    /// does not publishes 0..0, so this is the ONE place the doctrine learns
    /// whether a diagonal launch exists. Wave 4 measured its own gun as the
    /// straight lane only, which silently under-priced the gun and over-sold
    /// the fan the moment the offsets came back.
    /// </summary>
    public (int Min, int Max) AimSteps(string formId)
    {
        GenericActorRulesContract.AttackProfile? attack = Attack(formId);
        if (attack is null || !attack.ShotProgram.Enabled)
            return (0, 0);
        return (
            attack.ShotProgram.MinInitialAimSteps,
            attack.ShotProgram.MaxInitialAimSteps);
    }

    /// <summary>
    /// The facing coupling declared for a form's movement profile. Absent means
    /// preserve-facing, which is exactly what the enum's default value says, so
    /// this is safe on the baseline contract that omits the field.
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

    public bool FacingLocked(string formId) =>
        Coupling(formId)
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;

    /// <summary>How many bolts one accepted attack from this form launches.</summary>
    public int BoltsPerAttack(string formId) =>
        Attack(formId)?.ProjectilesPerAttack ?? 1;

    /// <summary>
    /// True when the form's gun launches a fan rather than a single bolt. Read
    /// from the attack profile's optional volley shape, never from a form name.
    /// </summary>
    public bool IsFanForm(string formId) => BoltsPerAttack(formId) > 1;

    /// <summary>
    /// True when the form deflects hostile bolts arriving inside its facing
    /// quadrant and returns them team-flipped. Used to recognise an enemy shell
    /// and to price poking its face.
    /// </summary>
    public bool IsGuardForm(string formId) =>
        Form(formId)?.ProjectileGuard
            == GenericActorRulesContract.FormProjectileGuard
                .FacingQuadrantContactsDeflected;

    /// <summary>
    /// Same-life routes out of <paramref name="formId"/>, so a doctrine can ask
    /// "what can this body become?" without knowing any route ID.
    /// </summary>
    public IEnumerable<GenericActorRulesContract.FormTransition> RoutesFrom(
        string formId) =>
        Routes.Where(route =>
            string.Equals(
                route.SourceFormId,
                formId,
                StringComparison.Ordinal));

    /// <summary>
    /// The route from <paramref name="formId"/> into a fan-gun stance, or null
    /// when this chassis has no such skill in the resolved contract — which is
    /// the kit-off cell and the classless qualification profile.
    /// </summary>
    public GenericActorRulesContract.FormTransition? FanStanceRoute(
        string formId) =>
        RoutesFrom(formId)
            .Where(route => IsFanForm(route.TargetFormId))
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The route from <paramref name="formId"/> into a stance that DEFLECTS
    /// hostile bolts arriving inside its facing quadrant, or null when this
    /// chassis has no such skill. A striker never has one; the same code drives
    /// the artifact correctly when it is handed a chassis that does, because the
    /// guard is a published form property rather than a class name.
    /// </summary>
    public GenericActorRulesContract.FormTransition? GuardStanceRoute(
        string formId) =>
        RoutesFrom(formId)
            .Where(route => IsGuardForm(route.TargetFormId))
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The route out of a stance back to a mobile form. This is the
    /// parameterless leave-early decision, and it is also the route the engine
    /// fires when the stance's declared budget runs out.
    /// </summary>
    public GenericActorRulesContract.FormTransition? ReturnRoute(string formId) =>
        RoutesFrom(formId)
            .Where(route =>
                route.AutomaticReturn is not null
                || Form(route.TargetFormId)?.AllowedActionIds.Contains(
                        "move",
                        StringComparer.Ordinal)
                    == true)
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The budget a stance form spends before the engine returns it, or null
    /// when the form has no automatic return at all. Canonical contracts omit
    /// the property, so null is a real answer.
    /// </summary>
    public GenericActorRulesContract.AutomaticReturnTrigger? StanceBudget(
        string stanceFormId) =>
        RoutesFrom(stanceFormId)
            .Select(route => route.AutomaticReturn)
            .FirstOrDefault(trigger => trigger is not null);

    /// <summary>
    /// Ticks between submitting a route and acting in the target form: the
    /// declared windup, expressed the way the engine schedules it
    /// (<c>startedTick + duration - 1</c> is the completion tick, so the body
    /// is usable on the tick after that).
    /// </summary>
    public static int CommitTicks(
        GenericActorRulesContract.FormTransition route) =>
        Math.Max(1, route.Windup.DurationTicks);

    /// <summary>
    /// Whole ticks a stance costs a body: the entry windup that permits nothing
    /// but waiting, the tick the budget is spent on, and the exit windup the
    /// engine's own return pays. Derived from both routes, so it is right on an
    /// arm that retunes either windup.
    /// </summary>
    public int StanceCycleTicks(
        GenericActorRulesContract.FormTransition entry)
    {
        int exit = ReturnRoute(entry.TargetFormId) is { } back
            ? CommitTicks(back)
            : 1;
        return CommitTicks(entry) + 1 + exit;
    }

    /// <summary>
    /// How many bolts of the fan must actually connect for the cast to beat the
    /// gun it replaces. This is pure contract arithmetic and it is the wave's
    /// central pricing decision: the ordinary gun would fire
    /// <c>cycle / cadence</c> aimed bolts in the ticks the stance costs, so a
    /// fan that lands fewer than that is a tempo loss no matter how good it
    /// looks. On the measured arm — windup 2, one cast, exit 1, mobile cadence
    /// 2 — the answer is TWO distinct bodies, which is exactly the multi-body
    /// bearing the swarm matchup supplies and the duel does not.
    /// </summary>
    public int RequiredFanHits(
        GenericActorRulesContract.FormTransition entry,
        string mobileFormId)
    {
        int cycle = StanceCycleTicks(entry);
        int cadence = Cadence(mobileFormId);
        return Math.Max(1, (cycle + cadence - 1) / cadence);
    }

    /// <summary>
    /// Whether a same-life route may COMPLETE on <paramref name="tile"/>,
    /// answered from the route's own declared placement rather than from the
    /// map's tag set.
    ///
    /// <para>This distinction is the whole of the open-ground arm. The map still
    /// publishes a transition-placement-forbidden tag over the objectives and
    /// the central corridor; what changed is that a stance route no longer
    /// declares that tag kind as forbidden. A doctrine that intersects the MAP
    /// tag — which is what wave 4 did — refuses to cast on 112 tiles the engine
    /// would have accepted, including every objective tile it is supposed to be
    /// denying. Ask the route.</para>
    /// </summary>
    public bool PlacementAllows(
        GenericActorRulesContract.FormTransition route,
        Position tile)
    {
        if (!_routeForbidden.TryGetValue(
                route.TransitionId,
                out HashSet<Position>? forbidden))
        {
            forbidden = [];
            foreach (GenericActorMapContract.TileTagKind kind
                     in route.Placement.ForbiddenTileTags)
            {
                foreach (Position tagged in Tagged(kind))
                    forbidden.Add(tagged);
            }
            _routeForbidden[route.TransitionId] = forbidden;
        }
        if (forbidden.Contains(tile))
            return false;

        if (!_routeRequired.TryGetValue(
                route.TransitionId,
                out HashSet<Position>? required))
        {
            required = null;
            foreach (GenericActorMapContract.TileTagKind kind
                     in route.Placement.RequiredTileTags)
            {
                HashSet<Position> tiles = Tagged(kind);
                if (required is null)
                    required = [.. tiles];
                else
                    required.IntersectWith(tiles);
            }
            required ??= [];
            _routeRequired[route.TransitionId] = required;
        }
        return route.Placement.RequiredTileTags.Length == 0
            || required.Contains(tile);
    }

    /// <summary>Objective tiles for one index in the ordered chain.</summary>
    public Position[] ObjectiveTiles(int index) =>
        index >= 0 && index < ObjectiveTilesByIndex.Length
            ? ObjectiveTilesByIndex[index]
            : [];

    // ------------------------------------------------------------- chokes

    /// <summary>
    /// Which 1-tile corridor run a tile belongs to, or -1 for open ground.
    ///
    /// <para>A CHOKE here is exactly what the coordination bar names: a tile
    /// whose open cardinal neighbours number at most two and, when there are
    /// two, lie on one line. That is a doorway — a tile whose occupant is the
    /// only traffic that can pass. Runs are the connected groups of such tiles,
    /// so "who owns the corridor" is one comparison rather than a walk.</para>
    ///
    /// <para>Derived from the wall grid the contract publishes, once per life,
    /// so it costs nothing per tick and needs no map name. On the measured map
    /// it finds sixteen runs of one or two tiles, six of them on the central
    /// row that every approach to the middle objective has to cross — including
    /// the pair a striker walks through on its way from a forward rally to the
    /// centre, which is the doorway this wave is about.</para>
    /// </summary>
    public int ChokeRun(Position tile) =>
        InBounds(tile) ? _chokeRun[(tile.Y * Width) + tile.X] : -1;

    public bool IsChoke(Position tile) => ChokeRun(tile) >= 0;

    private int[] BuildChokeRuns()
    {
        var runs = new int[Width * Height];
        Array.Fill(runs, -1);
        var choke = new List<Position>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var tile = new Position(x, y);
                if (IsWall(tile))
                    continue;
                var open = new List<Position>();
                foreach (Direction direction in ArcBoard.Cardinals)
                {
                    Position next = ArcBoard.Step(tile, direction);
                    if (!IsWall(next))
                        open.Add(next);
                }
                bool corridor = open.Count <= 1
                    || (open.Count == 2
                        && (open[0].X == open[1].X || open[0].Y == open[1].Y));
                if (corridor)
                    choke.Add(tile);
            }
        }

        var members = choke.ToHashSet();
        int runId = 0;
        foreach (Position start in choke)
        {
            if (runs[(start.Y * Width) + start.X] >= 0)
                continue;
            var stack = new Stack<Position>();
            stack.Push(start);
            runs[(start.Y * Width) + start.X] = runId;
            while (stack.Count > 0)
            {
                Position tile = stack.Pop();
                foreach (Direction direction in ArcBoard.Cardinals)
                {
                    Position next = ArcBoard.Step(tile, direction);
                    if (!members.Contains(next)
                        || runs[(next.Y * Width) + next.X] >= 0)
                    {
                        continue;
                    }
                    runs[(next.Y * Width) + next.X] = runId;
                    stack.Push(next);
                }
            }
            runId++;
        }
        return runs;
    }

    // -------------------------------------------------------------- rally

    /// <summary>
    /// The order the contract fills the forward-rally region in: the own-side
    /// chain-adjacent objective, sorted REAR-MOST FIRST along this team's own
    /// advance direction, then in the region's canonical order.
    ///
    /// <para>The policy ID says "own-side chain-adjacent objective tile in team
    /// advance order, then assigned spawn", and the arm's own documentation says
    /// "the rear-most free tile of that region measured along your own advance
    /// direction". Both halves matter to a coordination rule: the FIRST element
    /// of this order that is free is the tile the next arrival takes, so a body
    /// of mine standing on it does not merely crowd the arrival — it pushes the
    /// arrival one tile forward, and when the whole region is occupied the
    /// contract falls back to the assigned home anchor at the far end of the
    /// map. Empty when the contract does not rally forward, which is the honest
    /// answer: on an anchor-placing arm the arrival tile is fixed and no body of
    /// mine can influence it.</para>
    /// </summary>
    public Position[] RallyOrder(GenericActorContext context)
    {
        if (!RalliesForward
            || context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return [];
        }
        Position[] tiles = ObjectiveTiles(mode.ActivePositionIndex - AdvanceDelta);
        if (tiles.Length == 0)
            return [];
        Direction? forward = ArenaBasics.AdvanceDirection(Contract, TeamId);
        if (forward is not Direction advance)
            return tiles;
        (int dx, int dy) = advance.Vector();
        // Rear-most is the smallest projection onto the advance vector; the
        // region's declared order breaks the ties, exactly as a canonical
        // collection would be walked.
        return tiles
            .Select((tile, index) => (tile, index))
            .OrderBy(entry => (entry.tile.X * dx) + (entry.tile.Y * dy))
            .ThenBy(entry => entry.index)
            .Select(entry => entry.tile)
            .ToArray();
    }
}
