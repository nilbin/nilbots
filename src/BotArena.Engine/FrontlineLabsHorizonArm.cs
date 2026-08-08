namespace BotArena.Engine;

/// <summary>
/// Registered match-length levels (owner ruling on the wave-8 read: "longer
/// games at this point is ok"). Match length has been a fixed 500 ticks for
/// the whole campaign, and every pacing number in the game — the 18-tick Prime
/// return, the 40-tick ratchet hold, the vein cadence, the roster's tranche
/// schedule — was chosen against it, so moving it is a re-pricing of all of
/// them at once rather than a tuning knob.
/// <para>It is a limits change, which is contract data: the level is declared
/// per arm rather than globally, so every measured identity keeps its exact
/// bytes and only a cell that asks for the longer horizon gets it.</para>
/// </summary>
public enum FrontlineLabsHorizonArm
{
    /// <summary>
    /// The measured horizon: 500 ticks. Not an arm — selecting it changes
    /// nothing and writes no contract bytes.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// LONG: 750 ticks. The owner's ruling exercises the registered
    /// <c>match-horizon</c> lever, and the three mechanisms that arrived with
    /// it are the reason: home respawns make every arrival a walk, the LEGION
    /// roster's late tranche unlocks at tick 300, and the economy's deposits
    /// run from tick 60. At 500 the late roster wave was a twenty-percent
    /// coda; at 750 it is a second act with two hundred ticks to spend, and
    /// the deposit series can run to nine events instead of six.
    /// </summary>
    Long,
}
