using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// The single projection from the kernel's private objective state to the
/// public Frontline mode observation. Both the live mode driver and the
/// replay-v3 chronology validator go through here, which is what makes the
/// validator's re-derivation an actual check rather than a second opinion: a
/// forged, mis-owned, or mis-expiring hold in a recorded observation cannot
/// match a projection computed from the authoritative kernel state.
/// </summary>
internal static class FrontlineControlProjection
{
    /// <param name="modeId">The declared mode identity.</param>
    /// <param name="control">Authoritative objective state.</param>
    /// <param name="scrap">
    /// Authoritative economy state and the kernel that reads it, or null on
    /// every ruleset that declares no economy — in which case both scrap
    /// facts stay empty for the whole match, which is their inert default.
    /// </param>
    public static GenericActorRuntimeObservation.ModeObservationState.Frontline
        Project(
            string modeId,
            FrontlineControlState control,
            (FrontlineScrapKernel Kernel, FrontlineScrapState State)? scrap =
                null)
    {
        // The hold is published only while it is LIVE. The kernel keeps the
        // last protected advance on the state after it lapses (a later advance
        // replaces it), and denies regression while the acting tick is at or
        // below HoldsThroughTick — so the observed clock is that tick plus
        // one, read with the same grammar as ControlResumesAtTick: the tick
        // the restriction lifts. A ruleset with no ratchet never sets a hold
        // at all, so both fields stay null for its whole match.
        bool live = control.RatchetHold is { } hold
            && control.NextTick <= hold.HoldsThroughTick;
        var mode = new GenericActorRuntimeObservation.ModeObservationState
            .Frontline(
                modeId,
                control.ActivePositionIndex,
                control.ClaimingTeamId,
                control.CaptureProgress,
                control.DecayTicksElapsed,
                control.ControlResumesAtTick,
                live ? control.RatchetHold!.TeamId : null,
                live ? control.RatchetHold!.HoldsThroughTick + 1 : null,
                control.SecondaryControl?.OwnerTeamId,
                SecondaryClaimProgress(control.SecondaryControl));
        if (scrap is not { } economy)
            return mode;

        (ImmutableArray<GenericActorRuntimeObservation.ScrapTeamState> teams,
                ImmutableArray<GenericActorRuntimeObservation.ScrapPile> piles)
            = economy.Kernel.Project(economy.State);
        return mode with
        {
            ScrapTeams = teams,
            ScrapPiles = piles,
        };
    }

    /// <summary>
    /// The side objective's running claim as one signed number: the
    /// accumulated sole-presence ticks, positive for team 0 and negative for
    /// team 1 — the same direction the public team-advance ordering uses, so
    /// the sign names the claimant without spending a second published fact
    /// on it. Zero means no claim stands, including on every ruleset that
    /// declares no side objective at all.
    /// </summary>
    private static int SecondaryClaimProgress(
        FrontlineSecondaryControlState? secondary) =>
        secondary?.ClaimingTeamId switch
        {
            null => 0,
            0 => secondary!.ClaimTicks,
            _ => -secondary!.ClaimTicks,
        };
}
