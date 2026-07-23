namespace BotArena.Engine;

public sealed record BotTickResolution(
    int Slot, BotAction ChosenAction, BotAction ValidatedAction, ActionResult Result, bool Faulted);

public sealed class TickResult
{
    public required int Tick { get; init; }
    public required IReadOnlyList<BotTickResolution> Bots { get; init; }
    public required IReadOnlyList<GameEvent> Events { get; init; }
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
        // Quiet events (Turn/Move/MoveBlocked) stay sight-gated always.
        var events = _lastTickEvents
            .Where(e => e.ReferencePositions().Any(visibleSet.Contains))
            .ToArray();
        List<HeardSound>? heard = null;
        if (State.Rules.HearingRadius > 0)
        {
            heard = [];
            foreach (var e in _lastTickEvents)
            {
                if (!IsLoud(e.Type) || e.ReferencePositions().Any(visibleSet.Contains))
                    continue;
                // The loudest (nearest) reference position defines what is heard.
                Position source = default;
                int nearest = int.MaxValue;
                foreach (var p in e.ReferencePositions())
                {
                    int d = bot.Position.ChebyshevDistance(p);
                    if (d < nearest)
                    {
                        nearest = d;
                        source = p;
                    }
                }
                if (nearest > State.Rules.HearingRadius)
                    continue;
                heard.Add(new HeardSound(
                    e.Type, Hearing.BearingOctant(bot.Position, source), Hearing.DistanceBand(nearest)));
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
            MyZoneTicks = State.Rules.ZoneControl ? bot.ZoneTicks : null,
            EnemyZoneTicks = State.Rules.ZoneControl
                ? State.Bots.Where(b => b.Slot != slot).Select(b => b.ZoneTicks).FirstOrDefault()
                : null,
            PreviousActionResult = bot.LastActionResult,
            VisibleTiles = tiles,
            VisibleEnemies = enemies,
            VisibleProjectiles = State.Rules.ProjectileTicksPerTile > 0
                ? State.Projectiles
                    .Where(p => visibleSet.Contains(p.Position))
                    .Select(p => new ObservedProjectile(p.Position, p.Direction, p.OwnerSlot,
                        State.Rules.ProjectileTicksPerTile - p.Phase,
                        State.Rules.ShotRange > 0 ? State.Rules.ShotRange - p.TilesTraveled : -1))
                    .ToArray()
                : null,
            VisibleEvents = events,
            HeardSounds = heard,
        };
    }

    private static bool IsLoud(GameEventType type) => type is GameEventType.Shot
        or GameEventType.Damage or GameEventType.Destroyed or GameEventType.Disqualified;

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
        if (State.Rules.ProjectileTicksPerTile > 0 && State.Projectiles.Count > 0)
            AdvanceProjectiles(pendingHits);

        // 6.–7. Resolve shooting from post-movement state; apply damage simultaneously
        // (bolt occupancy hits land in the same simultaneous batch).
        var shotThisTick = ResolveShooting(validated, events, pendingHits);

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

        // 9.5 Zone control: active bots standing on zone tiles accrue at end of tick
        // (post-move, post-damage: a bot destroyed this tick earns nothing). Under
        // exclusive accrual a contested zone pays nobody — sole occupancy is the point.
        if (State.Rules.ZoneControl)
        {
            var occupants = bots.Where(b => b.IsActive && _zoneLookup.Contains(b.Position)).ToList();
            if (!State.Rules.ZoneExclusiveAccrual || occupants.Count == 1)
                foreach (var bot in occupants)
                    bot.ZoneTicks++;
        }

        // 10. Determine completion.
        int executedTick = State.Tick;
        State.Tick++;
        bool anyInactive = bots.Any(b => !b.IsActive);
        int? dominator = ComputeDominator();
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
            MatchCompleted = IsCompleted,
        };
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
    /// §B). Runs after movement, before new shots. Occupancy is checked BOTH before and
    /// after a bolt advances: without the pre-advance check, stepping onto a bolt's tile
    /// on exactly its advance tick is safe — the phase-surfing gap (§H item 2). Bolts
    /// despawn on walls, hits, or after ShotRange tiles.</summary>
    private void AdvanceProjectiles(List<(int TargetSlot, int BySlot)> pendingHits)
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
            bool despawn = false;
            if (bolt.Phase >= State.Rules.ProjectileTicksPerTile)
            {
                bolt.Phase = 0;
                var (dx, dy) = bolt.Direction.Vector();
                var next = bolt.Position.Offset(dx, dy);
                if (State.Map.IsWall(next))
                    despawn = true;
                else
                {
                    bolt.Position = next;
                    bolt.TilesTraveled++;
                    if (State.Rules.ShotRange > 0 && bolt.TilesTraveled >= State.Rules.ShotRange)
                        despawn = true; // lethal on this, its final, tile — checked below
                }
            }
            if (FindBoltVictim(bolt) is { } victim)
            {
                pendingHits.Add((victim.Slot, bolt.OwnerSlot));
                continue;
            }
            if (!despawn)
                alive.Add(bolt);
        }
        State.Projectiles.Clear();
        State.Projectiles.AddRange(alive);
    }

    private BotState? FindBoltVictim(ProjectileState bolt) =>
        State.Bots.FirstOrDefault(b =>
            b.Slot != bolt.OwnerSlot && b.IsActive && b.Position == bolt.Position);

    private bool[] ResolveShooting(BotAction[] validated, List<GameEvent> events,
        List<(int TargetSlot, int BySlot)> pendingHits)
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
                if (pointBlank is not null)
                    pendingHits.Add((pointBlank.Slot, slot));
                else
                    State.Projectiles.Add(new ProjectileState
                    {
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

    /// <summary>Domination check (RULES-0.3-DESIGN §C): the sole active bot at or above
    /// the threshold wins; if both cross simultaneously the higher total wins and equal
    /// totals play on (they resolve by the MaxTicks zone tiebreak at the latest).</summary>
    private int? ComputeDominator()
    {
        if (!State.Rules.ZoneControl || State.Rules.ZoneDominationTicks <= 0)
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
            // §4.9: more health wins, then more damage dealt, else draw. Under zone
            // control the zone comes FIRST — a turtled health lead loses to the bot on
            // the hill; that ordering is the anti-entrenchment teeth (0.3-DESIGN §C).
            bool zone = State.Rules.ZoneControl;
            var best = active
                .OrderByDescending(b => zone ? b.ZoneTicks : 0)
                .ThenByDescending(b => b.Health)
                .ThenByDescending(b => b.DamageDealt)
                .ThenBy(b => b.Slot)
                .ToList();
            winnerSlot =
                (!zone || best[0].ZoneTicks == best[1].ZoneTicks)
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
            State.Rules.ZoneControl ? b.ZoneTicks : null)).ToArray();

        return new MatchResultInfo
        {
            WinnerSlot = winnerSlot,
            Reason = reason,
            EndTick = endTick,
            Bots = perBot,
        };
    }
}
