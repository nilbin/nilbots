namespace BotArena.Engine;

/// <summary>
/// Registered capture-mechanism levels
/// (<c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> parts 2–3, DECISIONS
/// #187). The arm exists because the channel is a capture-CORE change: it
/// re-prices every pacing gate, every class edge, and the whole pendulum
/// campaign, so it ships as its own measurable factor rather than buried
/// inside something else.
/// </summary>
public enum FrontlineLabsCaptureArm
{
    /// <summary>
    /// Today's capture: whoever has the presence the declared control policy
    /// asks for gains, moving or not, and nothing a body takes on the point
    /// costs its team any progress. Not an arm — selecting it changes
    /// nothing and writes no contract bytes.
    /// </summary>
    Frozen = 0,

    /// <summary>
    /// The CAPTURE CHANNEL. Gain counts only bodies that held their tile this
    /// tick (denial still counts all of them), the gain multiplier is capped
    /// so extra channelers buy screens rather than speed, an opposing claim
    /// erodes at a multiple of build speed so retaking ground costs
    /// 1.0–1.125× a fresh capture, and hostile damage to a controlling body ON
    /// the objective reverts the controller's work on the current run. The
    /// paired speed factor moves the threshold from 15 to 8, because each
    /// channeling tick is now risky and must pay more.
    /// <para>Version 1.1 (owner ruling on the #188 read, "recapture needs to
    /// be faster") raises the opposing-erosion multiplier 4 → 8: a maximal
    /// standing claim now falls to one controlling tick, so a full flip is 9
    /// ticks rather than 10. Behaviour moved on a MEASURED arm, so the
    /// identity re-mints — <c>siege</c>/<c>sap</c>/<c>mantlet</c> keep meaning
    /// the wave-8 bytes and this version spells
    /// <c>storm</c>/<c>mine</c>/<c>pavise</c>, exactly as <c>surf</c> re-minted
    /// to <c>swell</c> when the fan's entry sharpened.</para>
    /// <para>It is a real arm on every pair — it changes capture for both
    /// teams whatever chassis they are — so it is never inert-omitted the way
    /// a class-scoped skill tuning is. It publishes no new observation fact:
    /// every rule above moves <c>captureProgress</c> and
    /// <c>claimingTeamId</c> in their exact published shape.</para>
    /// </summary>
    Channel,
}
