using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Every dump-then-tune number the MUSTER arm ships with, in one place.
/// None of them is sacred: each is a single lever registered as its own
/// unattributed factor in <c>balance/frontline-ablation-debt-v1.json</c>, so
/// a measured muster effect is not attributable to any one of them until the
/// registered isolations are run.
/// </summary>
public static class FrontlineLabsMusterSite
{
    /// <summary>
    /// Consecutive SOLE-presence ticks one claim needs. Twelve is a bit under
    /// one Prime respawn (18) and a bit under one front capture (15): long
    /// enough that a body has to commit and short enough that one errand can
    /// pay for itself.
    /// <para>
    /// Ablation debt: <c>muster-latch-duration</c>. Threshold, the
    /// reset-on-interruption rule, and the latch-until-recaptured ownership
    /// are three levers inside one number and are bundled here.
    /// </para>
    /// </summary>
    public const int LatchTicks = 12;

    /// <summary>
    /// The north site: the widened centre-column alcove's two tiles. Both
    /// carry at least two approach headings on the muster map, which is what
    /// stops an AEGIS SHELL in a 1-wide cul-de-sac from being an unflankable
    /// holder.
    /// <para>
    /// Ablation debt: <c>muster-site-placement</c>. Distance from the front,
    /// tile count, approach count, and the widening edit that buys them are
    /// one bundled factor.
    /// </para>
    /// </summary>
    public const string NorthRegionId = "muster-site-north";

    /// <summary>The south site, the exact y-mirror of the north one.</summary>
    public const string SouthRegionId = "muster-site-south";

    /// <summary>North site tiles: the alcove mouth and its depth.</summary>
    public static ImmutableArray<(int X, int Y)> NorthTiles { get; } =
        [(11, 2), (11, 3)];

    /// <summary>South site tiles, mirrored across the map's centre row.</summary>
    public static ImmutableArray<(int X, int Y)> SouthTiles { get; } =
        [(11, 11), (11, 12)];

    /// <summary>
    /// The alcove shoulders the muster map opens so neither site tile is a
    /// 1-wide dead end: <c>(10,3)</c>/<c>(12,3)</c> and their mirrors. It is
    /// the map-as-tuning-surface edit the side-objective memo §2 required
    /// before any of this could be built, and it also shortens the errand
    /// from the centre objective, which is deliberately part of the same
    /// registered placement factor.
    /// </summary>
    public static ImmutableArray<(int X, int Y)> OpenedShoulders { get; } =
        [(10, 3), (12, 3), (10, 11), (12, 11)];

    /// <summary>
    /// Which arrivals the owner's flag moves. Prime-only: the 18-tick clock
    /// every class shares and the one a fabricator's fourth body does not
    /// use, so the reward stays flat per team.
    /// <para>
    /// Ablation debt: <c>muster-rally-scope</c>. A wider scope (children,
    /// fabricated arrivals) is a separate registered level.
    /// </para>
    /// </summary>
    public const FrontlineSecondaryControlDefinition.SecondaryRallyScopeKind
        RallyScope = FrontlineSecondaryControlDefinition
            .SecondaryRallyScopeKind.PrimeAutomaticReturnOnly;

    /// <summary>
    /// The ruleset-identity token. Registered so a muster candidate is a
    /// content-identified arm rather than a re-tuned baseline.
    /// </summary>
    public const string ArmToken = "muster";

    /// <summary>The declared capability for the MUSTER arm.</summary>
    public static FrontlineSecondaryControlDefinition Definition { get; } =
        new(
            [NorthRegionId, SouthRegionId],
            LatchTicks,
            FrontlineSecondaryControlDefinition.SecondaryOwnershipKind
                .LatchedUntilRecapturedBySoleObjectiveWeight,
            FrontlineSecondaryControlDefinition.SecondaryEffectKind.Muster,
            RallyScope);
}
