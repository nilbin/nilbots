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

    // ---------------------------------------------------------------- wave 7
    //
    // The fan was re-armed. Every one of the five switches below is a
    // consequence of a number that MOVED in the contract, and every one is read
    // from the contract rather than written down: the fan bolt's damage, the
    // entry windup, the fan profile's cooldown, and the entry route's own
    // cooldown clock. Wave 6 shipped a cast rule calibrated against a fan that
    // cost four immobile ticks, taxed the gun on the way out, and hit for the
    // same damage as the gun it replaced. None of that is true any more, and a
    // doctrine that keeps the old arithmetic simply stops casting — which is
    // exactly what the wave-6 artifact does on this arm.

    /// <summary>
    /// V1 — THE BREAK-EVEN IS PRICED IN DAMAGE, NOT IN BODIES. The stance costs
    /// a whole declared cycle; the ordinary gun would have fired
    /// <c>ceil(cycle / cadence)</c> bolts in that window, each worth its own
    /// declared <c>damagePerHit</c>. So the honest question is not "does the fan
    /// touch as many bodies as the gun would have fired bolts" but "does the fan
    /// deal at least as much damage as the gun would have dealt". A fan bolt
    /// that hits twice as hard needs half as many bodies, and the arithmetic
    /// says so without naming an arm: on the wave-6 contract this returns the
    /// wave-6 answer (two), and on the re-armed one it returns ONE.
    /// </summary>
    public static readonly bool FanPricedInDamage = true;

    /// <summary>
    /// V2 — THE FAN EXECUTES. A fan bolt removes <c>damagePerHit</c> health in
    /// one contact, so any body whose current health is at or below that number
    /// dies to a single bolt — and the fan is the only weapon in the contract
    /// that can present that bolt to three bearings at once. A forecast body the
    /// fan KILLS is credited as such, and a cast that kills is the one case
    /// allowed to pre-empt an available aimed bolt, because the aimed bolt at
    /// the same tick removes a body's health and the cast removes the body.
    /// </summary>
    public static readonly bool FanExecutes = true;

    /// <summary>
    /// V3 — THE ENTRY IS A CHARGE, AND THE CLOCK IS PUBLISHED. Frequency is
    /// priced on the entry route, not on the shot: the route declares a
    /// cooldown, the clock is scoped to the unit slot, it survives the body, and
    /// it is published on <c>self.routeCooldowns</c>. Three consequences, all
    /// one rule: never request an entry the published clock holds shut; never
    /// spend a rotation buying a bearing for it; and never spend a charge that
    /// cannot be fired — the gun must be able to fire on the first stance tick,
    /// and a stance that has been entered fires before it returns, because
    /// leaving unfired costs the identical exit windup and the whole clock.
    /// </summary>
    public static readonly bool EntryClockIsACharge = true;

    /// <summary>
    /// V4 — THE FAN DOES NOT FEED A SHELL. A guarding form deflects contacts
    /// arriving inside its facing quadrant and launches the bolt back along the
    /// exact reverse heading, carrying the damage class of the bolt that was
    /// returned. A fan bolt is now the hardest-hitting projectile this doctrine
    /// owns, so a fan cast into a shell's face is a two-damage bolt fired at a
    /// three-health chassis by the opposition, for free. A body that would
    /// deflect is not a forecast hit, and a denial lane stops at one.
    /// </summary>
    public static readonly bool FanRespectsGuards = true;

    /// <summary>
    /// V5 — SHIPPED OFF. This wave's negative result, kept in the source with
    /// its numbers so it stays auditable rather than becoming folklore.
    ///
    /// <para>The idea: wave 6 refused a cast whenever loaded enemy guns
    /// outnumbered the surplus over break-even, a rule calibrated against a
    /// two-tick entry inside a four-tick immobile cycle — the game's slowest
    /// public telegraph. The entry is one tick now, the immobile exposure is the
    /// entry plus the cast, and what the body can afford looks like a question
    /// about its HEALTH against the hardest bolt visible rather than a constant.
    /// So: one survivable contact buys one bearing of tolerance.</para>
    ///
    /// <para>It measured as a straight loss, one flag apart, 16 seeds a leg.
    /// Against the sibling striker <c>still-water</c>: <b>16-0-0 / +30.00 in 157
    /// ticks</b> with the tolerance OFF against <b>13-3-0 / +19.00 in 383</b>
    /// with it on. In the mirror against my own wave-6 predecessor: 16-0-0 /
    /// +28.44 off against 16-0-0 / +25.81 on. Against the bulwark
    /// <c>march-wall</c> it is the one leg that prefers it, and barely: 3-9-4 /
    /// −8.50 on against 3-13-0 / −9.12 off — the same three wins, four draws
    /// turned into losses, and 0.62 of progress.</para>
    ///
    /// <para>Why, in one line: a bearing is not a bolt, but a stance still
    /// cannot dodge, and the shorter telegraph shortened the window the
    /// opposition needs rather than the window it gets. The health term prices
    /// SURVIVING the contact; what the cast actually loses when it eats one is
    /// the exchange, and a striker that trades a body for a body has spent the
    /// scarcer thing. The surplus rule wave 6 wrote is still the right rule, and
    /// it is right for a reason that did not move.</para>
    /// </summary>
    public static readonly bool EntryBearingBudget = false;
}
