using System;
using System.Collections.Generic;
using System.Linq;
using BotArena.Sdk;

/// <summary>
/// Aggressive objective bot: occupy first, then use its body and privately curved
/// bolts as the two jaws of a pincer around likely zone exits.
/// </summary>
public sealed class Pincer : IBot
{
    private readonly HashSet<Position> _walls = new();
    private Position? _lastEnemy;
    private Direction _lastEnemyFacing;
    private int _lastEnemyTick = -1000;
    private int _lastEnemyShotTick = -1000;
    private int? _previousPressure;
    private int _patrolIndex;
    private int _blockedTurns;

    public BotAction Tick(BotContext context)
    {
        Learn(context);

        var enemy = context.VisibleEnemies.Count == 0
            ? null
            : context.VisibleEnemies
                .OrderBy(e => e.Position.ChebyshevDistance(context.Position))
                .First();
        if (enemy is not null)
        {
            _lastEnemy = enemy.Position;
            _lastEnemyFacing = enemy.Facing;
            _lastEnemyTick = context.Tick;
        }

        var zones = context.ZoneTiles ?? Array.Empty<Position>();
        bool onZone = zones.Contains(context.Position);
        bool enemyOnZone = enemy is not null && zones.Contains(enemy.Position);
        bool pressureAdvanced = PressureAdvanced(context);
        bool likelyContested = onZone
            && (enemyOnZone || (_previousPressure is not null && !pressureAdvanced));
        _previousPressure = context.ControlPressure;

        if (TryDodge(context, out var dodge))
            return Act(context, dodge, "dodge advancing bolt");

        // A body pincer needs a live second jaw. When the opponent can fire again,
        // orbit out of its presently programmable envelope instead of accepting a
        // cooldown-losing trade. At touching distance, also create space while our
        // own launcher cools; this prevents a turn/turn co-occupancy checkmate.
        if (enemy is not null
            && EnemyMayFire(context)
            && (ThreatenedBy(context, enemy, context.Position)
                || (enemy.Position.ChebyshevDistance(context.Position) <= 1
                    && !context.CanShoot)))
        {
            Position escape = context.Ahead();
            if (CanEnter(context, escape, enemy.Position)
                && !ThreatenedBy(context, enemy, escape))
            {
                return Act(context, Actions.MoveForward(), "orbit firing envelope");
            }
        }

        // Fire before navigation when a current/predicted enemy tile or remembered
        // zone exit can be cut by this facing. The bolt is one jaw; our next advance
        // through the objective is the other.
        if (context.CanShoot && context.ShotPrograms is { } limits)
        {
            Position? fireTarget = enemy?.Position;
            Position? escapeTarget = enemy is null
                ? null
                : Step(enemy.Position, enemy.Facing);

            if (fireTarget is null && likelyContested
                && _lastEnemy is { } remembered
                && context.Tick - _lastEnemyTick <= 18)
            {
                fireTarget = remembered;
                escapeTarget = Step(remembered, _lastEnemyFacing);
            }

            if (fireTarget is { } target
                && FindShot(context, limits, context.Facing, target, escapeTarget)
                    is { Score: >= 75 } shot)
            {
                return Act(
                    context,
                    Actions.Shoot(shot.Program),
                    $"pincer shot score={shot.Score} target={target} arc={shot.Program}");
            }
        }

        if (enemy is not null)
        {
            Direction attackFacing = BestAttackFacing(context, enemy, zones);
            if (attackFacing != context.Facing)
                return Act(
                    context,
                    TurnToward(context.Facing, attackFacing, context.Tick),
                    $"turn for fork enemy={enemy.Position}");

            // Body pressure: close on a zone occupant, but stop one tile short so a
            // failed swap cannot settle into a permanent co-occupancy loop.
            if (enemyOnZone || likelyContested)
            {
                var engage = BestAdjacentTo(context, enemy.Position, zones);
                if (engage is { } engageTarget)
                    return Navigate(context, engageTarget, enemy.Position, "body-evict");
            }
        }

        if (!onZone && zones.Count > 0)
        {
            var objective = NearestReachable(context, zones, enemy?.Position);
            if (objective is { } zoneTarget)
                return Navigate(context, zoneTarget, enemy?.Position, "claim-zone");
        }

        if (onZone)
        {
            // Never passively settle. Move within the objective when possible and
            // continuously turn the cone through blind approaches otherwise.
            var localZone = zones
                .Where(z => z != context.Position)
                .OrderBy(z => z.ChebyshevDistance(
                    _lastEnemy ?? ApproximateSoundTarget(context)))
                .ThenBy(z => z.ChebyshevDistance(context.Position))
                .ToArray();

            if (localZone.Length > 0 && context.Tick % 4 != 0)
            {
                var patrol = localZone[_patrolIndex++ % localZone.Length];
                return Navigate(context, patrol, enemy?.Position, "sweep-zone");
            }

            Direction scan = _lastEnemy is { } seen && context.Tick - _lastEnemyTick <= 30
                ? DirectionToward(context.Position, seen, context.Facing)
                : DirectionToward(
                    context.Position,
                    ApproximateSoundTarget(context),
                    context.Facing.TurnedRight());
            if (scan == context.Facing)
                return Act(
                    context,
                    (context.Tick & 1) == 0 ? Actions.TurnLeft() : Actions.TurnRight(),
                    likelyContested ? "eviction scan" : "active sole scan");
            return Act(
                context,
                TurnToward(context.Facing, scan, context.Tick),
                likelyContested ? "seek contesting bot" : "guard approach");
        }

        // Zone-less fallback and temporary navigation failure: investigate the most
        // useful direction while still acting.
        Direction fallback = _lastEnemy is { } last && context.Tick - _lastEnemyTick <= 40
            ? DirectionToward(context.Position, last, context.Facing)
            : DirectionToward(
                context.Position,
                ApproximateSoundTarget(context),
                context.Facing.TurnedRight());
        if (fallback != context.Facing)
            return Act(context, TurnToward(context.Facing, fallback, context.Tick), "investigate");
        if (CanEnter(context, context.Ahead(), enemy?.Position))
            return Act(context, Actions.MoveForward(), "advance");
        return Act(context, Actions.TurnRight(), "unblock");
    }

    private void Learn(BotContext context)
    {
        foreach (var tile in context.VisibleTiles)
        {
            if (tile.IsWall)
                _walls.Add(tile.Position);
            else
                _walls.Remove(tile.Position);
        }

        // Only mark a failed forward tile when it is visibly a wall. A failed move
        // may instead be the opponent body, which is exactly where we want pressure.
        if (context.PreviousActionResult == ActionResult.Blocked && context.IsWallAhead())
            _walls.Add(context.Ahead());

        if (context.VisibleEvents.Any(e =>
            e.Kind == VisibleEventKind.Shot && e.Slot != context.Slot))
            _lastEnemyShotTick = context.Tick - 1;
        else if (context.HeardSounds?.Any(s => s.Kind == VisibleEventKind.Shot) == true)
            _lastEnemyShotTick = context.Tick - 1;
    }

    private bool PressureAdvanced(BotContext context)
    {
        if (_previousPressure is not int before || context.ControlPressure is not int now)
            return false;
        return context.Slot == 0 ? now > before : now < before;
    }

    private bool TryDodge(BotContext context, out BotAction action)
    {
        action = default;
        var projectiles = context.VisibleProjectiles;
        if (projectiles is null)
            return false;

        bool currentThreat = false;
        var lethal = new HashSet<Position>();
        foreach (var bolt in projectiles)
        {
            if (bolt.OwnerSlot == context.Slot || bolt.TicksUntilAdvance != 1)
                continue;
            var (dx, dy) = bolt.Heading.Vector();
            var p = bolt.Position;
            int steps = Math.Min(bolt.TilesPerAdvance, Math.Max(0, bolt.RemainingTiles));
            if (bolt.RemainingTiles < 0)
                steps = bolt.TilesPerAdvance;
            for (int i = 0; i < steps; i++)
            {
                p = p.Offset(dx, dy);
                lethal.Add(p);
            }
            currentThreat |= lethal.Contains(context.Position);
        }

        if (!currentThreat)
            return false;
        Position ahead = context.Ahead();
        if (CanEnter(context, ahead, null) && !lethal.Contains(ahead))
        {
            action = Actions.MoveForward();
            return true;
        }
        return false;
    }

    private BotAction Navigate(
        BotContext context,
        Position target,
        Position? blockedByEnemy,
        string reason)
    {
        Direction? step = FirstStep(context, target, blockedByEnemy);
        if (step is null)
        {
            _blockedTurns++;
            return Act(
                context,
                (_blockedTurns & 1) == 0 ? Actions.TurnLeft() : Actions.TurnRight(),
                $"{reason}: search");
        }

        _blockedTurns = 0;
        if (step.Value != context.Facing)
            return Act(
                context,
                TurnToward(context.Facing, step.Value, context.Tick),
                $"{reason}: face {step.Value}");
        if (!CanEnter(context, context.Ahead(), blockedByEnemy))
            return Act(context, Actions.TurnRight(), $"{reason}: blocked");
        var enemy = context.VisibleEnemies.FirstOrDefault();
        if (enemy is not null
            && EnemyMayFire(context)
            && ThreatenedBy(context, enemy, context.Ahead()))
            return Act(context, Actions.TurnRight(), $"{reason}: hold outside firing arc");
        return Act(context, Actions.MoveForward(), $"{reason}: move {target}");
    }

    private Position? NearestReachable(
        BotContext context,
        IReadOnlyList<Position> candidates,
        Position? enemy)
    {
        foreach (var candidate in candidates
            .OrderBy(z => Manhattan(context.Position, z)))
        {
            if (candidate == context.Position
                || FirstStep(context, candidate, enemy) is not null)
                return candidate;
        }
        return null;
    }

    private Position? BestAdjacentTo(
        BotContext context,
        Position enemy,
        IReadOnlyList<Position> zones)
    {
        var candidates = Neighbors(enemy)
            .Where(p => InBounds(context, p) && !_walls.Contains(p))
            .OrderByDescending(p => zones.Contains(p))
            .ThenBy(p => context.VisibleEnemies.Count > 0
                && ThreatenedBy(context, context.VisibleEnemies[0], p))
            .ThenBy(p => p == Step(enemy, context.VisibleEnemies.Count > 0
                ? context.VisibleEnemies[0].Facing
                : _lastEnemyFacing))
            .ThenBy(p => Manhattan(context.Position, p))
            .ToArray();
        return NearestReachable(context, candidates, enemy);
    }

    private Direction? FirstStep(
        BotContext context,
        Position target,
        Position? blockedByEnemy)
    {
        if (target == context.Position)
            return context.Facing;

        var queue = new Queue<Position>();
        var first = new Dictionary<Position, Direction?>();
        queue.Enqueue(context.Position);
        first[context.Position] = null;

        while (queue.Count > 0)
        {
            Position p = queue.Dequeue();
            foreach (Direction direction in PreferredDirections(p, target))
            {
                Position next = Step(p, direction);
                if (!InBounds(context, next)
                    || _walls.Contains(next)
                    || next == blockedByEnemy
                    || first.ContainsKey(next))
                    continue;
                first[next] = p == context.Position ? direction : first[p];
                if (next == target)
                    return first[next];
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private Direction BestAttackFacing(
        BotContext context,
        VisibleEnemy enemy,
        IReadOnlyList<Position> zones)
    {
        if (context.ShotPrograms is { } limits)
        {
            Direction best = context.Facing;
            int score = int.MinValue;
            foreach (Direction direction in Enum.GetValues<Direction>())
            {
                var candidate = FindShot(
                    context,
                    limits,
                    direction,
                    enemy.Position,
                    Step(enemy.Position, enemy.Facing));
                int candidateScore = candidate?.Score ?? int.MinValue / 2;
                if (candidateScore > score)
                {
                    score = candidateScore;
                    best = direction;
                }
            }
            if (score >= 75)
                return best;
        }

        // If no immediate arc is available, face the opponent while biasing movement
        // toward the objective instead of chasing away from it.
        if (zones.Contains(context.Position) || zones.Contains(enemy.Position))
            return DirectionToward(context.Position, enemy.Position, context.Facing);
        Position? nearestZone = zones
            .OrderBy(z => Manhattan(context.Position, z))
            .Cast<Position?>()
            .FirstOrDefault();
        return nearestZone is { } z
            ? DirectionToward(context.Position, z, context.Facing)
            : DirectionToward(context.Position, enemy.Position, context.Facing);
    }

    private ShotChoice? FindShot(
        BotContext context,
        ShotProgramLimits limits,
        Direction facing,
        Position target,
        Position? escape)
    {
        ShotChoice? best = null;
        foreach (var program in Programs(limits))
        {
            var path = ShotPaths.Preview(
                context.Position,
                facing,
                program,
                limits.MaxPathTiles,
                IsKnownWall);
            int targetIndex = IndexOf(path, target);
            int escapeIndex = escape is { } e ? IndexOf(path, e) : -1;
            int score = targetIndex >= 0 ? 110 - targetIndex : 0;
            if (escapeIndex >= 0)
                score = Math.Max(score, 88 - escapeIndex);

            // Prefer trajectories that continue across several objective tiles: they
            // remove refuge while Pincer advances through another approach.
            int zoneCoverage = (context.ZoneTiles ?? Array.Empty<Position>())
                .Count(path.Contains);
            score += Math.Min(18, zoneCoverage * 3);
            score -= Math.Abs(program.InitialAimOffset);
            score -= program.BendCount;
            if (best is null || score > best.Value.Score)
                best = new ShotChoice(program, score);
        }
        return best;
    }

    private static IEnumerable<ShotProgram> Programs(ShotProgramLimits limits)
    {
        var straight = ShotProgram.Straight;
        if (limits.IsValid(straight))
            yield return straight;

        int aim = Math.Min(1, limits.MaxInitialAimOctants);
        for (int initial = -aim; initial <= aim; initial++)
        {
            var diagonal = new ShotProgram(initial, 0, 0, 1, 0);
            if (limits.IsValid(diagonal))
                yield return diagonal;

            for (int bend = -1; bend <= 1; bend += 2)
            for (int after = 1; after <= Math.Min(3, limits.MaxBendAfterTiles); after++)
            for (int every = 1; every <= Math.Min(3, limits.MaxBendEveryTiles); every++)
            for (int count = 1; count <= Math.Min(2, limits.MaxBendCount); count++)
            {
                var program = new ShotProgram(initial, bend, after, every, count);
                if (limits.IsValid(program))
                    yield return program;
            }
        }
    }

    private Position ApproximateSoundTarget(BotContext context)
    {
        var sound = context.HeardSounds?.LastOrDefault();
        if (sound is null)
            return _lastEnemy ?? context.Position.Offset(
                context.Facing.Vector().Dx * 3,
                context.Facing.Vector().Dy * 3);

        var (dx, dy) = sound.Bearing switch
        {
            SoundBearing.North => (0, -1),
            SoundBearing.NorthEast => (1, -1),
            SoundBearing.East => (1, 0),
            SoundBearing.SouthEast => (1, 1),
            SoundBearing.South => (0, 1),
            SoundBearing.SouthWest => (-1, 1),
            SoundBearing.West => (-1, 0),
            _ => (-1, -1),
        };
        int distance = sound.Distance switch
        {
            SoundDistance.Near => 2,
            SoundDistance.Medium => 4,
            _ => 7,
        };
        return context.Position.Offset(dx * distance, dy * distance);
    }

    private bool CanEnter(BotContext context, Position p, Position? enemy) =>
        InBounds(context, p)
        && !_walls.Contains(p)
        && p != enemy
        && !IsProjectileTile(context, p);

    private bool IsProjectileTile(BotContext context, Position p)
    {
        if (context.VisibleProjectiles is not { } projectiles)
            return false;
        return projectiles.Any(b => b.OwnerSlot != context.Slot && b.Position == p);
    }

    private bool EnemyMayFire(BotContext context) =>
        context.Tick - _lastEnemyShotTick >= 3;

    private bool ThreatenedBy(
        BotContext context,
        VisibleEnemy enemy,
        Position target)
    {
        if (context.ShotPrograms is not { } limits)
            return false;
        foreach (var program in Programs(limits))
        {
            var path = ShotPaths.Preview(
                enemy.Position,
                enemy.Facing,
                program,
                limits.MaxPathTiles,
                IsKnownWall);
            if (path.Contains(target))
                return true;
        }
        return false;
    }

    private bool InBounds(BotContext context, Position p) =>
        p.X >= 0 && p.Y >= 0
        && (context.MapWidth <= 0 || p.X < context.MapWidth)
        && (context.MapHeight <= 0 || p.Y < context.MapHeight);

    private bool IsKnownWall(Position p) => _walls.Contains(p);

    private static IEnumerable<Direction> PreferredDirections(Position from, Position to)
    {
        Direction horizontal = to.X >= from.X ? Direction.East : Direction.West;
        Direction vertical = to.Y >= from.Y ? Direction.South : Direction.North;
        if (Math.Abs(to.X - from.X) >= Math.Abs(to.Y - from.Y))
        {
            yield return horizontal;
            yield return vertical;
            yield return vertical.TurnedRight().TurnedRight();
            yield return horizontal.TurnedRight().TurnedRight();
        }
        else
        {
            yield return vertical;
            yield return horizontal;
            yield return horizontal.TurnedRight().TurnedRight();
            yield return vertical.TurnedRight().TurnedRight();
        }
    }

    private static Direction DirectionToward(
        Position from,
        Position to,
        Direction tieBreaker)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        if (Math.Abs(dy) > Math.Abs(dx))
            return dy >= 0 ? Direction.South : Direction.North;
        if (dx == 0 && dy == 0)
            return tieBreaker;
        bool tieIsHorizontal = tieBreaker is Direction.East or Direction.West;
        return tieIsHorizontal
            ? (dx >= 0 ? Direction.East : Direction.West)
            : (dy >= 0 ? Direction.South : Direction.North);
    }

    private static BotAction TurnToward(Direction from, Direction to, int tick)
    {
        int right = ((int)to - (int)from + 4) % 4;
        if (right == 1)
            return Actions.TurnRight();
        if (right == 3)
            return Actions.TurnLeft();
        return (tick & 1) == 0 ? Actions.TurnLeft() : Actions.TurnRight();
    }

    private BotAction Act(BotContext context, BotAction action, string reason)
    {
        context.Debug.Write(
            "PINCER {0} pos={1} face={2} hp={3} pressure={4}",
            reason,
            context.Position,
            context.Facing,
            context.Health,
            context.ControlPressure?.ToString() ?? "-");
        return action;
    }

    private static Position Step(Position p, Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return p.Offset(dx, dy);
    }

    private static IEnumerable<Position> Neighbors(Position p)
    {
        yield return p.Offset(0, -1);
        yield return p.Offset(1, 0);
        yield return p.Offset(0, 1);
        yield return p.Offset(-1, 0);
    }

    private static int Manhattan(Position a, Position b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static int IndexOf(IReadOnlyList<Position> path, Position target)
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == target)
                return i;
        }
        return -1;
    }

    private readonly record struct ShotChoice(ShotProgram Program, int Score);
}
