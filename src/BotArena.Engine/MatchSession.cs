namespace BotArena.Engine;

public sealed record BotTickResolution(
    int Slot, BotAction ChosenAction, BotAction ValidatedAction, ActionResult Result, bool Faulted);

public sealed record ProjectileTickTraversal(
    int Id, int OwnerSlot, Direction Direction, Position From, IReadOnlyList<Position> Path,
    ProjectileHeading? Heading = null, IReadOnlyList<Position>? ProgrammedPath = null);

public sealed class TickResult
{
    public required int Tick { get; init; }
    public required IReadOnlyList<BotTickResolution> Bots { get; init; }
    public required IReadOnlyList<GameEvent> Events { get; init; }
    public required IReadOnlyList<ProjectileTickTraversal> ProjectileTraversals { get; init; }
    public required bool MatchCompleted { get; init; }
}

/// <summary>
/// The stepping core of the simulation (plan §24). Holds authoritative state and resolves one
/// tick at a time using the versioned resolution order of §4.7. It knows nothing about
/// runtimes, replays, or presentation — callers feed it decisions and read back events.
/// </summary>
public sealed class MatchSession
{
    public GameState State { get; }
    public bool IsCompleted { get; private set; }
    public MatchResultInfo? Result { get; private set; }

    /// <summary>Zone-control tiles (empty unless rules.ZoneControl) — resolved once.</summary>
    public IReadOnlyList<Position> ZoneTiles { get; }

    private readonly HashSet<Position> _zoneLookup;
    private IReadOnlyList<GameEvent> _lastTickEvents = [];

    public MatchSession(ArenaMap map, GameRules rules, IReadOnlyList<Spawn>? spawns = null)
    {
        spawns ??= map.Spawns; // Callers with seed-spawn variation resolve first (MatchEngine).
        if (spawns.Count != 2)
            throw new ArgumentException("MatchSession currently requires exactly 2 spawns.");
        var bots = new List<BotState>();
        for (int slot = 0; slot < spawns.Count; slot++)
        {
            var spawn = spawns[slot];
            bots.Add(new BotState
            {
                Slot = slot,
                Position = new Position(spawn.X, spawn.Y),
                Facing = spawn.Facing,
                Health = rules.MaxHealth,
                Energy = rules.MaxEnergy,
            });
        }
        State = new GameState { Map = map, Rules = rules, Bots = bots };
        ZoneTiles = rules.ZoneControl ? map.EffectiveZone() : [];
        _zoneLookup = [.. ZoneTiles];
    }

    /// <summary>Step 1 of §4.7: observations always come from the pre-tick state.</summary>
    public BotObservation BuildObservation(int slot)
    {
        var bot = State.Bots[slot];
        var map = State.Map;
        var visible = Visibility.ComputeVisibleTiles(map, bot.Position, State.Rules.VisionRange,
            State.Rules.VisionCone ? bot.Facing : null);
        var visibleSet = new HashSet<Position>(visible);
        var tiles = visible.Select(p => new ObservedTile(p, map.IsWall(p))).ToArray();
        var enemies = State.Bots
            .Where(other => other.Slot != slot && other.IsActive && visibleSet.Contains(other.Position))
            .Select(other => new ObservedBot(other.Slot, other.Position, other.Facing, other.Health))
            .ToArray();
        // Sighted events are authoritative and full. Loud events beyond sight arrive
        // REDACTED as sounds — bearing octant + distance band, never coordinates — so
        // hearing is a cue, not a radar (RULES-0.5-DESIGN §A, hardened per §H item 1).
        // Quiet events (Turn/Move/MoveBlocked) stay sight-gated always. Under cone
        // vision "sighted" means the event's PRIMARY position (the actor: shooter,
        // mover-from, victim) is visible — seeing only a ray's endpoint must not
        // reveal an unseen shooter's exact tile and slot (§I follow-up review); such
        // events degrade to sounds like everything else beyond the cone.
        bool FullyVisible(GameEvent e) => State.Rules.VisionCone
            ? e.ReferencePositions().Take(1).Any(visibleSet.Contains)
            : e.ReferencePositions().Any(visibleSet.Contains);
        var events = _lastTickEvents.Where(FullyVisible).ToArray();
        List<HeardSound>? heard = null;
        if (State.Rules.HearingRadius > 0)
        {
            heard = [];
            foreach (var e in _lastTickEvents)
            {
                if (!IsLoud(e.Type) || FullyVisible(e))
                    continue;
                // The sound is located at the event's PRIMARY position — the bang at
                // the muzzle, the thud at the victim — never at a ray's endpoint,
                // which can be a tile the listener SEES (even its own, on a shot in
                // the back): a sound must always point at something unseen.
                Position? source = null;
                foreach (var p in e.ReferencePositions())
                {
                    source = p;
                    break;
                }
                if (source is not Position at)
                    continue;
                int distance = bot.Position.ChebyshevDistance(at);
                if (distance > State.Rules.HearingRadius)
                    continue;
                heard.Add(new HeardSound(
                    e.Type, Hearing.BearingOctant(bot.Position, at), Hearing.DistanceBand(distance)));
            }
        }
        return new BotObservation
        {
            Tick = State.Tick,
            Slot = slot,
            Position = bot.Position,
            Facing = bot.Facing,
            Health = bot.Health,
            Cooldown = bot.Cooldown,
            Energy = State.Rules.MaxEnergy > 0 ? bot.Energy : null,
            MapWidth = map.Width,
            MapHeight = map.Height,
            ZoneTiles = State.Rules.ZoneControl ? ZoneTiles : null,
            MyZoneTicks = State.Rules.ZoneControl && !State.Rules.ActiveZoneControl ? bot.ZoneTicks : null,
            EnemyZoneTicks = State.Rules.ZoneControl && !State.Rules.ActiveZoneControl
                ? State.Bots.Where(b => b.Slot != slot).Select(b => b.ZoneTicks).FirstOrDefault()
                : null,
            ControlPressure = State.Rules.ActiveZoneControl ? State.ControlPressure : null,
            ControlPressureLimit = State.Rules.ActiveZoneControl
                ? State.Rules.EffectiveControlPressureLimit(State.Tick)
                : null,
            PreviousActionResult = bot.LastActionResult,
            VisibleTiles = tiles,
            VisibleEnemies = enemies,
            VisibleProjectiles = State.Rules.ProjectileTicksPerTile > 0
                ? State.Projectiles
                    .Where(p => visibleSet.Contains(p.Position))
                    .Select(p => new ObservedProjectile(p.Position, p.Direction, p.OwnerSlot,
                        State.Rules.ProjectileTilesPerAdvance,
                        State.Rules.ProjectileTicksPerTile - p.Phase,
                        State.Rules.ShotRange > 0 ? State.Rules.ShotRange - p.TilesTraveled : -1,
                        p.Heading))
                    .ToArray()
                : null,
            ShotPrograms = State.Rules.GetShotProgramLimits(),
            VisibleEvents = events,
            HeardSounds = heard,
        };
    }

    // Disqualification is deliberately NOT loud: the event carries no world position
    // (nothing physical happened anywhere), and a disqualification ends the match on
    // the same tick — there is no next observation that could use the sound (§I).
    private static bool IsLoud(GameEventType type) => type is GameEventType.Shot
        or GameEventType.Damage or GameEventType.Destroyed;

    /// <summary>Steps 3–11 of §4.7. Decisions must be indexed by slot.</summary>
    public TickResult Step(IReadOnlyList<BotDecision> decisions)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Match already completed.");
        var bots = State.Bots;
        if (decisions.Count != bots.Count)
            throw new ArgumentException($"Expected {bots.Count} decisions, got {decisions.Count}.");

        int n = bots.Count;
        var events = new List<GameEvent>();
        var chosen = new BotAction[n];
        var validated = new BotAction[n];
        var results = new ActionResult[n];
        var faulted = new bool[n];

        // 3. Validate returned actions.
        for (int slot = 0; slot < n; slot++)
        {
            var decision = decisions[slot];
            if (decision.Faulted || !Enum.IsDefined(decision.Action))
            {
                chosen[slot] = BotAction.Wait;
                validated[slot] = BotAction.Wait;
                results[slot] = ActionResult.Faulted;
                faulted[slot] = true;
            }
            else if (decision.ShotProgram is not null && decision.Action != BotAction.Shoot)
            {
                chosen[slot] = BotAction.Wait;
                validated[slot] = BotAction.Wait;
                results[slot] = ActionResult.Faulted;
                faulted[slot] = true;
            }
            else if (decision.ShotProgram is not null
                     && (!State.Rules.AllowProgrammedShots
                         || State.Rules.ProjectileTicksPerTile <= 0))
            {
                // Additive action payload: on rules that do not support programs,
                // degrade to a blocked Wait rather than faulting a newer bot.
                chosen[slot] = BotAction.Shoot;
                validated[slot] = BotAction.Wait;
                results[slot] = ActionResult.Blocked;
            }
            else if (decision.ShotProgram is ShotProgram program
                     && !State.Rules.IsValidShotProgram(program))
            {
                chosen[slot] = BotAction.Wait;
                validated[slot] = BotAction.Wait;
                results[slot] = ActionResult.Faulted;
                faulted[slot] = true;
            }
            else if (decision.Action == BotAction.Shoot && bots[slot].Cooldown > 0)
            {
                chosen[slot] = BotAction.Shoot;
                validated[slot] = BotAction.Wait;
                results[slot] = ActionResult.OnCooldown;
            }
            else if (decision.Action == BotAction.Shoot
                     && State.Rules.MaxEnergy > 0 && bots[slot].Energy < State.Rules.ShotEnergyCost)
            {
                // Dry gun. OnCooldown is reused deliberately: "the gun is not ready" —
                // no new enum value, so pre-energy bots stay wire-compatible.
                chosen[slot] = BotAction.Shoot;
                validated[slot] = BotAction.Wait;
                results[slot] = ActionResult.OnCooldown;
            }
            else if (decision.Action is BotAction.StrafeLeft or BotAction.StrafeRight
                     && !State.Rules.AllowStrafe)
            {
                // Graceful degradation for newer bots on older rules — never a fault.
                chosen[slot] = decision.Action;
                validated[slot] = BotAction.Wait;
                results[slot] = ActionResult.Blocked;
            }
            else
            {
                chosen[slot] = decision.Action;
                validated[slot] = decision.Action;
                results[slot] = ActionResult.Success;
            }
        }

        // 4. Resolve rotations.
        for (int slot = 0; slot < n; slot++)
        {
            if (validated[slot] is not (BotAction.TurnLeft or BotAction.TurnRight))
                continue;
            var bot = bots[slot];
            var from = bot.Facing;
            bot.Facing = validated[slot] == BotAction.TurnLeft ? from.TurnedLeft() : from.TurnedRight();
            events.Add(GameEvent.Turn(slot, bot.Position, from, bot.Facing));
        }

        // 5. Resolve movement (§4.8).
        ResolveMovement(validated, results, events);

        // 5.5 Bolts in flight advance and hit against post-move positions
        // (RULES-0.5-DESIGN §B) — before new shots spawn, so a fresh bolt never
        // moves on its spawn tick.
        var pendingHits = new List<(int TargetSlot, int BySlot)>();
        var projectileTraversals = new List<ProjectileTickTraversal>();
        if (State.Rules.ProjectileTicksPerTile > 0 && State.Projectiles.Count > 0)
            AdvanceProjectiles(pendingHits, projectileTraversals);

        // 6.–7. Resolve shooting from post-movement state; apply damage simultaneously
        // (bolt occupancy hits land in the same simultaneous batch).
        var shotThisTick = ResolveShooting(
            validated,
            decisions,
            events,
            pendingHits,
            projectileTraversals);

        // 8. Update cooldowns and energy (shots spend first, then the regen cadence).
        for (int slot = 0; slot < n; slot++)
        {
            var bot = bots[slot];
            bot.Cooldown = shotThisTick[slot]
                ? State.Rules.ShootCooldownTicks
                : Math.Max(0, bot.Cooldown - 1);
            if (State.Rules.MaxEnergy > 0)
            {
                if (shotThisTick[slot])
                    bot.Energy -= State.Rules.ShotEnergyCost;
                if (State.Rules.EnergyRegenTicks > 0 && (State.Tick + 1) % State.Rules.EnergyRegenTicks == 0)
                    bot.Energy = Math.Min(State.Rules.MaxEnergy, bot.Energy + 1);
            }
        }

        // 9. Apply runtime-fault rules.
        for (int slot = 0; slot < n; slot++)
        {
            if (!faulted[slot])
                continue;
            var bot = bots[slot];
            bot.Faults++;
            events.Add(GameEvent.Fault(slot, decisions[slot].FaultMessage ?? "Runtime fault"));
            if (bot.Faults >= State.Rules.FaultLimit && bot.Status == BotStatus.Active)
            {
                bot.Status = BotStatus.Disqualified;
                events.Add(GameEvent.Disqualified(slot));
            }
        }

        for (int slot = 0; slot < n; slot++)
            bots[slot].LastActionResult = results[slot];

        // 9.5 Zone control is evaluated after damage. Legacy rules bank per-bot
        // occupancy ticks. Active rules instead require a successful validated Wait
        // and update one signed, decaying tug-of-war meter.
        UpdateZoneControl(validated, results);

        // 10. Determine completion.
        int executedTick = State.Tick;
        bool anyInactive = bots.Any(b => !b.IsActive);
        int? dominator = ComputeDominator(executedTick);
        State.Tick++;
        if (anyInactive || dominator is not null || State.Tick >= State.Rules.MaxTicks)
        {
            IsCompleted = true;
            Result = ComputeResult(executedTick, dominator);
        }

        _lastTickEvents = events;
        return new TickResult
        {
            Tick = executedTick,
            Bots = Enumerable.Range(0, n)
                .Select(s => new BotTickResolution(s, chosen[s], validated[s], results[s], faulted[s]))
                .ToArray(),
            Events = events,
            ProjectileTraversals = projectileTraversals,
            MatchCompleted = IsCompleted,
        };
    }

    private void UpdateZoneControl(BotAction[] validated, ActionResult[] results)
    {
        if (!State.Rules.ZoneControl)
            return;

        var bots = State.Bots;
        if (!State.Rules.ActiveZoneControl)
        {
            var occupants = bots.Where(b => b.IsActive && _zoneLookup.Contains(b.Position)).ToList();
            if (!State.Rules.ZoneExclusiveAccrual || occupants.Count == 1)
                foreach (var bot in occupants)
                    bot.ZoneTicks++;
            return;
        }

        var holders = bots
            .Where(b => b.IsActive
                        && _zoneLookup.Contains(b.Position)
                        && validated[b.Slot] == BotAction.Wait
                        && results[b.Slot] == ActionResult.Success)
            .ToList();
        int limit = Math.Max(0, State.Rules.EffectiveControlPressureLimit(State.Tick));
        if (limit > 0)
            State.ControlPressure = Math.Clamp(State.ControlPressure, -limit, limit);

        if (holders.Count == 1)
        {
            int direction = holders[0].Slot == 0 ? 1 : -1;
            int gain = Math.Max(0, State.Rules.EffectiveControlPressureGain(State.Tick));
            State.ControlPressure = Math.Clamp(State.ControlPressure + direction * gain, -limit, limit);
            return;
        }

        // Two committed holders contest and freeze the meter. Decay represents
        // abandoned control, so it applies only when nobody actively holds.
        int decayInterval = State.Rules.EffectiveControlPressureDecayInterval(State.Tick);
        if (holders.Count > 1
            || State.ControlPressure == 0
            || decayInterval <= 0
            || State.Tick % decayInterval != 0)
            return;
        State.ControlPressure -= Math.Sign(State.ControlPressure);
    }

    private void ResolveMovement(BotAction[] validated, ActionResult[] results, List<GameEvent> events)
    {
        var bots = State.Bots;
        int n = bots.Count;
        var wantsMove = new bool[n];
        var target = new Position[n];
        var blocked = new bool[n];

        for (int slot = 0; slot < n; slot++)
        {
            // Movement actions share one resolution: forward moves along facing,
            // strafes move perpendicular WITHOUT rotating (RULES-0.3-DESIGN §B).
            var direction = validated[slot] switch
            {
                BotAction.MoveForward => bots[slot].Facing,
                BotAction.StrafeLeft => bots[slot].Facing.TurnedLeft(),
                BotAction.StrafeRight => bots[slot].Facing.TurnedRight(),
                _ => (Direction?)null,
            };
            if (direction is null)
                continue;
            wantsMove[slot] = true;
            var (dx, dy) = direction.Value.Vector();
            target[slot] = bots[slot].Position.Offset(dx, dy);
            blocked[slot] = State.Map.IsWall(target[slot]);
        }

        // Same destination: neither moves. Swap: both fail.
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                if (!wantsMove[a] || !wantsMove[b])
                    continue;
                if (target[a] == target[b])
                {
                    blocked[a] = blocked[b] = true;
                }
                else if (target[a] == bots[b].Position && target[b] == bots[a].Position)
                {
                    blocked[a] = blocked[b] = true;
                }
            }
        }

        // Moving into a tile occupied by a bot that is not successfully vacating it fails.
        // Two passes reach a fixed point for two bots (a chain: A follows B, B hits a wall).
        for (int pass = 0; pass < n; pass++)
        {
            for (int slot = 0; slot < n; slot++)
            {
                if (!wantsMove[slot] || blocked[slot])
                    continue;
                for (int other = 0; other < n; other++)
                {
                    if (other == slot || bots[other].Position != target[slot])
                        continue;
                    bool vacating = wantsMove[other] && !blocked[other];
                    if (!vacating)
                        blocked[slot] = true;
                }
            }
        }

        for (int slot = 0; slot < n; slot++)
        {
            if (!wantsMove[slot])
                continue;
            var bot = bots[slot];
            if (blocked[slot])
            {
                results[slot] = ActionResult.Blocked;
                events.Add(GameEvent.MoveBlocked(slot, bot.Position, target[slot]));
            }
            else
            {
                var from = bot.Position;
                bot.Position = target[slot];
                events.Add(GameEvent.Move(slot, from, bot.Position));
            }
        }
    }

    /// <summary>Advances bolts in flight and collects occupancy hits (RULES-0.5-DESIGN
    /// §B/§J). Runs after movement, before new shots. Occupancy is checked before an
    /// advance, then after EACH ordered tile substep; walls, bots, and the final range
    /// tile therefore cannot be tunnelled through at speed two.</summary>
    private void AdvanceProjectiles(
        List<(int TargetSlot, int BySlot)> pendingHits,
        List<ProjectileTickTraversal> traversals)
    {
        var alive = new List<ProjectileState>();
        foreach (var bolt in State.Projectiles)
        {
            if (FindBoltVictim(bolt) is { } early)
            {
                pendingHits.Add((early.Slot, bolt.OwnerSlot));
                continue; // bolt consumed by the hit
            }
            bolt.Phase++;
            if (bolt.Phase < State.Rules.ProjectileTicksPerTile)
            {
                alive.Add(bolt);
                continue;
            }

            bolt.Phase = 0;
            if (bolt.ProgrammedPath is { } programmedPath)
            {
                var programmedFrom = bolt.Position;
                var programmedTraversal = new List<Position>();
                bool programmedConsumed = false;
                bool programmedDespawn = false;
                int programmedSubsteps = Math.Max(1, State.Rules.ProjectileTilesPerAdvance);
                for (int step = 0; step < programmedSubsteps; step++)
                {
                    if (bolt.NextProgrammedPathIndex >= programmedPath.Count)
                    {
                        programmedDespawn = true;
                        break;
                    }

                    var next = programmedPath[bolt.NextProgrammedPathIndex++];
                    bolt.Heading = ProjectileHeadingExtensions.Between(bolt.Position, next);
                    bolt.Position = next;
                    bolt.TilesTraveled++;
                    programmedTraversal.Add(next);

                    if (FindBoltVictim(bolt) is { } victim)
                    {
                        pendingHits.Add((victim.Slot, bolt.OwnerSlot));
                        programmedConsumed = true;
                        break;
                    }
                    if (bolt.NextProgrammedPathIndex >= programmedPath.Count)
                    {
                        programmedDespawn = true;
                        break;
                    }
                }

                if (programmedTraversal.Count > 0)
                    traversals.Add(new ProjectileTickTraversal(
                        bolt.Id,
                        bolt.OwnerSlot,
                        bolt.Direction,
                        programmedFrom,
                        programmedTraversal,
                        bolt.Heading,
                        programmedPath));
                if (!programmedConsumed && !programmedDespawn)
                    alive.Add(bolt);
                continue;
            }

            var from = bolt.Position;
            var path = new List<Position>();
            bool consumed = false;
            bool despawn = false;
            var (dx, dy) = bolt.Direction.Vector();
            int substeps = Math.Max(1, State.Rules.ProjectileTilesPerAdvance);
            for (int step = 0; step < substeps; step++)
            {
                var next = bolt.Position.Offset(dx, dy);
                if (State.Map.IsWall(next))
                {
                    despawn = true;
                    break;
                }

                bolt.Position = next;
                bolt.TilesTraveled++;
                path.Add(next);

                if (FindBoltVictim(bolt) is { } victim)
                {
                    pendingHits.Add((victim.Slot, bolt.OwnerSlot));
                    consumed = true;
                    break;
                }

                if (State.Rules.ShotRange > 0 && bolt.TilesTraveled >= State.Rules.ShotRange)
                {
                    despawn = true; // final tile was entered and checked before despawn
                    break;
                }
            }

            if (path.Count > 0)
                traversals.Add(new ProjectileTickTraversal(
                    bolt.Id, bolt.OwnerSlot, bolt.Direction, from, path));
            if (!consumed && !despawn)
                alive.Add(bolt);
        }
        State.Projectiles.Clear();
        State.Projectiles.AddRange(alive);
    }

    private BotState? FindBoltVictim(ProjectileState bolt) =>
        State.Bots.FirstOrDefault(b =>
            b.Slot != bolt.OwnerSlot && b.IsActive && b.Position == bolt.Position);

    private bool[] ResolveShooting(
        BotAction[] validated,
        IReadOnlyList<BotDecision> decisions,
        List<GameEvent> events,
        List<(int TargetSlot, int BySlot)> pendingHits,
        List<ProjectileTickTraversal> traversals)
    {
        var bots = State.Bots;
        int n = bots.Count;
        var shotThisTick = new bool[n];

        for (int slot = 0; slot < n; slot++)
        {
            if (validated[slot] != BotAction.Shoot)
                continue;
            shotThisTick[slot] = true;
            var shooter = bots[slot];
            var (dx, dy) = shooter.Facing.Vector();

            if (State.Rules.ProjectileTicksPerTile > 0)
            {
                if (State.Rules.AllowProgrammedShots)
                {
                    ResolveProgrammedShot(
                        slot,
                        shooter,
                        decisions[slot].ShotProgram ?? ShotProgram.Straight,
                        events,
                        pendingHits,
                        traversals);
                    continue;
                }

                // Projectile mode: spawn a bolt on the first tile in facing. Walls
                // swallow it; a point-blank occupant is an immediate hit (matching
                // the instant ray at range 1); otherwise it enters flight.
                var spawn = shooter.Position.Offset(dx, dy);
                if (State.Map.IsWall(spawn))
                {
                    events.Add(GameEvent.Shot(slot, shooter.Position, spawn, null));
                    continue;
                }
                var pointBlank = bots.FirstOrDefault(b =>
                    b.Slot != slot && b.IsActive && b.Position == spawn);
                events.Add(GameEvent.Shot(slot, shooter.Position, spawn, pointBlank?.Slot));
                int projectileId = State.NextProjectileId++;
                traversals.Add(new ProjectileTickTraversal(
                    projectileId, slot, shooter.Facing, shooter.Position, [spawn]));
                if (pointBlank is not null)
                    pendingHits.Add((pointBlank.Slot, slot));
                else if (State.Rules.ShotRange <= 0 || State.Rules.ShotRange > 1)
                    State.Projectiles.Add(new ProjectileState
                    {
                        Id = projectileId,
                        Position = spawn,
                        Direction = shooter.Facing,
                        OwnerSlot = slot,
                        TilesTraveled = 1,
                    });
                continue;
            }

            var current = shooter.Position;
            int? hitSlot = null;
            int traveled = 0;
            while (State.Rules.ShotRange <= 0 || traveled < State.Rules.ShotRange)
            {
                var next = current.Offset(dx, dy);
                if (State.Map.IsWall(next))
                {
                    current = next; // the wall the ray hit — matches pre-cap event shape
                    break;
                }
                current = next;
                traveled++;
                var occupant = bots.FirstOrDefault(b => b.Slot != slot && b.IsActive && b.Position == current);
                if (occupant is not null)
                {
                    hitSlot = occupant.Slot;
                    break;
                }
            }
            events.Add(GameEvent.Shot(slot, shooter.Position, current, hitSlot));
            if (hitSlot is int hit)
                pendingHits.Add((hit, slot));
        }

        // 7. Apply damage simultaneously: all hits use pre-damage health.
        var healthBefore = bots.Select(b => b.Health).ToArray();
        foreach (var (targetSlot, bySlot) in pendingHits)
        {
            var target = bots[targetSlot];
            int amount = Math.Min(State.Rules.DamagePerHit, healthBefore[targetSlot]);
            target.Health = Math.Max(0, target.Health - State.Rules.DamagePerHit);
            bots[bySlot].DamageDealt += amount;
            events.Add(GameEvent.Damage(targetSlot, bySlot, target.Position, State.Rules.DamagePerHit, target.Health));
        }
        foreach (var bot in bots)
        {
            if (bot.Health <= 0 && bot.Status == BotStatus.Active)
            {
                bot.Status = BotStatus.Destroyed;
                events.Add(GameEvent.Destroyed(bot.Slot, bot.Position));
            }
        }
        return shotThisTick;
    }

    private void ResolveProgrammedShot(
        int slot,
        BotState shooter,
        ShotProgram program,
        List<GameEvent> events,
        List<(int TargetSlot, int BySlot)> pendingHits,
        List<ProjectileTickTraversal> traversals)
    {
        var path = ProgrammedProjectilePath.Trace(
            State.Map,
            shooter.Position,
            shooter.Facing,
            program,
            State.Rules);
        var initialHeading = shooter.Facing
            .ToProjectileHeading()
            .Turned(program.InitialAimOffset);
        var (dx, dy) = initialHeading.Vector();
        var desiredFirstTile = shooter.Position.Offset(dx, dy);
        if (path.Count == 0)
        {
            events.Add(GameEvent.Shot(slot, shooter.Position, desiredFirstTile, null));
            return;
        }

        int launchTiles = Math.Max(1, State.Rules.ProgrammedShotLaunchTiles);
        int enteredCount = Math.Min(launchTiles, path.Count);
        var launchPath = path.Take(enteredCount).ToArray();
        int? hitSlot = null;
        int entered = 0;
        var current = shooter.Position;
        var currentHeading = initialHeading;
        foreach (var next in launchPath)
        {
            currentHeading = ProjectileHeadingExtensions.Between(current, next);
            current = next;
            entered++;
            var victim = State.Bots.FirstOrDefault(b =>
                b.Slot != slot && b.IsActive && b.Position == current);
            if (victim is not null)
            {
                hitSlot = victim.Slot;
                break;
            }
        }

        var traversed = launchPath.Take(entered).ToArray();
        events.Add(GameEvent.Shot(slot, shooter.Position, current, hitSlot));
        int projectileId = State.NextProjectileId++;
        traversals.Add(new ProjectileTickTraversal(
            projectileId,
            slot,
            shooter.Facing,
            shooter.Position,
            traversed,
            currentHeading,
            path));
        if (hitSlot is int hit)
        {
            pendingHits.Add((hit, slot));
            return;
        }

        if (entered >= path.Count)
            return;
        State.Projectiles.Add(new ProjectileState
        {
            Id = projectileId,
            Position = current,
            Direction = shooter.Facing,
            Heading = currentHeading,
            ProgrammedPath = path,
            NextProgrammedPathIndex = entered,
            OwnerSlot = slot,
            TilesTraveled = entered,
        });
    }

    /// <summary>Domination check. Active-control rules use the signed shared limit;
    /// passive rules retain the historical per-bot zone-tick threshold.</summary>
    private int? ComputeDominator(int tick)
    {
        if (!State.Rules.ZoneControl)
            return null;
        if (State.Rules.ActiveZoneControl)
        {
            int limit = State.Rules.EffectiveControlPressureLimit(tick);
            if (limit <= 0)
                return null;
            if (State.ControlPressure >= limit)
                return 0;
            if (State.ControlPressure <= -limit)
                return 1;
            return null;
        }
        if (State.Rules.ZoneDominationTicks <= 0)
            return null;
        var crossed = State.Bots
            .Where(b => b.IsActive && b.ZoneTicks >= State.Rules.ZoneDominationTicks)
            .OrderByDescending(b => b.ZoneTicks)
            .ToList();
        return crossed.Count switch
        {
            0 => null,
            1 => crossed[0].Slot,
            _ => crossed[0].ZoneTicks == crossed[1].ZoneTicks ? null : crossed[0].Slot,
        };
    }

    private MatchResultInfo ComputeResult(int endTick, int? dominator = null)
    {
        var bots = State.Bots;
        var active = bots.Where(b => b.IsActive).ToList();
        int? winnerSlot;
        MatchEndReason reason;

        if (dominator is int dominantSlot && active.Count > 1)
        {
            winnerSlot = dominantSlot;
            reason = MatchEndReason.Domination;
        }
        else if (active.Count == 1)
        {
            winnerSlot = active[0].Slot;
            reason = bots.Any(b => b.Status == BotStatus.Disqualified)
                ? MatchEndReason.Disqualification
                : MatchEndReason.Elimination;
        }
        else if (active.Count == 0)
        {
            winnerSlot = null;
            reason = bots.All(b => b.Status == BotStatus.Disqualified)
                ? MatchEndReason.Disqualification
                : MatchEndReason.Elimination;
        }
        else
        {
            reason = MatchEndReason.MaxTicks;
            // §4.9: objective, then health, then damage, else draw. Passive rules use
            // banked per-bot ticks; active rules use the sign of the shared pressure.
            bool zone = State.Rules.ZoneControl;
            int ObjectiveScore(BotState b) => !zone
                ? 0
                : State.Rules.ActiveZoneControl
                    ? (b.Slot == 0 ? State.ControlPressure : -State.ControlPressure)
                    : b.ZoneTicks;
            var best = active
                .OrderByDescending(ObjectiveScore)
                .ThenByDescending(b => b.Health)
                .ThenByDescending(b => b.DamageDealt)
                .ThenBy(b => b.Slot)
                .ToList();
            winnerSlot =
                ObjectiveScore(best[0]) == ObjectiveScore(best[1])
                && best[0].Health == best[1].Health
                && best[0].DamageDealt == best[1].DamageDealt
                ? null
                : best[0].Slot;
        }

        var perBot = bots.Select(b => new BotMatchResult(
            b.Slot,
            winnerSlot is null ? BotOutcome.Draw : (b.Slot == winnerSlot ? BotOutcome.Win : BotOutcome.Loss),
            b.Health,
            b.DamageDealt,
            b.Faults,
            b.Status,
            State.Rules.ZoneControl && !State.Rules.ActiveZoneControl ? b.ZoneTicks : null)).ToArray();

        return new MatchResultInfo
        {
            WinnerSlot = winnerSlot,
            Reason = reason,
            EndTick = endTick,
            Bots = perBot,
            ControlPressure = State.Rules.ActiveZoneControl ? State.ControlPressure : null,
        };
    }
}
