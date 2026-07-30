namespace BotArena.Engine;

/// <summary>
/// Typed additive modifiers one mode currently applies to one body's declared
/// form stats. This is the alternative to pre-declaring an upgraded form for
/// every reachable combination: three tracks at two tiers each would multiply
/// the resolved form catalog by twenty-seven, which is a contract-size
/// disaster, so the modifier is resolved at the point of use instead and both
/// operands stay published — the form catalog's declared number in the
/// contract, the tier vector in the observation.
/// <para>Every mode that owns no such store returns
/// <see cref="None"/>, and every historical contract therefore behaves exactly
/// as it always has.</para>
/// </summary>
/// <param name="AttackTravelTilesDelta">
/// Extra tiles this body's bolts travel.
/// </param>
/// <param name="VisionRangeDelta">Extra tiles this body can see.</param>
/// <param name="MaxHealthDelta">
/// Extra maximum health a life of this slot SPAWNS with. It never heals a
/// standing body: current health is untouched by a purchase, so buying is
/// never a rescue.
/// </param>
public readonly record struct GenericActorModeStatModifiers(
    int AttackTravelTilesDelta,
    int VisionRangeDelta,
    int MaxHealthDelta)
{
    /// <summary>The inert default: the declared form stats, unchanged.</summary>
    public static GenericActorModeStatModifiers None { get; } =
        new(0, 0, 0);

    /// <summary>True when this modifier changes nothing at all.</summary>
    public bool IsNone =>
        AttackTravelTilesDelta == 0
        && VisionRangeDelta == 0
        && MaxHealthDelta == 0;
}
