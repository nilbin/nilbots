namespace BotArena.Engine;

/// <summary>
/// The redacted hearing model (RULES-0.5-DESIGN §A, hardened per §H item 1): loud events
/// beyond sight arrive as sounds — event type, a coarse 8-way bearing, a coarse distance
/// band — never exact coordinates. Enough to investigate, evade, or be decoyed; not
/// enough to aim. Sighted events are always delivered in full instead.
/// </summary>
public static class Hearing
{
    /// <summary>Chebyshev upper bound of the Near band.</summary>
    public const int NearMax = 2;

    /// <summary>Chebyshev upper bound of the Medium band; beyond it (up to the rules'
    /// hearing radius) is Far.</summary>
    public const int MediumMax = 5;

    /// <summary>8-way bearing octant from listener to source: 0=N, 1=NE, 2=E, 3=SE, 4=S,
    /// 5=SW, 6=W, 7=NW (y grows south). An axis dominating the other by MORE than 2:1
    /// reads as cardinal; everything else is the diagonal between them. Callers never
    /// pass from == to — a sound on your own tile would be a sighted event.</summary>
    public static int BearingOctant(Position from, Position to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        if (ax > 2 * ay)
            return dx > 0 ? 2 : 6;
        if (ay > 2 * ax)
            return dy > 0 ? 4 : 0;
        return (dx > 0, dy > 0) switch
        {
            (true, false) => 1,
            (true, true) => 3,
            (false, true) => 5,
            (false, false) => 7,
        };
    }

    /// <summary>0 = Near (≤ <see cref="NearMax"/>), 1 = Medium (≤ <see cref="MediumMax"/>),
    /// 2 = Far.</summary>
    public static int DistanceBand(int chebyshevDistance) =>
        chebyshevDistance <= NearMax ? 0 : chebyshevDistance <= MediumMax ? 1 : 2;
}
