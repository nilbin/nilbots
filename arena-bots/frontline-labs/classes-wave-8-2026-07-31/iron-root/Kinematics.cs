using BotArena.Sdk;

/// <summary>
/// What a step costs when the contract couples facing to movement.
///
/// Three arms exist and the doctrine must price all of them from the declared
/// movement profile rather than from a remembered rule:
///
/// <list type="bullet">
/// <item><c>preserve-facing</c> — a step is a free strafe; turning is a
/// separate action that costs a tick.</item>
/// <item><c>face-movement-direction</c> — a successful step turns the body.
/// Travelling is aiming, so approaching a site lays the muzzle along the
/// approach, and any sidestep throws the aim away.</item>
/// <item><c>facing-locked</c> — a body may only step where it faces. A route
/// that turns a corner costs one rotation per corner, and leaving a tile in
/// any other direction costs a rotation before the step.</item>
/// </list>
///
/// Two doctrine quantities fall straight out of that: how long an enemy needs
/// before it can actually land a shot on a tile (which prices the anchor
/// windup), and how long this body needs before it can actually leave one
/// (which prices evasion).
/// </summary>
internal static class Kinematics
{
    /// <summary>
    /// Ticks this body needs to vacate its tile in a chosen direction: one
    /// step, plus the rotation a facing-locked profile demands first.
    /// </summary>
    public static int EvadeCost(
        GenericActorRulesContract.MovementFacingCoupling coupling) =>
        coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
            ? 2
            : 1;

    /// <summary>
    /// Ticks a body needs before it can leave toward <paramref name="direction"/>
    /// specifically. Under a facing-locked profile the direction it already
    /// faces is free and every other one costs the turn first.
    /// </summary>
    public static int EvadeCost(
        GenericActorRulesContract.MovementFacingCoupling coupling,
        Direction facing,
        Direction direction) =>
        coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
        && facing != direction
            ? 2
            : 1;

    /// <summary>
    /// The facing a successful step leaves behind. Only the
    /// face-movement-direction arm rewrites it; the other two keep it, which is
    /// why an aimed body there must choose between aiming and travelling.
    /// </summary>
    public static Direction FacingAfterStep(
        GenericActorRulesContract.MovementFacingCoupling coupling,
        Direction facing,
        Direction step) =>
        coupling
            == GenericActorRulesContract.MovementFacingCoupling
                .FaceMovementDirection
            ? step
            : facing;

    /// <summary>
    /// True when a step in this direction also lays the muzzle where it is
    /// wanted, so the rotation that would otherwise be needed is free.
    /// </summary>
    public static bool StepAlsoAims(
        GenericActorRulesContract.MovementFacingCoupling coupling) =>
        coupling
            == GenericActorRulesContract.MovementFacingCoupling
                .FaceMovementDirection;

    /// <summary>
    /// Ticks before a mobile enemy could put a shot on <paramref name="target"/>.
    /// It has to reach a tile with a real firing lane and be pointed down it,
    /// and the coupling decides how much of that is free.
    ///
    /// <para>Under <c>preserve-facing</c> aim is free, so only the walk counts.
    /// Under <c>face-movement-direction</c> the walk sets the enemy's facing to
    /// its own travel direction, so unless it happens to arrive travelling
    /// straight at us it owes a rotation. Under <c>facing-locked</c> it owes a
    /// rotation to start travelling at all and another to aim on arrival.
    /// Omnidirectional guns never owe either.</para>
    ///
    /// <para>This is precisely why a forward anchor is cheaper in the coupled
    /// arms than it looks in the uncoupled one: the punisher pays for its own
    /// approach in the same currency the windup is spent in.</para>
    /// </summary>
    public static int TicksToFirstShot(
        GenericActorRulesContract.MovementFacingCoupling coupling,
        int stepsToLane,
        bool omnidirectional,
        bool alreadyAimed)
    {
        if (stepsToLane == int.MaxValue)
            return int.MaxValue;
        if (omnidirectional)
            return stepsToLane;
        if (stepsToLane <= 0)
            return alreadyAimed ? 0 : 1;   // one absolute rotation, then fire

        return coupling switch
        {
            GenericActorRulesContract.MovementFacingCoupling
                .FaceMovementDirection => stepsToLane + 1,
            GenericActorRulesContract.MovementFacingCoupling.FacingLocked =>
                stepsToLane + 2,
            _ => stepsToLane,
        };
    }

    /// <summary>
    /// The rotation that unlocks a wanted step under a facing-locked profile,
    /// or <see langword="null"/> when the step is already legal. Navigation
    /// that skips this simply stops moving on that arm — the movement mask
    /// offers exactly one direction and a route almost never runs straight.
    /// </summary>
    public static GenericActorDecision? TryTurnToTravel(
        ContractLens lens,
        GenericActorContext context,
        Direction direction,
        string reason)
    {
        foreach (GenericActorActionLegality rotation in lens.Available(
                     context,
                     GenericActorRulesContract.ActionKind.Rotation))
        {
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in rotation.Constraints)
            {
                if (constraint is GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint directions
                    && directions.AllowedValues.Contains(direction))
                {
                    return new GenericActorDecision(
                        rotation.ActionId,
                        rotation.ActionCode,
                        [
                            new GenericActorActionArgument.DirectionArgument(
                                direction),
                        ],
                        reason);
                }
            }
        }
        return null;
    }
}
