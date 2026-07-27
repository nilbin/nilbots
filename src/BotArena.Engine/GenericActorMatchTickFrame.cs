using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete chronology for one resolved joint tick.
/// </summary>
public sealed record GenericActorMatchTickFrame
{
    public GenericActorMatchTickFrame(
        GenericActorMatchTickStart tickStart,
        IReadOnlyCollection<GenericActorMatchActorTurn> actorTurns,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
        GenericActorWorldSnapshot postState)
    {
        ArgumentNullException.ThrowIfNull(tickStart);
        ArgumentNullException.ThrowIfNull(actorTurns);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(traversals);
        ArgumentNullException.ThrowIfNull(postState);
        int expectedNextTick = checked(tickStart.Tick + 1);
        if (postState.NextTick != expectedNextTick)
        {
            throw new ArgumentException(
                "Post-state NextTick must be exactly one past the executed tick.",
                nameof(postState));
        }

        GenericActorMatchActorTurn[] turnSnapshot = [.. actorTurns];
        ValidateTurns(tickStart, turnSnapshot);
        GenericActorAuthoritativeEvent[] eventSnapshot = [.. events];
        ValidateEvents(tickStart, eventSnapshot);
        GenericActorProjectileTraversal[] traversalSnapshot =
            [.. traversals];
        ValidateTraversals(tickStart, traversalSnapshot);
        ValidateSharedFactOrdinals(
            tickStart,
            eventSnapshot,
            traversalSnapshot);

        TickStart = tickStart;
        ActorTurns = turnSnapshot
            .OrderBy(turn => turn.ActorId)
            .ToImmutableArray();
        Events = eventSnapshot
            .OrderBy(item => item.Ordinal)
            .ToImmutableArray();
        Traversals = traversalSnapshot
            .OrderBy(item => item.Ordinal)
            .ToImmutableArray();
        PostState = postState;
    }

    public int Tick => TickStart.Tick;
    public GenericActorMatchTickStart TickStart { get; }
    public ImmutableArray<GenericActorMatchActorTurn> ActorTurns { get; }
    public ImmutableArray<GenericActorAuthoritativeEvent> Events { get; }
    public ImmutableArray<GenericActorProjectileTraversal> Traversals { get; }
    public GenericActorWorldSnapshot PostState { get; }

    private static void ValidateTurns(
        GenericActorMatchTickStart tickStart,
        IReadOnlyCollection<GenericActorMatchActorTurn> turns)
    {
        if (turns.Any(turn => turn is null)
            || turns.Select(turn => turn.ActorId).Distinct().Count()
                != turns.Count)
        {
            throw new ArgumentException(
                "Actor turns must be non-null and actor-unique.",
                nameof(turns));
        }
        if (!turns.Select(turn => turn.ActorId)
            .Order()
            .SequenceEqual(tickStart.ActiveActorIds))
        {
            throw new ArgumentException(
                "Actor turns must cover exactly the frozen active actor set.",
                nameof(turns));
        }

        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            lives = tickStart.State.ActiveLives.ToDictionary(
                life => life.ActorId);
        foreach (GenericActorMatchActorTurn turn in turns)
        {
            GenericActorWorldSnapshot.LifeSnapshot life =
                lives[turn.ActorId];
            GenericActorRuntimeObservation.ObservedSelfState self =
                turn.Observation.Self;
            if (turn.Tick != tickStart.Tick
                || turn.ParticipantId != life.ParticipantId
                || self.Generation != life.Generation
                || !string.Equals(
                    self.FormId,
                    life.FormId,
                    StringComparison.Ordinal)
                || self.Position != life.Position
                || self.Facing != life.Facing
                || self.Health != life.Health
                || self.Cooldown != life.Cooldown
                || self.Energy != life.Energy
                || !GenericActorMatchActorTurn
                    .ActionResolutionsSemanticallyEqual(
                        self.PreviousActionResolution,
                        life.PreviousActionResolution)
                || self.PendingSameLifeTransition
                    != life.PendingSameLifeTransition)
            {
                throw new ArgumentException(
                    "Every actor turn must use the exact authoritative pre-tick self state.",
                    nameof(turns));
            }
        }
    }

    private static void ValidateEvents(
        GenericActorMatchTickStart tickStart,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events)
    {
        if (events.Any(item =>
                item is null || item.Tick != tickStart.Tick)
            || events.Select(item => item.Ordinal).Distinct().Count()
                != events.Count)
        {
            throw new ArgumentException(
                "Resolution events must be non-null, tick-aligned, and ordinal-unique.",
                nameof(events));
        }

        long[] allOrdinals =
        [
            .. tickStart.Events.Select(item => item.Ordinal),
            .. events.Select(item => item.Ordinal),
        ];
        if (allOrdinals.Distinct().Count() != allOrdinals.Length
            || (tickStart.Events.Length != 0
                && events.Count != 0
                && tickStart.Events.Max(item => item.Ordinal)
                    >= events.Min(item => item.Ordinal)))
        {
            throw new ArgumentException(
                "Tick-start and resolution event chronology must be disjoint and ordered.",
                nameof(events));
        }
    }

    private static void ValidateTraversals(
        GenericActorMatchTickStart tickStart,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals)
    {
        if (traversals.Any(item =>
                item is null
                || item.Tick != tickStart.Tick
                || item.Phase
                    != GenericActorProjectileTraversal.TraversalPhase
                        .Resolution)
            || traversals.Select(item => item.Ordinal).Distinct().Count()
                != traversals.Count)
        {
            throw new ArgumentException(
                "Resolution projectile transitions must be tick-aligned resolution facts with unique ordinals.",
                nameof(traversals));
        }

        long[] allOrdinals =
        [
            .. tickStart.Traversals.Select(item => item.Ordinal),
            .. traversals.Select(item => item.Ordinal),
        ];
        if (allOrdinals.Distinct().Count() != allOrdinals.Length
            || (tickStart.Traversals.Length != 0
                && traversals.Count != 0
                && tickStart.Traversals.Max(item => item.Ordinal)
                    >= traversals.Min(item => item.Ordinal)))
        {
            throw new ArgumentException(
                "Tick-start and resolution projectile chronology must be disjoint and ordered.",
                nameof(traversals));
        }
    }

    private static void ValidateSharedFactOrdinals(
        GenericActorMatchTickStart tickStart,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals)
    {
        long[] tickStartOrdinals =
        [
            .. tickStart.Events.Select(item => item.Ordinal),
            .. tickStart.Traversals.Select(item => item.Ordinal),
        ];
        long[] resolutionOrdinals =
        [
            .. events.Select(item => item.Ordinal),
            .. traversals.Select(item => item.Ordinal),
        ];
        long[] allOrdinals =
        [
            .. tickStartOrdinals,
            .. resolutionOrdinals,
        ];
        if (allOrdinals.Distinct().Count() != allOrdinals.Length
            || (tickStartOrdinals.Length != 0
                && resolutionOrdinals.Length != 0
                && tickStartOrdinals.Max() >= resolutionOrdinals.Min()))
        {
            throw new ArgumentException(
                "Tick-start and resolution facts must have disjoint, phase-ordered global ordinals.");
        }
    }
}
