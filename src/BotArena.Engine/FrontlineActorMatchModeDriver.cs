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
    private readonly ImmutableDictionary<string, int> _objectiveWeights;
    private FrontlineControlState _control;

    public FrontlineActorMatchModeDriver(
        PublicMatchTopology topology,
        ActorMapDefinition map,
        IReadOnlyCollection<ActorFormDefinition> forms,
        FrontlineGameModeDefinition gameMode,
        FrontlineActorModeMapBindingDefinition mapBinding)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(forms);
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
            previousMode = ProjectControl(previousControl);
        ImmutableHashSet<Position> activeTiles =
            _objectiveTiles[previousControl.ActivePositionIndex];
        ImmutableDictionary<int, int> objectiveWeightByTeam =
            world.ActiveLives
            .Where(life =>
                _objectiveWeights.TryGetValue(
                    life.FormId,
                    out int objectiveWeight)
                && objectiveWeight > 0
                && activeTiles.Contains(life.Position))
            .GroupBy(life => life.ActorId.TeamId)
            .OrderBy(group => group.Key)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.Sum(life =>
                    _objectiveWeights[life.FormId]));

        FrontlineControlStepResult step = _kernel.ApplyJointTick(
            previousControl,
            input.Tick,
            objectiveWeightByTeam);
        _control = step.State;
        FrontlineScoreState scores = _kernel.CreateScoreState(_control);
        GenericActorRuntimeObservation.ModeObservationState.Frontline mode =
            ProjectControl(_control);

        return new GenericActorModeTickResult(
            ScoreChanges(previousScores, scores),
            Equals(previousMode, mode) ? null : mode,
            _control.WinnerTeamId is not null);
    }

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
            ProjectControl(_control),
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
            ProjectControl(_control));
    }

    private GenericActorRuntimeObservation.ModeObservationState.Frontline
        ProjectControl(FrontlineControlState control) =>
        FrontlineControlProjection.Project(_gameMode.ModeId, control);

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
