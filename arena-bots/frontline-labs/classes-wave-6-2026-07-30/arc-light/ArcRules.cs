/// <summary>
/// The wave-6 coordination rules, each behind one switch so that removing
/// exactly one of them is a one-line edit and therefore a measurable ablation.
///
/// <para>Every one of these is a rule about arc-light's OWN bodies. Wave 5
/// priced enemies well and siblings not at all: it read an enemy's gun envelope,
/// an enemy's fan lanes and an enemy's reachable ray, and then walked two of its
/// own bodies into the same tile on the same tick, twenty-eight times a match,
/// and re-walked them into it twenty-four of those times. The switches below are
/// the five places that gap was closed.</para>
///
/// <para>They are <c>static readonly</c> rather than <c>const</c> on purpose: a
/// const would fold at compile time and make an ablation build differ by dead
/// code elimination as well as by behaviour.</para>
/// </summary>
internal static class ArcRules
{
    /// <summary>
    /// C1 — YIELD PRECEDENCE. No body of mine steps onto, or keeps standing on,
    /// a tile a higher-precedence sibling's committed route needs this tick.
    /// Precedence is a total order computed from the shared observation, so
    /// every independent life derives the same answer without shared state:
    /// (1) a body frozen in a transition windup outranks everything, because it
    /// cannot move and disputing its tile is pointless; (2) a body already
    /// inside a 1-tile corridor outranks a body outside one; (3) the shorter
    /// remaining route to the active objective outranks the longer; (4) lower
    /// <c>(unitId, lifeId)</c> breaks the remaining ties.
    /// </summary>
    public static readonly bool YieldPrecedence = true;

    /// <summary>
    /// C2 — CHOKE PRECEDENCE. At most one of my bodies occupies or claims one
    /// 1-tile corridor run at a time, and the body already inside has
    /// precedence: it leaves before another enters. A choke tile is never a
    /// destination in its own right — never a goal, never a cast tile, never a
    /// fortify tile — so no body of mine ever stops in a doorway.
    /// </summary>
    public static readonly bool ChokePrecedence = true;

    /// <summary>
    /// C3 — RALLY AND FABRICATION TRAFFIC. Where the contract lets my own
    /// bodies influence where a new body appears, do not stand in the way. Two
    /// mechanisms, both read rather than assumed: a published
    /// <c>LifecyclePending.ReservedPosition</c> is a tile a sibling is already
    /// committed to, and a forward rally fills the own-side objective region
    /// rear-most-first along this team's advance direction, so the tile the next
    /// arrival will take is derivable from the slot clocks in
    /// <c>TeamUnits</c> and vacated before the clock runs out.
    /// </summary>
    public static readonly bool RallyTraffic = true;

    /// <summary>
    /// C4 — SPACING. Do not put two of my bodies where one enemy volley fan, or
    /// one deflection return, covers both, when an equal-value adjacent pose
    /// exists. A fan's break-even is a body count; standing in pairs is paying
    /// the opposition's price for it.
    /// </summary>
    public static readonly bool FanSpacing = true;

    /// <summary>
    /// C5a — THE CAST DOES NOT SEAL A DOORWAY. A stance is immobile for its
    /// whole declared cycle, so entering one converts my body into terrain for
    /// that many ticks; on a 1-tile corridor that terrain is a closed
    /// reinforcement route. This is arc-light's own named gap, and it is
    /// separated from C5b below because the two halves measured differently.
    /// </summary>
    public static readonly bool CastPricesOwnPaths = true;

    /// <summary>
    /// C5b — SHIPPED OFF, and this is the wave's one negative result kept in the
    /// source so it stays auditable rather than becoming folklore.
    ///
    /// <para>The broader form of C5a: refuse a cast on any tile a sibling's
    /// committed route needs inside the stance cycle, on a published lifecycle
    /// reservation, and on the tile the next arrival is due to take. It reads
    /// like the obvious generalisation and it measured as a straight loss —
    /// 32 seeds on the swarm leg, one flag apart: <b>20-11-1 / +6.47</b> with
    /// C5a alone against <b>18-13-1 / +3.47</b> with C5a and C5b together, and
    /// C5a alone already drives choke casts to exactly zero, so C5b bought no
    /// silliness reduction to trade for the two games.</para>
    ///
    /// <para>Why, in one line: a cast is rare (0.5 per match) and this half
    /// refuses tiles that merely LIE ON a sibling's two-tile ray, which on a
    /// four-tile objective is most of the objective. C5a refuses tiles that ARE
    /// the route — a doorway — and that is the distinction the measurement
    /// found. The brief's rule is to keep only what wins or removes visible
    /// silliness without losing, and this half did neither.</para>
    /// </summary>
    public static readonly bool CastYieldsSiblingRoutes = false;
}
