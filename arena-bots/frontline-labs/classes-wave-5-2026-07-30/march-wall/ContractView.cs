using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// One immutable reading of the resolved match contract for this body life.
/// Every doctrine decision resolves forms, action codes, transition routes,
/// regions, tile tags and objective ordering through this view, so the bot
/// never depends on a particular class arm, form name, numeric code, unit
/// count, or map. Anything the contract does not declare is simply absent and
/// the caller falls back.
/// </summary>
internal sealed class ContractView
{
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms;
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks;
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movements;
    private readonly Dictionary<string, GenericActorMapContract.Region> _regions;
    private readonly Dictionary<
        GenericActorMapContract.TileTagKind,
        HashSet<Position>> _tags;
    private readonly HashSet<string> _movementActionIds;
    private readonly Dictionary<
        GenericActorRulesContract.ActionKind,
        HashSet<string>> _actionIdsByKind;

    public ContractView(GenericActorMatchStart start)
    {
        Contract = start.Contract;
        Rules = start.Contract.Rules;
        Map = start.Contract.Map;
        MyTeamId = start.ActorId.TeamId;
        MyUnitId = start.ActorId.UnitId;
        MyParticipantId = start.ParticipantId;

        _forms = [];
        foreach (GenericActorRulesContract.Form form in Rules.Forms)
            _forms[form.Id] = form;

        _attacks = [];
        foreach (GenericActorRulesContract.AttackProfile attack
                 in Rules.AttackProfiles)
        {
            _attacks[attack.Id] = attack;
        }

        _movements = [];
        foreach (GenericActorRulesContract.MovementProfile movement
                 in Rules.MovementProfiles)
        {
            _movements[movement.Id] = movement;
        }

        _regions = [];
        foreach (GenericActorMapContract.Region region in Map.Regions)
            _regions[region.RegionId] = region;

        _tags = [];
        foreach (GenericActorMapContract.TileTag tag in Map.TileTags)
        {
            if (!_tags.TryGetValue(tag.Kind, out HashSet<Position>? tiles))
            {
                tiles = [];
                _tags[tag.Kind] = tiles;
            }
            tiles.UnionWith(tag.Tiles);
        }

        _actionIdsByKind = [];
        foreach (GenericActorRulesContract.ActionDefinition action
                 in Rules.Actions)
        {
            if (!_actionIdsByKind.TryGetValue(
                    action.Kind,
                    out HashSet<string>? ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                _actionIdsByKind[action.Kind] = ids;
            }
            ids.Add(action.Id);
        }
        _movementActionIds =
            ActionIds(GenericActorRulesContract.ActionKind.Movement);

        Frontline = Rules.GameMode
            as GenericActorRulesContract.FrontlineGameMode;
        if (Contract.ModeMapBinding
            is GenericActorResolvedMatchContract.FrontlineModeMapBinding binding)
        {
            ObjectiveRegionIds = binding.OrderedObjectiveRegionIds;
            GenericActorResolvedMatchContract.FrontlineTeamAdvance? advance =
                binding.TeamAdvances
                    .FirstOrDefault(entry => entry.TeamId == MyTeamId);
            AdvanceDelta = advance?.ObjectiveIndexDelta ?? 1;
        }
        else
        {
            ObjectiveRegionIds = [];
            AdvanceDelta = 1;
        }

        // The pendulum arms move no observation field and no class stat; they
        // move exactly these three policy reads, which is why one artifact can
        // play every arm. Absent values are real answers, not gaps: no declared
        // hold means a capture can be pushed straight back, and a control
        // policy that does not scale with surplus weight means the second body
        // on the objective adds nothing to the claim.
        Capture = ArenaBasics.Capture(Contract);
        ArrivalsRallyForward = ArenaBasics.ArrivalsRallyForward(Contract);

        OpposingTeamIds = Contract.Topology.Teams
            .Select(team => team.TeamId)
            .Where(teamId => teamId != MyTeamId)
            .ToArray();
        OpposingSlotCount = OpposingTeamIds.Length == 0
            ? 0
            : OpposingTeamIds.Max(SlotCount);
        OpposingFieldsAVolley = OpposingTeamIds.Any(TeamFieldsAVolley);
        OpposingFieldsAGuard = OpposingTeamIds.Any(TeamFieldsAGuard);

        IsPrimeSlot = Contract.Topology.InitialLives
            .Any(life => life.TeamId == MyTeamId && life.UnitId == MyUnitId)
            || Contract.InitialDeployment.Lives
                .Any(life => life.TeamId == MyTeamId && life.UnitId == MyUnitId);

        HomeReference = ResolveSpawnReference(sameTeam: true);
        EnemyReference = ResolveSpawnReference(sameTeam: false);
        ReservedSpawnTiles = ResolveReservedSpawnTiles();
        FabricationTransition = Rules.FabricationTransitions
            .OfType<GenericActorRulesContract.BoundedChildFabricationTransition>()
            .FirstOrDefault();

        // Three fields jointly decide whether OUR OWN bolt is an obstacle; a bot
        // that reads only `ProjectilesBlockMovement` walks around its own
        // covering fire. Read the allied-contact policy rather than assume it.
        AlliedBoltsBlockUs =
            Rules.Collisions.ProjectilesBlockMovement
            && !Rules.Collisions.AlliedProjectileContact
                .Contains("pass-through", StringComparison.Ordinal);
    }

    public GenericActorResolvedMatchContract Contract { get; }
    public GenericActorRulesContract Rules { get; }
    public GenericActorMapContract Map { get; }
    public int MyTeamId { get; }
    public int MyUnitId { get; }
    public int MyParticipantId { get; }

    /// <summary>True when this stable slot holds the team's tick-zero body.</summary>
    public bool IsPrimeSlot { get; }

    /// <summary>Every declared team that is not ours.</summary>
    public int[] OpposingTeamIds { get; } = [];

    /// <summary>
    /// The largest stable-slot roster on the other side, from the topology. A
    /// fortification ration cannot honestly demand more scoring bodies than the
    /// opposition is able to field at once.
    /// </summary>
    public int OpposingSlotCount { get; }

    /// <summary>
    /// True when the declared opposing roster contains a form whose gun launches
    /// more than one bolt per attack. A queue of bodies sharing one straight lane
    /// is what a fan is for, so this is read from the roster rather than learned
    /// from the first fan.
    /// </summary>
    public bool OpposingFieldsAVolley { get; }

    /// <summary>
    /// True when the declared opposing roster contains a projectile-guarding
    /// form: a shield may rise over there even though none has yet.
    /// </summary>
    public bool OpposingFieldsAGuard { get; }

    public GenericActorRulesContract.FrontlineGameMode? Frontline { get; }
    public ImmutableArray<string> ObjectiveRegionIds { get; }

    /// <summary>Signed objective-index step of one advance for our team.</summary>
    public int AdvanceDelta { get; }

    /// <summary>
    /// What one objective push costs and what protects it. Null when the mode
    /// declares no capture at all.
    /// </summary>
    public ArenaBasics.CaptureRules? Capture { get; }

    /// <summary>
    /// True when automatic returns and activations land on our own-side
    /// chain-adjacent objective instead of the slot's spawn anchor. It changes
    /// no observation and no stat; it changes what a death costs.
    /// </summary>
    public bool ArrivalsRallyForward { get; }

    /// <summary>
    /// True when net objective weight scales capture pressure. False means
    /// control is binary: one body of positive weight nulls any number of
    /// opposing bodies, so the second body on the objective is insurance
    /// rather than pressure.
    /// </summary>
    public bool SurplusWeightScalesGain =>
        Capture?.SurplusWeightScalesGain ?? false;

    /// <summary>
    /// How long a completed advance is protected from being pushed back, or
    /// null when the capture definition declares no hold and the front can
    /// come straight back.
    /// </summary>
    public int? HoldTicks => Capture?.HoldTicks;

    /// <summary>Ticks after an advance during which control cannot resume.</summary>
    public int RedeployPauseTicks => Capture?.RedeployPauseTicks ?? 0;

    /// <summary>Base claim progress per tick of control.</summary>
    public int CaptureGain => Math.Max(1, Capture?.GainPerSoleTeamTick ?? 1);

    /// <summary>Own deployment anchor, used only as a rear reference point.</summary>
    public Position HomeReference { get; }

    /// <summary>Opposing deployment anchor, used as the forward reference.</summary>
    public Position EnemyReference { get; }

    public GenericActorRulesContract.BoundedChildFabricationTransition?
        FabricationTransition
    { get; }

    /// <summary>
    /// Spawn anchors this team's own slots deploy and return into. The ruleset
    /// keeps them reserved against our own bodies, so a route planned through
    /// one is a route that blocks every tick. Reading them from the declared
    /// anchors and lifecycle assignments keeps that out of the pathfinder.
    /// </summary>
    public HashSet<Position> ReservedSpawnTiles { get; } = [];

    public int MaxTicks => Rules.Limits.MaxTicks;

    public int CaptureThreshold => Frontline?.Capture.Threshold ?? 1;

    public int PushesToBreach =>
        Frontline?.FrontlineVictory.PushesToBreach ?? 1;

    public int PositionCount =>
        Frontline?.FrontlinePositionCount
        ?? Math.Max(1, ObjectiveRegionIds.Length);

    public bool IsWall(Position position) =>
        position.X < 0
        || position.Y < 0
        || position.Y >= Map.TileRows.Length
        || position.X >= Map.TileRows[position.Y].Length
        || Map.TileRows[position.Y][position.X] == '#';

    public bool IsOpen(Position position) => !IsWall(position);

    public HashSet<string> ActionIds(
        GenericActorRulesContract.ActionKind kind) =>
        _actionIdsByKind.TryGetValue(kind, out HashSet<string>? ids)
            ? ids
            : new HashSet<string>(StringComparer.Ordinal);

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
                out GenericActorRulesContract.AttackProfile? attack)
                ? attack
                : null;
    }

    /// <summary>
    /// A form is fortified when its declared action mask contains no movement
    /// action AND it is not a projectile guard. That is the class-neutral way to
    /// recognize a gun emplacement.
    ///
    /// <para>The second clause is a repair. The kit added a second immobile shape
    /// — the shield — so "cannot move" stopped being the same question as "is an
    /// emplacement", and every route search keyed on the first predicate became
    /// ambiguous: from one mobile form the contract declares a route to a turret
    /// AND a route to a shell, both immobile. Revision 4 got the right answer by
    /// transition-ID ordinal accident (<c>anchor-…</c> sorts before
    /// <c>shell-…</c>), which is not a contract fact. A guard declares itself; a
    /// gun emplacement is what is left.</para>
    /// </summary>
    public bool IsFortified(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && !form.AllowedActionIds.Any(_movementActionIds.Contains)
            && !HasGuard(formId);
    }

    /// <summary>
    /// True when this form's declared action mask contains a movement action —
    /// the positive half of <see cref="IsFortified"/>, and the predicate every
    /// return route is looking for. "Not an emplacement" and "can walk" stopped
    /// being the same statement the moment a second immobile form existed.
    /// </summary>
    public bool IsMobile(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && form.AllowedActionIds.Any(_movementActionIds.Contains);
    }

    /// <summary>
    /// True when this form deflects contacts arriving inside its facing arc.
    /// The field is absent on every unguarded form — canonical contracts omit
    /// the inert default — so an absent guard is a real answer.
    /// </summary>
    public bool HasGuard(string formId) =>
        Form(formId)?.ProjectileGuard
            == GenericActorRulesContract.FormProjectileGuard
                .FacingQuadrantContactsDeflected;

    /// <summary>
    /// Bolts one accepted attack launches from this form. One for every
    /// ordinary gun; more only where the attack profile declares a volley, and
    /// the doctrine reads it rather than assuming a single bolt per shot.
    /// </summary>
    public int ProjectilesPerAttack(string formId) =>
        Attack(formId)?.ProjectilesPerAttack ?? 1;

    /// <summary>
    /// True when any form a team's slots are allowed to hold launches more than
    /// one bolt per attack. That is the contract's answer to "can anything over
    /// there punish a queue of bodies sharing one lane", asked from the
    /// declared roster instead of waiting to be taught by a fan.
    /// </summary>
    public bool TeamFieldsAVolley(int teamId) =>
        Contract.LifecycleAssignments
            .Where(assignment => assignment.TeamId == teamId)
            .SelectMany(assignment => assignment.AllowedFormIds)
            .Any(formId => ProjectilesPerAttack(formId) > 1);

    /// <summary>
    /// True when any form a team's slots may hold declares a projectile guard —
    /// so a shield may appear even though none is standing yet.
    /// </summary>
    public bool TeamFieldsAGuard(int teamId) =>
        Contract.LifecycleAssignments
            .Where(assignment => assignment.TeamId == teamId)
            .SelectMany(assignment => assignment.AllowedFormIds)
            .Any(HasGuard);

    /// <summary>
    /// A team's declared chassis class, taken from the topology the contract
    /// publishes. It is here to be READ rather than reconstructed: the scaffold
    /// once recovered this by splitting a form ID on its first hyphen, and that
    /// is exactly the kind of inference a published field retires. The doctrine
    /// deliberately branches on declared stats and routes instead of on this
    /// name — a stat-based counter generalizes to a chassis that does not exist
    /// yet — so it is read for identity and diagnostics, never for tactics.
    /// </summary>
    public string? ClassIdOf(int teamId) =>
        Contract.Topology.Teams
            .FirstOrDefault(team => team.TeamId == teamId)
            ?.ClassId;

    /// <summary>
    /// How many stable slots a team owns, from the topology rather than from a
    /// constant. An asymmetric-slot arm gives one side five and the other
    /// three; a ration that assumes three is wrong on both sides of it.
    /// </summary>
    public int SlotCount(int teamId) =>
        Contract.Topology.UnitSlots.Count(slot => slot.TeamId == teamId);

    public int ObjectiveWeight(string formId) => Form(formId)?.ObjectiveWeight ?? 0;

    public int MaxHealth(string formId) => Form(formId)?.MaxHealth ?? 1;

    /// <summary>True when our own projectiles obstruct our own movement.</summary>
    public bool AlliedBoltsBlockUs { get; }

    /// <summary>
    /// How this form's movement profile couples facing to a step. An experiment
    /// arm may turn the body with the step (`face-movement-direction`) or allow
    /// movement only along the current facing (`facing-locked`); the inert
    /// default leaves facing alone. Facing is this chassis' whole firing
    /// envelope, so every doctrine step that plans a move asks this first.
    /// </summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling(string formId)
    {
        GenericActorRulesContract.Form? form = Form(formId);
        return form is not null
            && _movements.TryGetValue(
                form.MovementProfileId,
                out GenericActorRulesContract.MovementProfile? movement)
            ? movement.FacingCoupling
            : GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>The facing a body would have after stepping <paramref name="step"/>.</summary>
    public Direction FacingAfterStep(
        string formId,
        Direction facing,
        Direction step) =>
        Coupling(formId)
            == GenericActorRulesContract.MovementFacingCoupling.FaceMovementDirection
            ? step
            : facing;

    /// <summary>The declared same-life route from a mobile form into a turret.</summary>
    public GenericActorRulesContract.FormTransition? AnchorRoute(string formId) =>
        Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(route =>
                string.Equals(route.SourceFormId, formId, StringComparison.Ordinal)
                && IsFortified(route.TargetFormId))
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The declared same-life route from a turret back to a mobile form. Keyed on
    /// the target being able to WALK rather than on it merely not being an
    /// emplacement, because a shield is neither.
    /// </summary>
    public GenericActorRulesContract.FormTransition? MobilizeRoute(string formId) =>
        Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(route =>
                string.Equals(route.SourceFormId, formId, StringComparison.Ordinal)
                && IsMobile(route.TargetFormId))
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The declared automatic-return delay for one stable slot, taken from its
    /// assigned lifecycle profile. Null when the slot has no automatic return
    /// (an explicitly refabricated child) or is not declared at all — the
    /// caller decides what an absent clock is worth rather than assuming 18.
    /// </summary>
    public int? ReturnDelay(int teamId, int unitId)
    {
        string? profileId = Contract.LifecycleAssignments
            .FirstOrDefault(assignment =>
                assignment.TeamId == teamId && assignment.UnitId == unitId)
            ?.LifecycleProfileId;
        if (profileId is null)
            return null;
        foreach (GenericActorRulesContract.LifecycleProfile profile
                 in Rules.Lifecycle.Profiles)
        {
            if (string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal))
                return profile.DelayTicks;
        }
        return null;
    }

    public IReadOnlyList<Position> ObjectiveTiles(int index)
    {
        if (index < 0 || index >= ObjectiveRegionIds.Length)
            return [];
        return _regions.TryGetValue(
            ObjectiveRegionIds[index],
            out GenericActorMapContract.Region? region)
            ? region.Tiles
            : [];
    }

    public HashSet<Position> TilesTagged(
        GenericActorMapContract.TileTagKind kind) =>
        _tags.TryGetValue(kind, out HashSet<Position>? tiles)
            ? tiles
            : [];

    /// <summary>
    /// Tiles where our Prime may start a fabrication, taken from the transition's
    /// declared source region role and required tile tags.
    /// </summary>
    public IReadOnlyList<Position> FabricationSourceTiles()
    {
        if (FabricationTransition is not
            GenericActorRulesContract.BoundedChildFabricationTransition transition)
        {
            return [];
        }

        GenericActorResolvedMatchContract.ParticipantRegionAssignment? assignment =
            Contract.ParticipantRegionAssignments
                .FirstOrDefault(entry =>
                    entry.ParticipantId == MyParticipantId
                    && string.Equals(
                        entry.RegionRoleId,
                        transition.SourceRegionRoleId,
                        StringComparison.Ordinal));
        if (assignment is null
            || !_regions.TryGetValue(
                assignment.MapRegionId,
                out GenericActorMapContract.Region? region))
        {
            return [];
        }

        IEnumerable<Position> tiles = region.Tiles.Where(IsOpen);
        foreach (GenericActorMapContract.TileTagKind kind
                 in transition.RequiredSourceTileTags)
        {
            HashSet<Position> tagged = TilesTagged(kind);
            tiles = tiles.Where(tagged.Contains);
        }
        return tiles.ToArray();
    }

    /// <summary>
    /// Tiles on which the declared anchor route would be refused, from the
    /// transition's own forbidden-tag list rather than copied coordinates.
    /// </summary>
    public HashSet<Position> AnchorForbiddenTiles(
        GenericActorRulesContract.FormTransition route)
    {
        HashSet<Position> forbidden = [];
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.ForbiddenTileTags)
        {
            forbidden.UnionWith(TilesTagged(kind));
        }
        return forbidden;
    }

    public bool AnchorTileSatisfiesRequirements(
        GenericActorRulesContract.FormTransition route,
        Position tile)
    {
        foreach (GenericActorMapContract.TileTagKind kind
                 in route.Placement.RequiredTileTags)
        {
            if (!TilesTagged(kind).Contains(tile))
                return false;
        }
        return true;
    }

    private HashSet<Position> ResolveReservedSpawnTiles()
    {
        var spawnIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GenericActorResolvedMatchContract.LifecycleAssignment assignment
                 in Contract.LifecycleAssignments)
        {
            if (assignment.TeamId != MyTeamId)
                continue;
            if (assignment.AssignedRespawnSpawnId is string spawnId)
                spawnIds.Add(spawnId);
        }
        foreach (GenericActorResolvedMatchContract.InitialLifeDeployment life
                 in Contract.InitialDeployment.Lives)
        {
            if (life.TeamId == MyTeamId)
                spawnIds.Add(life.SpawnId);
        }

        HashSet<Position> tiles = [];
        foreach (GenericActorMapContract.SpawnAnchor anchor in Map.SpawnAnchors)
        {
            if (spawnIds.Contains(anchor.SpawnId))
                tiles.Add(anchor.Position);
        }
        foreach (GenericActorResolvedMatchContract.InitialSpawn spawn
                 in Contract.InitialDeployment.Spawns)
        {
            if (spawnIds.Contains(spawn.SpawnId))
                tiles.Add(spawn.Position);
        }
        return tiles;
    }

    private Position ResolveSpawnReference(bool sameTeam)
    {
        GenericActorResolvedMatchContract.InitialLifeDeployment? life =
            Contract.InitialDeployment.Lives
                .Where(entry => (entry.TeamId == MyTeamId) == sameTeam)
                .OrderBy(entry => entry.TeamId)
                .ThenBy(entry => entry.UnitId)
                .FirstOrDefault();
        if (life is not null)
        {
            GenericActorResolvedMatchContract.InitialSpawn? spawn =
                Contract.InitialDeployment.Spawns
                    .FirstOrDefault(entry =>
                        string.Equals(
                            entry.SpawnId,
                            life.SpawnId,
                            StringComparison.Ordinal));
            if (spawn is not null)
                return spawn.Position;
        }

        // No declared deployment for that side: fall back to the far end of the
        // objective order in the relevant direction.
        int index = sameTeam
            ? AdvanceDelta > 0 ? 0 : ObjectiveRegionIds.Length - 1
            : AdvanceDelta > 0 ? ObjectiveRegionIds.Length - 1 : 0;
        IReadOnlyList<Position> tiles = ObjectiveTiles(index);
        if (tiles.Count > 0)
            return tiles[0];
        return new Position(Math.Max(0, Map.Width / 2), Math.Max(0, Map.Height / 2));
    }
}
