using System;
using System.Collections.Generic;
using BotArena.Sdk;

/// <summary>
/// Bastille — hill-fortress tactician for the "hill" zone-control experiment.
/// Doctrine: seize the zone early (turn-cost BFS race), entrench facing the
/// likely approach, and win every exchange on tempo: shoot first from flanks,
/// dodge on the enemy's shot ticks, never stand in a pre-aimed lane.
/// </summary>
public sealed class Bastille : IBot
{
    private const int ShotRange = 8;

    // --- map memory ---
    private int _w, _h;
    private bool[,]? _wall;

    // --- zone ---
    private readonly List<Position> _zone = new();
    private readonly HashSet<Position> _zoneSet = new();

    // --- enemy model ---
    private Position _enemyPos;
    private Direction _enemyFacing;
    private int _enemyHealth = 3;
    private int _enemySeenTick = -1000;
    private int _enemyShotTick = -1000;   // tick the enemy last fired (exact, from events)

    private int _tick;
    private int _mySlot;

    // --- anti-deadlock ---
    private BotActionKind _lastKind = BotActionKind.Wait;
    private Position _lastIntendedTile;
    private Position _tempBlockedTile = new(-1, -1);
    private int _tempBlockedUntil = -1;
    private int _lastProgressTick;
    private int _lastMyZ = int.MinValue;
    private int _lastMyHealth, _lastEnemyHealth = 3;
    private bool _hunting;
    private int _huntStartTick = -1000;
    private Direction _aimDir;
    private int _aimUntil = -1;
    private bool _pressureAim = true;
    private int _pressureUntil = -1;

    private BotAction Emit(BotContext ctx, BotAction action)
    {
        _lastKind = action.Kind;
        if (action.Kind == BotActionKind.MoveForward)
            _lastIntendedTile = Step(ctx.Position, ctx.Facing);
        return action;
    }

    public BotAction Tick(BotContext ctx)
    {
        try
        {
            return Emit(ctx, Decide(ctx));
        }
        catch (Exception e)
        {
            ctx.Debug.Write("guard: {0}", e.GetType().Name);
            return Actions.Wait();
        }
    }

    private BotAction Decide(BotContext ctx)
    {
        _tick = ctx.Tick;
        _mySlot = ctx.Slot;
        InitOnce(ctx);
        Observe(ctx);

        Position me = ctx.Position;
        bool onZone = _zoneSet.Contains(me);
        VisibleEnemy? enemy = ctx.VisibleEnemies.Count > 0 ? ctx.VisibleEnemies[0] : null;
        if (enemy is not null)
        {
            _enemyPos = enemy.Position;
            _enemyFacing = enemy.Facing;
            _enemyHealth = enemy.Health;
            _enemySeenTick = _tick;
            _hunting = false; // found them — normal contact logic takes over
        }

        int myZ = ctx.MyZoneTicks ?? 0;
        int eZ = ctx.EnemyZoneTicks ?? 0;
        bool ahead = myZ > eZ;
        bool enemyOnZone = enemy is not null && _zoneSet.Contains(_enemyPos);
        int enemyCd = Math.Max(0, _enemyShotTick + 3 - _tick);  // 0 when unknown = cautious

        // A blocked MoveForward means a simultaneous-move conflict (or unseen
        // wall): blacklist the tile briefly, with randomized expiry so two
        // deterministic bots cannot re-collide forever.
        if (ctx.PreviousActionResult == ActionResult.Blocked
            && _lastKind == BotActionKind.MoveForward)
        {
            _tempBlockedTile = _lastIntendedTile;
            _tempBlockedUntil = _tick + 2 + ctx.Random.NextInt(0, 3);
        }

        // Progress = the zone DIFFERENTIAL improves or somebody bleeds. Anything
        // else for 12+ ticks with contact is a limit cycle: break phase randomly.
        int delta = myZ - eZ;
        if (delta > _lastMyZ || ctx.Health != _lastMyHealth || _enemyHealth != _lastEnemyHealth)
            _lastProgressTick = _tick;
        _lastMyZ = Math.Max(_lastMyZ, delta);
        _lastMyHealth = ctx.Health;
        _lastEnemyHealth = _enemyHealth;
        bool stagnant = enemy is not null && _tick - _lastProgressTick > 12;

        if (enemy is not null)
        {
            // ---------- live contact: tempo duel ----------
            Direction? toEnemy = AlignedDir(me, _enemyPos);
            int dist = Dist(me, _enemyPos);
            bool laneExists = toEnemy is not null && dist <= ShotRange && LaneClear(me, _enemyPos);

            if (laneExists)
            {
                Direction lane = toEnemy!.Value;
                int myTts = Math.Max(ctx.Cooldown, TurnCost(ctx.Facing, lane));
                int enTts = Math.Max(enemyCd, TurnCost(_enemyFacing, Opposite(lane)));
                bool laneHorizontal = _enemyPos.Y == me.Y;

                // Kill shot: take it unless it can only produce a mutual KO while a
                // dodge could still keep the zone win alive.
                if (myTts == 0 && _enemyHealth == 1
                    && !(ctx.Health == 1 && enTts == 0))
                    return Actions.Shoot();

                // Tight contested zone: an off-zone dodge is a BAIT — the leak of
                // exclusive accrual while we loop back is worth more than 1 HP.
                bool refuseBait = onZone && enemyOnZone && ctx.Health >= 2
                    && Math.Abs(myZ - eZ) < 15;

                if (enTts == 0)
                {
                    // Their shot comes THIS tick: a perpendicular step evades it
                    // (moves resolve before shots). Health beats a few zone-ticks.
                    BotAction? dodge = DodgeMove(ctx, me, laneHorizontal, refuseBait);
                    if (dodge is not null)
                        return dodge.Value;
                    if (myTts == 0)
                        return Actions.Shoot(); // can't dodge — at least trade
                    if (TurnCost(ctx.Facing, lane) > 0)
                        return TurnToward(ctx.Facing, lane);
                    return Actions.Wait();
                }

                // Last-HP caution: shooting now leaves us facing down the lane
                // when their answer arrives next tick — prep the dodge instead.
                bool lastHpCaution = ctx.Health == 1 && _enemyHealth > 1 && enTts == 1;
                if (myTts == 0 && !lastHpCaution)
                    return Actions.Shoot(); // they cannot answer before we recover

                if (lastHpCaution)
                {
                    BotAction? evade = PrepDodge(ctx, me, laneHorizontal, enTts, onZone, enemyOnZone);
                    if (evade is not null)
                        return evade.Value;
                    if (myTts == 0)
                        return Actions.Shoot(); // boxed in — take them with us if we can
                }

                // Frozen standoff while we are NOT winning it: poised waiting is a
                // guaranteed loss at tick 500. Force the duel — turn in and fire
                // on cooldown; the dodge logic still answers their actual shots.
                if (stagnant && !ahead)
                {
                    if (TurnCost(ctx.Facing, lane) > 0)
                        return TurnToward(ctx.Facing, lane);
                    return Actions.Wait(); // cooldown runs out, then we shoot
                }

                if (myTts < enTts)
                {
                    // We win the tempo race: bring the gun in.
                    if (TurnCost(ctx.Facing, lane) > 0)
                        return TurnToward(ctx.Facing, lane);
                    return Actions.Wait(); // cooldown expires before their shot
                }

                // They out-tempo us: prep a dodge we can be poised for in time.
                BotAction? prep = PrepDodge(ctx, me, laneHorizontal, enTts, onZone,
                    enemyOnZone, refuseBait);
                if (prep is not null)
                    return prep.Value;
                if (TurnCost(ctx.Facing, lane) > 0)
                    return TurnToward(ctx.Facing, lane); // boxed in: duel anyway
                return Actions.Wait();
            }

            // No lane between us: maneuver. Break mirrored limit cycles first —
            // a random Wait shifts our phase against a deterministic opponent.
            if (stagnant && !ahead && myZ + eZ > 0 && ctx.Random.NextInt(0, 3) == 0)
                return Actions.Wait();

            // Contested and losing the freeze, enemy diagonal-adjacent: alternate
            // two pressure modes on a randomized cadence. AIM-LOCK (hold the tile,
            // gun on a tile they must cross) beats dancers; the BFS flank chase
            // beats turtles. Either alone can be statue-locked or ghost-chased.
            if (onZone && enemyOnZone && !ahead && stagnant && Dist(me, _enemyPos) == 1)
            {
                if (_tick > _pressureUntil)
                {
                    _pressureAim = !_pressureAim;
                    _pressureUntil = _tick + 8 + ctx.Random.NextInt(0, 7);
                }
                if (_pressureAim && AimLock(ctx, me) is BotAction aim)
                    return aim;
            }

            // Evict NOW when they accrue exclusively. A contested freeze while
            // not ahead only becomes an eviction war once it stagnates —
            // premature aggression against a reactive shooter is how double-KOs
            // happen, while patient denial out-scores any bot that ever leaves.
            bool mustEvict = enemyOnZone && (!onZone || (!ahead && stagnant));
            if (mustEvict)
            {
                BotAction? step = PathTo(ctx, (p, f) =>
                        AlignedDir(p, _enemyPos) is Direction d
                        && d == f
                        && Dist(p, _enemyPos) <= ShotRange
                        && LaneClear(p, _enemyPos),
                    AttackTilePenalty);
                if (step is not null)
                    return step.Value;
            }

            if (!onZone)
            {
                // Take (or retake) the hill, preferring tiles out of their lanes.
                BotAction? step = PathTo(ctx, (p, f) => _zoneSet.Contains(p),
                    p => InEnemyLane(p) ? 4 : 0);
                if (step is not null)
                    return step.Value;
            }

            // Entrenched: with the enemy close, hold POISED — face an adjacent
            // zone tile so the reflex dodge lands on the hill instead of
            // stepping off it (being laser-herded off zone loses the count).
            if (onZone && Dist(me, _enemyPos) <= 3
                && PoiseDirection(ctx, me) is Direction poise)
            {
                if (ctx.Facing != poise)
                    return TurnToward(ctx.Facing, poise);
                return Actions.Wait();
            }

            // Otherwise keep the gun trained on their bearing.
            Direction guard = DominantDir(me, _enemyPos);
            if (guard != ctx.Facing)
                return TurnToward(ctx.Facing, guard);
            return Actions.Wait();
        }

        // ---------- no contact: hunt / race / entrench ----------
        // Hidden freeze while strictly behind (split zones: they camp a pod we
        // cannot even see): sitting still is a guaranteed loss — go dig them out.
        bool behindStrict = eZ > myZ;
        if (!behindStrict || _tick - _huntStartTick > 40)
            _hunting = false;
        if (behindStrict && !_hunting && onZone && _tick - _lastProgressTick > 20)
        {
            _hunting = true;
            _huntStartTick = _tick;
        }
        if (_hunting)
        {
            Position from = me;
            BotAction? step = PathTo(ctx, (p, f) => LaneOntoDistantZone(p, f, from), p => 0);
            if (step is not null)
                return step.Value;
        }

        if (!onZone)
        {
            BotAction? step = PathTo(ctx, (p, f) => _zoneSet.Contains(p), p => 0);
            if (step is not null)
                return step.Value;
            return Actions.Wait();
        }

        // Relocate to the best-covered zone tile once the dust has settled.
        Position best = BestZoneTile(me);
        if (best != me && _enemySeenTick < _tick - 4)
        {
            BotAction? step = PathTo(ctx, (p, f) => p == best, p => 0);
            if (step is not null)
                return step.Value;
        }

        Direction guardDir = GuardDirection(me);
        if (guardDir != ctx.Facing)
            return TurnToward(ctx.Facing, guardDir);
        return Actions.Wait();
    }

    // ================= dodging =================

    /// <summary>Immediate perpendicular step out of a firing lane, or null.</summary>
    private BotAction? DodgeMove(BotContext ctx, Position me, bool laneHorizontal,
        bool refuseOffZone)
    {
        bool facingPerp = laneHorizontal
            ? ctx.Facing is Direction.North or Direction.South
            : ctx.Facing is Direction.East or Direction.West;
        if (!facingPerp)
            return null;
        // Ignore the temp-blocked blacklist here: a dodge that bounces costs
        // nothing (Blocked becomes Wait and we were getting shot regardless).
        Position to = Step(me, ctx.Facing);
        if (!Passable(to) || (_enemySeenTick >= _tick - 1 && to == _enemyPos))
            return null;
        if (refuseOffZone && !_zoneSet.Contains(to))
            return null; // eat the bait shot; keep the freeze
        return Actions.MoveForward();
    }

    /// <summary>
    /// Turn perpendicular to the lane toward a free dodge tile we can be POISED at
    /// before the enemy's shot tick (turn cost ≤ their ticks-to-shoot), or null.
    /// </summary>
    private BotAction? PrepDodge(BotContext ctx, Position me, bool laneHorizontal,
        int enTts, bool onZone, bool enemyOnZone, bool refuseOffZone = false)
    {
        Direction[] perp = laneHorizontal
            ? new[] { Direction.North, Direction.South }
            : new[] { Direction.East, Direction.West };
        Direction? pick = null;
        int bestScore = int.MinValue;
        foreach (Direction d in perp)
        {
            if (TurnCost(ctx.Facing, d) > enTts)
                continue;                       // cannot be poised in time
            Position to = Step(me, d);
            if (!Free(to))
                continue;
            if (refuseOffZone && !_zoneSet.Contains(to))
                continue;                       // never PREP a bait-dodge either
            int score = 0;
            if (_zoneSet.Contains(to))
                score += 4;                     // dodge within the zone keeps accrual
            else if (onZone && enemyOnZone)
                score -= 3;                     // stepping off feeds their accrual
            if (d == ctx.Facing)
                score += 2;                     // already poised: zero prep cost
            if (!InEnemyLane(to))
                score += 1;
            if (score > bestScore)
            {
                bestScore = score;
                pick = d;
            }
        }
        if (pick is null)
            return null;
        if (ctx.Facing == pick.Value)
            return Actions.Wait();              // poised: dodge fires on their shot tick
        return TurnToward(ctx.Facing, pick.Value);
    }

    /// <summary>
    /// Stationary gun pressure on a diagonal-adjacent enemy: face one of the two
    /// tiles adjacent to both of us (sticky, re-rolled on a random cadence) so any
    /// transit through it meets a hot pre-aimed gun. Null when both are walls.
    /// </summary>
    private BotAction? AimLock(BotContext ctx, Position me)
    {
        Direction dirV = _enemyPos.Y > me.Y ? Direction.South : Direction.North;
        Direction dirH = _enemyPos.X > me.X ? Direction.East : Direction.West;
        bool vOk = !KnownWall(new Position(me.X, _enemyPos.Y));
        bool hOk = !KnownWall(new Position(_enemyPos.X, me.Y));
        if (!vOk && !hOk)
            return null;
        bool currentValid = (_aimDir == dirV && vOk) || (_aimDir == dirH && hOk);
        if (_tick > _aimUntil || !currentValid)
        {
            _aimDir = vOk && hOk ? (ctx.Random.NextBool() ? dirV : dirH) : (vOk ? dirV : dirH);
            _aimUntil = _tick + 4 + ctx.Random.NextInt(0, 6);
        }
        if (ctx.Facing != _aimDir)
            return TurnToward(ctx.Facing, _aimDir);
        return Actions.Wait();
    }

    /// <summary>
    /// Facing that keeps a dodge-step onto another ZONE tile ready, preferring the
    /// axis perpendicular to the lane the enemy can form soonest. Null when no
    /// adjacent zone tile is free.
    /// </summary>
    private Direction? PoiseDirection(BotContext ctx, Position me)
    {
        int adx = Math.Abs(_enemyPos.X - me.X);
        int ady = Math.Abs(_enemyPos.Y - me.Y);
        // Smaller offset = the axis they align on soonest. Vertical lane (same X)
        // is dodged by E/W steps; horizontal by N/S.
        bool verticalLaneSoonest = adx <= ady;
        Direction? pick = null;
        int bestScore = int.MinValue;
        foreach (Direction d in Dirs)
        {
            Position to = Step(me, d);
            if (!_zoneSet.Contains(to) || !Free(to))
                continue;
            int score = 0;
            bool perpToSoonest = verticalLaneSoonest
                ? d is Direction.East or Direction.West
                : d is Direction.North or Direction.South;
            if (perpToSoonest)
                score += 4;
            if (Dist(to, _enemyPos) >= 2)
                score += 3;                     // a tile they cannot contest next tick
            else if (Dist(to, _enemyPos) <= 1 && Dist(me, _enemyPos) <= 1)
                score -= 2;                     // fighting over their doorstep
            if (d == ctx.Facing)
                score += 1;                     // avoid pointless churn
            if (score > bestScore)
            {
                bestScore = score;
                pick = d;
            }
        }
        return pick;
    }

    /// <summary>Score attack tiles: flank shots beat face shots, zone beats off-zone.</summary>
    private int AttackTilePenalty(Position p)
    {
        int penalty = 0;
        Direction enemyToP = AlignedDir(_enemyPos, p) ?? DominantDir(_enemyPos, p);
        int theirTurns = TurnCost(_enemyFacing, enemyToP);
        if (theirTurns == 0)
            penalty += 8;                       // walking into their gun
        else if (theirTurns == 1)
            penalty += 2;
        if (!_zoneSet.Contains(p))
            penalty += 5;
        if (Dist(p, _enemyPos) == 1)
            penalty += 1;                       // point-blank leaves no dodge room
        return penalty;
    }

    // ================= perception =================

    private void InitOnce(BotContext ctx)
    {
        if (_wall is not null)
            return;
        _w = Math.Max(1, ctx.MapWidth);
        _h = Math.Max(1, ctx.MapHeight);
        _wall = new bool[_w, _h];
        if (ctx.ZoneTiles is not null)
        {
            foreach (Position z in ctx.ZoneTiles)
            {
                _zone.Add(z);
                _zoneSet.Add(z);
            }
        }
    }

    private void Observe(BotContext ctx)
    {
        foreach (VisibleTile t in ctx.VisibleTiles)
        {
            if (InBounds(t.Position))
                _wall![t.Position.X, t.Position.Y] = t.IsWall;
        }

        foreach (VisibleEvent e in ctx.VisibleEvents)
        {
            if (e.Slot is int slot && slot != _mySlot)
            {
                if (e.Kind == VisibleEventKind.Shot)
                {
                    _enemyShotTick = _tick - 1;
                    if (_enemySeenTick < _tick - 1)
                    {
                        _enemyPos = e.Position; // shot origin = their tile last tick
                        _enemySeenTick = _tick - 1;
                    }
                }
                else if ((e.Kind == VisibleEventKind.Move || e.Kind == VisibleEventKind.Turn
                          || e.Kind == VisibleEventKind.MoveBlocked)
                         && _enemySeenTick < _tick - 1)
                {
                    _enemyPos = e.Position;
                    _enemySeenTick = _tick - 1;
                }
            }
        }
    }

    // ================= geometry =================

    private static int Dist(Position a, Position b) => a.ChebyshevDistance(b);

    private static Position Step(Position p, Direction d)
    {
        (int dx, int dy) = d.Vector();
        return p.Offset(dx, dy);
    }

    private static Direction Opposite(Direction d) => (Direction)(((int)d + 2) % 4);

    private static int TurnCost(Direction from, Direction to)
    {
        int diff = Math.Abs((int)from - (int)to) % 4;
        return diff == 3 ? 1 : diff;
    }

    /// <summary>Direction from a to b when axis-aligned (and not equal), else null.</summary>
    private static Direction? AlignedDir(Position a, Position b)
    {
        if (a.X == b.X && a.Y != b.Y)
            return b.Y > a.Y ? Direction.South : Direction.North;
        if (a.Y == b.Y && a.X != b.X)
            return b.X > a.X ? Direction.East : Direction.West;
        return null;
    }

    private static Direction DominantDir(Position from, Position to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    private static BotAction TurnToward(Direction from, Direction to)
    {
        if (from.TurnedRight() == to)
            return Actions.TurnRight();
        if (from.TurnedLeft() == to)
            return Actions.TurnLeft();
        return Actions.TurnRight(); // 180: either way, two ticks
    }

    private bool InBounds(Position p) => p.X >= 0 && p.X < _w && p.Y >= 0 && p.Y < _h;

    private bool KnownWall(Position p) => InBounds(p) && _wall![p.X, p.Y];

    private bool Passable(Position p) => InBounds(p) && !_wall![p.X, p.Y];

    /// <summary>Passable and not recently contested by a simultaneous-move conflict.</summary>
    private bool Free(Position p) =>
        Passable(p)
        && !(p == _tempBlockedTile && _tick <= _tempBlockedUntil)
        && !(_enemySeenTick >= _tick - 1 && p == _enemyPos);

    /// <summary>No KNOWN wall strictly between axis-aligned a and b.</summary>
    private bool LaneClear(Position a, Position b)
    {
        if (AlignedDir(a, b) is not Direction d)
            return false;
        Position cur = Step(a, d);
        while (cur != b)
        {
            if (KnownWall(cur))
                return false;
            cur = Step(cur, d);
        }
        return true;
    }

    /// <summary>A firing position onto some zone tile well away from where we sit —
    /// the hunt target when an unseen camper freezes the score from another pod.</summary>
    private bool LaneOntoDistantZone(Position p, Direction f, Position from)
    {
        foreach (Position z in _zone)
        {
            if (Dist(from, z) < 3)
                continue;
            if (AlignedDir(p, z) is Direction d && d == f
                && Dist(p, z) <= ShotRange && LaneClear(p, z))
                return true;
        }
        return false;
    }

    private bool InEnemyLane(Position p) =>
        _enemySeenTick >= _tick - 2
        && AlignedDir(_enemyPos, p) is not null
        && Dist(_enemyPos, p) <= ShotRange
        && LaneClear(_enemyPos, p);

    // ================= holding =================

    private static readonly Direction[] Dirs =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    private int CoverScore(Position p)
    {
        int score = 0;
        foreach (Direction d in Dirs)
        {
            Position n = Step(p, d);
            if (!InBounds(n) || KnownWall(n))
                score++; // a wall (or arena edge) closes that whole firing axis
        }
        return score;
    }

    private Position BestZoneTile(Position me)
    {
        Position best = me;
        int bestScore = _zoneSet.Contains(me) ? CoverScore(me) : -1;
        foreach (Position z in _zone)
        {
            int s = CoverScore(z);
            if (s > bestScore)
            {
                best = z;
                bestScore = s;
            }
        }
        return best;
    }

    private Direction GuardDirection(Position me)
    {
        if (_enemySeenTick >= _tick - 40)
            return DominantDir(me, _enemyPos);
        Direction best = Direction.North;
        int bestLen = -1;
        foreach (Direction d in Dirs)
        {
            int len = 0;
            Position cur = Step(me, d);
            while (InBounds(cur) && !KnownWall(cur) && len < ShotRange)
            {
                len++;
                cur = Step(cur, d);
            }
            if (len > bestLen)
            {
                bestLen = len;
                best = d;
            }
        }
        return best;
    }

    // ================= pathfinding =================

    /// <summary>
    /// Dijkstra over (position, facing) with unit action costs (turn/move) plus a
    /// per-goal-tile penalty. Returns the first action of the cheapest path to a
    /// goal state, or null when unreachable or already standing on a zero-penalty goal.
    /// </summary>
    private BotAction? PathTo(BotContext ctx, Func<Position, Direction, bool> goal,
        Func<Position, int> goalPenalty)
    {
        int W = _w, H = _h;
        int StateId(int x, int y, int f) => ((y * W) + x) * 4 + f;
        var dist = new int[W * H * 4];
        for (int i = 0; i < dist.Length; i++)
            dist[i] = int.MaxValue;
        var firstAct = new BotAction?[W * H * 4];

        Position start = ctx.Position;
        int startId = StateId(start.X, start.Y, (int)ctx.Facing);
        dist[startId] = 0;
        var pq = new PriorityQueue<int, int>();
        pq.Enqueue(startId, 0);

        int bestGoalCost = int.MaxValue;
        BotAction? bestFirst = null;
        if (goal(start, ctx.Facing) && goalPenalty(start) == 0)
            return null; // already there — caller decides what "stay" means

        while (pq.TryDequeue(out int id, out int d0))
        {
            if (d0 > dist[id] || d0 >= bestGoalCost || d0 > 200)
                continue;
            int f = id % 4;
            int cell = id / 4;
            int x = cell % W, y = cell / W;
            var p = new Position(x, y);
            var dir = (Direction)f;

            if ((p != start || dir != ctx.Facing) && goal(p, dir))
            {
                int total = d0 + goalPenalty(p);
                if (total < bestGoalCost && firstAct[id] is not null)
                {
                    bestGoalCost = total;
                    bestFirst = firstAct[id];
                }
                continue;
            }

            foreach ((Direction nd, BotAction act) in new[]
                     {
                         (dir.TurnedLeft(), Actions.TurnLeft()),
                         (dir.TurnedRight(), Actions.TurnRight()),
                     })
            {
                int nid = StateId(x, y, (int)nd);
                if (d0 + 1 < dist[nid])
                {
                    dist[nid] = d0 + 1;
                    firstAct[nid] = firstAct[id] ?? act;
                    pq.Enqueue(nid, d0 + 1);
                }
            }

            Position np = Step(p, dir);
            if (Free(np) && !(_enemySeenTick >= _tick - 2 && np == _enemyPos))
            {
                // Walking through the enemy's gun lines is how head-on trades
                // happen: tax lane tiles, hardest right in front of their muzzle.
                int stepCost = 1;
                if (_enemySeenTick >= _tick - 2 && InEnemyLane(np))
                {
                    stepCost += 2;
                    if (AlignedDir(_enemyPos, np) == _enemyFacing)
                        stepCost += 2;
                }
                int nid = StateId(np.X, np.Y, f);
                if (d0 + stepCost < dist[nid])
                {
                    dist[nid] = d0 + stepCost;
                    firstAct[nid] = firstAct[id] ?? Actions.MoveForward();
                    pq.Enqueue(nid, d0 + stepCost);
                }
            }
        }

        return bestFirst;
    }
}
