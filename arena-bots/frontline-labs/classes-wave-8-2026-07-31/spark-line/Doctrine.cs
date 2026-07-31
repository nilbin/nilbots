/// <summary>
/// The wave-8 pass, one switch per rule.
///
/// <para>Every switch is on in the shipped artifact. They exist so the pass can
/// be attributed the only way a composed doctrine honestly can be — LEAVE ONE
/// OUT, with the other eight standing — because wave 6 already established that
/// this policy's rules do not decompose: several of them are inert or a loss in
/// isolation and only mean anything inside the composition. A variant is built
/// by flipping exactly one of these to false and rebuilding; nothing else in
/// the tree changes, so the artifact hash difference is the rule.</para>
///
/// <para>They are properties rather than constants on purpose: a
/// <c>const false</c> makes every guarded block unreachable code, and the
/// controlled builder is entitled to treat that as an error.</para>
/// </summary>
internal static class Doctrine
{
    /// <summary>Weight target is enemy denial plus the declared stationary cap.</summary>
    internal static bool ChannelWeight => true;

    /// <summary>Stillness is a purchase: a paid channel refuses every optional step.</summary>
    internal static bool Stillness => true;

    /// <summary>An unpaid body on the point walks the region instead of leaving it.</summary>
    internal static bool WalkThePoint => true;

    /// <summary>Tiles a visible gun one-shots this body on are ground, not risk.</summary>
    internal static bool LethalLanes => true;

    /// <summary>A body one contact kills does not hold the contested ground alone.</summary>
    internal static bool Standoff => true;

    /// <summary>The ladder is bought, health first, by one elected caster.</summary>
    internal static bool Invest => true;

    /// <summary>Loose scrap is collected by whoever the front misses least.</summary>
    internal static bool Supply => true;

    /// <summary>A surplus body stands on the bolt, not merely near the point.</summary>
    internal static bool Screens => true;
}
