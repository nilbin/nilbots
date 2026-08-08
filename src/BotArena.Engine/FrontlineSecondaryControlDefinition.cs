using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One closed typed capability for a Frontline SIDE objective: a capturable
/// site that is not part of the frontline chain, resolved by the same
/// objective weight the front uses, latched to an owner, and paying in
/// something other than the victory currency.
/// <para>
/// The shape is deliberately the shared skeleton of a whole family
/// (<c>docs/DESIGN-SIDE-OBJECTIVES-2026-07-30.md</c> §3): a typed region
/// reference, a latch, and one tagged <see cref="Effect"/>. MUSTER is the
/// first effect; BATTERY, KILN and the rest arrive as additional
/// <see cref="SecondaryEffectKind"/> values rather than as new definitions,
/// so the bot, replay, result, and competition envelopes never move again.
/// </para>
/// <para>
/// Presence is measured in objective weight, never in bodies — an anchored
/// turret declares weight zero and therefore cannot hold a side point, which
/// is the one rule the whole class slate rests on. The site never pays
/// <c>TerritorialProgress</c>: Frontline declares exactly one score channel
/// and ranks timeouts by exactly it, so a side objective that paid score
/// would be a way to win without contesting the front.
/// </para>
/// </summary>
public sealed record FrontlineSecondaryControlDefinition
{
    /// <summary>What owning the site does. The tagged part of the shared
    /// capability: every future side objective is one more value here plus
    /// one branch in the mode driver.</summary>
    public enum SecondaryEffectKind
    {
        /// <summary>
        /// MUSTER, the rally flag. While a team owns the site, that team's
        /// automatic arrivals inside <see cref="RallyScope"/> land on the
        /// forward rally placement — the rear-most free tile of its own-side
        /// chain-adjacent objective, measured along its own advance
        /// direction — instead of on the assigned home spawn. Without
        /// ownership every arrival is a home arrival. Nothing about capture,
        /// decay, score, or breach changes.
        /// </summary>
        Muster = 0,
    }

    /// <summary>How ownership is won and how long it survives.</summary>
    public enum SecondaryOwnershipKind
    {
        /// <summary>
        /// A team claims the site by holding it with SOLE positive objective
        /// weight for <see cref="CaptureThresholdTicks"/> consecutive ticks;
        /// any empty or contested tick resets the running claim to zero. The
        /// completed claim latches: the owner keeps the site — including
        /// while it is empty, and including while the enemy stands on it —
        /// until the other team completes a claim of its own. A
        /// decays-to-neutral level is the registered alternative and would be
        /// a second value here.
        /// </summary>
        LatchedUntilRecapturedBySoleObjectiveWeight = 0,
    }

    /// <summary>Which of the owning team's automatic arrivals the MUSTER
    /// effect moves.</summary>
    public enum SecondaryRallyScopeKind
    {
        /// <summary>
        /// Only the Prime's automatic return — the one respawn clock every
        /// class shares and the only one a three-body team pays on every
        /// death. Companion activations, fabricated children, and replicas
        /// keep their declared placement, so a four-slot team gains no more
        /// from the flag than a three-slot team does.
        /// </summary>
        PrimeAutomaticReturnOnly = 0,
    }

    /// <summary>Creates the capability declaration.</summary>
    /// <param name="regionIds">
    /// The site's map regions, each <see
    /// cref="ActorMapRegionDefinition.RegionKind.Objective"/> and none of them
    /// bound into <see
    /// cref="FrontlineActorModeMapBindingDefinition.OrderedObjectiveRegionIds"/>.
    /// More than one region is one site with more than one place to stand,
    /// not more than one flag: presence is summed across the union.
    /// </param>
    /// <param name="captureThresholdTicks">
    /// Consecutive sole-presence ticks a claim needs. Positive.
    /// </param>
    /// <param name="ownership">How the latch behaves.</param>
    /// <param name="effect">What owning the site does.</param>
    /// <param name="rallyScope">
    /// Which automatic arrivals <see cref="SecondaryEffectKind.Muster"/>
    /// moves. Carried by exactly the effects that rally, the same way <see
    /// cref="FrontlineCaptureDefinition.RatchetHoldTicks"/> is carried by
    /// exactly the one redeploy policy that holds a high-water mark.
    /// </param>
    public FrontlineSecondaryControlDefinition(
        IReadOnlyList<string> regionIds,
        int captureThresholdTicks,
        SecondaryOwnershipKind ownership,
        SecondaryEffectKind effect,
        SecondaryRallyScopeKind rallyScope)
    {
        ArgumentNullException.ThrowIfNull(regionIds);
        string[] ids = [.. regionIds];
        if (ids.Length == 0 || ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "A Frontline secondary control must name at least one "
                + "non-blank site region.",
                nameof(regionIds));
        }
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            throw new ArgumentException(
                "Frontline secondary-control site region IDs must be unique.",
                nameof(regionIds));
        }
        if (captureThresholdTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(captureThresholdTicks),
                captureThresholdTicks,
                "A secondary-control latch needs a positive tick threshold.");
        }
        if (!Enum.IsDefined(ownership))
            throw new ArgumentOutOfRangeException(nameof(ownership));
        if (!Enum.IsDefined(effect))
            throw new ArgumentOutOfRangeException(nameof(effect));
        if (!Enum.IsDefined(rallyScope))
            throw new ArgumentOutOfRangeException(nameof(rallyScope));

        // Declared order, deliberately NOT sorted: the site regions are a
        // sequence a bot reads positionally out of the contract, exactly like
        // the objective chain.
        RegionIds = ids.ToImmutableArray();
        CaptureThresholdTicks = captureThresholdTicks;
        Ownership = ownership;
        Effect = effect;
        RallyScope = rallyScope;
    }

    /// <summary>The site's map regions, in declared order.</summary>
    public ImmutableArray<string> RegionIds { get; }

    /// <summary>Consecutive sole-presence ticks one claim needs.</summary>
    public int CaptureThresholdTicks { get; }

    /// <summary>How the latch behaves.</summary>
    public SecondaryOwnershipKind Ownership { get; }

    /// <summary>What owning the site does.</summary>
    public SecondaryEffectKind Effect { get; }

    /// <summary>Which automatic arrivals the MUSTER effect moves.</summary>
    public SecondaryRallyScopeKind RallyScope { get; }
}
