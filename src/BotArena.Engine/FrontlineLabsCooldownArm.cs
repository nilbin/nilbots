namespace BotArena.Engine;

/// <summary>
/// Registered cooldown-clock levels (DECISIONS #180, the striker
/// conversation after wave 6). The gunless-stance cooldown freeze was a
/// wave-5 engine discovery, never a design ruling: a shell or windup
/// stopped time for the mobile gun, taxing exactly the skills the game
/// exists to showcase. Ticking is general for every class and form.
/// </summary>
public enum FrontlineLabsCooldownArm
{
    /// <summary>Today's clock: an unarmed form freezes the remaining
    /// cooldown. Not an arm — selecting it changes nothing.</summary>
    Frozen = 0,

    /// <summary>The cooldown advances with time in every form: a stance,
    /// windup, or turret transition no longer pauses gun recovery.
    /// Selects the advances-with-time tick-resolution clock.</summary>
    Ticking,
}
