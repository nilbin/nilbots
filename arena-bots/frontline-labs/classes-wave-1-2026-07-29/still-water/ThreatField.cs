using BotArena.Sdk;

/// <summary>
/// What the other side can do to a tile, and how soon. Bolts already in flight
/// are projected across every continuation their owner's shot envelope still
/// permits — a cardinal bolt that has not yet spent its bend is treated as
/// three futures, not one line. Muzzles are projected through the same
/// trajectory algebra, separated into what needs a turn first and what does not.
/// </summary>
internal sealed class ThreatField
{
    private readonly Dictionary<Position, int> _incoming = [];
    private readonly HashSet<Position> _boltTiles = [];
    private readonly List<(Position Origin, GenericActorRulesContract.AttackProfile Attack,
        Direction Facing, bool Omni)> _muzzles = [];
    private readonly List<Position> _bodies = [];
    private readonly Field _field;

    public ThreatField(
        Field field,
        Doctrine doctrine,
        GenericActorContext context)
    {
        _field = field;
        int teamId = context.Self.ActorId.TeamId;

        var projectiles = context.VisibleProjectiles ?? [];
        foreach (var projectile in projectiles)
        {
            if (projectile.OwnerTeamId == teamId)
                continue;
            _boltTiles.Add(projectile.Position);
            ProjectBolt(doctrine, context, projectile);
        }

        foreach (var enemy in context.Enemies)
        {
            _bodies.Add(enemy.Position);
            if (doctrine.Attack(enemy.FormId) is not { } attack)
                continue;
            _muzzles.Add(
                (enemy.Position, attack, enemy.Facing, attack.OmnidirectionalAim));
        }
    }

    /// <summary>A bolt that will traverse this tile during the coming resolution.</summary>
    public bool ImmediateImpact(Position tile) =>
        _incoming.TryGetValue(tile, out int offset) && offset <= 0;

    /// <summary>Standing on a bolt's current tile is a hit; so is walking onto it.</summary>
    public bool OccupiedByBolt(Position tile) => _boltTiles.Contains(tile);

    public double Danger(Position tile)
    {
        double danger = 0;
        if (_incoming.TryGetValue(tile, out int offset))
            danger += 7.0 / (1 + Math.Max(0, offset));
        if (_boltTiles.Contains(tile))
            danger += 6.0;

        foreach (var muzzle in _muzzles)
        {
            if (!ForkPlanner.CanCover(_field, muzzle.Origin, tile, muzzle.Attack))
                continue;
            danger += muzzle.Omni || Aligned(muzzle.Origin, tile, muzzle.Facing)
                ? 3.0
                : 1.1;
        }

        foreach (Position body in _bodies)
        {
            int distance = body.ChebyshevDistance(tile);
            if (distance <= 1)
                danger += 2.5;
            else if (distance == 2)
                danger += 0.6;
        }
        return danger;
    }

    private static bool Aligned(Position from, Position to, Direction facing)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        Direction required = Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? Direction.East : Direction.West)
            : (dy >= 0 ? Direction.South : Direction.North);
        return facing == required;
    }

    private void ProjectBolt(
        Doctrine doctrine,
        GenericActorContext context,
        GenericActorContext.ObservedProjectile projectile)
    {
        GenericActorRulesContract.AttackProfile? attack = null;
        if (projectile.OwnerActorId is { } ownerId)
        {
            var owner = context.Enemies
                .FirstOrDefault(enemy => enemy.ActorId.Equals(ownerId));
            if (owner is not null)
                attack = doctrine.Attack(owner.FormId);
        }
        attack ??= doctrine.OpposingFormIds
            .Select(doctrine.Attack)
            .Where(profile => profile is not null)
            .OrderByDescending(profile => profile!.Projectile.MaxTravelTiles)
            .FirstOrDefault();

        int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
        int remaining = projectile.RemainingTiles > 0
            ? projectile.RemainingTiles
            : attack?.Projectile.MaxTravelTiles ?? perAdvance * 2;
        remaining = Math.Min(remaining, 12);
        int firstOffset = Math.Max(0, projectile.TicksUntilAdvance - 1);

        bool diagonal = ((int)projectile.Heading % 2) != 0;
        var program = attack?.ShotProgram;
        bool mayBend = !diagonal
            && program is { Enabled: true }
            && program.MaxBendCount > 0;

        Walk(projectile.Position, projectile.Heading, remaining, -1, 0,
            perAdvance, firstOffset, attack);
        if (!mayBend || program is null)
            return;

        for (int after = Math.Max(1, program.MinBendAfterTiles);
             after <= Math.Min(program.MaxBendAfterTiles, remaining);
             after++)
        {
            foreach (int bendDirection in program.AllowedCurvedBendDirections)
            {
                if (bendDirection is not (-1 or 1))
                    continue;
                Walk(projectile.Position, projectile.Heading, remaining, after,
                    bendDirection, perAdvance, firstOffset, attack);
            }
        }
    }

    private void Walk(
        Position start,
        ProjectileHeading heading,
        int remaining,
        int bendAfter,
        int bendDirection,
        int perAdvance,
        int firstOffset,
        GenericActorRulesContract.AttackProfile? attack)
    {
        bool strict = attack?.Projectile.DiagonalCornersMustBeClear ?? true;
        Position cursor = start;
        ProjectileHeading current = heading;
        for (int step = 1; step <= remaining; step++)
        {
            if (bendAfter >= 0 && step - 1 == bendAfter)
                current = current.Turned(bendDirection);
            (int dx, int dy) = current.Vector();
            Position next = cursor.Offset(dx, dy);
            if (_field.IsWall(next))
                return;
            if (strict
                && dx != 0
                && dy != 0
                && (_field.IsWall(cursor.Offset(dx, 0))
                    || _field.IsWall(cursor.Offset(0, dy))))
            {
                return;
            }

            int offset = firstOffset + ((step - 1) / perAdvance);
            if (!_incoming.TryGetValue(next, out int known) || offset < known)
                _incoming[next] = offset;
            cursor = next;
        }
    }
}
