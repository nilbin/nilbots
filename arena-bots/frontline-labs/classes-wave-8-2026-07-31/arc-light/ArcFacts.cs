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
    private readonly HashSet<(int TeamId, int UnitId)> _primeSlots;

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
        Channel = ReadChannel(contract);
        Economy = ReadEconomy(contract);
        BankTiles = Economy is null
            ? []
            : ReadBankTiles(contract, Economy, teamId);
        _primeSlots = contract.InitialDeployment.Lives
            .Select(life => (life.TeamId, life.UnitId))
            .ToHashSet();
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

    /// <summary>
    /// The channel, or null when this ruleset's control policy is not one. Every
    /// wave-8 capture rule branches on this being non-null, so a bot that reads
    /// it never has to know which arm it is in — and the three fields it carries
    /// are absent from the contract entirely on a ruleset without the arm,
    /// exactly like <c>ratchetHoldTicks</c> on a ruleset without a ratchet.
    /// </summary>
    public ChannelRules? Channel { get; }

    /// <summary>
    /// The battlefield economy, or null when this ruleset declares none.
    /// </summary>
    public EconomyRules? Economy { get; }

    /// <summary>Tiles of this team's own bank region; empty without an economy.</summary>
    public HashSet<Position> BankTiles { get; }

    /// <summary>
    /// Whether an upgrade tier applies to a slot's lives under the declared
    /// <c>prime-slot-lives-only</c> scope. Derived from the initial deployment
    /// rather than from a slot number: the slots that start the match active are
    /// the Prime slots, on every class in the slate, on both sides of an
    /// asymmetric topology, and on a class that does not exist yet.
    /// </summary>
    public bool IsPrimeSlot(int teamId, int unitId) =>
        _primeSlots.Contains((teamId, unitId));
    public ImmutableArray<GenericActorRulesContract.FormTransition> Routes { get; }
    public ImmutableArray<string> ObjectiveRegionIds { get; }
    public ImmutableArray<Position[]> ObjectiveTilesByIndex { get; }

    /// <summary>
    /// Forms from which a body can create another body. An enemy standing in
    /// one of these is the opposition's supply line, not just a target.
    /// </summary>
    public HashSet<string> SupplyForms { get; }

    // ------------------------------------------------------- the channel

    /// <summary>
    /// What a channelled capture costs and how it is interrupted, read from the
    /// capture definition. Canonical contracts omit every one of these fields on
    /// a ruleset that does not channel, so an absent block is a real answer:
    /// the mechanic does not exist for that match.
    /// </summary>
    /// <param name="StationaryCap">
    /// The ceiling on the gain multiplier stationary surplus can buy. Two
    /// stationary bodies against a dead defence take a point twice as fast; the
    /// third buys nothing, which is why this doctrine screens rather than
    /// stacks.
    /// </param>
    /// <param name="ErosionMultiplier">
    /// How many times faster a standing enemy claim erodes than a fresh claim
    /// builds, so a full flip is priced before it is started.
    /// </param>
    /// <param name="RevertPerDamagePoint">
    /// Progress the controlling team loses per point of health removed from one
    /// of its bodies standing on the active objective. Zero when the ruleset
    /// declares no interrupt at all.
    /// </param>
    /// <param name="WholeRun">
    /// True when one hit reverts the controller's whole run rather than the hit
    /// body's share — which is what makes a single fan worth three quarters of
    /// a capture.
    /// </param>
    public sealed record ChannelRules(
        int StationaryCap,
        int ErosionMultiplier,
        int RevertPerDamagePoint,
        bool WholeRun);

    private static ChannelRules? ReadChannel(
        GenericActorResolvedMatchContract contract)
    {
        if (contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline)
        {
            return null;
        }
        GenericActorRulesContract.FrontlineCapture capture = frontline.Capture;
        // The policy ID names the mechanism. Anything else and none of this
        // section applies, which is the whole of the degrade-gracefully rule.
        if (!capture.ControlPolicy.Contains(
                "stationary-claim-weight",
                StringComparison.Ordinal))
        {
            return null;
        }
        return new ChannelRules(
            capture.StationaryGainMultiplierCap > 0
                ? capture.StationaryGainMultiplierCap
                : int.MaxValue,
            Math.Max(1, capture.OpposingErosionMultiplier),
            capture.ClaimInterrupt?.RevertPerDamagePoint ?? 0,
            capture.ClaimInterrupt?.Granularity.Contains(
                "whole-run",
                StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// True when net objective weight scales capture pressure on this contract.
    /// The scaffold reader answers this from the older policy ID only; a
    /// channelling policy also scales gain — capped, and against a differently
    /// counted weight — so asking the scaffold alone silently reports the
    /// channel as a binary contest and prices every push wrong.
    /// </summary>
    public bool WeightScalesGain =>
        Channel is not null || Capture?.SurplusWeightScalesGain == true;

    // ------------------------------------------------------- the economy

    /// <summary>One purchasable track, as declared.</summary>
    /// <param name="TrackId">The <c>invest</c> argument.</param>
    /// <param name="Effect">
    /// Exact effect policy ID. Every doctrine decision below reads THIS and
    /// never the track's name, so a store that renames its ladder still drives
    /// the same purchases.
    /// </param>
    /// <param name="PerTier">Integer step one tier adds.</param>
    /// <param name="MaxTier">Deepest reachable tier.</param>
    public sealed record TrackRules(
        string TrackId,
        string Effect,
        int PerTier,
        int MaxTier);

    /// <summary>
    /// The battlefield economy as declared: the deposit metronome, what a pile
    /// is worth, how much a body carries, and the whole ladder. Absent means the
    /// mechanic does not exist for this match.
    /// </summary>
    public sealed record EconomyRules(
        Position[] VeinSites,
        int FirstSpawnTick,
        int IntervalTicks,
        int LastSpawnTick,
        int VeinAmount,
        int WreckAmount,
        int AssayAmount,
        int CarryCapacity,
        int PileLifetimeTicks,
        int MaxTotalTiers,
        bool InvestVerb,
        TrackRules[] Tracks);

    private static EconomyRules? ReadEconomy(
        GenericActorResolvedMatchContract contract)
    {
        if (contract.Rules.GameMode
                is not GenericActorRulesContract.FrontlineGameMode frontline
            || frontline.ScrapEconomy is not { } economy)
        {
            return null;
        }
        return new EconomyRules(
            economy.VeinSites
                .Select(site => new Position(site.X, site.Y))
                .ToArray(),
            economy.VeinFirstSpawnTick,
            Math.Max(1, economy.VeinSpawnIntervalTicks),
            economy.VeinLastSpawnTick,
            economy.VeinAmount,
            economy.WreckAmount,
            economy.AssayAmount,
            Math.Max(1, economy.CarryCapacity),
            economy.PileLifetimeTicks,
            economy.MaxTotalTiers,
            // The control level removes the verb from the catalog entirely and
            // buys by itself; a purchase routine there is a wasted branch.
            economy.PurchaseMode.Contains(
                "invest-action",
                StringComparison.Ordinal),
            economy.Tracks
                .Select(track => new TrackRules(
                    track.TrackId,
                    track.Effect,
                    track.PerTierMagnitude,
                    track.MaxTier))
                .ToArray());
    }

    private static HashSet<Position> ReadBankTiles(
        GenericActorResolvedMatchContract contract,
        EconomyRules economy,
        int teamId)
    {
        // bankRegionIds is indexed by team ID, so this is a lookup rather than a
        // guess about which pad is home.
        if (contract.Rules.GameMode
                is not GenericActorRulesContract.FrontlineGameMode frontline
            || frontline.ScrapEconomy is not { } declared
            || teamId < 0
            || teamId >= declared.BankRegionIds.Length)
        {
            return [];
        }
        string regionId = declared.BankRegionIds[teamId];
        foreach (GenericActorMapContract.Region region in contract.Map.Regions)
        {
            if (string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
            {
                return region.Tiles.ToHashSet();
            }
        }
        return [];
    }

    /// <summary>
    /// The tier a team currently holds on the track with this declared effect,
    /// read from the published economic position. Zero on every ruleset without
    /// an economy, which is what makes every caller inert there.
    /// </summary>
    public int TierFor(
        GenericActorContext.ModeObservationState.Frontline? mode,
        int teamId,
        string effect)
    {
        if (Economy is null || mode is null)
            return 0;
        int index = Array.FindIndex(
            Economy.Tracks,
            track => string.Equals(
                track.Effect,
                effect,
                StringComparison.Ordinal));
        if (index < 0)
            return 0;
        foreach (GenericActorContext.ScrapTeamState team in mode.ScrapTeams)
        {
            if (team.TeamId != teamId)
                continue;
            return index < team.TierLevels.Length
                ? team.TierLevels[index] * Economy.Tracks[index].PerTier
                : 0;
        }
        return 0;
    }

    /// <summary>Declared effect IDs, so no call site spells one twice.</summary>
    public const string TravelEffect = "mobile-attack-travel-tiles-delta";

    /// <summary>Declared effect ID for the sight ladder.</summary>
    public const string VisionEffect = "vision-range-delta";

    /// <summary>Declared effect ID for the spawn-health ladder.</summary>
    public const string HealthEffect = "spawn-max-health-delta";

    /// <summary>
    /// Gun travel a body of <paramref name="teamId"/> in this form actually
    /// has: the profile's DECLARED travel plus that team's edge tier when the
    /// body occupies an upgraded slot. Both operands are published; using the
    /// declared number alone is the way a lane model quietly under-reports an
    /// upgraded enemy's reach by exactly the tier it bought.
    /// </summary>
    public int EffectiveTravel(
        string formId,
        GenericActorContext.ModeObservationState.Frontline? mode,
        int teamId,
        bool upgradedSlot)
    {
        int declared = Attack(formId)?.Projectile.MaxTravelTiles ?? 0;
        return declared
            + (ArcRules.ReadEnemyTiers && upgradedSlot
                ? TierFor(mode, teamId, TravelEffect)
                : 0);
    }

    /// <summary>Declared sight range of a form, before any tier.</summary>
    public int DeclaredVision(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        if (form is null)
            return 0;
        foreach (GenericActorRulesContract.VisionProfile profile
                 in Contract.Rules.VisionProfiles)
        {
            if (string.Equals(
                    profile.Id,
                    form.VisionProfileId,
                    StringComparison.Ordinal))
            {
                return profile.Range;
            }
        }
        return 0;
    }

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
    /// Health one contact from this form's gun removes. Declared per projectile
    /// profile, so a fan bolt and an ordinary bolt need not agree — and on the
    /// re-armed arm they do not. This is the number the whole wave turns on and
    /// it was never read before: wave 6 priced a cast in BOLTS, which is only
    /// the same question as damage while every bolt on the board is worth one.
    /// </summary>
    public int Damage(string formId) =>
        Math.Max(1, Attack(formId)?.Projectile.DamagePerHit ?? 1);

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
    /// How many bodies the fan must actually connect with for the cast to beat
    /// the gun it replaces. Pure contract arithmetic, and the wave-7 correction
    /// is that the arithmetic is done in DAMAGE.
    ///
    /// <para>The stance costs its declared cycle; the ordinary gun would have
    /// fired <c>ceil(cycle / cadence)</c> aimed bolts in that window, each
    /// removing the mobile gun's declared <c>damagePerHit</c>. A diverging fan
    /// lands at most one bolt per body, so what a cast buys is
    /// <c>bodies x fan damagePerHit</c>. Break-even is therefore
    /// <c>ceil(forgone damage / fan bolt damage)</c>.</para>
    ///
    /// <para>Wave 6 compared bolts to bodies, which is the same question only
    /// while every bolt on the board removes one health. It stopped being the
    /// same question when the fan bolt started removing two: on the re-armed
    /// contract — entry 1, one cast, exit 1, mobile cadence 2, mobile damage 1,
    /// fan damage 2 — the honest answer is ONE body, and the wave-6 answer of
    /// two is what a doctrine refuses to pay forty-three times a match. Fed the
    /// wave-6 contract this returns the wave-6 number, because nothing here is
    /// a constant.</para>
    /// </summary>
    public int RequiredFanHits(
        GenericActorRulesContract.FormTransition entry,
        string mobileFormId)
    {
        int cycle = StanceCycleTicks(entry);
        int cadence = Cadence(mobileFormId);
        int forgoneBolts = Math.Max(1, (cycle + cadence - 1) / cadence);
        if (!ArcRules.FanPricedInDamage)
            return forgoneBolts;
        int forgoneDamage = forgoneBolts * Damage(mobileFormId);
        int perBolt = Damage(entry.TargetFormId);
        return Math.Max(1, (forgoneDamage + perBolt - 1) / perBolt);
    }

    /// <summary>
    /// The first tick a same-life route accepts a request again, or null when
    /// no clock is live for it.
    ///
    /// <para>Read from <c>self.routeCooldowns</c> and nowhere else. The clock is
    /// scoped to the UNIT SLOT, so it survives this body's death and a life born
    /// inside the window has no history to infer it from; the field is absent
    /// while nothing is held, so a contract that declares no route cooldown
    /// looks exactly as it always did. Inferring the window from this life's own
    /// completions — the obvious implementation — is wrong in precisely the case
    /// that matters, which is why it is published.</para>
    /// </summary>
    public static int? RouteReadyAt(
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route)
    {
        foreach (GenericActorContext.ObservedRouteCooldown clock
                 in context.Self.RouteCooldowns)
        {
            if (string.Equals(
                    clock.TransitionId,
                    route.TransitionId,
                    StringComparison.Ordinal))
            {
                return clock.ReadyAtTick;
            }
        }
        return null;
    }

    /// <summary>
    /// True when the published clock currently refuses this route. A request
    /// inside the window is an ordinary Blocked — a whole tick spent learning
    /// something the observation already said.
    /// </summary>
    public static bool RouteHeld(
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route) =>
        ArcRules.EntryClockIsACharge
        && RouteReadyAt(context, route) is int ready
        && context.Tick < ready;

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
