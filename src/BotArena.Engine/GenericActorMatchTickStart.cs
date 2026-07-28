using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete post-lifecycle, pre-decision boundary for one generic match tick.
/// </summary>
public sealed record GenericActorMatchTickStart
{
    public GenericActorMatchTickStart(
        int tick,
        GenericActorWorldSnapshot state,
        IReadOnlyCollection<ActorIdentity> activeActorIds,
        IReadOnlyCollection<GenericActorLifeStart> lifeStarts,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(activeActorIds);
        ArgumentNullException.ThrowIfNull(lifeStarts);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(traversals);
        if (state.NextTick != tick)
        {
            throw new ArgumentException(
                "Tick-start state must identify the tick about to execute.",
                nameof(state));
        }

        ActorIdentity[] actorSnapshot = [.. activeActorIds];
        if (actorSnapshot.Any(actor => actor is null)
            || actorSnapshot.Distinct().Count() != actorSnapshot.Length)
        {
            throw new ArgumentException(
                "Active actor identities must be non-null and unique.",
                nameof(activeActorIds));
        }
        ActorIdentity[] stateActors = state.ActiveLives
            .Select(life => life.ActorId)
            .Order()
            .ToArray();
        if (!actorSnapshot.Order().SequenceEqual(stateActors))
        {
            throw new ArgumentException(
                "Tick-start active actors must exactly match active world lives.",
                nameof(activeActorIds));
        }

        GenericActorLifeStart[] startSnapshot = [.. lifeStarts];
        ValidateLifeStarts(tick, state, startSnapshot);
        GenericActorAuthoritativeEvent[] eventSnapshot = [.. events];
        ValidateEvents(tick, eventSnapshot);
        GenericActorProjectileTraversal[] traversalSnapshot =
            [.. traversals];
        ValidateTraversals(tick, traversalSnapshot);
        ValidateSharedFactOrdinals(eventSnapshot, traversalSnapshot);

        Tick = tick;
        State = state;
        ActiveActorIds = actorSnapshot
            .Order()
            .ToImmutableArray();
        LifeStarts = startSnapshot
            .OrderBy(start => start.ActorId)
            .ToImmutableArray();
        Events = eventSnapshot
            .OrderBy(item => item.Ordinal)
            .ToImmutableArray();
        Traversals = traversalSnapshot
            .OrderBy(item => item.Ordinal)
            .ToImmutableArray();
    }

    public int Tick { get; }
    public GenericActorWorldSnapshot State { get; }
    public ImmutableArray<ActorIdentity> ActiveActorIds { get; }
    public ImmutableArray<GenericActorLifeStart> LifeStarts { get; }
    public ImmutableArray<GenericActorAuthoritativeEvent> Events { get; }
    public ImmutableArray<GenericActorProjectileTraversal> Traversals { get; }

    private static void ValidateLifeStarts(
        int tick,
        GenericActorWorldSnapshot state,
        IReadOnlyCollection<GenericActorLifeStart> starts)
    {
        if (starts.Any(start => start is null)
            || starts.Select(start => start.ActorId).Distinct().Count()
                != starts.Count)
        {
            throw new ArgumentException(
                "Tick-start life starts must be non-null and actor-unique.",
                nameof(starts));
        }

        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            activeLives = state.ActiveLives.ToDictionary(
                life => life.ActorId);
        if (starts.Any(start =>
                !activeLives.TryGetValue(
                    start.ActorId,
                    out GenericActorWorldSnapshot.LifeSnapshot? life)
                || life.ParticipantId != start.ParticipantId
                || life.SpawnedAtTick != tick
                || life.Generation != start.Origin.Generation
                || life.SpawnReason != start.Origin.Reason
                || life.ParentActorId != start.Origin.ParentActorId
                || !string.Equals(
                    life.SourceTransitionId,
                    start.Origin.SourceTransitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    life.SourceOperationId,
                    start.Origin.SourceOperationId,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Tick-start life starts must exactly describe lives created at that boundary.",
                nameof(starts));
        }
    }

    private static void ValidateEvents(
        int tick,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events)
    {
        if (events.Any(item => item is null || item.Tick != tick)
            || events.Select(item => item.Ordinal).Distinct().Count()
                != events.Count)
        {
            throw new ArgumentException(
                "Tick-start events must be non-null, tick-aligned, and ordinal-unique.",
                nameof(events));
        }
    }

    private static void ValidateTraversals(
        int tick,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals)
    {
        if (traversals.Any(item =>
                item is null
                || item.Tick != tick
                || item.Phase
                    != GenericActorProjectileTraversal.TraversalPhase
                        .TickStart)
            || traversals.Select(item => item.Ordinal).Distinct().Count()
                != traversals.Count)
        {
            throw new ArgumentException(
                "Tick-start projectile transitions must be tick-start facts with unique ordinals.",
                nameof(traversals));
        }
    }

    private static void ValidateSharedFactOrdinals(
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals)
    {
        long[] ordinals =
        [
            .. events.Select(item => item.Ordinal),
            .. traversals.Select(item => item.Ordinal),
        ];
        if (ordinals.Distinct().Count() != ordinals.Length)
        {
            throw new ArgumentException(
                "Tick-start authoritative facts must have unique global ordinals.");
        }
    }
}
