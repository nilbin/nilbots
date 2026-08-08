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

    /// <summary>Deflections seen per enemy guard, accumulated over the life.</summary>
    public IReadOnlyDictionary<ActorIdentity, int> Deflections => _deflections;

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

    /// <summary>Record a stance entry so the next one does not repeat it.</summary>
    public void RecordCast(Position tile, int tick)
    {
        LastCastTile = tile;
        LastCastTick = tick;
        Casts++;
    }
}
