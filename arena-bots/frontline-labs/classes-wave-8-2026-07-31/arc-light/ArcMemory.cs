using BotArena.Sdk;

/// <summary>
/// The little that is worth remembering, kept exactly as long as the contract
/// says private memory lives: one body life. A same-life form change preserves
/// it — which is the only reason a stance can remember where it last cast from —
/// and destruction, respawn and fabrication throw it away, so nothing here is
/// ever treated as team knowledge.
///
/// <para>Two facts genuinely need history, and wave 4 got both wrong by reading
/// them out of a single frozen observation.</para>
/// <list type="number">
/// <item>A shield's deflection count. Each observation carries only the events
/// visible on THAT tick, so counting deflections per tick can never reach a
/// threshold of three: the count has to accumulate.</item>
/// <item>Where this body last cast from. "Never squat predictably at cast range"
/// is not expressible without it, and the contract hands it to us for free
/// because the stance and the return are the same life.</item>
/// </list>
/// </summary>
internal sealed class ArcMemory
{
    private readonly Dictionary<ActorIdentity, int> _deflections = [];
    private Dictionary<ActorIdentity, Position> _wasAt = [];

    /// <summary>Deflections seen per enemy guard, accumulated over the life.</summary>
    public IReadOnlyDictionary<ActorIdentity, int> Deflections => _deflections;

    /// <summary>
    /// Where each body of this team stood on the previous tick, so "did this
    /// tile change" — the ONE question a channelled claim is decided by — has an
    /// answer for allies as well as for self.
    ///
    /// <para>Wave 8's one genuinely new memory. The channel counts a body toward
    /// claim weight only when its tile did not change this tick, and the
    /// observation publishes where every allied body IS and never where it WAS.
    /// Every life receives the same allied body state, so every life can
    /// accumulate the same previous-position map from its own frozen
    /// observations without a shared channel — and a life with no entry is
    /// treated as stationary, which is exactly the engine's own rule for a body
    /// with no previous position.</para>
    /// </summary>
    public bool MovedLastTick(ActorIdentity actor, Position now) =>
        _wasAt.TryGetValue(actor, out Position before) && before != now;

    /// <summary>Tile this body last entered a stance from, if it ever did.</summary>
    public Position? LastCastTile { get; private set; }

    /// <summary>Tick of the last stance entry, or a large negative sentinel.</summary>
    public int LastCastTick { get; private set; } = int.MinValue / 2;

    /// <summary>Stance entries this life has committed to.</summary>
    public int Casts { get; private set; }

    /// <summary>Fold this tick's visible events into the life's history.</summary>
    public void Observe(GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedEvent observed
                 in context.VisibleEvents)
        {
            if (observed.Payload
                is GenericActorContext.EventPayload.ProjectileDeflected
                    deflection)
            {
                _deflections.TryGetValue(deflection.TargetActorId, out int seen);
                _deflections[deflection.TargetActorId] = seen + 1;
            }
            else if (observed.Payload
                is GenericActorContext.EventPayload.FormTransition transition)
            {
                // A guard that returned — by its own budget or by choice — has
                // dropped its shield, so its count starts again on re-entry.
                if (_deflections.ContainsKey(transition.ActorId))
                    _deflections.Remove(transition.ActorId);
            }
        }
    }

    /// <summary>
    /// Close the tick: freeze where this team's bodies are standing so the next
    /// tick can ask what moved. Called after every decision, including the ones
    /// that return early, so the map never skips a tick and never reports a
    /// two-tick-old tile as "last tick".
    /// </summary>
    public void Close(GenericActorContext context)
    {
        var snapshot = new Dictionary<ActorIdentity, Position>
        {
            [context.Self.ActorId] = context.Self.Position,
        };
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            snapshot[ally.ActorId] = ally.Position;
        _wasAt = snapshot;
    }

    private int _flankEpoch = int.MinValue;
    private bool _flankValue;

    /// <summary>
    /// The team's shared flank bit for an epoch, held across the ticks inside
    /// it. The team stream re-derives every tick, so a choice that must persist
    /// has to be remembered somewhere — and life-scoped memory is the only
    /// memory there is. Every life that draws at the same point in the same
    /// epoch-boundary tick latches the same bit; a life created inside an epoch
    /// latches that tick's draw instead and rejoins at the next boundary.
    /// </summary>
    public bool SharedFlank(int epoch, bool fresh)
    {
        if (epoch != _flankEpoch)
        {
            _flankEpoch = epoch;
            _flankValue = fresh;
        }
        return _flankValue;
    }

    /// <summary>Record a stance entry so the next one does not repeat it.</summary>
    public void RecordCast(Position tile, int tick)
    {
        LastCastTile = tile;
        LastCastTick = tick;
        Casts++;
    }
}
