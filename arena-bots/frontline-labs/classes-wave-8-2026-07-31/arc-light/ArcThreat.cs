using BotArena.Sdk;

/// <summary>
/// What can hurt a tile, and when. Ownership is the only test applied to a
/// bolt: <b>every projectile this team does not own is hostile</b>, which is
/// deliberately the rule that makes a deflected bolt dangerous without a
/// special case. A shell returns the bolt you fired as a bolt owned by the
/// shell's team, so "it came out of my gun" is not a safety argument, and a
/// bot that filters incoming fire by "did I fire it" walks into its own shot.
/// </summary>
internal sealed class ArcThreat
{
    private readonly ArcFacts _facts;
    private readonly GenericActorContext _context;
    private readonly GenericActorContext.ObservedProjectile[] _hostile;
    private readonly ArcMemory _memory;
    private readonly GenericActorContext.ModeObservationState.Frontline? _mode;

    public ArcThreat(
        ArcFacts facts,
        GenericActorContext context,
        ArcMemory memory)
    {
        _facts = facts;
        _context = context;
        _memory = memory;
        _mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;
        _hostile = (context.VisibleProjectiles ?? [])
            .Where(projectile =>
                projectile.OwnerTeamId != facts.TeamId)
            .OrderBy(projectile => projectile.ProjectileId)
            .ToArray();
    }

    public int HostileBoltCount => _hostile.Length;

    /// <summary>
    /// W6. How far one enemy's gun actually reaches: its profile's DECLARED
    /// travel plus its team's published edge tier, when the body occupies a slot
    /// the declared upgrade scope applies to. The base is contract data and the
    /// tier is observation data, so nothing here is inferred — but a lane model
    /// that reads only the base under-reports an upgraded gun by exactly one
    /// tile, and the tile it under-reports is the last one, which is the only
    /// tile a skirmisher's whole doctrine is standing on.
    /// </summary>
    private int Reach(GenericActorContext.ObservedEnemyState enemy) =>
        Reach(enemy, enemy.FormId);

    private int Reach(
        GenericActorContext.ObservedEnemyState enemy,
        string formId) =>
        _facts.EffectiveTravel(
            formId,
            _mode,
            enemy.ActorId.TeamId,
            _facts.IsPrimeSlot(enemy.ActorId.TeamId, enemy.ActorId.UnitId));

    /// <summary>
    /// Ticks until the first hostile bolt reaches <paramref name="tile"/>, and
    /// the damage that contact costs. Null when no visible bolt can reach it on
    /// its current heading and remaining range.
    /// </summary>
    public (int Ticks, int Damage)? Incoming(Position tile)
    {
        int bestTicks = int.MaxValue;
        int damage = 0;
        foreach (GenericActorContext.ObservedProjectile bolt in _hostile)
        {
            ArenaBasics.Incoming? threat = ArenaBasics.Threat(bolt, tile);
            if (threat is null)
                continue;
            if (!ArcBoard.ClearRay(_facts, bolt.Position, tile, strictCorners: true))
                continue;
            if (threat.TicksUntilArrival < bestTicks)
            {
                bestTicks = threat.TicksUntilArrival;
                damage = threat.Damage;
            }
        }
        return bestTicks == int.MaxValue ? null : (bestTicks, damage);
    }

    /// <summary>
    /// True when a visible hostile bolt reaches <paramref name="tile"/> within
    /// <paramref name="ticks"/> ticks. The windup gate for every commitment
    /// arc-light makes: a wait-only stance windup cannot dodge, so entering one
    /// with a bolt in flight is how a skill-native doctrine throws a body away.
    /// </summary>
    public bool Threatened(Position tile, int ticks) =>
        Incoming(tile) is { } threat && threat.Ticks <= ticks;

    /// <summary>
    /// Tiles an enemy fan stance already covers, or is about to: the stance
    /// gun's own heading plus the adjacent headings its declared volley spread
    /// fans into. Read from the enemy's form and its pending transition, so a
    /// two-tick entry windup is two ticks of warning that a lane is closing.
    /// </summary>
    public HashSet<Position> FanLanes()
    {
        var lanes = new HashSet<Position>();
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            string fanForm = _facts.IsFanForm(enemy.FormId)
                ? enemy.FormId
                : enemy.PendingSameLifeTransition?.TargetFormId
                    is string pending
                    && _facts.IsFanForm(pending)
                    ? pending
                    : string.Empty;
            if (fanForm.Length == 0)
                continue;

            GenericActorRulesContract.AttackProfile? attack =
                _facts.Attack(fanForm);
            int reach = attack is null ? 8 : Reach(enemy, fanForm);
            int bolts = attack?.ProjectilesPerAttack ?? 1;
            int wing = (bolts - 1) / 2;
            ProjectileHeading centre = enemy.Facing.ToProjectileHeading();
            for (int offset = -wing; offset <= wing; offset++)
            {
                ProjectileHeading heading = centre.Turned(offset);
                Position cursor = enemy.Position;
                for (int tile = 0; tile < reach; tile++)
                {
                    cursor = ArcBoard.Step(cursor, heading);
                    if (_facts.IsWall(cursor))
                        break;
                    lanes.Add(cursor);
                }
            }
        }
        return lanes;
    }

    /// <summary>
    /// Every tile a visible enemy's gun can put a bolt on from its current pose,
    /// built by previewing that gun's OWN declared envelope through the engine's
    /// bend rule. This is the locked arc that flanking beats: a striker's deep
    /// commitment reaches further around a corner than a two-tile envelope does,
    /// and the difference is readable rather than guessable. Positional advice,
    /// not panic — standing here is a cost, being here is not a death.
    /// </summary>
    public HashSet<Position> EnemyLanes()
    {
        var lanes = new HashSet<Position>();
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                _facts.Attack(enemy.FormId);
            if (attack is null)
                continue;
            int reach = Reach(enemy);

            if (attack.OmnidirectionalAim)
            {
                foreach (ProjectileHeading heading in Enum.GetValues<ProjectileHeading>())
                    AddRay(lanes, enemy.Position, heading, reach);
                continue;
            }

            int wing = (attack.ProjectilesPerAttack - 1) / 2;
            for (int lane = -wing; lane <= wing; lane++)
                AddRay(lanes, enemy.Position, enemy.Facing.ToProjectileHeading().Turned(lane), reach);

            GenericActorRulesContract.ShotProgramDefinition program =
                attack.ShotProgram;
            if (!program.Enabled)
                continue;
            for (int aim = program.MinInitialAimSteps;
                 aim <= program.MaxInitialAimSteps;
                 aim++)
            {
                foreach (int direction in program.AllowedCurvedBendDirections)
                {
                    if (direction == 0)
                        continue;
                    for (int after = program.MinBendAfterTiles;
                         after <= program.MaxBendAfterTiles;
                         after++)
                    {
                        foreach (Position tile in ShotPaths.Preview(
                                     enemy.Position,
                                     enemy.Facing,
                                     new ShotProgram(
                                         aim,
                                         direction,
                                         after,
                                         Math.Max(1, program.MinBendEveryTiles),
                                         Math.Max(1, program.MinBendCount)),
                                     reach,
                                     _facts.IsWall))
                        {
                            lanes.Add(tile);
                        }
                    }
                }
            }
        }
        return lanes;
    }

    private void AddRay(
        HashSet<Position> lanes,
        Position origin,
        ProjectileHeading heading,
        int reach)
    {
        Position cursor = origin;
        for (int tile = 0; tile < reach; tile++)
        {
            cursor = ArcBoard.Step(cursor, heading);
            if (_facts.IsWall(cursor))
                break;
            lanes.Add(cursor);
        }
    }

    /// <summary>
    /// True when a bolt fired from <paramref name="from"/> at
    /// <paramref name="enemy"/> would be deflected instead of landing: the
    /// target form declares a facing-quadrant guard and the shooter stands
    /// inside that quadrant. Poking that face is a tempo tax with a bill —
    /// the bolt comes back along the exact reverse heading, owned by the
    /// guard's team.
    /// </summary>
    public bool WouldReturnFire(
        GenericActorContext.ObservedEnemyState enemy,
        Position from) =>
        _facts.IsGuardForm(enemy.FormId)
        && ArcBoard.InFacingQuadrant(enemy.Position, enemy.Facing, from);

    /// <summary>
    /// Enemy guards whose shield is one contact from shattering, counted from
    /// the published deflection events rather than guessed. The third contact
    /// forces a return, and the forced exit plus a fresh entry windup is the
    /// punish window — so this is the cue to close, not to keep poking.
    /// </summary>
    public HashSet<ActorIdentity> GuardsNearBreak()
    {
        var breaking = new HashSet<ActorIdentity>();
        foreach ((ActorIdentity actor, int seen) in _memory.Deflections)
        {
            GenericActorContext.ObservedEnemyState? enemy = _context.Enemies
                .FirstOrDefault(candidate => candidate.ActorId == actor);
            if (enemy is null || !_facts.IsGuardForm(enemy.FormId))
                continue;
            GenericActorRulesContract.AutomaticReturnTrigger? budget =
                _facts.StanceBudget(enemy.FormId);
            if (budget is not null && seen >= budget.Threshold - 1)
                breaking.Add(actor);
        }
        return breaking;
    }

    /// <summary>
    /// True when the first hostile bolt due at <paramref name="tile"/> arrives
    /// from inside the facing quadrant a guard would protect. A deflecting stance
    /// chooses its quadrant before it rises and cannot turn afterwards, so this
    /// is the whole question a shield decision has to answer.
    /// </summary>
    public bool ArrivesInQuadrant(Position tile, Direction facing)
    {
        int bestTicks = int.MaxValue;
        bool inside = false;
        foreach (GenericActorContext.ObservedProjectile bolt in _hostile)
        {
            ArenaBasics.Incoming? threat = ArenaBasics.Threat(bolt, tile);
            if (threat is null)
                continue;
            if (!ArcBoard.ClearRay(_facts, bolt.Position, tile, strictCorners: true))
                continue;
            if (threat.TicksUntilArrival >= bestTicks)
                continue;
            bestTicks = threat.TicksUntilArrival;
            inside = ArcBoard.InFacingQuadrant(tile, facing, bolt.Position);
        }
        return inside;
    }

    /// <summary>
    /// How many visible enemy bodies could put a bolt on <paramref name="tile"/>
    /// from the pose they are standing in, counting each one's OWN declared
    /// envelope. A bolt in flight is a fact; a gun bearing on you is a
    /// commitment price, and the difference matters for exactly one decision.
    ///
    /// <para>Entering a stance costs a windup that permits nothing but waiting,
    /// so <see cref="Threatened"/> — which only knows about bolts already
    /// launched — declares a tile safe while three loaded guns are pointed at
    /// it. In wave 4 that gap was invisible: five of seventeen bodies lost in
    /// the fabricator matchup died immobile inside a stance whose entry the
    /// bolt test had cleared. A swarm keeps more guns loaded than a duel does,
    /// so the fix belongs to this wave.</para>
    /// </summary>
    public int Bearing(Position tile) => Bearing(tile, quadrantFrom: null);

    /// <summary>
    /// Enemy guns bearing on a tile, optionally counted only for shooters that
    /// stand inside the facing quadrant of a body at that tile. The quadrant form
    /// is what a deflecting stance needs: a shield protects one quadrant, chosen
    /// before it rises and unturnable afterwards, so the question is not "am I
    /// under fire" but "is the fire coming from the arc I would be covering".
    /// </summary>
    public int Bearing(Position tile, Direction? quadrantFrom)
    {
        int bearing = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                _facts.Attack(enemy.FormId);
            if (attack is null)
                continue;
            if (quadrantFrom is Direction facing
                && !ArcBoard.InFacingQuadrant(tile, facing, enemy.Position))
            {
                continue;
            }
            if (BearsOn(enemy, attack, tile))
                bearing++;
        }
        return bearing;
    }

    private bool BearsOn(
        GenericActorContext.ObservedEnemyState enemy,
        GenericActorRulesContract.AttackProfile attack,
        Position tile)
    {
        int reach = Reach(enemy);
        if (enemy.Position.ChebyshevDistance(tile) > reach)
            return false;

        if (attack.OmnidirectionalAim)
        {
            return ArcBoard.TryHeading(
                    enemy.Position,
                    tile,
                    out ProjectileHeading _,
                    out int _)
                && ArcBoard.ClearRay(
                    _facts,
                    enemy.Position,
                    tile,
                    attack.Projectile.DiagonalCornersMustBeClear);
        }

        // A rotation is one action, so every cardinal facing is one tick away
        // and the honest envelope for a commitment longer than a tick is the
        // union over facings. The lane geometry is still the enemy's own —
        // its aim offsets, its bend depth, its travel.
        GenericActorRulesContract.ShotProgramDefinition program =
            attack.ShotProgram;
        int wing = (attack.ProjectilesPerAttack - 1) / 2;
        foreach (Direction facing in ArcBoard.Cardinals)
        {
            for (int lane = -wing; lane <= wing; lane++)
            {
                if (Covers(enemy.Position, facing, lane, 0, 0, attack, reach, tile))
                    return true;
            }
            if (!program.Enabled)
                continue;
            for (int aim = program.MinInitialAimSteps;
                 aim <= program.MaxInitialAimSteps;
                 aim++)
            {
                if (Covers(enemy.Position, facing, aim, 0, 0, attack, reach, tile))
                    return true;
                foreach (int direction in program.AllowedCurvedBendDirections)
                {
                    if (direction == 0)
                        continue;
                    for (int after = program.MinBendAfterTiles;
                         after <= program.MaxBendAfterTiles;
                         after++)
                    {
                        if (Covers(
                                enemy.Position,
                                facing,
                                aim,
                                direction,
                                after,
                                attack,
                                reach,
                                tile))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private bool Covers(
        Position origin,
        Direction facing,
        int aim,
        int bendDirection,
        int bendAfter,
        GenericActorRulesContract.AttackProfile attack,
        int reach,
        Position tile)
    {
        foreach (Position step in ShotPaths.Preview(
                     origin,
                     facing,
                     new ShotProgram(
                         aim,
                         bendDirection,
                         bendAfter,
                         Math.Max(1, attack.ShotProgram.MinBendEveryTiles),
                         bendDirection == 0
                             ? 0
                             : Math.Max(1, attack.ShotProgram.MinBendCount)),
                     reach,
                     _facts.IsWall))
        {
            if (step == tile)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Tiles currently held by a body or a bolt. Transient by definition: this
    /// is a first-step blocker, never a wall.
    /// </summary>
    public HashSet<Position> BlockedNow()
    {
        var blocked = _context.Allies
            .Select(ally => ally.Position)
            .Concat(_context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        foreach (GenericActorContext.ObservedProjectile bolt
                 in _context.VisibleProjectiles ?? [])
        {
            if (!_facts.AlliedBoltsPass || bolt.OwnerTeamId != _facts.TeamId)
                blocked.Add(bolt.Position);
        }
        // Spawn claims are published on the tiles they hold. An own slot's
        // permanent return anchor is reserved against this team's OTHER bodies,
        // and any queued lifecycle output blocks its tile until it completes.
        foreach (GenericActorContext.ObservedTile tile in _context.VisibleTiles)
        {
            if (tile.SpawnReservation is not { } reservation)
                continue;
            bool ownAnchorForAnotherSlot =
                reservation.Kind
                    == GenericActorContext.SpawnReservationKind.AutomaticReturn
                && reservation.TeamId == _facts.TeamId
                && reservation.UnitId != _context.Self.ActorId.UnitId;
            bool queuedOutput = reservation.Kind
                != GenericActorContext.SpawnReservationKind.AutomaticReturn;
            if (ownAnchorForAnotherSlot || queuedOutput)
                blocked.Add(tile.Position);
        }
        return blocked;
    }
}
