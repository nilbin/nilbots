using BotArena.Sdk;

/// <summary>
/// RAMPART — the fortress ambusher. Wins by controlling territory: picks strong
/// ground (corner tiles with short sight lanes), pre-aims the corridor the
/// enemy must cross, and punishes entry with first-hit lane control. Shots
/// resolve after moves, so a pre-aimed defender tags anyone striding into the
/// killzone before they can bring their own gun around. Hunts only when the
/// HP ledger or the clock demands it — passivity loses at tick 500.
/// </summary>
public sealed class Rampart : IBot
{
    // ---- map memory ----
    private readonly Dictionary<Position, bool> _map = new();
    private int _minX = int.MaxValue, _maxX = int.MinValue;
    private int _minY = int.MaxValue, _maxY = int.MinValue;

    // ---- enemy tracking ----
    private int? _enemySlot;
    private Position? _enemyLastPos;
    private Direction _enemyLastFacing;
    private int _enemyHealth = 3;
    private int _enemySeenTick = -1000;
    private int _enemyCd;                 // estimated ticks until enemy can shoot
    private bool _enemyMovedLastTick;
    private int _enemyMoveStreak;         // consecutive ticks the enemy has moved
    private int _enemyLastMoveTick = -1000;
    private int _enemyLastShotTick = -1000;
    private int _enemyNearTicks;          // ticks spent orbiting within range 3
    private int _breakOff;                // ticks left of duel break-off maneuver

    // ---- self bookkeeping ----
    private int _prevHealth = 3;
    private Position _prevPos;
    private bool _shotLastTick;
    private BotActionKind _lastKind = BotActionKind.Wait;
    private int _displace;                // emergency relocation ticks left
    private Position? _post;              // chosen fortress tile
    private int _postScore = int.MinValue;

    public BotAction Tick(BotContext ctx)
    {
        BotAction action;
        try
        {
            action = Decide(ctx);
        }
        catch (Exception ex)
        {
            ctx.Debug.Write("ERR {0}", ex.Message);
            action = Actions.Wait();
        }
        _prevHealth = ctx.Health;
        _prevPos = ctx.Position;
        _shotLastTick = action.Kind == BotActionKind.Shoot && ctx.CanShoot;
        _lastKind = action.Kind;
        return action;
    }

    private BotAction Decide(BotContext ctx)
    {
        Observe(ctx);

        var enemy = ctx.VisibleEnemies.Count > 0 ? ctx.VisibleEnemies[0] : null;

        if (ctx.Health < _prevHealth && enemy is null)
        {
            // Hit from beyond vision: this lane is a shooting gallery. Leave it
            // and write the post off.
            _displace = 4;
            _post = null;
            _postScore = int.MinValue;
        }

        if (enemy is not null)
            return Combat(ctx, enemy);

        if (_displace > 0)
        {
            _displace--;
            return DisplaceStep(ctx);
        }

        bool behind = ctx.Health < _enemyHealth;
        bool ahead = ctx.Health > _enemyHealth;
        if (behind || (!ahead && ctx.Tick >= 340))
            return Hunt(ctx);
        return Fortress(ctx);
    }

    // ================= observation =================

    private void Observe(BotContext ctx)
    {
        foreach (var t in ctx.VisibleTiles)
        {
            _map[t.Position] = t.IsWall;
            if (t.Position.X < _minX) _minX = t.Position.X;
            if (t.Position.X > _maxX) _maxX = t.Position.X;
            if (t.Position.Y < _minY) _minY = t.Position.Y;
            if (t.Position.Y > _maxY) _maxY = t.Position.Y;
        }

        if (_enemyCd > 0) _enemyCd--;

        if (ctx.VisibleEnemies.Count > 0)
        {
            var e = ctx.VisibleEnemies[0];
            _enemySlot ??= e.Slot;
            _enemyMovedLastTick =
                _enemyLastPos.HasValue &&
                e.Position != _enemyLastPos.Value &&
                ctx.Tick - _enemySeenTick <= 2;
            _enemyMoveStreak = _enemyMovedLastTick ? _enemyMoveStreak + 1 : 0;
            if (_enemyMovedLastTick) _enemyLastMoveTick = ctx.Tick;
            _enemyNearTicks = ctx.Position.ChebyshevDistance(e.Position) <= 3
                ? _enemyNearTicks + 1
                : 0;
            _enemyLastPos = e.Position;
            _enemyLastFacing = e.Facing;
            _enemyHealth = e.Health;
            _enemySeenTick = ctx.Tick;
        }

        foreach (var ev in ctx.VisibleEvents)
        {
            if (ev.Kind != VisibleEventKind.Shot) continue;
            bool mine = ev.Slot.HasValue && _enemySlot.HasValue
                ? ev.Slot.Value != _enemySlot.Value
                : _shotLastTick && ev.Position == _prevPos;
            if (!mine)
            {
                _enemyCd = 2;
                _enemyLastShotTick = ctx.Tick;
            }
        }
    }

    // ================= combat =================

    private BotAction Combat(BotContext ctx, VisibleEnemy e)
    {
        var m = ctx.Position;
        var ePos = e.Position;

        Direction? dirToE = LaneDir(m, ePos);
        bool alignedClear = dirToE.HasValue && LaneClear(m, ePos);

        Direction? dirEtoM = LaneDir(ePos, m);
        bool enemyAims = dirEtoM.HasValue && e.Facing == dirEtoM.Value && LaneClear(ePos, m);
        bool enemyReady = _enemyCd == 0;

        var (efx, efy) = e.Facing.Vector();
        var stride = ePos.Offset(efx, efy);
        // Only lead a target committed to a sprint, and only at range. Anything
        // slower (lurkers that bait-step, marchers with a move-move-shoot
        // cadence) or anything close must instead eat a reactive hit the tick
        // it enters the lane — keep the gun loaded. At distance <= 2 every
        // adjacent lane is ours already; speculation only spends the reload.
        bool strideLikely = !IsKnownWall(stride) && stride != m &&
                            _enemyMoveStreak >= 3 && m.ChebyshevDistance(ePos) > 2;

        // Break-off in progress: leave the lane during their reload.
        if (_breakOff > 0 && dirEtoM.HasValue)
        {
            _breakOff--;
            bool laneVert = ePos.X == m.X;
            Direction q1 = laneVert ? Direction.East : Direction.North;
            Direction q2 = laneVert ? Direction.West : Direction.South;
            if (ctx.Facing == q1 || ctx.Facing == q2)
            {
                if (!ctx.IsWallAhead() && ctx.Ahead() != ePos)
                {
                    ctx.Debug.Write("break off");
                    return Actions.MoveForward();
                }
            }
            else
            {
                Direction? pick = OpenFrom(m, q1) ? q1 : OpenFrom(m, q2) ? q2 : null;
                if (pick.HasValue)
                {
                    ctx.Debug.Write("break off: wheel");
                    return TurnToward(ctx.Facing, pick.Value);
                }
            }
            _breakOff = 0; // boxed in — fight instead
        }
        else if (_breakOff > 0)
        {
            _breakOff = 0; // lane already broken
        }

        if (ctx.CanShoot)
        {
            if (alignedClear && ctx.Facing == dirToE!.Value)
            {
                // Hold fire on targets my shot cannot reach next resolution:
                // - "leaving": stood in the lane, fired, turned across it — it
                //   is stepping out; a loaded gun punishes its next move.
                // - "transiting": crossing my lane perpendicular at speed with
                //   open ground ahead — it exits during my shot tick, and a
                //   cooldown-ledger duelist harvests exactly that whiff.
                // An arrival that must stop (wall ahead) still gets shot: the
                // free hit this fortress exists for.
                bool perpFacing = e.Facing != dirToE.Value &&
                                  e.Facing != Opposite(dirToE.Value);
                bool leaving = perpFacing && _enemyCd >= 1 && _enemyMoveStreak == 0;
                // A duelist that has been orbiting me dances THROUGH lanes to
                // harvest my whiffs — treat its in-lane moves as transits and
                // stay loaded. A fresh arrival that just closed in came here
                // for me and will stop — shoot it before it finishes turning.
                bool transiting = perpFacing && _enemyMovedLastTick &&
                                  _enemyNearTicks > 8 &&
                                  KnownOpen(stride) && stride != m &&
                                  LaneDir(m, stride) != ctx.Facing;
                bool mutual = enemyAims && enemyReady;
                if (!leaving && !transiting && (!mutual || ctx.Health >= e.Health))
                {
                    ctx.Debug.Write("killzone: fire at {0}", ePos);
                    return Actions.Shoot();
                }
                // behind in a mutual showdown, or target leaving — hold.
            }
            if (strideLikely)
            {
                Direction? dirToStride = LaneDir(m, stride);
                if (dirToStride.HasValue && dirToStride.Value == ctx.Facing && LaneClear(m, stride))
                {
                    ctx.Debug.Write("crossing shot at {0}", stride);
                    return Actions.Shoot();
                }
            }
        }

        // Staring down a loaded gun: leave the lane unless we are winning the trade.
        bool winningTrade = alignedClear && ctx.Facing == dirToE!.Value && ctx.Health > e.Health;
        if (enemyAims && enemyReady && !winningTrade &&
            !(alignedClear && ctx.Facing == dirToE!.Value && ctx.CanShoot && ctx.Health >= e.Health))
        {
            bool laneVertical = ePos.X == m.X;
            Direction p1 = laneVertical ? Direction.East : Direction.North;
            Direction p2 = laneVertical ? Direction.West : Direction.South;
            if (ctx.Facing == p1 || ctx.Facing == p2)
            {
                if (!ctx.IsWallAhead() && ctx.Ahead() != ePos)
                {
                    ctx.Debug.Write("dodge out of lane");
                    return Actions.MoveForward();
                }
                var other = ctx.Facing == p1 ? p2 : p1;
                if (OpenFrom(m, other)) return TurnToward(ctx.Facing, other);
            }
            else if (ctx.Health <= e.Health)
            {
                Direction? pick = OpenFrom(m, p1) ? p1 : OpenFrom(m, p2) ? p2 : null;
                if (pick.HasValue && ctx.Health < e.Health)
                {
                    ctx.Debug.Write("wheel to dodge");
                    return TurnToward(ctx.Facing, pick.Value);
                }
            }
            // No exit — turn the gun on them and make it ugly.
        }

        if (alignedClear && ctx.Facing != dirToE!.Value)
        {
            // Stand-off without a health lead: rotating onto each other only
            // ever trades even — a double kill at 1-1, a reset otherwise. If
            // they are a rotation away from aiming and we can slip off the
            // shared lane this tick (move resolves before shoot), refuse the
            // duel and keep maneuvering for the free hit. Trades happen from
            // ahead only.
            if (ctx.Health <= e.Health && dirEtoM.HasValue)
            {
                int turnsToAim = ((int)dirEtoM.Value - (int)e.Facing + 4) % 4;
                bool theyAlmostAim = turnsToAim == 1 || turnsToAim == 3;
                bool canStepOut = ctx.Facing != dirToE.Value &&
                                  ctx.Facing != Opposite(dirToE.Value) &&
                                  !ctx.IsWallAhead() && ctx.Ahead() != ePos &&
                                  !RayTiles(ePos, e.Facing).Contains(ctx.Ahead());
                if (theyAlmostAim && canStepOut)
                {
                    ctx.Debug.Write("refuse the duel");
                    return Actions.MoveForward();
                }
            }
            return TurnToward(ctx.Facing, dirToE.Value);
        }

        if (alignedClear)
        {
            // A synchronized duel at equal health only ever ends in a double
            // kill. After a mutual exchange, use their reload to break off the
            // lane, then re-ambush for the free hit. Never fight fair.
            if (enemyAims && ctx.Health == e.Health && ctx.Cooldown == 2)
            {
                bool laneVert = ePos.X == m.X;
                Direction q1 = laneVert ? Direction.East : Direction.North;
                Direction q2 = laneVert ? Direction.West : Direction.South;
                Direction? pick = OpenFrom(m, q1) ? q1 : OpenFrom(m, q2) ? q2 : null;
                if (pick.HasValue)
                {
                    _breakOff = 2;
                    ctx.Debug.Write("break off: wheel");
                    return TurnToward(ctx.Facing, pick.Value);
                }
            }
            // Gun on the lane, cooling down. Hold if they come to us; advance if they flee.
            bool fleeing = e.Facing == dirToE!.Value;
            if (fleeing && !ctx.IsWallAhead() && ctx.Ahead() != ePos)
            {
                ctx.Debug.Write("press the lane");
                return Actions.MoveForward();
            }
            ctx.Debug.Write("hold lane, cd {0}", ctx.Cooldown);
            return Actions.Wait();
        }

        // Not aligned. Chase only when the ledger or clock demands; otherwise ambush.
        int dx = ePos.X - m.X, dy = ePos.Y - m.Y;
        bool mustPress = ctx.Health < e.Health || (ctx.Health == e.Health && ctx.Tick >= 340);
        if (mustPress)
        {
            // A marching enemy about to cross one of my lanes gets a trap, not
            // a chase — turn the gun onto the crossing and let it walk in.
            // "Marching" tolerates shoot-pauses: moved within the last 3 ticks.
            var trapAim = ctx.Tick - _enemyLastMoveTick <= 3
                ? PredictedEntryAim(m, ePos, e.Facing)
                : null;
            if (trapAim.HasValue && m.ChebyshevDistance(ePos) <= 6)
            {
                if (ctx.Facing != trapAim.Value) return TurnToward(ctx.Facing, trapAim.Value);
                ctx.Debug.Write("trap set {0}", trapAim.Value);
                return Actions.Wait();
            }
            // Otherwise march to a firing position on the enemy's flank or rear
            // — a tile sharing a clear lane with it, not down its gun barrel.
            var targets = FiringPositions(ePos, e.Facing);
            var avoid = enemyReady ? RayTiles(ePos, e.Facing) : null;
            var step = NextStepTowardAny(ctx, targets, avoid);
            if (step.HasValue)
            {
                // Never step into any lane of a loaded, proven shooter: a
                // camper that ledgers my cooldown kills lane entries on
                // arrival. Stage out of lane and commit inside its reload.
                if (ctx.Facing == step.Value && !ctx.IsWallAhead())
                {
                    var dest = ctx.Ahead();
                    bool loadedShooter = _enemyCd == 0 &&
                                         ctx.Tick - _enemyLastShotTick <= 40;
                    Direction? destLane = LaneDir(dest, ePos);
                    if (loadedShooter && destLane.HasValue && LaneClear(dest, ePos))
                    {
                        ctx.Debug.Write("stage: their gun is loaded");
                        return Actions.Wait();
                    }
                }
                ctx.Debug.Write("press {0}", ePos);
                return StepOrTurn(ctx, step.Value);
            }
        }

        // Pre-aim the lane their trajectory will cross; fall back to the nearest
        // lane. A close enemy that just fired has its facing locked to the old
        // firing lane — noise, not intent. Any other close-quarters turn is a
        // telegraph of where it will stride.
        bool trustTrajectory = _enemyMoveStreak >= 1
            || Math.Max(Math.Abs(dx), Math.Abs(dy)) > 2
            || _enemyCd < 2;
        Direction want = (trustTrajectory ? PredictedEntryAim(ctx.Position, ePos, e.Facing) : null)
            ?? (Math.Abs(dx) <= Math.Abs(dy)
                ? (dy > 0 ? Direction.South : Direction.North)
                : (dx > 0 ? Direction.East : Direction.West));
        if (ctx.Facing != want) return TurnToward(ctx.Facing, want);
        ctx.Debug.Write("pre-aimed {0}", want);
        return Actions.Wait();
    }

    // ================= no-contact modes =================

    private BotAction Hunt(BotContext ctx)
    {
        Position? target = _enemyLastPos;
        if (target.HasValue &&
            (ctx.Position == target.Value || ctx.Position.ChebyshevDistance(target.Value) <= 1))
        {
            _enemyLastPos = null;
            target = null;
        }
        target ??= NearestFrontier(ctx);
        if (target.HasValue)
        {
            var step = NextStepToward(ctx, target.Value, null);
            if (step.HasValue)
            {
                ctx.Debug.Write("hunt -> {0}", target.Value);
                return StepOrTurn(ctx, step.Value);
            }
            _enemyLastPos = null;
        }
        return Patrol(ctx);
    }

    private BotAction Fortress(BotContext ctx)
    {
        if (_post is null || ctx.Tick % 24 == 0)
            ChoosePost(ctx);

        if (_post is null)
            return Explore(ctx);

        if (ctx.Position != _post.Value)
        {
            var step = NextStepToward(ctx, _post.Value, null);
            if (step.HasValue)
            {
                ctx.Debug.Write("to post {0}", _post.Value);
                return StepOrTurn(ctx, step.Value);
            }
            _post = null;
            _postScore = int.MinValue;
            return Explore(ctx);
        }

        var guard = GuardFacing(ctx);
        if (ctx.Facing != guard) return TurnToward(ctx.Facing, guard);
        // Suppressive fire: shots outrange sight. If the guarded corridor runs
        // deeper than vision, an unseen attacker may already be marching down
        // it — a shot every cooldown costs nothing while garrisoned and tags
        // anyone in the lane before the duel starts.
        if (ctx.CanShoot && ctx.Tick - _enemySeenTick >= 8 &&
            CorridorLength(ctx.Position, guard, 12) > 6)
        {
            ctx.Debug.Write("suppressive fire {0}", guard);
            return Actions.Shoot();
        }
        ctx.Debug.Write("garrison {0} watch {1}", ctx.Position, guard);
        return Actions.Wait();
    }

    private void ChoosePost(BotContext ctx)
    {
        Position best = default;
        int bestScore = int.MinValue;
        foreach (var kv in _map)
        {
            if (kv.Value) continue; // wall
            var p = kv.Key;
            int dist = ctx.Position.ChebyshevDistance(p);
            if (dist > 12) continue;
            int walls = 0, exposure = 0;
            foreach (var d in AllDirections)
            {
                var (vx, vy) = d.Vector();
                var n = p.Offset(vx, vy);
                if (IsKnownWall(n)) { walls++; continue; }
                exposure += CorridorLength(p, d, 8);
            }
            if (walls < 2 || walls > 3) continue;
            int score = 6 * walls - dist - exposure / 2;
            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }
        if (bestScore == int.MinValue) return;
        // Hysteresis: do not thrash between similar posts.
        if (_post.HasValue && bestScore <= _postScore + 2 && !IsKnownWall(_post.Value)) return;
        _post = best;
        _postScore = bestScore;
    }

    private Direction GuardFacing(BotContext ctx)
    {
        // Watch the lane the enemy's last-known trajectory will cross; failing
        // that, the direction of the sighting; failing that, the longest
        // corridor — the likeliest avenue of attack.
        if (_enemyLastPos.HasValue && ctx.Tick - _enemySeenTick < 120)
        {
            var predicted = PredictedEntryAim(ctx.Position, _enemyLastPos.Value, _enemyLastFacing);
            if (predicted.HasValue) return predicted.Value;
            int dx = _enemyLastPos.Value.X - ctx.Position.X;
            int dy = _enemyLastPos.Value.Y - ctx.Position.Y;
            Direction toward = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);
            if (CorridorLength(ctx.Position, toward, 8) >= 2) return toward;
        }
        Direction bestDir = ctx.Facing;
        int bestLen = -1;
        foreach (var d in AllDirections)
        {
            int len = CorridorLength(ctx.Position, d, 12);
            if (len > bestLen) { bestLen = len; bestDir = d; }
        }
        return bestDir;
    }

    private BotAction Explore(BotContext ctx)
    {
        var f = NearestFrontier(ctx);
        if (f.HasValue)
        {
            var step = NextStepToward(ctx, f.Value, null);
            if (step.HasValue)
            {
                ctx.Debug.Write("scout {0}", f.Value);
                return StepOrTurn(ctx, step.Value);
            }
        }
        return Patrol(ctx);
    }

    private BotAction Patrol(BotContext ctx)
    {
        if (ctx.PreviousActionResult == ActionResult.Blocked || ctx.IsWallAhead())
            return ctx.Random.NextBool() ? Actions.TurnLeft() : Actions.TurnRight();
        if (ctx.Random.NextInt(0, 8) == 0)
            return ctx.Random.NextBool() ? Actions.TurnLeft() : Actions.TurnRight();
        return Actions.MoveForward();
    }

    private BotAction DisplaceStep(BotContext ctx)
    {
        ctx.Debug.Write("displace");
        // Change both coordinates: move, then wheel perpendicular and move again.
        if (_lastKind == BotActionKind.MoveForward &&
            ctx.PreviousActionResult == ActionResult.Success)
        {
            var left = ctx.Facing.TurnedLeft();
            var right = ctx.Facing.TurnedRight();
            if (OpenFrom(ctx.Position, left)) return Actions.TurnLeft();
            if (OpenFrom(ctx.Position, right)) return Actions.TurnRight();
        }
        if (!ctx.IsWallAhead()) return Actions.MoveForward();
        var l2 = ctx.Facing.TurnedLeft();
        if (OpenFrom(ctx.Position, l2)) return Actions.TurnLeft();
        return Actions.TurnRight();
    }

    // ================= geometry helpers =================

    private static readonly Direction[] AllDirections =
    {
        Direction.North, Direction.East, Direction.South, Direction.West,
    };

    /// <summary>Neighbor order for pathfinding: straight ahead first, so equal
    /// paths do not thrash between turn choices tick over tick.</summary>
    private static Direction[] DirsFacingFirst(Direction facing) => new[]
    {
        facing, facing.TurnedRight(), facing.TurnedLeft(),
        facing.TurnedRight().TurnedRight(),
    };

    private bool IsKnownWall(Position p) => _map.TryGetValue(p, out bool w) && w;

    private bool KnownOpen(Position p) => _map.TryGetValue(p, out bool w) && !w;

    private bool OpenFrom(Position p, Direction d)
    {
        var (dx, dy) = d.Vector();
        return !IsKnownWall(p.Offset(dx, dy));
    }

    /// <summary>
    /// Extrapolate the enemy along its heading; if that path crosses my row or
    /// column with a clear lane between us, return the direction to pre-aim.
    /// </summary>
    private Direction? PredictedEntryAim(Position me, Position ePos, Direction eHeading)
    {
        var (vx, vy) = eHeading.Vector();
        var p = ePos;
        for (int k = 0; k < 10; k++)
        {
            p = p.Offset(vx, vy);
            if (IsKnownWall(p)) break;
            if (p == me) break;
            if (p.X == me.X || p.Y == me.Y)
            {
                var d = LaneDir(me, p);
                if (d.HasValue && LaneClear(me, p)) return d;
                break;
            }
        }
        return null;
    }

    private static Direction? LaneDir(Position from, Position to)
    {
        if (from == to) return null;
        if (from.X == to.X) return to.Y < from.Y ? Direction.North : Direction.South;
        if (from.Y == to.Y) return to.X > from.X ? Direction.East : Direction.West;
        return null;
    }

    private bool LaneClear(Position a, Position b)
    {
        int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
        var p = a.Offset(dx, dy);
        for (int i = 0; i < 100 && p != b; i++)
        {
            if (IsKnownWall(p)) return false;
            p = p.Offset(dx, dy);
        }
        return true;
    }

    private int CorridorLength(Position from, Direction d, int cap)
    {
        var (dx, dy) = d.Vector();
        int len = 0;
        var p = from.Offset(dx, dy);
        while (len < cap)
        {
            if (_map.TryGetValue(p, out bool wall))
            {
                if (wall) break;
            }
            else
            {
                len++; // unknown: assume it keeps going — exposure, not safety
                break;
            }
            len++;
            p = p.Offset(dx, dy);
        }
        return len;
    }

    /// <summary>Tiles that share a clear lane with the enemy, excluding the ray
    /// in front of its gun — flanking and rear firing positions.</summary>
    private HashSet<Position> FiringPositions(Position ePos, Direction eFacing)
    {
        var set = new HashSet<Position>();
        foreach (var d in AllDirections)
        {
            if (d == eFacing) continue;
            var (dx, dy) = d.Vector();
            var p = ePos.Offset(dx, dy);
            for (int i = 0; i < 8 && !IsKnownWall(p); i++)
            {
                set.Add(p);
                p = p.Offset(dx, dy);
            }
        }
        return set;
    }

    private HashSet<Position> RayTiles(Position from, Direction d)
    {
        var set = new HashSet<Position>();
        var (dx, dy) = d.Vector();
        var p = from.Offset(dx, dy);
        for (int i = 0; i < 12 && !IsKnownWall(p); i++)
        {
            set.Add(p);
            p = p.Offset(dx, dy);
        }
        return set;
    }

    private Position? NearestFrontier(BotContext ctx)
    {
        if (_minX > _maxX) return null;
        var visited = new HashSet<Position> { ctx.Position };
        var queue = new Queue<Position>();
        queue.Enqueue(ctx.Position);
        int guard = 0;
        while (queue.Count > 0 && guard++ < 4000)
        {
            var cur = queue.Dequeue();
            foreach (var d in AllDirections)
            {
                var (dx, dy) = d.Vector();
                var n = cur.Offset(dx, dy);
                if (n.X < _minX - 1 || n.X > _maxX + 1 || n.Y < _minY - 1 || n.Y > _maxY + 1)
                    continue;
                if (visited.Contains(n)) continue;
                visited.Add(n);
                if (!_map.ContainsKey(n)) return cur == ctx.Position ? n : cur;
                if (_map[n]) continue;
                queue.Enqueue(n);
            }
        }
        return null;
    }

    private Direction? NextStepTowardAny(BotContext ctx, HashSet<Position> targets, HashSet<Position>? avoid)
    {
        if (targets.Count == 0) return null;
        var dirs = DirsFacingFirst(ctx.Facing);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var use = attempt == 0 ? avoid : null;
            var from = new Dictionary<Position, Position> { [ctx.Position] = ctx.Position };
            var queue = new Queue<Position>();
            queue.Enqueue(ctx.Position);
            int guard = 0;
            Position? goal = null;
            while (queue.Count > 0 && guard++ < 4000)
            {
                var cur = queue.Dequeue();
                if (cur != ctx.Position && targets.Contains(cur)) { goal = cur; break; }
                foreach (var d in dirs)
                {
                    var (dx, dy) = d.Vector();
                    var n = cur.Offset(dx, dy);
                    if (n.X < _minX - 1 || n.X > _maxX + 1 || n.Y < _minY - 1 || n.Y > _maxY + 1)
                        continue;
                    if (from.ContainsKey(n) || IsKnownWall(n)) continue;
                    if (use is not null && !targets.Contains(n) && use.Contains(n)) continue;
                    from[n] = cur;
                    queue.Enqueue(n);
                }
            }
            if (!goal.HasValue) continue;
            var walk = goal.Value;
            while (from[walk] != ctx.Position) walk = from[walk];
            return LaneDir(ctx.Position, walk);
        }
        return null;
    }

    private Direction? NextStepToward(BotContext ctx, Position target, HashSet<Position>? avoid)
    {
        if (ctx.Position == target) return null;
        var dirs = DirsFacingFirst(ctx.Facing);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var use = attempt == 0 ? avoid : null;
            var from = new Dictionary<Position, Position> { [ctx.Position] = ctx.Position };
            var queue = new Queue<Position>();
            queue.Enqueue(ctx.Position);
            int guard = 0;
            bool found = false;
            while (queue.Count > 0 && guard++ < 4000)
            {
                var cur = queue.Dequeue();
                if (cur == target) { found = true; break; }
                foreach (var d in dirs)
                {
                    var (dx, dy) = d.Vector();
                    var n = cur.Offset(dx, dy);
                    if (n.X < _minX - 1 || n.X > _maxX + 1 || n.Y < _minY - 1 || n.Y > _maxY + 1)
                        continue;
                    if (from.ContainsKey(n) || IsKnownWall(n)) continue;
                    if (use is not null && n != target && use.Contains(n)) continue;
                    from[n] = cur;
                    queue.Enqueue(n);
                }
            }
            if (!found) continue;
            var walk = target;
            while (from[walk] != ctx.Position) walk = from[walk];
            return LaneDir(ctx.Position, walk);
        }
        return null;
    }

    private BotAction StepOrTurn(BotContext ctx, Direction step)
    {
        if (ctx.Facing == step)
            return ctx.IsWallAhead() ? Actions.Wait() : Actions.MoveForward();
        return TurnToward(ctx.Facing, step);
    }

    private static BotAction TurnToward(Direction cur, Direction want)
    {
        int diff = ((int)want - (int)cur + 4) % 4;
        return diff == 3 ? Actions.TurnLeft() : Actions.TurnRight();
    }

    private static Direction Opposite(Direction d) => (Direction)(((int)d + 2) % 4);
}
