using BotArena.Sdk;

/// <summary>
/// What the other side can do to a tile, and how soon. Bolts already in flight
/// are projected across every continuation their owner's shot envelope still
/// permits — a cardinal bolt that has not yet spent its bend is treated as
/// three futures, not one line. Muzzles are projected through the same
/// trajectory algebra, separated into what needs a turn first and what does not.
///
/// A bolt is also projected in <em>time</em>: every tile it will traverse is
/// stamped with the tick offset at which it arrives, and every tile it comes to
/// rest on is stamped with the following offset, because a body that walks onto
/// a resting bolt is hit exactly as hard as one that stands in its path. That
/// timed map is what <see cref="Survivable"/> searches: standing where a bolt
/// arrives in three ticks is only safe if some legal sequence of steps leaves
/// before it does. In a corridor with no lateral exit there is no such
/// sequence, and the tile is a coffin however quiet it looks this tick.
///
/// <para>
/// Every timing number now comes off the BOLT rather than off the attack profile
/// that is guessed to have fired it: <c>ticksPerAdvance</c> is the cadence,
/// <c>ticksUntilAdvance</c> the next advance, <c>tilesPerAdvance</c> the stride,
/// and <c>damagePerHit</c> the bill. All four are published per projectile, and
/// they have to be — a volley bolt, an ordinary bolt, and a bolt a shell has
/// turned around need not agree on any of them, and a deflected bolt carries
/// the damage class of the bolt that was returned. A bolt is hostile because its
/// owning TEAM is not ours, which is exactly why a bolt of ours that comes back
/// off an aegis arc is treated as the hostile projectile it has become.
/// </para>
/// </summary>
internal sealed class ThreatField
{
    /// <summary>How many ticks ahead survivability is searched.</summary>
    public const int Horizon = 3;

    private readonly Dictionary<Position, int> _incoming = [];
    private readonly Dictionary<Position, int> _damage = [];
    private readonly HashSet<(Position Tile, int Offset)> _hazard = [];
    private readonly HashSet<Position> _boltTiles = [];
    private readonly List<Muzzle> _muzzles = [];
    private readonly List<Position> _bodies = [];
    private readonly Field _field;

    private readonly record struct Muzzle(
        Position Origin,
        GenericActorRulesContract.AttackProfile Attack,
        Direction Facing,
        bool Omni,
        int FanBolts,
        bool Ready);

    public ThreatField(
        Field field,
        Doctrine doctrine,
        GenericActorContext context,
        QuarryTracker tracker)
    {
        _field = field;
        int teamId = context.Self.ActorId.TeamId;

        var projectiles = context.VisibleProjectiles ?? [];
        foreach (var projectile in projectiles)
        {
            if (projectile.OwnerTeamId == teamId)
                continue;
            _boltTiles.Add(projectile.Position);
            _hazard.Add((projectile.Position, 0));
            Record(projectile.Position, 0, projectile.DamagePerHit);
            ProjectBolt(doctrine, context, projectile);
        }

        foreach (var enemy in context.Enemies)
        {
            _bodies.Add(enemy.Position);
            if (doctrine.Attack(enemy.FormId) is not { } attack)
                continue;
            var trace = tracker.Get(enemy.ActorId);
            // Enemy cooldown is redacted, but its attack event is not. With the
            // declared cooldown of the form that fired, the last observed shot
            // dates the earliest tick that gun can speak again. Unknown counts
            // as ready: never assume an unseen gun is cold.
            bool ready = trace is null
                || trace.LastAttackTick == int.MinValue
                || context.Tick - trace.LastAttackTick > attack.CooldownTicks;
            _muzzles.Add(
                new Muzzle(
                    enemy.Position,
                    attack,
                    enemy.Facing,
                    attack.OmnidirectionalAim,
                    attack.ProjectilesPerAttack,
                    ready));
        }
    }

    /// <summary>Whether any hostile bolt is in flight at all.</summary>
    public bool HasBolts => _hazard.Count > 0;

    /// <summary>A bolt that will traverse this tile during the coming resolution.</summary>
    public bool ImmediateImpact(Position tile) =>
        _incoming.TryGetValue(tile, out int offset) && offset <= 0;

    /// <summary>Standing on a bolt's current tile is a hit; so is walking onto it.</summary>
    public bool OccupiedByBolt(Position tile) => _boltTiles.Contains(tile);

    /// <summary>Whether a bolt occupies or crosses this tile at that tick offset.</summary>
    public bool Hazard(Position tile, int offset) =>
        _hazard.Contains((tile, offset));

    /// <summary>
    /// Worst single-contact damage projected onto a tile by anything in flight,
    /// read from the bolts themselves. Zero when nothing reaches it.
    /// </summary>
    public int WorstDamage(Position tile) =>
        _damage.TryGetValue(tile, out int damage) ? damage : 0;

    /// <summary>
    /// Whether a body that cannot move — because it is inside a wait-only windup
    /// or wearing an immobile stance — survives standing here for
    /// <paramref name="ticks"/> more ticks against what is already in flight.
    /// This is the question a blind commitment actually asks, and it is not the
    /// same question as <see cref="Survivable"/>, which is allowed to walk.
    /// </summary>
    public bool SafeStandingFor(Position tile, int ticks, int health)
    {
        if (OccupiedByBolt(tile))
            return false;
        int budget = Math.Max(0, health - 1);
        for (int offset = 0; offset <= ticks; offset++)
        {
            if (!Hazard(tile, offset))
                continue;
            budget -= Math.Max(1, WorstDamage(tile));
            if (budget < 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Whether a ready enemy gun is already POINTED at this tile — its current
    /// facing, not a facing it could turn to. A blind windup spent under a gun
    /// that is already aimed is a gift; spending one under a gun that must first
    /// turn is an ordinary risk, and refusing every such tile refuses every tile
    /// within eight of a visible body, which is the whole board.
    /// </summary>
    public bool AimedByReadyMuzzle(Position tile)
    {
        foreach (Muzzle muzzle in _muzzles)
        {
            if (!muzzle.Ready)
                continue;
            if (muzzle.FanBolts > 1)
            {
                if (FanCovers(muzzle, tile))
                    return true;
                continue;
            }
            if (muzzle.Omni)
            {
                if (ForkPlanner.CanCover(
                        _field, muzzle.Origin, tile, muzzle.Attack))
                {
                    return true;
                }
                continue;
            }
            if (ForkPlanner.CanCoverFrom(
                    _field, muzzle.Origin, tile, muzzle.Attack, muzzle.Facing))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether one enemy's directional threat covers both tiles at once — the
    /// same fan, or the same guarded quadrant, or the same aimed lane.
    ///
    /// <para>This is the geometry behind "come from two bearings". A fan sprays
    /// one facing's neighbourhood and a guard protects one quadrant, and neither
    /// tracks; two bodies inside the same cone are two bodies one decision
    /// answers, while two bodies on genuinely different bearings cannot both be
    /// covered by either. It is a positional cost, not a prohibition: standing
    /// on the point still beats standing off it.</para>
    /// </summary>
    public bool SharedCone(Position first, Position second)
    {
        foreach (Muzzle muzzle in _muzzles)
        {
            if (muzzle.Omni)
                continue;
            if (muzzle.FanBolts > 1)
            {
                if (FanCovers(muzzle, first) && FanCovers(muzzle, second))
                    return true;
                continue;
            }
            if (Aligned(muzzle.Origin, first, muzzle.Facing)
                && Aligned(muzzle.Origin, second, muzzle.Facing)
                && ForkPlanner.CanCover(_field, muzzle.Origin, first, muzzle.Attack)
                && ForkPlanner.CanCover(_field, muzzle.Origin, second, muzzle.Attack))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether some legal sequence of decisions starting from this tile is still
    /// alive after <see cref="Horizon"/> ticks of the bolts currently in flight.
    /// The step model follows the contract's declared facing coupling: a
    /// facing-locked chassis may only walk where it points, so turning costs a
    /// whole tick and the search has to spend it.
    /// </summary>
    public bool Survivable(
        Position start,
        Direction facing,
        GenericActorRulesContract.MovementFacingCoupling coupling,
        IReadOnlySet<Position> blocked)
    {
        if (_hazard.Count == 0)
            return true;
        if (Hazard(start, 0))
            return false;

        bool locked = coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;
        var frontier = new List<(Position Tile, Direction Facing)>
        {
            (start, facing),
        };
        var seen = new HashSet<(Position Tile, Direction Facing)>();

        for (int offset = 1; offset <= Horizon; offset++)
        {
            var next = new List<(Position Tile, Direction Facing)>();
            seen.Clear();
            foreach ((Position tile, Direction pointing) in frontier)
            {
                foreach ((Position candidate, Direction after) in
                         Steps(tile, pointing, locked, blocked))
                {
                    if (Hazard(candidate, offset))
                        continue;
                    if (!seen.Add((candidate, after)))
                        continue;
                    next.Add((candidate, after));
                }
            }
            if (next.Count == 0)
                return false;
            frontier = next;
        }
        return true;
    }

    /// <summary>
    /// One tick's worth of legal poses. Preserve-facing and face-movement
    /// chassis may step any open cardinal; a facing-locked one may only step
    /// where it points, and spends the whole tick to point somewhere else.
    /// </summary>
    private IEnumerable<(Position Tile, Direction Facing)> Steps(
        Position tile,
        Direction facing,
        bool locked,
        IReadOnlySet<Position> blocked)
    {
        yield return (tile, facing);
        if (locked)
        {
            foreach (Direction turned in Field.Cardinals)
            {
                if (turned != facing)
                    yield return (tile, turned);
            }
            (int fx, int fy) = facing.Vector();
            Position ahead = tile.Offset(fx, fy);
            if (_field.IsOpen(ahead) && !blocked.Contains(ahead))
                yield return (ahead, facing);
            yield break;
        }

        foreach (Direction direction in Field.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position candidate = tile.Offset(dx, dy);
            if (!_field.IsOpen(candidate) || blocked.Contains(candidate))
                continue;
            yield return (candidate, direction);
        }
    }

    public double Danger(Position tile)
    {
        double danger = 0;
        if (_incoming.TryGetValue(tile, out int offset))
        {
            // Scale by what one contact actually costs. A bolt is published
            // with its own damage, so a heavier bolt is a heavier tile.
            danger += 7.0 * Math.Max(1, WorstDamage(tile))
                / (1 + Math.Max(0, offset));
        }
        if (_boltTiles.Contains(tile))
            danger += 6.0;

        foreach (Muzzle muzzle in _muzzles)
        {
            double reach;
            if (muzzle.FanBolts > 1)
            {
                // A fan does not aim: one attack sprays the aim heading and its
                // neighbours at once. Being on any of those rays is the same
                // exposure, and there is no "it would have to turn first"
                // discount to take, because the turn buys all three rays.
                if (!FanCovers(muzzle, tile))
                    continue;
                reach = 3.4 * muzzle.Attack.Projectile.DamagePerHit;
            }
            else
            {
                if (!ForkPlanner.CanCover(
                        _field, muzzle.Origin, tile, muzzle.Attack))
                {
                    continue;
                }
                reach = muzzle.Omni || Aligned(muzzle.Origin, tile, muzzle.Facing)
                    ? 3.0
                    : 1.1;
            }
            // A gun still on cooldown cannot punish this tile this tick. It is a
            // discount, never a dismissal: the cooldown is inferred, not read.
            danger += muzzle.Ready ? reach : reach * 0.4;
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

    /// <summary>
    /// Whether one of a fan's simultaneous rays reaches the tile. The offsets
    /// come from the declared projectile count, and the rays are straight,
    /// because a profile carrying a volley refuses programmed shots outright.
    /// </summary>
    private bool FanCovers(Muzzle muzzle, Position tile)
    {
        int count = Math.Min(8, Math.Max(1, muzzle.FanBolts));
        int half = (count - 1) / 2;
        ProjectileHeading aim = muzzle.Facing.ToProjectileHeading();
        bool strict = muzzle.Attack.Projectile.DiagonalCornersMustBeClear;
        int range = muzzle.Attack.Projectile.MaxTravelTiles;
        for (int index = 0; index < count; index++)
        {
            ProjectileHeading heading = aim.Turned(index - half);
            (int dx, int dy) = heading.Vector();
            Position cursor = muzzle.Origin;
            for (int step = 1; step <= range; step++)
            {
                Position next = cursor.Offset(dx, dy);
                if (_field.IsWall(next))
                    break;
                if (strict
                    && dx != 0
                    && dy != 0
                    && (_field.IsWall(cursor.Offset(dx, 0))
                        || _field.IsWall(cursor.Offset(0, dy))))
                {
                    break;
                }
                if (next == tile)
                    return true;
                cursor = next;
            }
        }
        return false;
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

    private void Record(Position tile, int offset, int damage)
    {
        if (!_damage.TryGetValue(tile, out int known) || damage > known)
            _damage[tile] = damage;
        if (!_incoming.TryGetValue(tile, out int soonest) || offset < soonest)
            _incoming[tile] = offset;
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
        // The cadence is published on the bolt. Reverse-engineering it from a
        // guessed attack profile is exactly what a deflected or volley bolt
        // breaks, because those need not share the shooter's timing at all.
        int cadence = Math.Max(1, projectile.TicksPerAdvance);
        int remaining = projectile.RemainingTiles > 0
            ? projectile.RemainingTiles
            : attack?.Projectile.MaxTravelTiles ?? perAdvance * 2;
        remaining = Math.Min(remaining, 12);
        int firstOffset = Math.Max(0, projectile.TicksUntilAdvance - 1);

        // Until it advances the bolt simply sits where it is, and a body that
        // walks onto that tile is hit as surely as one standing in its path.
        for (int idle = 0; idle <= firstOffset; idle++)
        {
            _hazard.Add((projectile.Position, idle));
            Record(projectile.Position, idle, projectile.DamagePerHit);
        }

        bool diagonal = ((int)projectile.Heading % 2) != 0;
        var program = attack?.ShotProgram;
        bool mayBend = !diagonal
            && program is { Enabled: true }
            && program.MaxBendCount > 0;

        Walk(projectile, projectile.Heading, remaining, -1, 0,
            perAdvance, cadence, firstOffset, attack);
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
                Walk(projectile, projectile.Heading, remaining, after,
                    bendDirection, perAdvance, cadence, firstOffset, attack);
            }
        }
    }

    private void Walk(
        GenericActorContext.ObservedProjectile projectile,
        ProjectileHeading heading,
        int remaining,
        int bendAfter,
        int bendDirection,
        int perAdvance,
        int cadence,
        int firstOffset,
        GenericActorRulesContract.AttackProfile? attack)
    {
        bool strict = attack?.Projectile.DiagonalCornersMustBeClear ?? true;
        Position cursor = projectile.Position;
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

            int offset = firstOffset + (((step - 1) / perAdvance) * cadence);
            Record(next, offset, projectile.DamagePerHit);
            _hazard.Add((next, offset));
            // End of an advance: the bolt rests here until its next advance, so
            // stepping onto it during that pause is a hit rather than an escape.
            if (step % perAdvance == 0)
            {
                for (int idle = 1; idle <= cadence; idle++)
                    _hazard.Add((next, offset + idle));
            }
            cursor = next;
        }
    }
}
