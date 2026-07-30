using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// The striker's gun, priced as an interception problem rather than a
/// line-of-sight problem. Two contract facts drive all of it:
/// <list type="bullet">
/// <item>movement resolves before combat, so the tile that matters is the one
/// the target will stand on when the bolt arrives, not the one the observation
/// drew;</item>
/// <item>under a facing-locked coupling a body may only step where it faces, so
/// a target's reachable set is a RAY. Shooting the ray a body is confined to is
/// the difference between hoping and denying.</item>
/// </list>
/// Every candidate arc is validated against the declared envelope and previewed
/// through the SDK's own bend rule, so the same code fires straight-only guns,
/// the striker's 1-4 tile commitment, the shallower universal-bend envelope, a
/// three-bolt fan, and an omnidirectional turret without knowing which arm it
/// is in.
/// </summary>
internal sealed class ArcGun
{
    private const int ReachHorizon = 6;

    private readonly ArcFacts _facts;
    private readonly GenericActorContext _context;
    private readonly ArcThreat _threat;
    private readonly HashSet<Position> _objective;
    private readonly HashSet<ActorIdentity> _breaking;
    private readonly Dictionary<ActorIdentity, Dictionary<Position, int>> _reach;

    public ArcGun(ArcFacts facts, GenericActorContext context, ArcThreat threat)
    {
        _facts = facts;
        _context = context;
        _threat = threat;
        _objective = context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? facts.ObjectiveTiles(mode.ActivePositionIndex).ToHashSet()
            : [];
        _breaking = threat.GuardsNearBreak();
        _reach = [];
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            _reach[enemy.ActorId] = ReachMap(
                facts,
                enemy.Position,
                enemy.Facing,
                enemy.FormId);
        }
    }

    /// <summary>One scored firing option.</summary>
    public sealed record Shot(
        GenericActorDecision Decision,
        int Score,
        ActorIdentity Target,
        int EarliestTileIndex);

    /// <summary>
    /// Tile to earliest tick on which the body could stand there. A facing-locked
    /// body walks a ray; any other coupling spreads. Built once per enemy per
    /// tick because the interception search asks the question thousands of times.
    /// </summary>
    private static Dictionary<Position, int> ReachMap(
        ArcFacts facts,
        Position position,
        Direction facing,
        string formId)
    {
        var map = new Dictionary<Position, int> { [position] = 0 };
        if (facts.Form(formId)?.AllowedActionIds.Contains(
                "move",
                StringComparer.Ordinal) != true)
        {
            return map;
        }

        if (facts.FacingLocked(formId))
        {
            Position cursor = position;
            for (int step = 1; step <= ReachHorizon; step++)
            {
                cursor = ArcBoard.Step(cursor, facing);
                if (facts.IsWall(cursor))
                    break;
                map[cursor] = step;
            }
            return map;
        }

        var frontier = new List<Position> { position };
        for (int step = 1; step <= ReachHorizon; step++)
        {
            var next = new List<Position>();
            foreach (Position tile in frontier)
            {
                foreach (Direction direction in ArcBoard.Cardinals)
                {
                    Position candidate = ArcBoard.Step(tile, direction);
                    if (facts.IsWall(candidate) || map.ContainsKey(candidate))
                        continue;
                    map[candidate] = step;
                    next.Add(candidate);
                }
            }
            frontier = next;
        }
        return map;
    }

    /// <summary>
    /// The best shot available from the current pose, or null when nothing legal
    /// intercepts anything. The caller decides whether the score is worth the
    /// tick.
    /// </summary>
    public Shot? Best() =>
        Best(_context.Self.Facing, _context.Self.Position, deep: true);

    /// <summary>
    /// The best shot this body would have from <paramref name="facing"/>. Deep
    /// search walks the whole declared bend envelope; the shallow search prices
    /// a rotation with straight and aim-only arcs only, because a rotation is a
    /// question about which lane to own, not about which arc to commit to.
    /// </summary>
    public Shot? Best(Direction facing, Position origin, bool deep)
    {
        GenericActorRulesContract.AttackProfile? attack =
            _facts.Attack(_context.Self.FormId);
        if (attack is null || _context.Enemies.IsEmpty)
            return null;

        Shot? best = null;
        foreach (GenericActorActionLegality action in AttackActions())
        {
            Shot? candidate = attack.OmnidirectionalAim
                ? BestAbsolute(action, attack, origin)
                : BestProgrammed(action, attack, facing, origin, deep);
            if (candidate is not null
                && (best is null || candidate.Score > best.Score))
            {
                best = candidate;
            }
        }
        return best;
    }

    /// <summary>
    /// The rotation that would buy the best shot next tick, with its score.
    /// Under facing-locked, rotating is both aiming and choosing the only
    /// direction this body may walk, so it is never a free action.
    /// </summary>
    public (Direction Facing, int Score)? BestRotation()
    {
        (Direction Facing, int Score)? best = null;
        foreach (Direction facing in ArcBoard.Cardinals)
        {
            if (facing == _context.Self.Facing)
                continue;
            Shot? shot = Best(facing, _context.Self.Position, deep: false);
            if (shot is not null && (best is null || shot.Score > best.Value.Score))
                best = (facing, shot.Score);
        }
        return best;
    }

    private IEnumerable<GenericActorActionLegality> AttackActions()
    {
        HashSet<string> attackIds = _facts.Contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return _context.ActionLegalities
            .Where(action =>
                action.Available && attackIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal);
    }

    private Shot? BestAbsolute(
        GenericActorActionLegality action,
        GenericActorRulesContract.AttackProfile attack,
        Position origin)
    {
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (headings is null)
            return null;

        Shot? best = null;
        foreach (ProjectileHeading heading in headings.AllowedValues)
        {
            List<Position> path = Ray(origin, heading, attack);
            (int score, ActorIdentity? target, int index, _) =
                ScorePath(path, attack, origin);
            if (target is null || (best is not null && score <= best.Score))
                continue;
            best = new Shot(
                new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.ProjectileHeadingArgument(
                            heading),
                    ],
                    $"turret fire {heading} at {target}"),
                score,
                target,
                index);
        }
        return best;
    }

    private List<Position> Ray(
        Position origin,
        ProjectileHeading heading,
        GenericActorRulesContract.AttackProfile attack)
    {
        var path = new List<Position>();
        Position cursor = origin;
        (int dx, int dy) = heading.Vector();
        for (int tile = 0; tile < attack.Projectile.MaxTravelTiles; tile++)
        {
            Position next = cursor.Offset(dx, dy);
            if (_facts.IsWall(next))
                break;
            if (attack.Projectile.DiagonalCornersMustBeClear
                && dx != 0
                && dy != 0
                && (_facts.IsWall(cursor.Offset(dx, 0))
                    || _facts.IsWall(cursor.Offset(0, dy))))
            {
                break;
            }
            cursor = next;
            path.Add(cursor);
        }
        return path;
    }

    private Shot? BestProgrammed(
        GenericActorActionLegality action,
        GenericActorRulesContract.AttackProfile attack,
        Direction facing,
        Position origin,
        bool deep)
    {
        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programs = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint>()
                .SingleOrDefault();
        bool payloadAllowed =
            programs is { Allowed: true } && attack.ShotProgram.Enabled;
        bool bareAllowed =
            !attack.ShotProgram.Enabled || attack.ShotProgram.PayloadOptional;
        int wing = (attack.ProjectilesPerAttack - 1) / 2;

        Shot? best = null;
        foreach ((ShotProgram? program, string label) in Candidates(
                     attack,
                     payloadAllowed,
                     bareAllowed,
                     deep))
        {
            ShotProgram effective = program ?? ShotProgram.Straight;
            int laneScore = int.MinValue;
            ActorIdentity? laneTarget = null;
            int laneIndex = int.MaxValue;
            // One decision launches every declared lane at once, so the fan is
            // the same interception question asked once per heading offset.
            for (int lane = -wing; lane <= wing; lane++)
            {
                ShotProgram shifted = lane == 0
                    ? effective
                    : effective with
                    {
                        InitialAimOffset = effective.InitialAimOffset + lane,
                    };
                List<Position> path = ShotPaths.Preview(
                        origin,
                        facing,
                        shifted,
                        attack.Projectile.MaxTravelTiles,
                        _facts.IsWall)
                    .ToList();
                (int score, ActorIdentity? target, int index, bool hard) =
                    ScorePath(path, attack, origin);
                // A curved arc is a commitment the engine validates against
                // strict diagonal corners and wall termination. Spending one on a
                // path that merely passes near a body is how a bot fires an
                // intercept that cannot land — so a bend needs a hard hit.
                if (effective.BendCount > 0 && !hard)
                    continue;
                if (target is not null && score > laneScore)
                {
                    laneScore = score;
                    laneTarget = target;
                    laneIndex = index;
                }
            }
            if (laneTarget is null || (best is not null && laneScore <= best.Score))
                continue;

            GenericActorDecision decision = program is null
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    $"{label} at {laneTarget}")
                : new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.ShotProgramArgument(
                            program.Value),
                    ],
                    $"{label} at {laneTarget}");
            best = new Shot(decision, laneScore, laneTarget, laneIndex);
        }
        return best;
    }

    /// <summary>
    /// Every arc this form may legally commit to, built from the declared
    /// envelope rather than from a remembered rule: aim offsets between the
    /// declared minimum and maximum, bend directions from the declared set, and
    /// bend timing inside the declared depth. A program outside those bounds is
    /// rejected by the host, so it is never offered. Repeat-bend timings are
    /// sampled at their declared extremes rather than exhaustively — the first
    /// bend depth is the striker's real commitment and the search budget belongs
    /// there.
    /// </summary>
    private static IEnumerable<(ShotProgram? Program, string Label)> Candidates(
        GenericActorRulesContract.AttackProfile attack,
        bool payloadAllowed,
        bool bareAllowed,
        bool deep)
    {
        GenericActorRulesContract.ShotProgramDefinition definition =
            attack.ShotProgram;
        if (bareAllowed)
            yield return (null, "straight fire");

        if (!payloadAllowed)
            yield break;

        GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
            definition.AimOnlyProgram;
        for (int aim = definition.MinInitialAimSteps;
             aim <= definition.MaxInitialAimSteps;
             aim++)
        {
            if (aim == 0 && bareAllowed)
                continue;
            yield return (
                new ShotProgram(
                    aim,
                    aimOnly.BendDirection,
                    aimOnly.BendAfterTiles,
                    aimOnly.BendEveryTiles,
                    aimOnly.BendCount),
                "aimed fire");
        }

        if (!deep)
            yield break;

        int[] everyValues = Distinct(
            definition.MinBendEveryTiles,
            definition.MaxBendEveryTiles);
        int[] countValues = Distinct(
            Math.Max(1, definition.MinBendCount),
            definition.MaxBendCount);
        foreach (int direction in definition.AllowedCurvedBendDirections)
        {
            if (direction == 0)
                continue;
            for (int after = definition.MinBendAfterTiles;
                 after <= definition.MaxBendAfterTiles;
                 after++)
            {
                foreach (int every in everyValues)
                {
                    foreach (int count in countValues)
                    {
                        for (int aim = definition.MinInitialAimSteps;
                             aim <= definition.MaxInitialAimSteps;
                             aim++)
                        {
                            yield return (
                                new ShotProgram(
                                    aim,
                                    direction,
                                    after,
                                    every,
                                    count),
                                $"bend {direction} after {after}");
                        }
                    }
                }
            }
        }
    }

    private static int[] Distinct(int low, int high) =>
        high <= low ? [low] : [low, high];

    /// <summary>
    /// Ticks after the firing tick on which the bolt occupies path index
    /// <paramref name="index"/>, derived from the declared launch length,
    /// tiles-per-advance, and ticks-per-advance rather than assumed.
    /// </summary>
    private static int TickOffset(
        int index,
        GenericActorRulesContract.Projectile projectile)
    {
        int launch = Math.Max(1, projectile.LaunchTiles);
        if (index < launch)
            return 0;
        int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
        int advance = ((index - launch) / perAdvance) + 1;
        return advance * Math.Max(1, projectile.TicksPerAdvance);
    }

    private (int Score, ActorIdentity? Target, int Index, bool Hard) ScorePath(
        List<Position> path,
        GenericActorRulesContract.AttackProfile attack,
        Position origin)
    {
        int bestScore = int.MinValue;
        ActorIdentity? bestTarget = null;
        int bestIndex = int.MaxValue;
        bool bestHard = false;

        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            // A guard shatters on its declared threshold and the forced return
            // is the punish window, so feeding a nearly-broken shield is
            // correct while poking a fresh one is paying to be shot.
            bool returns = _threat.WouldReturnFire(enemy, origin);
            if (returns && !_breaking.Contains(enemy.ActorId))
                continue;
            if (!_reach.TryGetValue(
                    enemy.ActorId,
                    out Dictionary<Position, int>? reach))
            {
                continue;
            }

            // Interception is a TIMING match, not a set intersection. A tile the
            // target could reach four ticks before the bolt gets there is almost
            // no evidence; the tile it is standing on, or one it arrives at
            // exactly as the bolt does, is nearly all of it. Weighting by that
            // slack is what stops a straight ray from claiming credit for
            // crossing somewhere the target might wander, and it is what makes a
            // bend win against an off-axis body instead of losing a tie.
            int coverage = 0;
            int earliest = int.MaxValue;
            bool hitsCurrent = false;
            bool hard = false;
            for (int index = 0; index < path.Count; index++)
            {
                if (!reach.TryGetValue(path[index], out int needed))
                    continue;
                int arrival = TickOffset(index, attack.Projectile) + 1;
                if (needed > arrival)
                    continue;
                int slack = arrival - needed;
                // A HARD interception: the bolt lands on the tile the target is
                // standing on, or on the ONE tile it could step to arriving
                // exactly as the bolt does. A tile three steps away that the
                // target could theoretically be standing on by then is
                // speculation, and spending a committed arc on speculation is how
                // a bot fires an intercept the corner rule was always going to
                // refuse.
                if (needed == 0 || (slack == 0 && needed <= 1))
                    hard = true;
                coverage += slack switch
                {
                    0 => 20,
                    1 => 10,
                    2 => 4,
                    _ => 1,
                };
                if (needed == 0)
                {
                    coverage += 30;
                    hitsCurrent = true;
                }
                if (index < earliest)
                    earliest = index;
            }
            if (coverage == 0)
                continue;

            int score = coverage
                + (hitsCurrent ? 20 : 0)
                + (enemy.Health <= 1 ? 40 : 0)
                + (_objective.Contains(enemy.Position) ? 20 : 0)
                + (returns ? 35 : 0)
                + (_facts.IsFanForm(enemy.FormId) ? 25 : 0)
                + (enemy.PendingSameLifeTransition is not null ? 20 : 0)
                - earliest;
            if (score <= bestScore)
                continue;
            bestScore = score;
            bestTarget = enemy.ActorId;
            bestIndex = earliest;
            bestHard = hard;
        }
        return (bestScore, bestTarget, bestIndex, bestHard);
    }

    /// <summary>
    /// How much pressure a set of lanes puts on what the enemy can still reach,
    /// ignoring cooldown. Availability decides whether a bolt leaves this tick;
    /// it does not decide where to point while waiting for one, and under a
    /// facing-locked coupling pre-aiming during a cooldown is the cheapest tick
    /// in the match.
    ///
    /// <para>The unit is ESCAPE, not crossings. One lane crosses a facing-locked
    /// body's ray at most once, so counting crossings says a guaranteed kill and
    /// a one-in-five guess are both "1". What matters is how much of the target's
    /// reachable set is left uncovered: a body whose every legal destination sits
    /// on a lane cannot dodge, and that is worth far more than a lane that
    /// happens to touch a body with four ways out.</para>
    /// </summary>
    public int LaneValue(Direction facing, Position origin, int ticksFromNow)
    {
        GenericActorRulesContract.AttackProfile? attack =
            _facts.Attack(_context.Self.FormId);
        if (attack is null || _reach.Count == 0)
            return 0;
        int wing = (attack.ProjectilesPerAttack - 1) / 2;
        var lanes = new List<List<Position>>();
        for (int lane = -wing; lane <= wing; lane++)
        {
            lanes.Add(
                ShotPaths.Preview(
                        origin,
                        facing,
                        new ShotProgram(lane, 0, 0, 1, 0),
                        attack.Projectile.MaxTravelTiles,
                        _facts.IsWall)
                    .ToList());
        }
        return Pressure(lanes, attack, ticksFromNow);
    }

    /// <summary>
    /// The best lane pressure available from <paramref name="origin"/> over every
    /// facing, discounted for the rotation it would cost. Answers "is that tile a
    /// better place to shoot from than this one?", which is the whole question
    /// when the gun has no initial aim offset and a diagonally adjacent body is
    /// therefore unhittable.
    /// </summary>
    public int BestLaneValueFrom(
        Position origin,
        Direction currentFacing,
        int ticksFromNow)
    {
        int best = LaneValue(currentFacing, origin, ticksFromNow);
        foreach (Direction facing in ArcBoard.Cardinals)
        {
            if (facing == currentFacing)
                continue;
            best = Math.Max(
                best,
                LaneValue(facing, origin, ticksFromNow + 1) - 1);
        }
        return best;
    }

    private int Pressure(
        List<List<Position>> lanes,
        GenericActorRulesContract.AttackProfile attack,
        int ticksFromNow)
    {
        int window = ticksFromNow + 3;
        int score = 0;
        foreach (Dictionary<Position, int> reach in _reach.Values)
        {
            int options = reach.Count(entry => entry.Value <= window);
            if (options == 0)
                continue;
            var covered = new HashSet<Position>();
            foreach (List<Position> lane in lanes)
            {
                for (int index = 0; index < lane.Count; index++)
                {
                    int arrival = TickOffset(index, attack.Projectile)
                        + 1
                        + ticksFromNow;
                    if (reach.TryGetValue(lane[index], out int needed)
                        && needed <= arrival
                        && needed <= window)
                    {
                        covered.Add(lane[index]);
                    }
                }
            }
            if (covered.Count == 0)
                continue;
            score += covered.Count * 2;
            // Nowhere left to step is a kill, not a crossing.
            if (covered.Count >= options)
                score += 12;
        }
        return score;
    }

    /// <summary>
    /// Absolute headings a fan launched from <paramref name="facing"/> would
    /// cover, so a stance can be priced before its windup is paid.
    /// </summary>
    public ImmutableArray<ProjectileHeading> FanHeadings(
        string formId,
        Direction facing)
    {
        int wing = (_facts.BoltsPerAttack(formId) - 1) / 2;
        var headings = ImmutableArray.CreateBuilder<ProjectileHeading>();
        ProjectileHeading centre = facing.ToProjectileHeading();
        for (int lane = -wing; lane <= wing; lane++)
            headings.Add(centre.Turned(lane));
        return headings.ToImmutable();
    }

    /// <summary>
    /// How many enemy-reachable tiles a fan from <paramref name="facing"/> would
    /// cross, counted over the same reach maps the aimed search uses. This is
    /// the number that decides whether three lanes beat one aimed bolt.
    /// </summary>
    public int FanCoverage(
        string stanceFormId,
        Direction facing,
        Position origin,
        int ticksFromNow)
    {
        GenericActorRulesContract.AttackProfile? attack =
            _facts.Attack(stanceFormId);
        if (attack is null)
            return 0;
        var lanes = new List<List<Position>>();
        foreach (ProjectileHeading heading in FanHeadings(stanceFormId, facing))
            lanes.Add(Ray(origin, heading, attack));
        return Pressure(lanes, attack, ticksFromNow);
    }
}
