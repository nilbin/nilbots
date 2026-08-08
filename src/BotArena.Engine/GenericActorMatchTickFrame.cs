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
        : this(
            tickStart,
            actorTurns,
            events,
            traversals,
            postState,
            [])
    {
    }

    /// <summary>
    /// The mind-profile overload. <paramref name="mindTurns"/> is empty on the
    /// per-life generation and carries one turn per ticking participant under
    /// the mind, which is what the replay document writes INSTEAD of N per-life
    /// turns (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.1).
    /// <para>
    /// The frame keeps its actor turns either way: they are the authoritative
    /// per-body record the rest of the engine's chronology validation reads,
    /// and they are what the mind turn's <c>resolutions[]</c> is projected
    /// from. What changes at the DOCUMENT boundary is which of the two is
    /// written.
    /// </para>
    /// </summary>
    public GenericActorMatchTickFrame(
        GenericActorMatchTickStart tickStart,
        IReadOnlyCollection<GenericActorMatchActorTurn> actorTurns,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
        GenericActorWorldSnapshot postState,
        IReadOnlyCollection<GenericActorMatchMindTurn> mindTurns)
    {
        ArgumentNullException.ThrowIfNull(tickStart);
        ArgumentNullException.ThrowIfNull(actorTurns);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(traversals);
        ArgumentNullException.ThrowIfNull(postState);
        ArgumentNullException.ThrowIfNull(mindTurns);
        int expectedNextTick = checked(tickStart.Tick + 1);
        if (postState.NextTick != expectedNextTick)
        {
            throw new ArgumentException(
                "Post-state NextTick must be exactly one past the executed tick.",
                nameof(postState));
        }

        GenericActorMatchActorTurn[] turnSnapshot = [.. actorTurns];
        ValidateTurns(tickStart, turnSnapshot);
        GenericActorMatchMindTurn[] mindSnapshot = [.. mindTurns];
        ValidateMindTurns(tickStart, turnSnapshot, mindSnapshot);
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
        MindTurns = mindSnapshot
            .OrderBy(turn => turn.ParticipantId)
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

    /// <summary>
    /// One turn per ticking participant under the mind profile, empty on the
    /// per-life generation. Canonical order is by participant.
    /// </summary>
    public ImmutableArray<GenericActorMatchMindTurn> MindTurns { get; }

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

    /// <summary>
    /// The mind-era coverage rule. Every ticking participant contributes one
    /// turn, no participant contributes two, and the union of every turn's
    /// resolved bodies is EXACTLY the frozen active actor set — which is where
    /// the per-life "exactly those keys" strictness belongs once the host stops
    /// mapping N runtimes onto N keys
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.3).
    /// </summary>
    private static void ValidateMindTurns(
        GenericActorMatchTickStart tickStart,
        IReadOnlyCollection<GenericActorMatchActorTurn> actorTurns,
        IReadOnlyCollection<GenericActorMatchMindTurn> mindTurns)
    {
        if (mindTurns.Count == 0)
            return;
        if (mindTurns.Any(turn => turn is null)
            || mindTurns.Select(turn => turn.ParticipantId)
                .Distinct()
                .Count() != mindTurns.Count)
        {
            throw new ArgumentException(
                "Mind turns must be non-null and participant-unique.",
                nameof(mindTurns));
        }
        if (mindTurns.Any(turn => turn.Tick != tickStart.Tick))
        {
            throw new ArgumentException(
                "Mind turns must be aligned to their tick.",
                nameof(mindTurns));
        }
        if (!mindTurns
            .SelectMany(turn => turn.ResolvedBodies)
            .Order()
            .SequenceEqual(tickStart.ActiveActorIds))
        {
            throw new ArgumentException(
                "Mind turns must resolve exactly the frozen active actor set exactly once.",
                nameof(mindTurns));
        }

        Dictionary<ActorIdentity, int> participantByActor = actorTurns
            .ToDictionary(turn => turn.ActorId, turn => turn.ParticipantId);
        foreach (GenericActorMatchMindTurn turn in mindTurns)
        {
            foreach (ActorIdentity body in turn.ResolvedBodies)
            {
                if (!participantByActor.TryGetValue(body, out int owner)
                    || owner != turn.ParticipantId)
                {
                    throw new ArgumentException(
                        "A mind turn resolved a body its participant does not control.",
                        nameof(mindTurns));
                }
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
