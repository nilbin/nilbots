using BotArena.Sdk;

/// <summary>
/// THE SALVO PASS. Everything wave 7 changed about the fan lives here or is
/// gated by a switch declared here, so every rule can be removed from the
/// working whole one at a time and measured. Nothing in this file names the
/// volley, a class, a form, or a number: it reads the same contract fields the
/// rest of the bot reads and re-derives what a stance is worth from them.
///
/// <para>The re-armed fan changed three prices at once, and wave 6's ledger was
/// priced against all three of the old ones.</para>
///
/// <list type="bullet">
/// <item><description><b>The bolt is worth twice as much.</b> The stance gun's
/// declared <c>damagePerHit</c> is now double the mobile gun's. A diverging fan
/// still lands at most one bolt per body, so this is not triple damage — it is a
/// doubled kill threshold, and the doubling is what makes the difference between
/// chipping a body and removing it.</description></item>
/// <item><description><b>The stance no longer taxes the gun.</b> The stance
/// gun's declared <c>cooldownTicks</c> is at or below the mobile gun's, and the
/// cooldown clock advances with TIME on this arm, so a cast costs no gun tempo
/// at all. Wave 6 charged the fan a tempo tax it no longer pays.</description>
/// </item>
/// <item><description><b>The entry is one tick.</b> Entry and exit windups are
/// read from the routes; when the entry completes on the tick it started, the
/// blind commitment wave 6 priced — two ticks of wait-only with lethal damage
/// cancelling — is one tick, which is the same exposure as spending the tick on
/// the gun.</description></item>
/// </list>
///
/// <para>And one price appeared: the entry route declares a
/// <c>cooldownTicks</c> scoped to the UNIT SLOT. Frequency is charged on the
/// entry, not on the shot. That clock survives this body's death, so it cannot
/// be inferred from a life's own history — it is read from
/// <c>self.routeCooldowns</c>, and an ally's is read from its own state.</para>
/// </summary>
internal static class Salvo
{
    /// <summary>
    /// RULE 1 — THE ENTRY CLOCK IS READ. The stance entry route may declare a
    /// slot-scoped cooldown; a request inside the window is refused. The window
    /// survives this body's death and a life born inside it has no history to
    /// infer it from, so it is taken from the published clock or not at all.
    /// Off: the ledger ignores the clock and spends ticks on refused requests.
    /// </summary>
    public static readonly bool EntryClockRead = true;

    /// <summary>
    /// RULE 2 — THE LEDGER IS RE-DERIVED FROM THE DECLARED PRICES. The bar a
    /// cast has to clear is built from the two windups, the two guns' cooldowns
    /// and the two guns' damage, instead of from wave 6's constants. Off: wave
    /// 6's bar (a 15%-per-lane margin over the displaced bolt, floored at the
    /// fan's bolt count) is used unchanged.
    /// </summary>
    public static readonly bool RepricedBar = true;

    /// <summary>
    /// RULE 2b — THE ENTRY IS PRICED BY ITS DECLARED LENGTH. Two clauses. The
    /// window the body cannot walk out of is the entry windup, the firing tick
    /// and the exit windup — all three read from the routes — so that whole
    /// window is what in-flight bolts are checked against. And a loaded enemy gun
    /// that merely COULD cover the tile stops being a veto: on a widened envelope
    /// that is most of the ground in front of you, and the rule that was meant is
    /// "the body can afford one hit it did not see coming", priced off the worst
    /// damage the opposing catalog declares. Off: wave 6's shorter window and its
    /// blanket refusal of any covered tile.
    /// </summary>
    public static readonly bool CheapEntry = false;

    /// <summary>
    /// RULE 3 — THE KILL THRESHOLD. A fan bolt whose declared damage is at or
    /// above a body's observed health, where the mobile bolt's is not, removes
    /// that body instead of wounding it. That is not more damage per tick; it is
    /// a threshold, and it is the only thing a doubled bolt actually buys a
    /// doctrine that converts damage into TIME. One such body is worth a stance.
    /// Off: wave 6's two-body rule stands and the threshold is invisible.
    /// </summary>
    public static readonly bool KillThreshold = false;

    /// <summary>
    /// RULE 3b — THE LANE HEDGE. A body still choosing its tile that two or more
    /// rays can reach is the case a one-bolt gun structurally cannot answer: the
    /// bolt must pick one of those tiles and lose the others, the fan picks none
    /// and covers them all on one tick. Wave 6 required the OPPOSITE — a steady
    /// target — because a two-tick entry made a fan aimed at a mover a bet placed
    /// before the dice landed. Off: no cast is justified by movement alone.
    /// </summary>
    public static readonly bool HedgeFan = false;

    /// <summary>
    /// RULE 4 — THE FAN RESPECTS A PROJECTILE GUARD. A guarded form returns a
    /// bolt that arrives inside its facing quadrant, team-flipped, along the
    /// exactly reversed heading, carrying THE DAMAGE CLASS OF THE BOLT IT
    /// RETURNED. The mobile shot planner has priced that since wave 5; the fan
    /// never did, and under a doubled fan bolt the returned bolt is doubled too.
    /// Off: the fan is scored as if no form deflected anything.
    /// </summary>
    public static readonly bool GuardAwareFan = true;

    /// <summary>
    /// RULE 5 — THE GUN IS HELD FOR THE REOPENING STANCE. The entry is refused
    /// while the body's attack cooldown exceeds the entry windup, so firing the
    /// mobile gun on the wrong tick closes a stance window that the published
    /// clock says is about to open. A speculative bolt is not worth a cast.
    /// Off: the gun fires on its own ladder and the clock is spent late.
    /// </summary>
    public static readonly bool ClockTempo = true;

    /// <summary>
    /// RULE 6 — SIBLINGS STAGGER THEIR CASTS. Allies publish their own entry
    /// clocks. Two fans opened on the same tick into the same arcs are six bolts
    /// competing for three lanes, and a diverging fan lands at most one bolt per
    /// body either way; a fan held eight ticks is a second window. A body defers
    /// to a better-ranked sibling whose clock is open and whose arcs overlap
    /// mine. Off: every body casts on its own ledger alone.
    /// </summary>
    public static readonly bool CastStagger = false;

    /// <summary>
    /// RULE 7 — THE STANCE IS COMMITTED. Firing IS the return: the automatic
    /// return spends the same exit windup a requested <c>mobilize</c> spends,
    /// and the stance gun's cooldown is at or below the mobile gun's, so leaving
    /// without firing costs exactly what firing costs and throws the whole entry
    /// clock away. So the stance never walks out under its own power while it
    /// can still shoot. Off: wave 6's ladder, which mobilizes out when the fan
    /// has nothing named to arrive on.
    /// </summary>
    public static readonly bool StanceCommit = true;

    /// <summary>
    /// Per-tick tempo tax on the ticks a cast spends unable to move, over and
    /// above the one tick the bolt it displaces also costs. This is the pass's
    /// single tuned coefficient; everything it multiplies is contract data.
    /// </summary>
    public const double IdleTickTax = 0.12;
}

/// <summary>
/// One same-life route's live slot-scoped cooldown, read from the published
/// clock. Absent means the route is open — including on every contract that
/// declares no route cooldown at all, where the collection is always empty.
/// </summary>
internal readonly record struct EntryClock(bool Live, int ReadyAtTick)
{
    public static EntryClock Open => new(false, int.MinValue);

    /// <summary>Reads this body's own clock for a route.</summary>
    public static EntryClock ForSelf(GenericActorContext context, StanceRoute route)
    {
        foreach (var clock in context.Self.RouteCooldowns)
        {
            if (string.Equals(
                    clock.TransitionId,
                    route.Entry.TransitionId,
                    StringComparison.Ordinal))
            {
                // The route accepts a request the first tick at or after the
                // published one; it never publishes a window already lapsed, but
                // reading the comparison rather than the presence is what makes
                // the tempo rule below able to count the ticks that remain.
                return new EntryClock(context.Tick < clock.ReadyAtTick, clock.ReadyAtTick);
            }
        }
        return Open;
    }

    /// <summary>Reads an ally's clock for the same route of its own slot.</summary>
    public static EntryClock ForAlly(
        GenericActorContext context,
        GenericActorContext.ObservedAllyState ally,
        StanceRoute route)
    {
        foreach (var clock in ally.RouteCooldowns)
        {
            if (string.Equals(
                    clock.TransitionId,
                    route.Entry.TransitionId,
                    StringComparison.Ordinal))
            {
                return new EntryClock(context.Tick < clock.ReadyAtTick, clock.ReadyAtTick);
            }
        }
        return Open;
    }

    /// <summary>
    /// Ticks until the route accepts a request again; zero when it already
    /// does, which is the same answer as "no clock is running", because both
    /// mean the same thing to a caller deciding whether to keep the gun cold.
    /// </summary>
    public int TicksUntilOpen(int tick) =>
        !Live ? 0 : Math.Max(0, ReadyAtTick - tick);
}
