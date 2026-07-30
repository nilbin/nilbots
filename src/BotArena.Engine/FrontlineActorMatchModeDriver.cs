using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Frontline implementation of the generic actor mode seam. World simulation
/// supplies post-combat active bodies; this driver owns objective control,
/// territorial scoring, projection, and completion.
/// </summary>
internal sealed class FrontlineActorMatchModeDriver
    : IGenericActorMatchModeDriver
{
    private const string TerritorialProgressChannel =
        "territorial-progress";

    private readonly PublicMatchTopology _topology;
    private readonly FrontlineGameModeDefinition _gameMode;
    private readonly FrontlineModeKernel _kernel;
    private readonly ImmutableArray<ImmutableHashSet<Position>>
        _objectiveTiles;
    private readonly ImmutableHashSet<Position> _secondarySiteTiles;
    private readonly ImmutableDictionary<string, int> _objectiveWeights;
    private readonly FrontlineScrapKernel? _scrapKernel;
    private FrontlineControlState _control;
    private FrontlineScrapState? _scrap;

    /// <summary>
    /// The economy as it stood when this tick's observations were frozen, kept
    /// only while an <c>invest</c> has already moved it this tick. The mode
    /// change a purchase publishes has to be measured against the state the
    /// bots were handed, not against the state the purchase left behind.
    /// </summary>
    private FrontlineScrapState? _scrapAtTickStart;
    private int _investTick = -1;

    public FrontlineActorMatchModeDriver(
        PublicMatchTopology topology,
        ActorMapDefinition map,
        IReadOnlyCollection<ActorFormDefinition> forms,
        IReadOnlyCollection<ActorUnitSlotLifecycleAssignmentDefinition>
            unitSlotLifecycle,
        FrontlineGameModeDefinition gameMode,
        FrontlineActorModeMapBindingDefinition mapBinding)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(forms);
        ArgumentNullException.ThrowIfNull(unitSlotLifecycle);
        ArgumentNullException.ThrowIfNull(gameMode);
        ArgumentNullException.ThrowIfNull(mapBinding);

        Dictionary<string, ActorMapRegionDefinition> regions =
            map.Regions.ToDictionary(
                region => region.RegionId,
                StringComparer.Ordinal);
        try
        {
            _objectiveTiles = mapBinding.OrderedObjectiveRegionIds
                .Select(regionId => regions[regionId].Tiles.ToImmutableHashSet())
                .ToImmutableArray();
        }
        catch (KeyNotFoundException exception)
        {
            throw new ArgumentException(
                "Frontline map binding references an unknown objective region.",
                nameof(mapBinding),
                exception);
        }

        // One site, however many regions declare it: presence sums across the
        // union, so opposing bodies in two different alcoves contest each
        // other exactly as two bodies on one tile-set would.
        try
        {
            _secondarySiteTiles = gameMode.SecondaryControl is null
                ? []
                : gameMode.SecondaryControl.RegionIds
                    .SelectMany(regionId => regions[regionId].Tiles)
                    .ToImmutableHashSet();
        }
        catch (KeyNotFoundException exception)
        {
            throw new ArgumentException(
                "Frontline secondary control references an unknown site region.",
                nameof(gameMode),
                exception);
        }

        ActorFormDefinition[] formSnapshot = [.. forms];
        if (formSnapshot.Length == 0
            || formSnapshot.Any(form => form is null)
            || formSnapshot
                .Select(form => form.Id)
                .Distinct(StringComparer.Ordinal)
                .Count() != formSnapshot.Length)
        {
            throw new ArgumentException(
                "Frontline forms must be non-empty, non-null, and ID-unique.",
                nameof(forms));
        }

        _topology = topology;
        _gameMode = gameMode;
        _kernel = new FrontlineModeKernel(topology, gameMode, mapBinding);
        _objectiveWeights = formSnapshot.ToImmutableDictionary(
            form => form.Id,
            form => form.ObjectiveWeight,
            StringComparer.Ordinal);
        _control = _kernel.CreateInitialState();
        // Absent means inert throughout: a mode that declares no economy
        // builds no kernel, holds no state, and publishes two empty arrays for
        // the whole match.
        _scrapKernel = gameMode.ScrapEconomy is null
            ? null
            : new FrontlineScrapKernel(
                topology,
                map,
                formSnapshot,
                unitSlotLifecycle,
                gameMode.ScrapEconomy);
        _scrap = _scrapKernel?.CreateInitialState();
    }

    public GenericActorModeState State =>
        new GenericActorModeState.Frontline(
            _control,
            _kernel.CreateScoreState(_control));

    public GenericActorModeTickResult ApplyJointTick(
        GenericActorModeWorldView world,
        GenericActorModeTickInput input)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(input);

        FrontlineControlState previousControl = _control;
        FrontlineScoreState previousScores =
            _kernel.CreateScoreState(previousControl);
        GenericActorRuntimeObservation.ModeObservationState.Frontline
            previousMode = ProjectControl(
                previousControl,
                _scrapAtTickStart ?? _scrap);
        ImmutableHashSet<Position> activeTiles =
            _objectiveTiles[previousControl.ActivePositionIndex];

        FrontlineControlStepResult step = _kernel.ApplyJointTick(
            previousControl,
            input.Tick,
            Presence(world, input, activeTiles),
            WeightOn(world, _secondarySiteTiles));
        _control = step.State;
        // The economy resolves against the same post-combat bodies the
        // objective does, and lands before the objective's own publication so
        // one ModeChanged fact carries both.
        if (_scrapKernel is not null && _scrap is not null)
        {
            _scrap = _scrapKernel.ApplyJointTick(
                _scrap,
                input.Tick,
                world.ActiveLives
                    .Select(life => new FrontlineScrapBody(
                        life.ActorId,
                        life.FormId,
                        life.Position))
                    .ToArray(),
                input.Destructions);
        }
        _scrapAtTickStart = null;
        _investTick = -1;
        FrontlineScoreState scores = _kernel.CreateScoreState(_control);
        GenericActorRuntimeObservation.ModeObservationState.Frontline mode =
            ProjectControl(_control, _scrap);

        return new GenericActorModeTickResult(
            ScoreChanges(previousScores, scores),
            Equals(previousMode, mode) ? null : mode,
            _control.WinnerTeamId is not null);
    }

    public IReadOnlyList<string> InvestableTracks(int teamId) =>
        _scrapKernel is null || _scrap is null
            ? []
            : _scrapKernel.InvestableTracks(_scrap, teamId);

    public bool TryInvest(int tick, ActorIdentity actor, string trackId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (_scrapKernel is null || _scrap is null)
            return false;
        if (_investTick != tick)
        {
            _scrapAtTickStart = _scrap;
            _investTick = tick;
        }
        if (!_scrapKernel.TryInvest(
                _scrap,
                actor.TeamId,
                trackId,
                out FrontlineScrapState bought))
        {
            return false;
        }
        _scrap = bought;
        return true;
    }

    public GenericActorModeStatModifiers StatModifiersFor(
        ActorIdentity actor) =>
        _scrapKernel is null || _scrap is null
            ? GenericActorModeStatModifiers.None
            : _scrapKernel.ModifiersFor(_scrap, actor);

    public GenericActorModeCompletion ResolveCompletion(
        GenericActorModeCompletionKind kind,
        int endTick,
        GenericActorModeWorldView world)
    {
        ArgumentNullException.ThrowIfNull(world);
        GenericFrontlineEndReason reason;
        string completionReason;
        TeamStandings standings;
        switch (kind)
        {
            case GenericActorModeCompletionKind.FaultEligibility:
                reason = GenericFrontlineEndReason.FaultEligibility;
                completionReason = "fault-eligibility";
                standings = _kernel.ResolveTimeoutStandings(
                    _control,
                    world.EligibleTeamIds);
                break;
            case GenericActorModeCompletionKind.ModeObjective:
                reason = GenericFrontlineEndReason.BaseBreach;
                completionReason = "base-breach";
                standings = _kernel.ResolveBreachStandings(
                    _control,
                    world.EligibleTeamIds);
                break;
            case GenericActorModeCompletionKind.MaxTicks:
                reason = GenericFrontlineEndReason.MaxTicks;
                completionReason = "max-ticks";
                standings = _kernel.ResolveTimeoutStandings(
                    _control,
                    world.EligibleTeamIds);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new GenericActorModeCompletion.Frontline(
            completionReason,
            endTick,
            standings,
            reason,
            ProjectControl(_control, _scrap),
            _kernel.CreateScoreState(_control));
    }

    public GenericActorModeProjection Project(
        GenericActorModeWorldView world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Dictionary<int, FrontlineTeamScore> scores =
            _kernel.CreateScoreState(_control)
                .Teams
                .ToDictionary(score => score.TeamId);
        HashSet<int> eligible = world.EligibleTeamIds.ToHashSet();
        return new GenericActorModeProjection(
            new GenericActorRuntimeObservation.ScoreboardState(
                _topology.Teams
                    .OrderBy(team => team.TeamId)
                    .Select(team =>
                        new GenericActorRuntimeObservation.TeamScoreState(
                            team.TeamId,
                            eligible.Contains(team.TeamId),
                            [
                                new GenericActorRuntimeObservation.ScoreValue(
                                    TerritorialProgressChannel,
                                    scores[team.TeamId]
                                        .TerritorialProgress),
                            ]))
                    .ToImmutableArray()),
            ProjectControl(_control, _scrap),
            _scrap?.CarriedByActor);
    }

    /// <summary>
    /// This tick's reading of the active objective. A ruleset that does not
    /// channel gets exactly today's objective weight and nothing else; a
    /// channeled one additionally gets the stationary part of that weight and
    /// the hostile damage that landed on the region, which are the two facts
    /// the channel resolves control and the interrupt from.
    /// </summary>
    private FrontlineObjectivePresence Presence(
        GenericActorModeWorldView world,
        GenericActorModeTickInput input,
        ImmutableHashSet<Position> activeTiles)
    {
        ImmutableDictionary<int, int> denial = WeightOn(world, activeTiles);
        if (_gameMode.Capture.ControlPolicy
            != FrontlineCaptureDefinition.ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds)
        {
            return new FrontlineObjectivePresence(denial);
        }

        return new FrontlineObjectivePresence(
            denial,
            WeightOn(world, activeTiles, stationaryOnly: true),
            input.DamageContacts
                .Where(contact =>
                    contact.IsHostile
                    && activeTiles.Contains(contact.TargetPosition)
                    && contact.ActualHealthRemoved > 0)
                .GroupBy(contact => contact.TargetTeamId)
                .OrderBy(group => group.Key)
                .ToImmutableDictionary(
                    group => group.Key,
                    group => group.Sum(contact =>
                        contact.ActualHealthRemoved)));
    }

    /// <summary>
    /// Positive objective weight each scoring team currently has standing on
    /// a tile set, from the post-damage active lives. Zero-weight forms — an
    /// anchored turret — never appear, which is why fortifying forfeits a
    /// side site exactly as it forfeits the front. Under
    /// <paramref name="stationaryOnly"/> the sum counts only bodies that held
    /// their tile this tick.
    /// </summary>
    private ImmutableDictionary<int, int> WeightOn(
        GenericActorModeWorldView world,
        ImmutableHashSet<Position> tiles,
        bool stationaryOnly = false) =>
        tiles.IsEmpty
            ? ImmutableDictionary<int, int>.Empty
            : world.ActiveLives
                .Where(life =>
                    _objectiveWeights.TryGetValue(
                        life.FormId,
                        out int objectiveWeight)
                    && objectiveWeight > 0
                    && tiles.Contains(life.Position)
                    && (!stationaryOnly || life.IsStationary))
                .GroupBy(life => life.ActorId.TeamId)
                .OrderBy(group => group.Key)
                .ToImmutableDictionary(
                    group => group.Key,
                    group => group.Sum(life =>
                        _objectiveWeights[life.FormId]));

    private GenericActorRuntimeObservation.ModeObservationState.Frontline
        ProjectControl(
            FrontlineControlState control,
            FrontlineScrapState? scrap) =>
        FrontlineControlProjection.Project(
            _gameMode.ModeId,
            control,
            _scrapKernel is null || scrap is null
                ? null
                : (_scrapKernel, scrap));

    private static ImmutableArray<GenericActorModeScoreChange> ScoreChanges(
        FrontlineScoreState previous,
        FrontlineScoreState current)
    {
        Dictionary<int, FrontlineTeamScore> before =
            previous.Teams.ToDictionary(score => score.TeamId);
        return current.Teams
            .Where(score =>
                before[score.TeamId].TerritorialProgress
                    != score.TerritorialProgress)
            .Select(score => new GenericActorModeScoreChange(
                score.TeamId,
                TerritorialProgressChannel,
                score.TerritorialProgress))
            .ToImmutableArray();
    }
}
