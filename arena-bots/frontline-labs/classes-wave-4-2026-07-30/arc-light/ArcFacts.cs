using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Everything arc-light needs to know that never changes during a life, read
/// once from the resolved contract. Nothing in here is a literal from a rule
/// card: the skill routes, the stance budgets, the bend envelope, the facing
/// coupling, and the capture policy are all discovered, so one artifact plays
/// the kit-off and kit-on cells, both bend envelopes, and the classless
/// qualification profile without branching on an arm name.
/// </summary>
internal sealed class ArcFacts
{
    private readonly Dictionary<string, GenericActorRulesContract.Form> _forms;
    private readonly Dictionary<string, GenericActorRulesContract.AttackProfile>
        _attacks;
    private readonly Dictionary<string, GenericActorRulesContract.MovementProfile>
        _movement;
    private readonly bool[] _walls;

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

        TransitionForbidden = contract.Map.TileTags
            .Where(tag =>
                tag.Kind
                    == GenericActorMapContract.TileTagKind
                        .TransitionPlacementForbidden)
            .SelectMany(tag => tag.Tiles)
            .ToHashSet();
        SpawnProtected = contract.Map.TileTags
            .Where(tag =>
                tag.Kind == GenericActorMapContract.TileTagKind.SpawnProtected)
            .SelectMany(tag => tag.Tiles)
            .ToHashSet();

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

        // A protected pad blocks OPPOSING ground entry, so the half of the
        // spawn-protected set that is not bound to this participant's own
        // regions is impassable terrain for this team's bodies. Derived from
        // the participant's region assignments, never from a coordinate.
        HashSet<string> ownRegionIds = contract.ParticipantRegionAssignments
            .Where(assignment => assignment.ParticipantId == participantId)
            .Select(assignment => assignment.MapRegionId)
            .ToHashSet(StringComparer.Ordinal);
        OwnRegionTiles = contract.Map.Regions
            .Where(region => ownRegionIds.Contains(region.RegionId))
            .SelectMany(region => region.Tiles)
            .ToHashSet();
        ForeignProtected = SpawnProtected
            .Where(tile => !OwnRegionTiles.Contains(tile))
            .ToHashSet();
    }

    public GenericActorResolvedMatchContract Contract { get; }
    public int TeamId { get; }
    public int ParticipantId { get; }
    public HashSet<Position> OwnRegionTiles { get; } = [];
    public HashSet<Position> ForeignProtected { get; } = [];
    public int Width { get; }
    public int Height { get; }
    public ArenaBasics.CaptureRules? Capture { get; }
    public int ObjectiveCount { get; }
    public int AdvanceDelta { get; }
    public bool RalliesForward { get; }
    public HashSet<Position> TransitionForbidden { get; }
    public HashSet<Position> SpawnProtected { get; }
    public bool AlliedBoltsPass { get; }
    public ImmutableArray<GenericActorRulesContract.FormTransition> Routes { get; }
    public ImmutableArray<string> ObjectiveRegionIds { get; }
    public ImmutableArray<Position[]> ObjectiveTilesByIndex { get; }

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
        IsWall(position) || ForeignProtected.Contains(position);

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

    /// <summary>Objective tiles for one index in the ordered chain.</summary>
    public Position[] ObjectiveTiles(int index) =>
        index >= 0 && index < ObjectiveTilesByIndex.Length
            ? ObjectiveTilesByIndex[index]
            : [];
}
