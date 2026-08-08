namespace BotArena.Engine;

/// <summary>
/// The pre-registered structural counterweights to the mean-reverting
/// frontline pendulum measured in the wave-2 corpus (DECISIONS #158;
/// <c>docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md</c>). Each flag is one
/// independent rules-side factor; they compose, and
/// <see cref="None"/> is the measured baseline rather than an arm.
/// </summary>
[Flags]
public enum FrontlineLabsPendulumArm
{
    /// <summary>Today's contract: no counterweight, no ruleset suffix.</summary>
    None = 0,

    /// <summary>
    /// S1 — territory ratchet. A completed advance holds its position against
    /// enemy regression for a configured number of ticks, so a capture cannot
    /// be undone by the reinforcement wave it triggered.
    /// </summary>
    StickyFrontline = 1,

    /// <summary>
    /// S2 — forward rally. Automatic returns and activations arrive at the
    /// own-side objective beside the fight instead of at the home spawn, so
    /// reinforcement transit stops rewarding the losing side. S1's mandatory
    /// companion in the pre-registration.
    /// </summary>
    ForwardRally = 2,

    /// <summary>
    /// S3 — contest costs something. Objective control resolves on the net
    /// positive objective-weight difference, so a lone body no longer nulls
    /// a larger committed force for free.
    /// </summary>
    ContestMajority = 4,

    /// <summary>
    /// N2 — decay only under enemy sole presence. Empty and contested ticks
    /// stop destroying capture progress; only an enemy standing alone on the
    /// objective erodes a claim. The cheapest partial ratchet.
    /// </summary>
    EnemySoleDecay = 8,
}
