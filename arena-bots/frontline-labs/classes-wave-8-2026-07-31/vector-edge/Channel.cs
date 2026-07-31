using BotArena.Sdk;

/// <summary>
/// What a CHANNELLED capture is, read from the contract's own capture
/// definition rather than from an arm's name.
///
/// <para>Three fields decide it, and all three are inert-absent on every
/// ruleset that does not channel — exactly like <c>ratchetHoldTicks</c> on a
/// ruleset without a ratchet — so <see cref="Read"/> returning null is a real
/// answer and every rule keyed off it disappears rather than misfiring.</para>
///
/// <list type="bullet">
/// <item><b>The stationary cap</b> says gain scales with surplus and stops.
/// It is the field that turns "does stacking help?" from a yes/no into a
/// number: the first extra body doubles the rate, the third buys nothing.</item>
/// <item><b>The erosion multiple</b> says an enemy claim comes off far faster
/// than a fresh one goes on, which is what makes taking ground BACK cheap and
/// makes a built claim worth defending rather than abandoning.</item>
/// <item><b>The interrupt</b> says a bolt into a body that is taking ground
/// is worth progress — measured in the same units as the threshold, which is
/// the only reason it belongs in a positional score at all.</item>
/// </list>
/// </summary>
internal sealed class ChannelRules
{
    private ChannelRules(
        int threshold,
        int gain,
        int stationaryCap,
        int erosionMultiplier,
        int revertPerDamagePoint,
        bool revertsWholeRun)
    {
        Threshold = Math.Max(1, threshold);
        Gain = Math.Max(1, gain);
        StationaryCap = stationaryCap;
        ErosionMultiplier = Math.Max(1, erosionMultiplier);
        RevertPerDamagePoint = Math.Max(0, revertPerDamagePoint);
        RevertsWholeRun = revertsWholeRun;
    }

    /// <summary>Progress one capture costs.</summary>
    public int Threshold { get; }
    /// <summary>Base progress a controlling tick earns before scaling.</summary>
    public int Gain { get; }
    /// <summary>Ceiling on the stationary-surplus gain multiplier.</summary>
    public int StationaryCap { get; }
    /// <summary>How many times faster an opposing claim erodes; 1 when plain.</summary>
    public int ErosionMultiplier { get; }
    /// <summary>Progress reverted per point of health removed; 0 when none.</summary>
    public int RevertPerDamagePoint { get; }
    /// <summary>True when one contact reverts the controller's whole run.</summary>
    public bool RevertsWholeRun { get; }

    /// <summary>
    /// The channel this capture definition declares, or <see langword="null"/>
    /// when it declares none. Presence is read off the two fields the
    /// canonical contract omits unless the mechanic exists; the policy string
    /// is confirmation, never the test, because a policy ID is prose and a
    /// number is a number.
    /// </summary>
    public static ChannelRules? Read(
        GenericActorRulesContract.FrontlineCapture? capture)
    {
        if (capture is null)
            return null;
        bool channels = capture.StationaryGainMultiplierCap > 0
            || capture.OpposingErosionMultiplier > 0
            || capture.ClaimInterrupt is not null;
        if (!channels)
            return null;
        GenericActorRulesContract.FrontlineClaimInterrupt? interrupt =
            capture.ClaimInterrupt;
        return new ChannelRules(
            capture.Threshold,
            capture.GainPerSoleTeamTick,
            capture.StationaryGainMultiplierCap > 0
                ? capture.StationaryGainMultiplierCap
                : int.MaxValue,
            capture.OpposingErosionMultiplier,
            interrupt?.RevertPerDamagePoint ?? 0,
            interrupt?.Granularity.Contains(
                "whole-run",
                StringComparison.Ordinal) ?? false);
    }

    /// <summary>
    /// Progress a tick of control earns for a claim weight against an enemy
    /// denial weight, under the declared cap.
    /// </summary>
    public int GainFor(int claimWeight, int denialWeight)
    {
        int surplus = claimWeight - denialWeight;
        if (surplus <= 0)
            return 0;
        return Math.Min(StationaryCap, surplus) * Gain;
    }
}
