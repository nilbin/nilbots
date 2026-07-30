namespace BotArena.Engine;

/// <summary>
/// Registered tuning variants of FIVE SLOTS (DECISIONS #171). Phase 2
/// measured the kit's counter-cycle failing in one direction — the
/// fabricator dominant against both classes on both seed sets — and the
/// pair structure attributed the overshoot to this skill. Each variant
/// moves exactly one lever, per the ablation discipline; the volley and
/// shell are untouched. Breach forensics behind the choices: against
/// strikers the fabricator's wins land at median tick ~217–253 (before
/// the fourth slot exists), against bulwarks at ~350–376 (the fourth
/// slot's window), and the fifth slot is nearly a bystander everywhere —
/// yet a striker that never casts still fell from even to −1.00 when the
/// late slots arrived, so the levers are the late-slot schedule and the
/// rebuild economy, not the fan.
/// </summary>
public enum FrontlineLabsFiveSlotVariant
{
    /// <summary>
    /// The phase-2 measured arm: extra slots at 300/420 on the class's own
    /// 120-tick cadence, rebuilding on the 30-tick baseline clock. Not a
    /// tuning arm — selecting it changes nothing about the ruleset.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Four slots: the fifth slot is dropped and the fourth keeps its
    /// 300 unlock. The slate's originally named knob, registered even
    /// though the forensics predict it is the weakest lever — the fifth
    /// body fielded for at most 80 ticks of a 500-tick match.
    /// </summary>
    Trim,

    /// <summary>
    /// The whole extra schedule swings late: 360/480 instead of 300/420,
    /// keeping the class's 120-tick cadence. The fourth slot now arrives
    /// after the measured vs-bulwark decisive window (~350–376) and the
    /// fifth is a 20-tick token.
    /// </summary>
    Boom,

    /// <summary>
    /// Count costs tempo: the fabricator's ordinary children rebuild on
    /// the 30-tick baseline clock instead of the class's native 15 while
    /// this skill is active (the extra slots already rebuild at 30). The
    /// schedule is untouched; what changes is that farming fabricator
    /// bodies stays farmed — the counter-play the volley was built for.
    /// </summary>
    Drag,
}
