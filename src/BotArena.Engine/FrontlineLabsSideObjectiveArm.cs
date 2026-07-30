namespace BotArena.Engine;

/// <summary>
/// Registered side-objective levels
/// (<c>docs/DESIGN-SIDE-OBJECTIVES-2026-07-30.md</c> §7). A side objective
/// opens the map and adds an allocation decision without paying the victory
/// currency: Frontline declares exactly one score channel and ranks timeouts
/// by exactly it, so anything that paid territorial progress off the front
/// would be a way to win a match by refusing to fight for it.
/// </summary>
public enum FrontlineLabsSideObjectiveArm
{
    /// <summary>Today's contract: no side objective at all. Not an arm —
    /// selecting it changes nothing, declares no capability, and writes no
    /// contract bytes.</summary>
    None = 0,

    /// <summary>
    /// MUSTER, the rally flag. A capturable site in the map's dead centre
    /// lanes whose owner's PRIME automatic returns land on the forward rally
    /// placement instead of at home; the loser of the flag walks home on
    /// every death. It also takes the unconditional forward rally away from
    /// both teams — the keel's free gift becomes the thing they are fighting
    /// over — and it changes the map for everyone, so it is a real arm on
    /// every pair rather than an inert-omitted one.
    /// </summary>
    Muster,
}
