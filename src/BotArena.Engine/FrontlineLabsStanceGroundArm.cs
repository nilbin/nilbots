namespace BotArena.Engine;

/// <summary>
/// Registered stance-ground levels (DECISIONS #171 tuning, round 3). The
/// map's <c>anchor-forbidden</c> tag — 112 of 233 open tiles: every
/// objective tile plus the central corridor — was built to price turret
/// anchors, and the skill stances inherited its tag kind wholesale, so the
/// shell can never rise on ground it holds and the volley is castable on
/// ~6% of a standoff striker's ticks (five wave-4 authors, quantified).
/// This arm frees exactly the SKILL stance entries; turret anchor routes
/// keep the tag — the weight-zero fortress-on-point question stays closed.
/// A finer level allowing objectives but not the corridor is deferred: the
/// placement gate is tag-KIND scoped and one kind covers both tile sets,
/// so that split is a map-format question, not a route-data one.
/// </summary>
public enum FrontlineLabsStanceGroundArm
{
    /// <summary>
    /// Today's rule: stance entries are forbidden wherever the tag kind
    /// stands. Not an arm — selecting it changes nothing.
    /// </summary>
    Strict = 0,

    /// <summary>
    /// Volley and shell entry routes drop the forbidden tag kind: a stance
    /// can rise on objective tiles and in the corridor. Weight-1 stances
    /// holding ground they protect, and the fan castable where the
    /// multi-body traffic is.
    /// </summary>
    Free,
}
