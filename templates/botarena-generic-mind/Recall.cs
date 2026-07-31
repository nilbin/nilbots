using BotArena.Sdk;

/// <summary>
/// PERSISTENT MEMORY, SHIPPED WORKING. This object lives as long as the mind
/// does — one instance per participant per match — so everything it remembers
/// survives every body that saw it.
///
/// <para>Under the per-life profile none of this was possible without pain.
/// Every body was a fresh instance with empty fields, so "where did I last see
/// that enemy" died with the body that saw it, and the single most-requested
/// platform fact in the last cohort — did an allied body move this tick — was,
/// in one author's words, "derivable only from life-scoped memory a newborn
/// lacks." Under a mind it is <see cref="MindBody.MovedLastTick"/>, published,
/// and the rest of this file is fifty lines instead of a subsystem.</para>
///
/// <para>One honest cost, and it is worth saying out loud: <b>a mind that
/// traps forgets the match</b>. There is no snapshot and no recovery — the
/// Store is discarded and a fresh mind starts with empty fields — and under
/// the shipped Labs contract the first runtime fault also disqualifies you. So
/// treat robustness as part of your doctrine: guard your indexing, do not
/// assume a body you remember is still alive, and prefer
/// <see cref="MindContext.TryBody"/> over trusting a stored unit id.</para>
/// </summary>
internal sealed class Recall
{
    private readonly Dictionary<ActorIdentity, Sighting> _enemies = [];
    private readonly List<GenericActorContext.ScrapPile> _piles = [];

    /// <summary>Tick of the most recent observation folded in.</summary>
    public int Tick { get; private set; } = -1;

    /// <summary>
    /// Folds one tick into memory. Call it first, every tick, including ticks
    /// on which you own no live body — the enemy is still capturing, veins are
    /// still landing, and this is exactly the window in which beliefs go stale
    /// fastest.
    /// </summary>
    public void Observe(MindContext mind)
    {
        Tick = mind.Tick;

        // Everything currently visible is fact; everything remembered from
        // before is a belief with an age. Keeping the two apart is the whole
        // discipline — see Staleness.
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            _enemies[enemy.ActorId] = new Sighting(
                enemy.ActorId,
                enemy.Position,
                enemy.Health,
                enemy.FormId,
                enemy.ClassId,
                enemy.RoleTag,
                mind.Tick);
        }

        // The pile ledger is fully public and refreshed every tick, so it is
        // replaced rather than merged. It is also a leak worth reading in both
        // directions: a pile is a tile somebody died on within the last eighty
        // ticks, which tells you where the fighting was even if you saw none
        // of it.
        _piles.Clear();
        if (mind.Mode
            is GenericActorContext.ModeObservationState.Frontline frontline)
        {
            _piles.AddRange(frontline.ScrapPiles);
        }
    }

    /// <summary>
    /// Every enemy body ever seen, newest sighting first. A sighting older than
    /// a handful of ticks is a guess: price it by <see cref="Staleness"/>
    /// rather than treating it as a position.
    /// </summary>
    public IEnumerable<Sighting> Enemies =>
        _enemies.Values.OrderByDescending(sighting => sighting.SeenAtTick);

    /// <summary>Ticks since a sighting was taken.</summary>
    public int Staleness(Sighting sighting) => Tick - sighting.SeenAtTick;

    /// <summary>
    /// The pile worth going for, or null when the contract declares no economy
    /// or nothing is on the board. Deliberately naive — richest first — because
    /// what a run is WORTH is doctrine, and doctrine is yours: weigh the walk,
    /// the carry cap, whether the front can spare the body, and whether a pile
    /// on the enemy's half is bait.
    /// </summary>
    public Position? BestPile(MindContext mind)
    {
        if (_piles.Count == 0)
            return null;
        return _piles
            .Where(pile => pile.ExpiresAtTick > mind.Tick)
            .OrderByDescending(pile => pile.Amount)
            .ThenBy(pile => pile.Position.X)
            .ThenBy(pile => pile.Position.Y)
            .Select(pile => (Position?)pile.Position)
            .FirstOrDefault();
    }

    /// <summary>Drops a body's traces when its slot is gone for good.</summary>
    public void Forget(ActorIdentity enemy) => _enemies.Remove(enemy);

    /// <param name="ActorId">Whose body it was.</param>
    /// <param name="Position">Where it was standing.</param>
    /// <param name="Health">How much it had left.</param>
    /// <param name="FormId">The form it wore.</param>
    /// <param name="ClassId">Its chassis, when the contract declares classes.</param>
    /// <param name="RoleTag">
    /// The label the enemy MIND published for it — free vocabulary, entirely
    /// non-authoritative, and public on purpose. Read it for what it says about
    /// their plan, and remember they can lie: calling a channeler a screen
    /// costs them nothing and the engine never checks.
    /// </param>
    /// <param name="SeenAtTick">The tick this sighting was taken.</param>
    internal sealed record Sighting(
        ActorIdentity ActorId,
        Position Position,
        int Health,
        string FormId,
        string? ClassId,
        string? RoleTag,
        int SeenAtTick);
}
