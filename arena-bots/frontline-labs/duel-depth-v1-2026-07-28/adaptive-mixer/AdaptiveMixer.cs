using BotArena.Sdk;

/// <summary>
/// Mixes straight and single-bend commitments, then biases later bends toward
/// the lateral response observed after its previous fire.
/// </summary>
public sealed class AdaptiveMixer : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;
    private Position? _previousEnemyPosition;
    private int _lastShotTick = int.MinValue;
    private int _observedBendBias;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife was not called.");
        GenericActorContext.ObservedEnemyState? enemy = context.Enemies
            .OrderBy(candidate =>
                context.Self.Position.ChebyshevDistance(
                    candidate.Position))
            .ThenBy(candidate => candidate.ActorId)
            .FirstOrDefault();

        LearnResponse(context, enemy);

        GenericActorDecision? decision =
            ArenaBasics.TryFabricateReady(contract, context)
            ?? TryMixedShot(contract, context, enemy)
            ?? ArenaBasics.TryDodge(contract, context)
            ?? ArenaBasics.TryDirectShot(contract, context)
            ?? ArenaBasics.TryAdvanceToActiveObjective(contract, context);

        _previousEnemyPosition = enemy?.Position;
        return decision
            ?? ArenaBasics.Wait(context, "holding for another trajectory");
    }

    private void LearnResponse(
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState? enemy)
    {
        if (enemy is null
            || _previousEnemyPosition is not Position previous
            || context.Tick - _lastShotTick is <= 0 or > 4)
        {
            return;
        }

        int moveX = enemy.Position.X - previous.X;
        int moveY = enemy.Position.Y - previous.Y;
        if (moveX == 0 && moveY == 0)
            return;

        (int facingX, int facingY) = context.Self.Facing.Vector();
        int cross = facingX * moveY - facingY * moveX;
        if (cross != 0)
        {
            int observed = Math.Sign(cross);
            _observedBendBias = Math.Clamp(
                _observedBendBias + observed,
                -3,
                3);
        }
    }

    private GenericActorDecision? TryMixedShot(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState? enemy)
    {
        if (enemy is null)
            return null;

        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                candidate.Id == context.Self.FormId);
        GenericActorRulesContract.AttackProfile? profile =
            form?.AttackProfileId is string profileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == profileId)
                : null;
        if (profile is null
            || !profile.ShotProgram.Enabled
            || profile.ShotProgram.MinInitialAimSteps > 0
            || profile.ShotProgram.MaxInitialAimSteps < 0
            || profile.ShotProgram.MinBendCount > 1
            || profile.ShotProgram.MaxBendCount < 1)
        {
            return null;
        }

        HashSet<string> attackIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? action = context.ActionLegalities
            .Where(candidate =>
                candidate.Available
                && attackIds.Contains(candidate.ActionId))
            .FirstOrDefault(candidate =>
                candidate.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint>()
                    .Any(constraint => constraint.Allowed));
        if (action is null)
            return null;

        (int facingX, int facingY) = context.Self.Facing.Vector();
        int toEnemyX = enemy.Position.X - context.Self.Position.X;
        int toEnemyY = enemy.Position.Y - context.Self.Position.Y;
        int forward = facingX * toEnemyX + facingY * toEnemyY;
        int distance = context.Self.Position.ChebyshevDistance(
            enemy.Position);
        if (forward <= 0
            || distance > profile.Projectile.MaxTravelTiles)
        {
            return null;
        }

        int randomChoice = context.Random.NextInt(0, 3) - 1;
        int bendDirection = _observedBendBias == 0
            ? randomChoice
            : context.Random.NextInt(0, 3) == 0
                ? 0
                : Math.Sign(_observedBendBias);
        if (bendDirection == 0
            && profile.ShotProgram.PayloadOptional)
        {
            _lastShotTick = context.Tick;
            return GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                "mix: straight commitment");
        }
        if (!profile.ShotProgram.AllowedCurvedBendDirections.Contains(
                bendDirection))
        {
            return null;
        }

        int bendAfter = Math.Clamp(
            Math.Max(1, forward - 1),
            profile.ShotProgram.MinBendAfterTiles,
            profile.ShotProgram.MaxBendAfterTiles);
        var program = new ShotProgram(
            InitialAimOffset: 0,
            BendDirection: bendDirection,
            BendAfterTiles: bendAfter,
            BendEveryTiles: profile.ShotProgram.MinBendEveryTiles,
            BendCount: 1);
        IReadOnlyList<Position> path = ShotPaths.Preview(
            context.Self.Position,
            context.Self.Facing,
            program,
            profile.Projectile.MaxTravelTiles,
            position => IsWall(contract.Map, position));
        if (path.Count == 0)
            return null;

        _lastShotTick = context.Tick;
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.ShotProgramArgument(program),
            ],
            $"mix: bend {bendDirection:+#;-#} after {bendAfter}");
    }

    private static bool IsWall(
        GenericActorMapContract map,
        Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= map.Width
        || position.Y >= map.Height
        || map.TileRows[position.Y][position.X] == '#';
}
