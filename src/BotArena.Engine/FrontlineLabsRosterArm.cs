namespace BotArena.Engine;

/// <summary>
/// Registered roster levels: how many bodies a team fields, and when they
/// arrive (owner ruling on the wave-8 read — "I want initial number of bots to
/// be higher ... should still increase as the game progresses ... so end game
/// is genuinely many bots").
/// <para>The arm exists because body COUNT has never been a measured factor of
/// this campaign. Every wave so far has been played on prime-plus-two, with
/// the fabricator's FIVE SLOTS as the only count variation, and the two
/// mechanisms adopted in #187 — the capture channel and the SCRAP economy —
/// are both explicitly priced against how many bodies a team can afford to
/// send away from the front. Changing the count is therefore a rework of the
/// whole game's allocation problem, not a tuning knob, which is exactly what a
/// registered arm is for.</para>
/// </summary>
public enum FrontlineLabsRosterArm
{
    /// <summary>
    /// The measured roster: prime plus two companions, unlocking on each
    /// class's own cadence. Not an arm — selecting it changes nothing, writes
    /// no contract bytes, and keeps every historical topology and map
    /// fingerprint exact.
    /// </summary>
    None = 0,

    /// <summary>
    /// LEGION. Every team starts with three live bodies instead of one (the
    /// fabricator with a fourth slot it must fabricate), gains two more slots
    /// at tick 150, and three more at tick 300 — an endgame roster of eight,
    /// nine for the fabricator.
    /// <para>It is a real arm on every pair: it changes what both teams field
    /// whatever chassis they are, so it is never inert-omitted. It needs its
    /// own map generation (<c>frontline-labs-03-legion</c>), because a slot
    /// that returns automatically needs a reserved spawn anchor and the
    /// measured pad has room for two.</para>
    /// </summary>
    Legion,
}
