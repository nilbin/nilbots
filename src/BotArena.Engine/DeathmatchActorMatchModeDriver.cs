using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Deathmatch implementation of the internal generic mode seam. It owns the
/// mutable score state while the public kernel remains pure.
/// </summary>
internal sealed class DeathmatchActorMatchModeDriver
    : IGenericActorMatchModeDriver
{
    private readonly DeathmatchGameModeDefinition _gameMode;
    private readonly DeathmatchModeKernel _kernel;
    private readonly PublicMatchTopology _topology;
    private readonly HashSet<string> _publicScoreChannels;
    private DeathmatchScoreState _scores;

    public DeathmatchActorMatchModeDriver(
        PublicMatchTopology topology,
        DeathmatchGameModeDefinition gameMode)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(gameMode);

        _topology = topology;
        _gameMode = gameMode;
        _kernel = new DeathmatchModeKernel(topology, gameMode);
        _publicScoreChannels = gameMode.ScoreCatalog
            .Select(channel =>
                ActorContractCanonicalIds.Id(channel.Channel))
            .ToHashSet(StringComparer.Ordinal);
        _scores = _kernel.CreateInitialState();
    }

    public DeathmatchScoreState Scores => _scores;
    public GenericActorModeState State =>
        new GenericActorModeState.Deathmatch(_scores);

    public GenericActorModeTickResult ApplyJointTick(
        GenericActorModeWorldView world,
        GenericActorModeTickInput input)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(input);

        DeathmatchScoreState previous = _scores;
        DeathmatchJointTickResult tick = _kernel.ApplyJointTick(
            previous,
            input.DamageContacts
                .Select(contact => new DeathmatchDamageContact(
                    contact.SourceTeamId,
                    contact.TargetTeamId,
                    contact.ActualHealthRemoved,
                    contact.CausedDestruction))
                .ToImmutableArray(),
            world.ActiveHealthByTeam,
            world.EligibleTeamIds);
        ImmutableArray<GenericActorModeScoreChange> scoreChanges =
            ScoreChanges(previous, tick.ScoreState);
        _scores = tick.ScoreState;
        return new GenericActorModeTickResult(
            scoreChanges,
            modeChange: null,
            tick.KillLimitCompleted);
    }

    public GenericActorModeCompletion ResolveCompletion(
        GenericActorModeCompletionKind kind,
        int endTick,
        GenericActorModeWorldView world)
    {
        ArgumentNullException.ThrowIfNull(world);
        TeamStandings standings;
        GenericDeathmatchEndReason reason;
        string completionReason;
        switch (kind)
        {
            case GenericActorModeCompletionKind.FaultEligibility:
                reason = GenericDeathmatchEndReason.FaultEligibility;
                completionReason = "fault-eligibility";
                standings = _kernel.ResolveTimeoutStandings(
                    _scores,
                    world.ActiveHealthByTeam,
                    world.EligibleTeamIds);
                break;
            case GenericActorModeCompletionKind.ModeObjective:
                reason = GenericDeathmatchEndReason.KillLimit;
                completionReason = "kill-limit";
                // Re-resolve after end-clock transitions. Contacts are empty so
                // kills are not applied twice, while terminal health reflects
                // the final post-transition world.
                standings = _kernel.ApplyJointTick(
                        _scores,
                        damageContacts: [],
                        world.ActiveHealthByTeam,
                        world.EligibleTeamIds)
                    .KillLimitStandings
                    ?? throw new InvalidOperationException(
                        "A reached Deathmatch kill limit disappeared before terminal resolution.");
                break;
            case GenericActorModeCompletionKind.MaxTicks:
                reason = GenericDeathmatchEndReason.MaxTicks;
                completionReason = "max-ticks";
                standings = _kernel.ResolveTimeoutStandings(
                    _scores,
                    world.ActiveHealthByTeam,
                    world.EligibleTeamIds);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new GenericActorModeCompletion.Deathmatch(
            completionReason,
            new GenericDeathmatchResult(
                reason,
                endTick,
                _scores,
                standings));
    }

    public GenericActorModeProjection Project(
        GenericActorModeWorldView world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Dictionary<int, DeathmatchTeamScore> scores =
            _scores.Teams.ToDictionary(score => score.TeamId);
        HashSet<int> eligible = world.EligibleTeamIds.ToHashSet();
        return new GenericActorModeProjection(
            new GenericActorRuntimeObservation.ScoreboardState(
                _topology.Teams
                    .OrderBy(team => team.TeamId)
                    .Select(team =>
                        new GenericActorRuntimeObservation.TeamScoreState(
                            team.TeamId,
                            eligible.Contains(team.TeamId),
                            _gameMode.ScoreCatalog
                                .Select(channel =>
                                    new GenericActorRuntimeObservation.ScoreValue(
                                        ActorContractCanonicalIds.Id(
                                            channel.Channel),
                                        ScoreValue(
                                            scores[team.TeamId],
                                            world.ActiveHealthByTeam[
                                                team.TeamId],
                                            channel.Channel)))
                                .ToImmutableArray()))
                    .ToImmutableArray()),
            new GenericActorRuntimeObservation.ModeObservationState.Deathmatch(
                _gameMode.ModeId));
    }

    /// <summary>
    /// Deathmatch owns no store, so it offers no track, refuses every
    /// purchase, and modifies nothing.
    /// </summary>
    public IReadOnlyList<string> InvestableTracks(int teamId) => [];

    /// <inheritdoc />
    public bool TryInvest(int tick, ActorIdentity actor, string trackId) =>
        false;

    /// <inheritdoc />
    public GenericActorModeStatModifiers StatModifiersFor(
        ActorIdentity actor) =>
        GenericActorModeStatModifiers.None;

    private ImmutableArray<GenericActorModeScoreChange> ScoreChanges(
        DeathmatchScoreState previous,
        DeathmatchScoreState current)
    {
        Dictionary<int, DeathmatchTeamScore> before =
            previous.Teams.ToDictionary(score => score.TeamId);
        var changes =
            ImmutableArray.CreateBuilder<GenericActorModeScoreChange>();
        foreach (DeathmatchTeamScore score in current.Teams)
        {
            DeathmatchTeamScore old = before[score.TeamId];
            AddIfChanged(
                changes,
                score.TeamId,
                "kills",
                old.Kills,
                score.Kills);
            AddIfChanged(
                changes,
                score.TeamId,
                "deaths",
                old.Deaths,
                score.Deaths);
            AddIfChanged(
                changes,
                score.TeamId,
                "damage-dealt",
                old.DamageDealt,
                score.DamageDealt);
        }
        return changes.ToImmutable();
    }

    private void AddIfChanged(
        ImmutableArray<GenericActorModeScoreChange>.Builder changes,
        int teamId,
        string channel,
        long before,
        long after)
    {
        if (before == after || !_publicScoreChannels.Contains(channel))
            return;
        changes.Add(new GenericActorModeScoreChange(
            teamId,
            channel,
            after));
    }

    private static long ScoreValue(
        DeathmatchTeamScore score,
        long activeHealth,
        ScoreChannelDefinition.ChannelKind channel) =>
        channel switch
        {
            ScoreChannelDefinition.ChannelKind.Kills => score.Kills,
            ScoreChannelDefinition.ChannelKind.Deaths => score.Deaths,
            ScoreChannelDefinition.ChannelKind.DamageDealt =>
                score.DamageDealt,
            ScoreChannelDefinition.ChannelKind.ActiveHealth => activeHealth,
            _ => throw new InvalidOperationException(
                "Deathmatch cannot project the declared score channel."),
        };
}
