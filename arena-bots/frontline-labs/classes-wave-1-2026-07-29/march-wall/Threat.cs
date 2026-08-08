using BotArena.Sdk;

/// <summary>
/// Where hostile bolts are going to be. A projectile's revealed heading is
/// evidence rather than a promise — a programmed bend stays private — but the
/// declared cadence, per-advance travel and remaining budget are exact, and a
/// body that only checks the tile a bolt is on today walks into the tile it
/// sweeps tonight.
/// </summary>
internal static class Threat
{
    /// <summary>Tiles hostile bolts currently occupy; entering one is a contact.</summary>
    public static HashSet<Position> BoltTiles(GenericActorContext context)
    {
        HashSet<Position> tiles = [];
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId != context.Self.ActorId.TeamId)
                tiles.Add(projectile.Position);
        }
        return tiles;
    }

    /// <summary>
    /// Every tile a hostile bolt traverses within the next
    /// <paramref name="advanceTicks"/> ticks, walls and travel budget included.
    /// </summary>
    public static HashSet<Position> Sweep(
        ContractView view,
        GenericActorContext context,
        int advanceTicks)
    {
        HashSet<Position> tiles = [];
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == context.Self.ActorId.TeamId)
                continue;
            Walk(view, projectile, advanceTicks, tiles.Add);
        }
        return tiles;
    }

    /// <summary>
    /// How many hostile bolts reach one tile within
    /// <paramref name="advanceTicks"/> ticks.
    /// </summary>
    public static int Hits(
        ContractView view,
        GenericActorContext context,
        Position tile,
        int advanceTicks)
    {
        int hits = 0;
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == context.Self.ActorId.TeamId)
                continue;
            if (projectile.Position == tile)
            {
                hits++;
                continue;
            }
            bool reaches = false;
            Walk(
                view,
                projectile,
                advanceTicks,
                swept =>
                {
                    reaches |= swept == tile;
                    return true;
                });
            if (reaches)
                hits++;
        }
        return hits;
    }

    private static void Walk(
        ContractView view,
        GenericActorContext.ObservedProjectile projectile,
        int advanceTicks,
        Func<Position, bool> visit)
    {
        int advances = advanceTicks
            - Math.Max(0, projectile.TicksUntilAdvance - 1);
        if (advances <= 0)
            return;
        int budget = Math.Min(
            advances * projectile.TilesPerAdvance,
            projectile.RemainingTiles);

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
            visit(cursor);
        }
    }
}
