using BotArena.Sdk;

/// <summary>
/// The one place that knows what a step costs. A movement profile may leave
/// facing alone, set facing to the direction moved, or refuse every direction
/// except the current facing, and the difference decides whether a route is one
/// tick per tile or two, whether a retreat also throws away the aim and the
/// vision cone it was holding, and whether a placement facing has to be bought
/// with a rotation at all. Everything is read from the declared movement
/// profile; nothing here is conditioned on an arm name.
/// </summary>
internal sealed class Kinematics
{
    private readonly MatchLens _lens;

    public Kinematics(MatchLens lens, GenericActorContext context)
    {
        _lens = lens;
        Coupling = lens.Coupling(context.Self.FormId);
        Facing = context.Self.Facing;
    }

    /// <summary>Declared facing coupling of the current form's movement.</summary>
    public GenericActorRulesContract.MovementFacingCoupling Coupling { get; }

    /// <summary>Facing this life carries into the decision.</summary>
    public Direction Facing { get; }

    /// <summary>A successful step turns the body to the direction moved.</summary>
    public bool TurnsOnMove =>
        Coupling
            == GenericActorRulesContract.MovementFacingCoupling
                .FaceMovementDirection;

    /// <summary>Only the current facing is a legal movement direction.</summary>
    public bool MoveLocked =>
        Coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;

    /// <summary>Ticks a step in this direction costs, rotation included.</summary>
    public int StepCost(Direction direction) =>
        MoveLocked && direction != Facing ? 2 : 1;

    /// <summary>Facing the body will carry after stepping this way.</summary>
    public Direction FacingAfter(Direction step) =>
        TurnsOnMove ? step : Facing;

    /// <summary>
    /// Turns a desired cardinal step into a legal decision. Under a locked
    /// profile the mask offers only the current facing, so the desired step is
    /// bought with a rotation first; every other profile moves directly.
    /// Returns null when neither action is legal this tick.
    /// </summary>
    public GenericActorDecision? Step(
        GenericActorContext context,
        GenericActorActionLegality? move,
        Direction direction,
        string reason)
    {
        if (move is not null
            && Field.LegalSteps(move).Contains(direction))
        {
            return Field.Step(move, direction, reason);
        }

        GenericActorActionLegality? rotate =
            Field.RotateAction(_lens, context, direction);
        if (rotate is null)
            return null;
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            $"{reason} (turning into the only legal lane)");
    }

    /// <summary>
    /// Extra cost of taking a step that turns the body away from
    /// <paramref name="watch"/>. Under a coupled profile a retreat is not free:
    /// it spends the facing quadrant that was holding the exchange and the
    /// straight-shot lane that was aimed down it. Zero when movement preserves
    /// facing.
    /// </summary>
    public int TurnAwayPenalty(
        Position from,
        Direction step,
        Position watch,
        int weight)
    {
        if (!TurnsOnMove)
            return 0;
        int dx = watch.X - from.X;
        int dy = watch.Y - from.Y;
        if (dx == 0 && dy == 0)
            return 0;
        (int sx, int sy) = step.Vector();
        int dot = (sx * dx) + (sy * dy);
        if (dot > 0)
            return 0;
        return dot == 0 ? weight : weight * 2;
    }
}
