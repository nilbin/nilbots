using System;
using BotArena.Sdk;

/// <summary>
/// Warden — adaptive zone controller.
/// Holds lanes and punishes approaches, but adapts: it tracks the enemy's shot
/// cooldown from visible events to avoid suicidal head-on trades, chases
/// opponents that refuse to engage, and turtles when ahead on health
/// (a tick-500 health tie-break win is still a win).
/// </summary>
public sealed class Warden : IBot
{
    private Position? _lastSeenPos;
    private Direction _lastSeenFacing;
    private bool _hasFacingIntel;
    private int _lastSeenTick = -1000;
    private int _enemyHealth = 3;
    private int _enemySlot = -1;
    private int _enemyCooldown;          // our estimate of their shot cooldown
    private int? _prevDist;
    private int _approach;               // >0 enemy tends to approach, <0 it retreats
    private int _stall;                  // consecutive sightings with no distance change
    private bool _hunting;               // sticky stance: false = guard, true = hunt
    private int _alignAxis;              // sticky lane choice: 0 none, 1 close dx, 2 close dy
    private int _exploreTurnBias = 1;    // +1 right, -1 left
    private Direction? _dodgeCommit;     // mid-dodge: finish the move, no rethink
    private int _visNoShoot;             // ticks with enemy visible but gun silent
    private int _forceChaseUntil = -1;   // cycle breaker: bulldoze straight at them

    public BotAction Tick(BotContext context)
    {
        try
        {
            return Decide(context);
        }
        catch
        {
            return Actions.Wait(); // never crash: 3 faults = disqualification
        }
    }

    private BotAction Decide(BotContext ctx)
    {
        TrackEnemyCooldown(ctx);
        VisibleEnemy? enemy = ctx.VisibleEnemies.Count > 0 ? ctx.VisibleEnemies[0] : null;

        if (enemy is not null)
        {
            Observe(ctx, enemy);
            return Combat(ctx, enemy);
        }

        return NoContact(ctx);
    }

    private void TrackEnemyCooldown(BotContext ctx)
    {
        if (_enemyCooldown > 0) _enemyCooldown--;
        foreach (var ev in ctx.VisibleEvents)
        {
            if (ev.Kind == VisibleEventKind.Shot && ev.Slot is int s && s == _enemySlot)
                _enemyCooldown = 2; // they fired last tick: silent for the next 2
        }
    }

    private void Observe(BotContext ctx, VisibleEnemy enemy)
    {
        _lastSeenPos = enemy.Position;
        _lastSeenFacing = enemy.Facing;
        _hasFacingIntel = true;
        _lastSeenTick = ctx.Tick;
        _enemyHealth = enemy.Health;
        _enemySlot = enemy.Slot;

        int dist = ctx.Position.ChebyshevDistance(enemy.Position);
        if (_prevDist is int prev)
        {
            if (dist < prev) _approach = Math.Min(_approach + 2, 6);
            else if (dist > prev) _approach = Math.Max(_approach - 2, -6);
            _stall = dist == prev ? _stall + 1 : 0;
        }
        _prevDist = dist;
    }

    private BotAction Combat(BotContext ctx, VisibleEnemy enemy)
    {
        Position me = ctx.Position;
        Position ep = enemy.Position;
        bool aligned = (me.X == ep.X || me.Y == ep.Y) && LineClear(ctx, me, ep);
        bool enemyFacesMe = aligned && DirExact(ep, me) == enemy.Facing;
        bool enemyGunLive = _enemyCooldown == 0;

        // A committed dodge finishes no matter what — rethinking mid-dodge
        // just eats shots while spinning in place.
        if (_dodgeCommit is Direction commit)
        {
            _dodgeCommit = null;
            var (cdx, cdy) = commit.Vector();
            if (ctx.Facing == commit && !ctx.IsWall(ctx.Position.Offset(cdx, cdy)))
                return Actions.MoveForward();
        }

        if (aligned)
        {
            _alignAxis = 0;
            Direction want = DirExact(me, ep);

            if (ctx.Facing == want && ctx.CanShoot)
            {
                // A live gun on a lane fires. Refusing a head-on trade only
                // donates a free hit — the enemy shoots the tick we turn away.
                ctx.Debug.Write("firing at {0} (ecd {1})", ep, _enemyCooldown);
                _visNoShoot = 0;
                return Actions.Shoot();
            }

            if (ctx.Facing == want)
            {
                // Gun down: hold the lane — leaving it throws away tempo, and
                // an imminent shot cannot be outrun anyway (turning is not
                // moving). The single true dodge window: our cd 2, theirs 1 —
                // turn now, and our move resolves exactly on their shot tick
                // (moves resolve before shots), so their volley whiffs.
                if (enemyFacesMe && ctx.Cooldown == 2 && _enemyCooldown == 1)
                {
                    var evade = StartDodge(ctx, want);
                    if (evade is not null) return evade.Value;
                }
                return Actions.Wait();
            }

            // Rotate onto the lane and be ready to fire next tick.
            return TurnTowards(ctx.Facing, want);
        }

        // ---- Not aligned ----
        // Kiters that strafe across our lane get shot mid-stride: moves
        // resolve before shots, so firing at their crossing tile connects.
        if (CrossingShot(ctx, enemy))
        {
            ctx.Debug.Write("crossing shot at {0}", ep);
            _visNoShoot = 0;
            return Actions.Shoot();
        }

        // Two deterministic bots can phase-lock into an orbit where neither
        // ever lines up (Phantom kited us into one for 350 ticks). If they
        // stay visible but our gun stays silent, bulldoze straight at them —
        // direct pursuit collapses the distance and breaks the cycle.
        _visNoShoot++;
        if (_visNoShoot > 20)
        {
            _visNoShoot = 0;
            _forceChaseUntil = ctx.Tick + 30;
        }
        if (ctx.Tick < _forceChaseUntil)
            return MoveTowards(ctx, ep);

        // Sticky stance with explicit switch conditions: flip-flopping between
        // guard-turns and hunt-turns every tick is a deadlock, not a strategy.
        bool enemyRetreating = _approach < 0 || FacesAway(ep, enemy.Facing, me);
        if (ctx.Health < _enemyHealth || enemyRetreating || _stall > 6
            || (ctx.Tick > 300 && ctx.Health <= _enemyHealth))
            _hunting = true;  // behind, stalled, or drawing late: go get them
        else if (_approach >= 2 && ctx.Health >= _enemyHealth && ctx.Tick <= 300)
            _hunting = false; // they are coming to us: set the ambush

        if (!_hunting)
        {
            // Zone control: pre-aim at the lane they will enter. Their facing
            // shows which offset they are closing; they will land on the other
            // axis — have the gun already there so their arrival is a free hit.
            Direction guard = PredictIntercept(me, ep, enemy.Facing);
            if (ctx.Facing == guard) return Actions.Wait();
            return TurnTowards(ctx.Facing, guard);
        }

        // Hunt: get on a firing lane, preferring the axis their gun is not on.
        return StepToAlign(ctx, ep, enemy.Facing);
    }

    /// <summary>Facing already points where the enemy is likely to cross our fire.</summary>
    private static bool IsInterceptFacing(Direction facing, Position me, Position ep)
    {
        int dx = ep.X - me.X, dy = ep.Y - me.Y;
        return facing switch
        {
            Direction.East => dx > 0,
            Direction.West => dx < 0,
            Direction.South => dy > 0,
            Direction.North => dy < 0,
            _ => false,
        };
    }

    private BotAction NoContact(BotContext ctx)
    {
        _visNoShoot = 0;
        int sinceSeen = ctx.Tick - _lastSeenTick;

        // Late game without a health lead: a timeout is at best a draw.
        // Hunt the last known position relentlessly instead of settling.
        if (ctx.Tick > 300 && ctx.Health <= _enemyHealth
            && _lastSeenPos is Position lateLast && sinceSeen < 150
            && !ctx.Position.Equals(lateLast))
        {
            return MoveTowards(ctx, lateLast);
        }

        // Ahead on health: turtle and scan; a timeout win is still a win.
        if (ctx.Health > _enemyHealth)
        {
            if (ctx.Tick % 3 == 0) return Actions.TurnRight();
            return Actions.Wait();
        }

        // Opening: hold ground with the gun up and make them come to us.
        // Whoever wanders into a guarded lane eats the first unanswered hit.
        if (_lastSeenTick < 0 && ctx.Tick < 80)
            return GuardScan(ctx);

        // Recently lost sight of a FLEEING enemy: pursue where they were
        // heading. But if they were closing in, do not chase shadows — hold
        // this ground, pre-aim the lane they will come from, and ambush.
        if (_lastSeenPos is Position last && sinceSeen < 40)
        {
            if (_approach < 0)
            {
                Position target = last;
                if (_hasFacingIntel)
                {
                    var (fdx, fdy) = _lastSeenFacing.Vector();
                    target = last.Offset(fdx * 2, fdy * 2);
                }
                if (!ctx.Position.Equals(target))
                    return MoveTowards(ctx, target);
            }
            else
            {
                Direction guard = _hasFacingIntel
                    ? PredictIntercept(ctx.Position, last, _lastSeenFacing)
                    : GuardDirection(ctx.Position, last);
                if (ctx.Facing == guard) return Actions.Wait();
                return TurnTowards(ctx.Facing, guard);
            }
        }

        // Early game or stale intel: patrol with a scanning sweep.
        return Explore(ctx);
    }

    /// <summary>Hold position, sweeping the gun across open approach lanes.</summary>
    private BotAction GuardScan(BotContext ctx)
    {
        if (ctx.Tick % 3 != 0) return Actions.Wait();
        // Rotate to the next direction with an open lane (skip wall-faces).
        Direction next = ctx.Facing.TurnedRight();
        for (int i = 0; i < 3; i++)
        {
            var (dx, dy) = next.Vector();
            if (!ctx.IsWall(ctx.Position.Offset(dx, dy))) break;
            next = next.TurnedRight();
        }
        return TurnTowards(ctx.Facing, next);
    }

    private BotAction Explore(BotContext ctx)
    {
        if (ctx.PreviousActionResult == ActionResult.Blocked || ctx.IsWallAhead())
        {
            if (ctx.Random.NextInt(0, 4) == 0) _exploreTurnBias = -_exploreTurnBias;
            return _exploreTurnBias > 0 ? Actions.TurnRight() : Actions.TurnLeft();
        }
        // Periodic sweep so nothing sneaks up while we roam.
        if (ctx.Tick % 13 == 0) return Actions.TurnRight();
        if (ctx.Random.NextInt(0, 10) == 0)
            return _exploreTurnBias > 0 ? Actions.TurnRight() : Actions.TurnLeft();
        return Actions.MoveForward();
    }

    private BotAction MoveTowards(BotContext ctx, Position target)
    {
        Direction want = DirTowards(ctx.Position, target);
        if (ctx.Facing == want)
        {
            if (ctx.IsWallAhead() || ctx.PreviousActionResult == ActionResult.Blocked)
                return _exploreTurnBias > 0 ? Actions.TurnRight() : Actions.TurnLeft();
            return Actions.MoveForward();
        }
        return TurnTowards(ctx.Facing, want);
    }

    /// <summary>
    /// Step so one coordinate matches the enemy's (gets on a firing lane).
    /// The lane axis is chosen once and committed — recomputing it from the
    /// enemy's per-tick facing lets a spinning opponent puppet us in place.
    /// </summary>
    private BotAction StepToAlign(BotContext ctx, Position ep, Direction enemyFacing)
    {
        Position me = ctx.Position;
        int dx = ep.X - me.X, dy = ep.Y - me.Y;
        if (_alignAxis == 1 && dx == 0) _alignAxis = 0;
        if (_alignAxis == 2 && dy == 0) _alignAxis = 0;
        if (_alignAxis == 0)
        {
            // Prefer the lane their gun does not currently cover; a vertical
            // lane (same X) is safe from an E/W gun, horizontal from N/S.
            bool enemyGuardsRows = enemyFacing is Direction.East or Direction.West;
            if (enemyGuardsRows && dx != 0) _alignAxis = 1;
            else if (!enemyGuardsRows && dy != 0) _alignAxis = 2;
            else _alignAxis = (Math.Abs(dx) <= Math.Abs(dy) && dx != 0) ? 1 : 2;
        }

        Direction want = _alignAxis == 1
            ? (dx > 0 ? Direction.East : Direction.West)
            : (dy > 0 ? Direction.South : Direction.North);

        if (ctx.Facing == want)
        {
            if (ctx.IsWallAhead() || ctx.PreviousActionResult == ActionResult.Blocked)
            {
                _alignAxis = _alignAxis == 1 ? 2 : 1; // this lane is walled off
                Direction alt = _alignAxis == 1
                    ? (dx >= 0 ? Direction.East : Direction.West)
                    : (dy >= 0 ? Direction.South : Direction.North);
                return TurnTowards(ctx.Facing, alt);
            }
            return Actions.MoveForward();
        }
        return TurnTowards(ctx.Facing, want);
    }

    /// <summary>
    /// Begin a perpendicular escape from a firing lane along <paramref name="lane"/>.
    /// If a turn is needed, commits so the move completes next tick.
    /// </summary>
    private BotAction? StartDodge(BotContext ctx, Direction lane)
    {
        Direction a = lane.TurnedLeft();
        Direction b = lane.TurnedRight();
        // Prefer a side we are already facing: escape in one tick, not two.
        if (ctx.Facing == a || ctx.Facing == b)
        {
            var (fdx, fdy) = ctx.Facing.Vector();
            if (!ctx.IsWall(ctx.Position.Offset(fdx, fdy)))
                return Actions.MoveForward();
        }
        foreach (Direction d in new[] { a, b })
        {
            var (dx, dy) = d.Vector();
            if (!ctx.IsWall(ctx.Position.Offset(dx, dy)))
            {
                if (ctx.Facing == d) return Actions.MoveForward();
                _dodgeCommit = d;
                return TurnTowards(ctx.Facing, d);
            }
        }
        return null;
    }

    /// <summary>
    /// True when the enemy is one tile off our firing ray and their facing
    /// says their next step lands on it: shoot NOW and the ray meets them
    /// on arrival, because moves resolve before shots within a tick.
    /// </summary>
    private bool CrossingShot(BotContext ctx, VisibleEnemy enemy)
    {
        if (!ctx.CanShoot) return false;
        var (fdx, fdy) = ctx.Facing.Vector();
        int rx = enemy.Position.X - ctx.Position.X, ry = enemy.Position.Y - ctx.Position.Y;
        int along = rx * fdx + ry * fdy;
        int perp = rx * fdy - ry * fdx;
        if (along < 1 || along > 7 || Math.Abs(perp) != 1) return false;
        var (edx, edy) = enemy.Facing.Vector();
        Position ct = enemy.Position.Offset(edx, edy); // their next tile if they step
        int cx = ct.X - ctx.Position.X, cy = ct.Y - ctx.Position.Y;
        if (cx * fdy - cy * fdx != 0 || cx * fdx + cy * fdy < 1) return false;
        if (ctx.IsWall(ct)) return false;
        Position p = ctx.Position.Offset(fdx, fdy); // ray must reach the crossing tile
        while (!p.Equals(ct))
        {
            if (ctx.IsWall(p)) return false;
            p = p.Offset(fdx, fdy);
        }
        return true;
    }

    /// <summary>
    /// The lane the enemy will enter: their facing shows which offset they are
    /// closing, so they will finish on the other axis relative to us.
    /// </summary>
    private static Direction PredictIntercept(Position me, Position ep, Direction enemyFacing)
    {
        int dx = ep.X - me.X, dy = ep.Y - me.Y;
        bool closingDx = enemyFacing is Direction.East or Direction.West;
        if (closingDx && dy != 0) return dy > 0 ? Direction.South : Direction.North;
        if (!closingDx && dx != 0) return dx > 0 ? Direction.East : Direction.West;
        return GuardDirection(me, ep);
    }

    private static bool FacesAway(Position ep, Direction facing, Position me)
    {
        var (dx, dy) = facing.Vector();
        int rx = me.X - ep.X, ry = me.Y - ep.Y;
        return rx * dx + ry * dy < 0; // their heading points away from us
    }

    private static Direction GuardDirection(Position me, Position ep)
    {
        int dx = ep.X - me.X, dy = ep.Y - me.Y;
        // They close the smaller offset to align, so they will enter our lane
        // along the axis with the larger offset — aim down that axis.
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    /// <summary>Direction from a to b assuming they share a row or column.</summary>
    private static Direction DirExact(Position a, Position b)
    {
        if (b.X > a.X) return Direction.East;
        if (b.X < a.X) return Direction.West;
        if (b.Y > a.Y) return Direction.South;
        return Direction.North;
    }

    private static Direction DirTowards(Position from, Position to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    private static BotAction TurnTowards(Direction facing, Direction desired)
    {
        int diff = (((int)desired - (int)facing) % 4 + 4) % 4;
        return diff == 3 ? Actions.TurnLeft() : Actions.TurnRight();
    }

    private static bool LineClear(BotContext ctx, Position from, Position to)
    {
        if (from.Equals(to)) return false;
        int dx = Math.Sign(to.X - from.X), dy = Math.Sign(to.Y - from.Y);
        if (dx != 0 && dy != 0) return false;
        Position p = from.Offset(dx, dy);
        while (!p.Equals(to))
        {
            if (ctx.IsWall(p)) return false;
            p = p.Offset(dx, dy);
        }
        return true;
    }
}
