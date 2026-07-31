using BotArena.Sdk;

/// <summary>
/// Where hostile bolts are going to be, and what arriving costs. A projectile's
/// revealed heading is evidence rather than a promise — a programmed bend stays
/// private — but four declared per-projectile facts are exact, and every one of
/// them is read here rather than assumed:
///
///  - <c>tilesPerAdvance</c> and <c>ticksUntilAdvance</c> place the next
///    advance,
///  - <c>ticksPerAdvance</c> spaces the ones after it — revision 3 assumed that
///    was one tick, which silently doubles the horizon for any slower bolt,
///  - <c>damagePerHit</c> prices the contact, so a batch is measured in health
///    rather than in bolts.
///
/// The last two are per PROJECTILE, not per profile: a fan bolt and an ordinary
/// bolt need not agree, and a bolt a shield returned carries the damage class of
/// the bolt that was sent. That is also why nothing here asks who fired: a
/// deflected bolt is an ordinary hostile projectile owned by the team that
/// turned it — including our own bolt, sent back — and the only question worth
/// asking about it is whether it is coming here.
/// </summary>
internal static class Threat
{
    /// <summary>One hostile bolt's arrival at a tile, priced and dated.</summary>
    internal sealed record Inbound(
        GenericActorContext.ObservedProjectile Projectile,
        int TicksUntilArrival,
        int Damage,
        ProjectileHeading Heading);

    /// <summary>Tiles hostile bolts currently occupy; entering one is a contact.</summary>
    public static HashSet<Position> BoltTiles(GenericActorContext context)
    {
        HashSet<Position> tiles = [];
        foreach (GenericActorContext.ObservedProjectile projectile
                 in Hostile(context))
        {
            tiles.Add(projectile.Position);
        }
        return tiles;
    }

    /// <summary>
    /// Every tile a hostile bolt traverses within the next
    /// <paramref name="ticks"/> ticks, walls, corners, cadence and travel budget
    /// included.
    /// </summary>
    public static HashSet<Position> Sweep(
        ContractView view,
        GenericActorContext context,
        int ticks)
    {
        HashSet<Position> tiles = [];
        foreach (GenericActorContext.ObservedProjectile projectile
                 in Hostile(context))
        {
            Walk(view, projectile, ticks, (tile, _) => tiles.Add(tile));
        }
        return tiles;
    }

    /// <summary>
    /// Every hostile bolt that reaches one tile within <paramref name="ticks"/>
    /// ticks, each with the exact tick it arrives, the health that contact
    /// costs, and the heading it arrives on. The heading is what a shield
    /// decides on, the tick is what a windup decides on, and the damage is what
    /// a body with one health left decides on.
    /// </summary>
    public static List<Inbound> Incoming(
        ContractView view,
        GenericActorContext context,
        Position tile,
        int ticks)
    {
        var inbound = new List<Inbound>();
        foreach (GenericActorContext.ObservedProjectile projectile
                 in Hostile(context))
        {
            if (projectile.Position == tile)
            {
                inbound.Add(
                    new Inbound(
                        projectile,
                        0,
                        projectile.DamagePerHit,
                        projectile.Heading));
                continue;
            }
            int? arrival = null;
            Walk(
                view,
                projectile,
                ticks,
                (swept, at) =>
                {
                    if (swept == tile && arrival is null)
                        arrival = at;
                });
            if (arrival is int when)
            {
                inbound.Add(
                    new Inbound(
                        projectile,
                        when,
                        projectile.DamagePerHit,
                        projectile.Heading));
            }
        }
        return inbound;
    }

    /// <summary>
    /// Health a tile loses to hostile fire inside <paramref name="ticks"/>
    /// ticks. Counting bolts instead of summing <c>damagePerHit</c> is the same
    /// mistake as counting bodies instead of objective weight: it happens to be
    /// right exactly while every value is one.
    /// </summary>
    public static int Damage(
        ContractView view,
        GenericActorContext context,
        Position tile,
        int ticks) =>
        Incoming(view, context, tile, ticks).Sum(bolt => bolt.Damage);

    /// <summary>
    /// True when a hostile bolt's whole declared remaining travel passes through
    /// this tile. The distinction from <see cref="Damage"/> matters for
    /// voluntary movement: a bolt whose budget expires two tiles short of where
    /// we stand is harmless while we stand there and lethal the moment we walk
    /// into it, and "it cannot reach me" is a statement about a tile, not about
    /// a body.
    /// </summary>
    public static bool InDeclaredPath(
        ContractView view,
        GenericActorContext context,
        Position tile)
    {
        foreach (GenericActorContext.ObservedProjectile projectile
                 in Hostile(context))
        {
            if (projectile.Position == tile)
                return true;
            bool reaches = false;
            Walk(
                view,
                projectile,
                ticks: int.MaxValue,
                (swept, _) => reaches |= swept == tile);
            if (reaches)
                return true;
        }
        return false;
    }

    private static IEnumerable<GenericActorContext.ObservedProjectile> Hostile(
        GenericActorContext context) =>
        (context.VisibleProjectiles ?? [])
            .Where(projectile =>
                projectile.OwnerTeamId != context.Self.ActorId.TeamId);

    /// <summary>
    /// Walks a bolt's declared path, reporting each tile and the tick it is
    /// swept on. Advances are placed by the projectile's own cadence: the next
    /// one is <c>ticksUntilAdvance</c> away and each later one adds
    /// <c>ticksPerAdvance</c>, so a bolt that moves every other tick is not
    /// credited with twice its reach.
    /// </summary>
    private static void Walk(
        ContractView view,
        GenericActorContext.ObservedProjectile projectile,
        int ticks,
        Action<Position, int> visit)
    {
        int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
        int cadence = Math.Max(1, projectile.TicksPerAdvance);
        int untilNext = Math.Max(1, projectile.TicksUntilAdvance);

        int advances;
        if (ticks == int.MaxValue)
        {
            advances = (projectile.RemainingTiles + perAdvance - 1) / perAdvance;
        }
        else
        {
            if (ticks < untilNext)
                return;
            advances = 1 + (ticks - untilNext) / cadence;
        }

        int budget = Math.Min(advances * perAdvance, projectile.RemainingTiles);
        (int dx, int dy) = projectile.Heading.Vector();
        Position cursor = projectile.Position;
        for (int step = 0; step < budget; step++)
        {
            Position next = cursor.Offset(dx, dy);
            if (view.IsWall(next))
                return;
            if (dx != 0
                && dy != 0
                && (view.IsWall(cursor.Offset(dx, 0))
                    || view.IsWall(cursor.Offset(0, dy))))
            {
                return;
            }
            cursor = next;
            visit(cursor, untilNext + step / perAdvance * cadence);
        }
    }
}
