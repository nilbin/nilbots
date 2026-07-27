using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Compatibility façade over the mode-neutral generic actor match session.
/// Existing Deathmatch callers retain their exact public DTOs while all world
/// orchestration lives in <see cref="GenericActorMatchSession"/>.
/// </summary>
public sealed class GenericDeathmatchSession : IDisposable
{
    private readonly GenericActorMatchSession _inner;
    private readonly IReadOnlyDictionary<string, ActorAttackProfileDefinition>
        _attackProfiles;
    private GenericActorMatchPreparedTick? _preparedSource;
    private GenericDeathmatchTickStart? _prepared;
    private GenericActorMatchResult? _resultSource;
    private GenericDeathmatchResult? _result;

    public GenericDeathmatchSession(
        ActorResolvedMatchDefinition definition,
        IEnumerable<GenericActorParticipantConfiguration> participants,
        ulong matchSeed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(participants);

        _attackProfiles = definition.Rules.AttackProfiles.ToDictionary(
            profile => profile.Id,
            StringComparer.Ordinal);
        _inner = new GenericActorMatchSession(
            definition,
            participants,
            matchSeed);
    }

    public ActorResolvedMatchDefinition Definition => _inner.Definition;
    public int Tick => _inner.Tick;
    public bool IsCompleted => _inner.IsCompleted;
    public GenericDeathmatchResult? Result => AdaptResult(_inner.Result);

    public DeathmatchScoreState Scores =>
        _inner.ModeState is GenericActorModeState.Deathmatch deathmatch
            ? deathmatch.Scores
            : throw new InvalidOperationException(
                "The Deathmatch façade received a non-Deathmatch mode state.");

    public GenericActorMatchDescriptor MatchDescriptor =>
        _inner.MatchDescriptor;

    public GenericActorMatchChronology Chronology => _inner.Chronology;

    public ImmutableArray<GenericDeathmatchLifeSnapshot> ActiveLives =>
        _inner.ActiveLives
            .Select(life => new GenericDeathmatchLifeSnapshot(
                life.ActorId,
                life.ParticipantId,
                life.Generation,
                life.FormId,
                life.Position,
                life.Facing,
                life.Health,
                life.Cooldown,
                life.Energy,
                life.PreviousActionResolution))
            .ToImmutableArray();

    public ImmutableArray<GenericDeathmatchProjectileSnapshot> Projectiles =>
        _inner.Projectiles
            .Select(projectile =>
                new GenericDeathmatchProjectileSnapshot(
                    projectile.ProjectileId,
                    projectile.OwnerTeamId,
                    projectile.OwnerActorId,
                    projectile.Position,
                    projectile.Heading,
                    _attackProfiles[projectile.AttackProfileId]
                        .Projectile.TilesPerAdvance,
                    projectile.TicksUntilAdvance,
                    projectile.RemainingTiles))
            .ToImmutableArray();

    public ImmutableArray<GenericDeathmatchSlotSnapshot> Slots =>
        _inner.Slots
            .Select(slot => new GenericDeathmatchSlotSnapshot(
                slot.TeamId,
                slot.UnitId,
                slot.ParticipantId,
                slot.State))
            .ToImmutableArray();

    public GenericDeathmatchTickStart PrepareTick() =>
        AdaptPrepared(_inner.PrepareTick());

    public GenericDeathmatchStepResult Step() =>
        AdaptStep(_inner.Step());

    public GenericDeathmatchStepResult Step(
        IEnumerable<GenericActorRuntimeObservation> observations) =>
        // Deliberately pass the caller's exact enumeration through. The inner
        // session validates the same frozen observation object references.
        AdaptStep(_inner.Step(observations));

    public GenericDeathmatchResult Run() =>
        AdaptResult(_inner.Run())
        ?? throw new InvalidOperationException(
            "A completed Deathmatch has no compatibility result.");

    public void Dispose() => _inner.Dispose();

    internal static GenericActorRuntimeObservation.EventPayload.LifeSpawned
        RedactLifeSpawned(
            GenericActorRuntimeObservation.EventPayload.LifeSpawned value,
            int observingTeamId,
            IReadOnlySet<ActorIdentity> visibleEnemyIds) =>
        GenericActorMatchSession.RedactLifeSpawned(
            value,
            observingTeamId,
            visibleEnemyIds);

    private GenericDeathmatchTickStart AdaptPrepared(
        GenericActorMatchPreparedTick source)
    {
        if (ReferenceEquals(source, _preparedSource))
            return _prepared!;

        _preparedSource = source;
        _prepared = new GenericDeathmatchTickStart(
            source.Tick,
            source.Observations,
            source.TickStartEvents);
        return _prepared;
    }

    private GenericDeathmatchStepResult AdaptStep(
        GenericActorMatchStepResult source)
    {
        GenericDeathmatchTickStart tickStart =
            AdaptPrepared(source.TickStart);
        GenericDeathmatchResult? result = AdaptResult(source.Result);
        var adapted = new GenericDeathmatchStepResult(
            source.Tick,
            tickStart,
            source.RuntimeTick,
            source.ActionResolutions
                .Select(resolution =>
                    new GenericDeathmatchActorResolution(
                        resolution.ParticipantId,
                        resolution.ActorId,
                        resolution.Resolution))
                .ToImmutableArray(),
            source.Events,
            Scores,
            source.IsCompleted,
            result);
        _preparedSource = null;
        _prepared = null;
        return adapted;
    }

    private GenericDeathmatchResult? AdaptResult(
        GenericActorMatchResult? source)
    {
        if (source is null)
            return null;
        if (ReferenceEquals(source, _resultSource))
            return _result;
        var mode = source.Mode as GenericActorMatchModeResult.Deathmatch
            ?? throw new InvalidOperationException(
                "The Deathmatch façade received a non-Deathmatch result.");

        _resultSource = source;
        _result = new GenericDeathmatchResult(
            mode.Reason,
            source.EndTick
                ?? throw new InvalidOperationException(
                    "A completed Deathmatch result has no end tick."),
            mode.Scores,
            source.Standings);
        return _result;
    }
}
