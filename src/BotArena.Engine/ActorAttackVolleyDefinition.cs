namespace BotArena.Engine;

/// <summary>
/// How many projectiles one successful attack launches, and how their
/// headings relate to the resolved launch heading. Absent (null on the attack
/// profile) means the historical one-bolt attack; the canonical writer emits
/// nothing for it, so every contract authored before multi-projectile attacks
/// existed keeps its exact fingerprint (the additive-field discipline of
/// DECISIONS #156).
/// </summary>
public sealed record ActorAttackVolleyDefinition
{
    public ActorAttackVolleyDefinition(
        int projectileCount,
        VolleySpreadKind spread)
    {
        if (projectileCount < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectileCount),
                projectileCount,
                "A volley launches at least two projectiles; a single-bolt "
                + "attack omits the volley definition entirely.");
        }
        if (!Enum.IsDefined(spread))
            throw new ArgumentOutOfRangeException(nameof(spread));
        if (spread
                == VolleySpreadKind
                    .SymmetricAdjacentHeadingFanAscendingSignedSectorOffset
            && projectileCount % 2 == 0)
        {
            throw new ArgumentException(
                "A symmetric heading fan needs an odd projectile count so one "
                + "bolt travels down the resolved heading itself.",
                nameof(projectileCount));
        }

        ProjectileCount = projectileCount;
        Spread = spread;
    }

    /// <summary>Projectiles launched by one successful attack action.</summary>
    public int ProjectileCount { get; }

    public VolleySpreadKind Spread { get; }

    /// <summary>
    /// Identity assignment across one volley: the projectile IDs are issued in
    /// the same order the bolts are launched, so a replay's bolt order is a
    /// contract fact rather than an implementation detail.
    /// </summary>
    public IdentityOrderKind IdentityOrder =>
        IdentityOrderKind.ContiguousAscendingInLaunchOrder;

    /// <summary>
    /// Half-width of a symmetric fan in heading sectors: three bolts occupy
    /// offsets -1, 0, +1, five occupy -2..+2, and so on.
    /// </summary>
    public int FanHalfWidthSectors => (ProjectileCount - 1) / 2;

    public enum VolleySpreadKind
    {
        /// <summary>
        /// Every bolt travels the resolved launch heading; the volley is a
        /// simultaneous stack rather than a fan.
        /// </summary>
        SharedResolvedHeading = 0,

        /// <summary>
        /// The bolts occupy consecutive heading sectors centred on the
        /// resolved heading and are launched in ascending signed sector
        /// offset (-k first, +k last), which is also their projectile-ID
        /// order.
        /// </summary>
        SymmetricAdjacentHeadingFanAscendingSignedSectorOffset = 1,
    }

    public enum IdentityOrderKind
    {
        ContiguousAscendingInLaunchOrder = 0,
    }
}
