using BotArena.Sdk;

namespace BotArena.Cli;

/// <summary>
/// Framework-owned deterministic pressure controller for the opening-entry
/// probe. It uses only the public generic SDK contract and receives no
/// privileged match state.
/// </summary>
internal sealed class FrontlineLabsQualificationSentinel : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife was not called.");
        return TryStraightShot(contract, context)
            ?? TryFaceVisibleEnemy(contract, context)
            ?? Wait(context);
    }

    private static GenericActorDecision? TryStraightShot(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorContext.ObservedEnemyState? target = context.Enemies
            .OrderBy(enemy =>
                context.Self.Position.ChebyshevDistance(
                    enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault(enemy =>
                IsForwardAligned(
                    context.Self.Position,
                    context.Self.Facing,
                    enemy.Position));
        if (target is null)
            return null;

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
            .OrderBy(candidate => candidate.ActionId, StringComparer.Ordinal)
            .FirstOrDefault(candidate =>
                candidate.Constraints.Any(constraint =>
                    constraint is GenericActorActionLegality
                        .ArgumentConstraint.ShotProgramConstraint));
        if (action is null)
            return null;

        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                candidate.Id == context.Self.FormId);
        GenericActorRulesContract.AttackProfile? profile =
            form?.AttackProfileId is string profileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == profileId)
                : null;
        if (profile?.ShotProgram.PayloadOptional == true)
        {
            return GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                "qualification sentinel: straight pressure");
        }

        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.ShotProgramArgument(
                    ShotProgram.Straight),
            ],
            "qualification sentinel: explicit straight pressure");
    }

    private static GenericActorDecision? TryFaceVisibleEnemy(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorContext.ObservedEnemyState? target = context.Enemies
            .OrderBy(enemy =>
                context.Self.Position.ChebyshevDistance(
                    enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (target is null
            || !TryCardinalDirection(
                context.Self.Position,
                target.Position,
                out Direction direction)
            || direction == context.Self.Facing)
        {
            return null;
        }

        HashSet<string> rotationIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Rotation)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? action = context.ActionLegalities
            .Where(candidate =>
                candidate.Available
                && rotationIds.Contains(candidate.ActionId))
            .FirstOrDefault(candidate =>
                candidate.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                    .Any(constraint =>
                        constraint.AllowedValues.Contains(direction)));
        if (action is null)
            return null;

        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            $"qualification sentinel: face {direction}");
    }

    private static GenericActorDecision Wait(
        GenericActorContext context)
    {
        GenericActorActionLegality action = context.ActionLegalities
            .Single(candidate =>
                candidate.Available
                && candidate.ActionId == "wait");
        return GenericActorDecision.WithoutArguments(
            action.ActionId,
            action.ActionCode,
            "qualification sentinel: hold lane");
    }

    private static bool IsForwardAligned(
        Position source,
        Direction facing,
        Position target)
    {
        (int dx, int dy) = facing.Vector();
        int targetX = target.X - source.X;
        int targetY = target.Y - source.Y;
        return dx == 0
            ? targetX == 0 && Math.Sign(targetY) == dy
            : targetY == 0 && Math.Sign(targetX) == dx;
    }

    private static bool TryCardinalDirection(
        Position source,
        Position target,
        out Direction direction)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        if (dx == 0 && dy != 0)
        {
            direction = dy < 0
                ? Direction.North
                : Direction.South;
            return true;
        }
        if (dy == 0 && dx != 0)
        {
            direction = dx < 0
                ? Direction.West
                : Direction.East;
            return true;
        }

        direction = default;
        return false;
    }
}
