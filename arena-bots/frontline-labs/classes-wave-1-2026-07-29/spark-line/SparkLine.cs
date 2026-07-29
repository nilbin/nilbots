using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// SparkLine — TEMPO ENGINE.
///
/// The doctrine is bodies-per-tick. Every companion slot is queued the instant
/// the contract makes it legal, children are placed as far forward as the
/// declared placement offsets allow, the fragile prime is kept alive only to
/// the extent that keeps it spending fabrication actions, and the objective
/// clock is won by never leaving the active position uncontested.
///
/// Every capability is discovered from the resolved contract and the per-tick
/// legality mask: fabrication route and placement offsets, unlock and rebuild
/// clocks, shot envelope, projectile cadence, objective regions, and pad
/// geometry. The same policy therefore runs unchanged on an explicit-fabricate
/// contract, a pad-bound one, and one where companions activate automatically.
/// </summary>
public sealed class SparkLine : IGenericActorBot
{
    private const int ThreatHorizonAdvances = 3;
    private const int PlacementRotateCooldown = 6;
    private const int PlacementRotateGain = 2;
    private const int CloseContactRange = 3;
    private const int MaxPrograms = 320;
    private const int BlockedMemoryTicks = 5;

    private readonly Dictionary<string, ShotProgram[]> _programCache =
        new(StringComparer.Ordinal);
    private readonly HashSet<Position> _occupied = [];
    private readonly List<GenericActorContext.ObservedProjectile> _hostile = [];
    private readonly Dictionary<ActorIdentity, (int Dx, int Dy)> _drift = [];

    private ContractLens? _lens;
    private Position[] _path = new Position[16];
    private int _placementRotateTick = -PlacementRotateCooldown - 1;
    private Position? _dodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;
    private readonly Position[] _blockedTile = new Position[4];
    private readonly int[] _blockedUntilTick = new int[4];
    private int _blockedSlot;

    private Position[] _objective = [];
    private int[] _objectiveField = [];
    private Position[] _goals = [];
    private bool _holdsObjective;
    private bool _atGoal;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new ContractLens(start);
        _programCache.Clear();
        _path = new Position[Math.Max(16, _lens.Width + _lens.Height)];
        _placementRotateTick = -PlacementRotateCooldown - 1;
        _dodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
    }

    /// <inheritdoc />
    public GenericActorDecision Tick(GenericActorContext context)
    {
        ContractLens lens = _lens
            ?? throw new InvalidOperationException("StartLife was not called.");

        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(context, lens, "committed to a form transition");

        Prepare(lens, context);

        // Order is the doctrine. Bodies first, then the ground, then the gun.
        // Companions are queued the instant they are legal; the elected holder
        // walks onto the contested tiles before it trades a long-range shot;
        // an unanswerable bolt is stepped out of rather than traded with,
        // because a body that survives keeps contesting the objective.
        return TryFabricate(lens, context)
            ?? TryEvade(lens, context, lethalOnly: true)
            ?? TryReachFabricationSource(lens, context)
            ?? TryEnterObjective(lens, context)
            ?? TryCloseOnObjective(lens, context)
            ?? TryEvade(lens, context, lethalOnly: false)
            ?? TryShoot(lens, context)
            ?? TryAim(lens, context)
            ?? TryUnstickTheGun(lens, context)
            ?? TryAdvance(lens, context)
            ?? TryFaceApproach(lens, context)
            ?? Fallback(context, lens, "holding the front");
    }

    // ------------------------------------------------------------------
    // Per-tick shared state
    // ------------------------------------------------------------------

    private void Prepare(ContractLens lens, GenericActorContext context)
    {
        _objective = ActiveObjective(lens, context);
        _objectiveField = Tactics.DistanceField(
            lens,
            _objective.Length > 0 ? _objective : FallbackGoals(lens, context));

        _occupied.Clear();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            _occupied.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            _occupied.Add(enemy.Position);

        _hostile.Clear();
        if (context.VisibleProjectiles is { } projectiles)
        {
            foreach (GenericActorContext.ObservedProjectile projectile
                     in projectiles)
            {
                // Projectiles block movement and entering one is a contact, so
                // an occupied bolt tile is as impassable as a body.
                _occupied.Add(projectile.Position);
                if (projectile.OwnerTeamId != lens.TeamId)
                    _hostile.Add(projectile);
            }
        }

        _drift.Clear();
        foreach (GenericActorContext.ObservedEvent visible in context.VisibleEvents)
        {
            if (visible.Payload
                is not GenericActorContext.EventPayload.Movement movement)
            {
                continue;
            }
            if (movement.ActorId.TeamId == lens.TeamId)
                continue;
            _drift[movement.ActorId] = (
                movement.To.X - movement.From.X,
                movement.To.Y - movement.From.Y);
        }

        RememberBlocked(context);

        _holdsObjective = Contains(_objective, context.Self.Position);
        _goals = ResolveGoals(lens, context);
        _atGoal = Contains(_goals, context.Self.Position);
    }

    /// <summary>
    /// A joint step can block an individually legal move, and re-submitting it
    /// next tick usually blocks again. Remembering the refused destination for
    /// a few ticks converts a two-tick deadlock into a detour.
    /// </summary>
    private void RememberBlocked(GenericActorContext context)
    {
        Position destination = BlockedDestination(context);
        if (destination.X < 0)
            return;
        for (int index = 0; index < _blockedTile.Length; index++)
        {
            if (_blockedUntilTick[index] > context.Tick
                && _blockedTile[index] == destination)
            {
                _blockedUntilTick[index] = context.Tick + BlockedMemoryTicks;
                return;
            }
        }
        _blockedTile[_blockedSlot] = destination;
        _blockedUntilTick[_blockedSlot] = context.Tick + BlockedMemoryTicks;
        _blockedSlot = (_blockedSlot + 1) % _blockedTile.Length;
    }

    private bool RecentlyBlocked(Position tile, int tick)
    {
        for (int index = 0; index < _blockedTile.Length; index++)
        {
            if (_blockedUntilTick[index] > tick && _blockedTile[index] == tile)
                return true;
        }
        return false;
    }

    private static Position[] ActiveObjective(
        ContractLens lens,
        GenericActorContext context)
    {
        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline frontline)
        {
            return lens.ObjectiveAt(frontline.ActivePositionIndex);
        }
        return [];
    }

    private static Position[] FallbackGoals(
        ContractLens lens,
        GenericActorContext context)
    {
        if (!context.Enemies.IsEmpty)
        {
            var tiles = new Position[context.Enemies.Length];
            for (int index = 0; index < context.Enemies.Length; index++)
                tiles[index] = context.Enemies[index].Position;
            return tiles;
        }
        return [lens.ForwardPoint];
    }

    /// <summary>
    /// Splits the team between one objective occupier and forward screens.
    /// Presence is binary, so a second body on the same tile set adds nothing;
    /// the surplus bodies stand between the objective and the enemy approach
    /// where they suppress entries instead of stacking.
    /// </summary>
    private Position[] ResolveGoals(ContractLens lens, GenericActorContext context)
    {
        if (_objective.Length == 0)
            return FallbackGoals(lens, context);
        if (ObjectiveWeightOf(lens, context.Self.FormId) <= 0)
            return _objective;

        int others = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (ObjectiveWeightOf(lens, ally.FormId) > 0)
                others++;
        }

        long myRank = OccupierRank(
            lens,
            context.Self.Position,
            context.Self.FormId,
            context.Self.ActorId,
            others > 0);
        bool occupier = true;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (ObjectiveWeightOf(lens, ally.FormId) <= 0)
                continue;
            long rank = OccupierRank(
                lens,
                ally.Position,
                ally.FormId,
                ally.ActorId,
                hasCompany: true);
            if (rank < myRank)
            {
                occupier = false;
                break;
            }
        }

        if (occupier)
            return _objective;

        Position[] screen = ScreenTiles(lens);
        return screen.Length > 0 ? screen : _objective;
    }

    private long OccupierRank(
        ContractLens lens,
        Position position,
        string formId,
        ActorIdentity actorId,
        bool hasCompany)
    {
        int distance = Tactics.DistanceAt(lens, _objectiveField, position);
        // A body that can still fabricate is worth more behind the line than
        // standing on it, but only while somebody else can hold the ground.
        if (hasCompany && lens.CanFabricate(formId))
            distance += 2;
        long rank = (long)distance * 4096;
        rank += (long)actorId.UnitId * 64;
        return rank + actorId.LifeId;
    }

    private Position[] ScreenTiles(ContractLens lens)
    {
        int objectiveReach = Tactics.NearestChebyshev(lens.ForwardPoint, _objective);
        var tiles = new List<Position>();
        for (int y = 0; y < lens.Height; y++)
        {
            for (int x = 0; x < lens.Width; x++)
            {
                var tile = new Position(x, y);
                if (lens.IsClosed(tile) || Contains(_objective, tile))
                    continue;
                int reach = Tactics.NearestChebyshev(tile, _objective);
                if (reach is < 1 or > 2)
                    continue;
                if (tile.ChebyshevDistance(lens.ForwardPoint) > objectiveReach)
                    continue;
                tiles.Add(tile);
            }
        }
        return tiles.ToArray();
    }

    private static int ObjectiveWeightOf(ContractLens lens, string formId) =>
        lens.Form(formId)?.ObjectiveWeight ?? 1;

    private static bool Contains(Position[] tiles, Position tile)
    {
        for (int index = 0; index < tiles.Length; index++)
        {
            if (tiles[index] == tile)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 1. Fabrication — the doctrine's first claim on every tick
    // ------------------------------------------------------------------

    private GenericActorDecision? TryFabricate(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorActionLegality? fabricate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Fabrication);
        if (fabricate is null)
            return null;

        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = Constraint<GenericActorActionLegality.ArgumentConstraint
                .UnitTargetConstraint>(fabricate);
        if (targets is null || targets.AllowedValues.IsEmpty)
            return null;

        GenericActorActionArgument.UnitTarget target = targets.AllowedValues[0];

        if (TryImprovePlacement(lens, context) is Direction facing)
        {
            _placementRotateTick = context.Tick;
            GenericActorDecision? rotate = Rotate(
                lens,
                context,
                facing,
                $"turning so companion {target.TeamId}:{target.UnitId} lands forward");
            if (rotate is not null)
                return rotate;
        }

        return new GenericActorDecision(
            fabricate.ActionId,
            fabricate.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(target)],
            $"queueing companion {target.TeamId}:{target.UnitId}");
    }

    /// <summary>
    /// The host takes the first eligible declared offset relative to the source
    /// pose at queue time, so facing is the only lever a fabricator has over
    /// where its child appears. When the front is quiet, spend one tick turning
    /// so the child lands toward the objective instead of behind it.
    /// </summary>
    private Direction? TryImprovePlacement(
        ContractLens lens,
        GenericActorContext context)
    {
        if (lens.PlacementOffsets.IsDefaultOrEmpty || _objective.Length == 0)
            return null;
        if (context.Tick - _placementRotateTick <= PlacementRotateCooldown)
            return null;
        if (ImpactIn(lens, context.Self.Position) <= ThreatHorizonAdvances)
            return null;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (enemy.Position.ChebyshevDistance(context.Self.Position)
                <= CloseContactRange + 1)
            {
                return null;
            }
        }

        GenericActorActionLegality? rotate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        if (rotate is null)
            return null;
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = Constraint<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>(rotate);
        if (allowed is null)
            return null;

        int current = PlacementScore(lens, context, context.Self.Facing);
        int bestScore = current;
        Direction? best = null;
        foreach (Direction candidate in Tactics.Cardinals)
        {
            if (candidate == context.Self.Facing
                || !allowed.AllowedValues.Contains(candidate))
            {
                continue;
            }
            int score = PlacementScore(lens, context, candidate);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return current - bestScore >= PlacementRotateGain ? best : null;
    }

    private int PlacementScore(
        ContractLens lens,
        GenericActorContext context,
        Direction facing)
    {
        Position? tile = PredictChildTile(lens, context, facing);
        return tile is Position placed
            ? Tactics.NearestChebyshev(placed, _objective)
            : Tactics.Unreachable;
    }

    private Position? PredictChildTile(
        ContractLens lens,
        GenericActorContext context,
        Direction facing)
    {
        (int forwardX, int forwardY) = facing.Vector();
        (int rightX, int rightY) = facing.TurnedRight().Vector();
        foreach (GenericActorRulesContract.RelativePositionOffset offset
                 in lens.PlacementOffsets)
        {
            Position tile = context.Self.Position.Offset(
                (offset.Forward * forwardX) + (offset.Right * rightX),
                (offset.Forward * forwardY) + (offset.Right * rightY));
            if (lens.IsWall(tile))
                continue;
            if (lens.FabricationOutputTiles.Count > 0
                && !lens.FabricationOutputTiles.Contains(tile))
            {
                continue;
            }
            if (lens.ReservedSpawnTiles.Contains(tile) || _occupied.Contains(tile))
                continue;
            return tile;
        }
        return null;
    }

    // ------------------------------------------------------------------
    // 2. Evasion that refuses to concede the objective for free
    // ------------------------------------------------------------------

    /// <summary>
    /// Evasion is a comparison, not a reflex. Each candidate tile is rated by
    /// how many projectile advances away its first contact is and by how many
    /// escape hatches it offers afterwards; the body only spends the tick when
    /// some tile is strictly safer than standing still, and never when leaving
    /// would hand over an objective it is currently the reason for holding.
    /// </summary>
    private GenericActorDecision? TryEvade(
        ContractLens lens,
        GenericActorContext context,
        bool lethalOnly)
    {
        int stayImpact = ImpactIn(lens, context.Self.Position);
        if (stayImpact > ThreatHorizonAdvances)
            return null;

        bool lethal = IncomingDamageAt(lens, context.Self.Position)
            >= context.Self.Health;
        if (lethalOnly && !lethal)
            return null;

        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        long bestScore = SafetyScore(
            stayImpact,
            EscapeCount(lens, context.Self.Position),
            _holdsObjective,
            KeepsFiringSolution(lens, context, context.Self.Position),
            direction: null);
        Direction? best = null;
        foreach (Direction direction in Tactics.Cardinals)
        {
            if (!allowed.AllowedValues.Contains(direction))
                continue;
            Position destination = Step(context, direction);
            if (lens.IsClosed(destination) || _occupied.Contains(destination))
                continue;
            // Never trade an objective this body is holding for a survivable
            // hit; concession is only worth it when the alternative is death.
            if (!lethal && _holdsObjective && !Contains(_objective, destination))
                continue;

            long score = SafetyScore(
                ImpactIn(lens, destination),
                EscapeCount(lens, destination),
                Contains(_objective, destination),
                KeepsFiringSolution(lens, context, destination),
                direction);
            if (score < bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        if (best is null)
            return null;

        _dodgeOrigin = context.Self.Position;
        _avoidDodgeOriginThroughTick = context.Tick + 1;
        return Move(
            move,
            best.Value,
            lethal ? "breaking a lethal line" : "sidestepping without conceding");
    }

    private static long SafetyScore(
        int impact,
        int escapes,
        bool onObjective,
        bool keepsFiringSolution,
        Direction? direction)
    {
        long score = ThreatHorizonAdvances + 1
            - Math.Min(impact, ThreatHorizonAdvances + 1);
        score = (score * 2) + (onObjective ? 0 : 1);
        score = (score * 2) + (keepsFiringSolution ? 0 : 1);
        score = (score * 8) + (7 - Math.Min(7, escapes));
        return (score * 8) + (direction is Direction value ? (int)value : 0);
    }

    /// <summary>
    /// Whether some facing from the tile still puts a legal trajectory on a
    /// visible enemy. Giving ground is cheap; giving up the shot is not.
    /// </summary>
    private bool KeepsFiringSolution(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        if (profile is null || context.Enemies.IsEmpty)
            return false;
        foreach (Direction facing in Tactics.Cardinals)
        {
            foreach (ShotProgram program in ProgramsFor(profile))
            {
                int count = Tactics.WalkProgram(
                    lens,
                    tile,
                    facing,
                    program,
                    profile.Projectile.MaxTravelTiles,
                    profile.Projectile.DiagonalCornersMustBeClear,
                    _path);
                if (ScorePath(lens, context, profile, count, bendPenalty: 0)
                    < long.MaxValue)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Steps into the contested region the moment one is adjacent. Presence is
    /// the only thing that moves the objective clock, so a survivable bolt is
    /// an acceptable price for standing on the ground.
    /// </summary>
    private GenericActorDecision? TryEnterObjective(
        ContractLens lens,
        GenericActorContext context)
    {
        if (_objective.Length == 0 || _holdsObjective)
            return null;
        if (ObjectiveWeightOf(lens, context.Self.FormId) <= 0)
            return null;

        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        Direction? best = null;
        long bestScore = long.MaxValue;
        foreach (Direction direction in Tactics.Cardinals)
        {
            if (!allowed.AllowedValues.Contains(direction))
                continue;
            Position destination = Step(context, direction);
            if (!Contains(_objective, destination)
                || lens.IsClosed(destination)
                || _occupied.Contains(destination)
                || RecentlyBlocked(destination, context.Tick))
            {
                continue;
            }
            if (IncomingDamageAt(lens, destination) >= context.Self.Health)
                continue;
            long score = SafetyScore(
                ImpactIn(lens, destination),
                EscapeCount(lens, destination),
                onObjective: true,
                KeepsFiringSolution(lens, context, destination),
                direction);
            if (score < bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        return best is null
            ? null
            : Move(move, best.Value, "entering the contested ground");
    }

    private static Position Step(GenericActorContext context, Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return context.Self.Position.Offset(dx, dy);
    }

    /// <summary>
    /// Projectile advances until the first hostile bolt would enter the tile,
    /// or one past the horizon when none does. Range, cadence, tiles per
    /// advance, and corner strictness all come from the observed projectile.
    /// </summary>
    private int ImpactIn(ContractLens lens, Position tile)
    {
        int soonest = ThreatHorizonAdvances + 1;
        foreach (GenericActorContext.ObservedProjectile projectile in _hostile)
        {
            for (int advance = 1; advance < soonest; advance++)
            {
                if (Tactics.Threatens(
                        lens,
                        projectile,
                        tile,
                        advance,
                        strictCorners: true))
                {
                    soonest = advance;
                    break;
                }
            }
        }
        return soonest;
    }

    private int EscapeCount(ContractLens lens, Position tile)
    {
        int escapes = 0;
        foreach (Direction direction in Tactics.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position neighbour = tile.Offset(dx, dy);
            if (lens.IsClosed(neighbour) || _occupied.Contains(neighbour))
                continue;
            if (ImpactIn(lens, neighbour) > ThreatHorizonAdvances)
                escapes++;
        }
        return escapes;
    }

    private int IncomingDamageAt(ContractLens lens, Position tile)
    {
        int damage = 0;
        foreach (GenericActorContext.ObservedProjectile projectile in _hostile)
        {
            if (Tactics.Threatens(
                    lens,
                    projectile,
                    tile,
                    ThreatHorizonAdvances,
                    strictCorners: true))
            {
                damage += MaxDamagePerHit(lens);
            }
        }
        return damage;
    }

    private static int MaxDamagePerHit(ContractLens lens)
    {
        int damage = 1;
        foreach (GenericActorRulesContract.AttackProfile profile
                 in lens.Contract.Rules.AttackProfiles)
        {
            damage = Math.Max(damage, profile.Projectile.DamagePerHit);
        }
        return damage;
    }

    // ------------------------------------------------------------------
    // 3. Fire — straight, aimed, or curved, whatever the contract allows
    // ------------------------------------------------------------------

    private GenericActorDecision? TryShoot(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        if (profile is null || context.Enemies.IsEmpty)
            return null;

        GenericActorDecision? best = null;
        long bestScore = long.MaxValue;
        foreach (GenericActorActionLegality action
                 in AvailableByKind(
                     lens,
                     context,
                     GenericActorRulesContract.ActionKind.Attack))
        {
            if (lens.HasParameter(
                    action.ActionId,
                    GenericActorRulesContract.ActionParameterKind
                        .ProjectileHeading))
            {
                EvaluateHeadings(
                    lens,
                    context,
                    profile,
                    action,
                    ref best,
                    ref bestScore);
                continue;
            }
            EvaluatePrograms(
                lens,
                context,
                profile,
                action,
                ref best,
                ref bestScore);
        }
        return best;
    }

    private void EvaluateHeadings(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile profile,
        GenericActorActionLegality action,
        ref GenericActorDecision? best,
        ref long bestScore)
    {
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = Constraint<GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>(action);
        if (headings is null)
            return;

        foreach (ProjectileHeading heading in headings.AllowedValues)
        {
            int count = Tactics.WalkHeading(
                lens,
                context.Self.Position,
                heading,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            long score = ScorePath(lens, context, profile, count, bendPenalty: 0);
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                $"omnidirectional fire {heading}");
        }
    }

    private void EvaluatePrograms(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile profile,
        GenericActorActionLegality action,
        ref GenericActorDecision? best,
        ref long bestScore)
    {
        bool hasProgramParameter = lens.HasParameter(
            action.ActionId,
            GenericActorRulesContract.ActionParameterKind.ShotProgram);
        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programConstraint = Constraint<GenericActorActionLegality
                .ArgumentConstraint.ShotProgramConstraint>(action);
        bool payloadAllowed = hasProgramParameter
            && profile.ShotProgram.Enabled
            && programConstraint is { Allowed: true };

        ShotProgram straight = Default(profile);
        foreach (ShotProgram program in ProgramsFor(profile))
        {
            bool isDefault = program.Equals(straight);
            if (!isDefault && !payloadAllowed)
                continue;

            int count = Tactics.WalkProgram(
                lens,
                context.Self.Position,
                context.Self.Facing,
                program,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            long score = ScorePath(
                lens,
                context,
                profile,
                count,
                program.BendCount + Math.Abs(program.InitialAimOffset));
            if (score >= bestScore)
                continue;

            bool omitPayload = isDefault
                && (!hasProgramParameter || profile.ShotProgram.PayloadOptional);
            bestScore = score;
            best = omitPayload
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    "straight fire")
                : new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [new GenericActorActionArgument.ShotProgramArgument(program)],
                    program.BendCount > 0 ? "curved intercept" : "aimed fire");
        }
    }

    /// <summary>
    /// Rates one candidate trajectory: earliest contact wins, an enemy already
    /// standing on the contested objective outranks one that is not, and a
    /// straight bolt outranks an equivalent curved one.
    /// </summary>
    private long ScorePath(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile profile,
        int pathLength,
        int bendPenalty)
    {
        long best = long.MaxValue;
        for (int index = 0; index < pathLength; index++)
        {
            Position tile = _path[index];
            int arrival = Tactics.ArrivalTick(profile.Projectile, index);
            foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            {
                bool hit = enemy.Position == tile;
                if (!hit
                    && _drift.TryGetValue(
                        enemy.ActorId,
                        out (int Dx, int Dy) drift))
                {
                    Position predicted = enemy.Position.Offset(
                        drift.Dx * arrival,
                        drift.Dy * arrival);
                    hit = predicted == tile && !lens.IsClosed(predicted);
                }
                if (!hit)
                    continue;

                long score = Contains(_objective, enemy.Position) ? 0 : 1;
                score = (score * 64) + Math.Min(63, arrival);
                score = (score * 64) + Math.Min(63, enemy.Health);
                score = (score * 64) + Math.Min(63, bendPenalty);
                best = Math.Min(best, score);
            }
        }
        return best;
    }

    private ShotProgram[] ProgramsFor(
        GenericActorRulesContract.AttackProfile profile)
    {
        if (_programCache.TryGetValue(profile.Id, out ShotProgram[]? cached))
            return cached;

        var programs = new List<ShotProgram> { Default(profile) };
        GenericActorRulesContract.ShotProgramDefinition definition =
            profile.ShotProgram;
        if (definition.Enabled)
        {
            GenericActorRulesContract.AimOnlyShotProgramValue aim =
                definition.AimOnlyProgram;
            for (int offset = definition.MinInitialAimSteps;
                 offset <= definition.MaxInitialAimSteps;
                 offset++)
            {
                var candidate = new ShotProgram(
                    offset,
                    aim.BendDirection,
                    aim.BendAfterTiles,
                    aim.BendEveryTiles,
                    aim.BendCount);
                if (!programs.Contains(candidate))
                    programs.Add(candidate);
            }

            for (int count = Math.Max(1, definition.MinBendCount);
                 count <= definition.MaxBendCount && programs.Count < MaxPrograms;
                 count++)
            {
                for (int after = definition.MaxBendAfterTiles;
                     after >= definition.MinBendAfterTiles
                     && programs.Count < MaxPrograms;
                     after--)
                {
                    for (int every = Math.Max(1, definition.MinBendEveryTiles);
                         every <= definition.MaxBendEveryTiles
                         && programs.Count < MaxPrograms;
                         every++)
                    {
                        for (int offset = definition.MinInitialAimSteps;
                             offset <= definition.MaxInitialAimSteps
                             && programs.Count < MaxPrograms;
                             offset++)
                        {
                            foreach (int bend
                                     in definition.AllowedCurvedBendDirections)
                            {
                                if (programs.Count >= MaxPrograms)
                                    break;
                                programs.Add(new ShotProgram(
                                    offset,
                                    bend,
                                    after,
                                    every,
                                    count));
                            }
                        }
                    }
                }
            }
        }

        ShotProgram[] resolved = programs.ToArray();
        _programCache[profile.Id] = resolved;
        return resolved;
    }

    private static ShotProgram Default(
        GenericActorRulesContract.AttackProfile profile)
    {
        GenericActorRulesContract.ShotProgramValue value =
            profile.ShotProgram.DefaultProgram;
        return new ShotProgram(
            value.InitialAimOffset,
            value.BendDirection,
            value.BendAfterTiles,
            value.BendEveryTiles,
            value.BendCount);
    }

    // ------------------------------------------------------------------
    // 4. Aim — a facing-locked gun is worth a tick when contact is close
    // ------------------------------------------------------------------

    private GenericActorDecision? TryAim(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        if (profile is null || profile.OmnidirectionalAim || context.Enemies.IsEmpty)
            return null;

        int nearest = Tactics.Unreachable;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            nearest = Math.Min(
                nearest,
                enemy.Position.ChebyshevDistance(context.Self.Position));
        }
        if (!_atGoal && !_holdsObjective && nearest > CloseContactRange)
            return null;

        GenericActorActionLegality? rotate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = rotate is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(rotate);
        if (rotate is null || allowed is null)
            return null;

        Direction? best = null;
        long bestScore = long.MaxValue;
        ShotProgram straight = Default(profile);
        foreach (Direction direction in Tactics.Cardinals)
        {
            if (direction == context.Self.Facing
                || !allowed.AllowedValues.Contains(direction))
            {
                continue;
            }
            int count = Tactics.WalkProgram(
                lens,
                context.Self.Position,
                direction,
                straight,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            long score = ScorePath(lens, context, profile, count, bendPenalty: 0);
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = direction;
        }
        return best is null
            ? null
            : Rotate(lens, context, best.Value, "laying the gun on the front");
    }

    // ------------------------------------------------------------------
    // 5. Suppression — never trade a firing line for a staring contest
    // ------------------------------------------------------------------

    /// <summary>
    /// Two bodies that share a region but no straight line will otherwise stand
    /// still until the tick cap. When neither the gun nor a rotation can reach
    /// an enemy, step to an adjacent tile that opens a line — but only to a
    /// tile that keeps the objective presence this body already provides.
    /// </summary>
    private GenericActorDecision? TryUnstickTheGun(
        ContractLens lens,
        GenericActorContext context)
    {
        if (context.Enemies.IsEmpty || (!_atGoal && !_holdsObjective))
            return null;
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        // Repositioning is only worth a tick while the gun is actually loaded;
        // during cooldown the same step is a free hit for the opponent.
        if (profile is null
            || FirstAvailable(
                lens,
                context,
                GenericActorRulesContract.ActionKind.Attack) is null)
        {
            return null;
        }

        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        int here = Tactics.DistanceAt(
            lens,
            _objectiveField,
            context.Self.Position);
        Position blocked = BlockedDestination(context);
        ShotProgram straight = Default(profile);
        Direction? best = null;
        long bestScore = long.MaxValue;
        foreach (Direction direction in Tactics.Cardinals)
        {
            if (!allowed.AllowedValues.Contains(direction))
                continue;
            Position destination = Step(context, direction);
            if (lens.IsClosed(destination)
                || _occupied.Contains(destination)
                || destination == blocked
                || RecentlyBlocked(destination, context.Tick)
                || ImpactIn(lens, destination) <= ThreatHorizonAdvances)
            {
                continue;
            }
            if (_holdsObjective && !Contains(_objective, destination))
                continue;
            if (!_holdsObjective
                && Tactics.DistanceAt(lens, _objectiveField, destination) > here)
            {
                continue;
            }
            // A tile an enemy body can also reach this tick is a coin flip that
            // usually ends with both moves refused; take the line elsewhere.
            if (Contested(context, destination))
                continue;

            foreach (Direction facing in Tactics.Cardinals)
            {
                int count = Tactics.WalkProgram(
                    lens,
                    destination,
                    facing,
                    straight,
                    profile.Projectile.MaxTravelTiles,
                    profile.Projectile.DiagonalCornersMustBeClear,
                    _path);
                long score = ScorePath(
                    lens,
                    context,
                    profile,
                    count,
                    bendPenalty: 0);
                if (score >= bestScore)
                    continue;
                bestScore = score;
                best = direction;
            }
        }
        return best is null
            ? null
            : Move(move, best.Value, "stepping into a firing line");
    }

    private static bool Contested(GenericActorContext context, Position tile)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (enemy.Position.ChebyshevDistance(tile) <= 1)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 6. Rejoin the fabrication source when the contract binds it to a region
    // ------------------------------------------------------------------

    private GenericActorDecision? TryReachFabricationSource(
        ContractLens lens,
        GenericActorContext context)
    {
        if (!lens.CanFabricate(context.Self.FormId))
            return null;
        if (lens.FabricationSourceTiles.Count == 0)
            return null;
        if (lens.FabricationSourceTiles.Contains(context.Self.Position))
            return null;

        int[] field = Tactics.DistanceField(lens, lens.FabricationSourceTiles);
        int distance = Tactics.DistanceAt(lens, field, context.Self.Position);
        if (distance >= Tactics.Unreachable)
            return null;
        if (!SlotDueWithin(context, lens.TeamId, distance + 2))
            return null;

        return StepAlong(lens, context, field, "returning to the fabrication pad");
    }

    private static bool SlotDueWithin(
        GenericActorContext context,
        int teamId,
        int ticks)
    {
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.TeamId != teamId)
                continue;
            switch (slot.State)
            {
                case GenericActorContext.UnitSlotState.Ready:
                    return true;
                case GenericActorContext.UnitSlotState.AvailabilityPending pending
                    when pending.DueTick - context.Tick <= ticks:
                    return true;
                default:
                    continue;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 7. Presence — enter the contested ground early and stay on it
    // ------------------------------------------------------------------

    /// <summary>
    /// The body the team has elected to hold the ground walks to it before it
    /// trades shots: a bolt fired from six tiles away is worth far less than
    /// the capture ticks lost standing in a corridor to fire it.
    /// </summary>
    private GenericActorDecision? TryCloseOnObjective(
        ContractLens lens,
        GenericActorContext context)
    {
        if (_holdsObjective
            || _objective.Length == 0
            || !ReferenceEquals(_goals, _objective))
        {
            return null;
        }
        return StepAlong(
            lens,
            context,
            _objectiveField,
            "closing on the contested ground");
    }

    private GenericActorDecision? TryAdvance(
        ContractLens lens,
        GenericActorContext context)
    {
        if (_atGoal)
            return null;

        int[] field = ReferenceEquals(_goals, _objective)
            ? _objectiveField
            : Tactics.DistanceField(lens, _goals);
        return StepAlong(lens, context, field, "taking the contested ground");
    }

    private GenericActorDecision? StepAlong(
        ContractLens lens,
        GenericActorContext context,
        int[] field,
        string reason)
    {
        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        int here = Tactics.DistanceAt(lens, field, context.Self.Position);
        Position blocked = BlockedDestination(context);
        Direction? best = null;
        long bestScore = long.MaxValue;
        foreach (Direction direction in Tactics.Cardinals)
        {
            if (!allowed.AllowedValues.Contains(direction))
                continue;
            Position destination = Step(context, direction);
            if (lens.IsClosed(destination) || _occupied.Contains(destination))
                continue;
            if (destination == blocked || RecentlyBlocked(destination, context.Tick))
                continue;
            if (_dodgeOrigin is Position origin
                && context.Tick <= _avoidDodgeOriginThroughTick
                && destination == origin)
            {
                continue;
            }
            int distance = Tactics.DistanceAt(lens, field, destination);
            if (distance >= here)
                continue;
            // Walking into a bolt that lands sooner than the one already
            // aimed at this tile is never progress, however short the path.
            int impact = ImpactIn(lens, destination);
            if (impact < ImpactIn(lens, context.Self.Position))
                continue;
            long score = impact <= ThreatHorizonAdvances ? 1 : 0;
            score = (score * 4096) + distance;
            score = (score * 8) + (int)direction;
            if (score < bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        return best is null ? null : Move(move, best.Value, reason);
    }

    // ------------------------------------------------------------------
    // 8. Vigilance — a facing-quadrant sensor is worthless pointed backwards
    // ------------------------------------------------------------------

    /// <summary>
    /// A body that has finished moving still owes the team a direction: sight
    /// and, for facing-locked guns, the whole firing arc follow the facing, so
    /// an idle tick is spent pointing at wherever the next contact must come
    /// from rather than at nothing.
    /// </summary>
    private GenericActorDecision? TryFaceApproach(
        ContractLens lens,
        GenericActorContext context)
    {
        Position anchor = ApproachAnchor(lens, context);
        int dx = anchor.X - context.Self.Position.X;
        int dy = anchor.Y - context.Self.Position.Y;
        if (dx == 0 && dy == 0)
            return null;
        Direction desired = Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
        if (desired == context.Self.Facing)
            return null;
        return Rotate(lens, context, desired, "watching the enemy approach");
    }

    private Position ApproachAnchor(ContractLens lens, GenericActorContext context)
    {
        Position nearest = default;
        int best = Tactics.Unreachable;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            int distance = enemy.Position.ChebyshevDistance(context.Self.Position);
            if (distance < best)
            {
                best = distance;
                nearest = enemy.Position;
            }
        }
        if (best < Tactics.Unreachable)
            return nearest;

        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline frontline)
        {
            Position[] ahead = lens.ObjectiveAt(
                frontline.ActivePositionIndex + lens.AdvanceDelta);
            if (ahead.Length > 0)
                return ahead[0];
        }
        return lens.ForwardPoint;
    }

    private static Position BlockedDestination(GenericActorContext context)
    {
        if (context.Self.PreviousActionResolution
            is not
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous)
        {
            return new Position(-1, -1);
        }
        foreach (GenericActorActionArgument argument
                 in previous.AcceptedAction.Arguments)
        {
            if (argument is GenericActorActionArgument.DirectionArgument direction)
            {
                (int dx, int dy) = direction.Value.Vector();
                return context.Self.Position.Offset(dx, dy);
            }
        }
        return new Position(-1, -1);
    }

    // ------------------------------------------------------------------
    // Action plumbing
    // ------------------------------------------------------------------

    private static GenericActorDecision Move(
        GenericActorActionLegality move,
        Direction direction,
        string reason) =>
        new(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);

    private GenericActorDecision? Rotate(
        ContractLens lens,
        GenericActorContext context,
        Direction direction,
        string reason)
    {
        GenericActorActionLegality? rotate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = rotate is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(rotate);
        if (rotate is null
            || allowed is null
            || !allowed.AllowedValues.Contains(direction))
        {
            return null;
        }
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
    }

    private static List<GenericActorActionLegality> AvailableByKind(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        var matches = new List<GenericActorActionLegality>();
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available && lens.KindOf(action.ActionId) == kind)
                matches.Add(action);
        }
        return matches;
    }

    private static GenericActorActionLegality? FirstAvailable(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available && lens.KindOf(action.ActionId) == kind)
                return action;
        }
        return null;
    }

    private static TConstraint? Constraint<TConstraint>(
        GenericActorActionLegality action)
        where TConstraint : GenericActorActionLegality.ArgumentConstraint
    {
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            if (constraint is TConstraint typed)
                return typed;
        }
        return null;
    }

    /// <summary>
    /// Last resort that always produces one legal bounded action, whatever the
    /// current form's mask happens to allow.
    /// </summary>
    private static GenericActorDecision Fallback(
        GenericActorContext context,
        ContractLens lens,
        string reason)
    {
        GenericActorActionLegality? wait = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Wait);
        if (wait is not null)
        {
            return GenericActorDecision.WithoutArguments(
                wait.ActionId,
                wait.ActionCode,
                reason);
        }

        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (!action.Available)
                continue;
            if (TryBuildArguments(
                    action,
                    out ImmutableArray<GenericActorActionArgument> arguments))
            {
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    arguments,
                    reason);
            }
        }

        GenericActorActionLegality last = context.ActionLegalities[0];
        return GenericActorDecision.WithoutArguments(
            last.ActionId,
            last.ActionCode,
            reason);
    }

    private static bool TryBuildArguments(
        GenericActorActionLegality action,
        out ImmutableArray<GenericActorActionArgument> arguments)
    {
        var built = new List<GenericActorActionArgument>();
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            switch (constraint)
            {
                case GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint directions:
                    if (directions.AllowedValues.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(new GenericActorActionArgument.DirectionArgument(
                        directions.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint units:
                    if (units.AllowedValues.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(new GenericActorActionArgument.UnitTargetArgument(
                        units.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint forms:
                    if (forms.AllowedFormIds.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(new GenericActorActionArgument.FormTargetArgument(
                        forms.AllowedFormIds[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint headings:
                    if (headings.AllowedValues.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(
                        new GenericActorActionArgument.ProjectileHeadingArgument(
                            headings.AllowedValues[0]));
                    break;
                default:
                    break;
            }
        }
        arguments = [.. built];
        return true;
    }
}
