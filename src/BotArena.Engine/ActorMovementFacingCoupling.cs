namespace BotArena.Engine;

/// <summary>
/// How a movement profile couples a life's body facing to its movement
/// action. Facing is what vision cones and straight-shot aim read, so this
/// is the exact knob that separates free strafing from committed kinematics
/// (DECISIONS #155). Every value is resolved from the contract before tick
/// zero, and the movement legality mask publishes the consequences, so a
/// contract-driven bot survives any arm without re-authoring.
/// </summary>
public enum ActorMovementFacingCoupling
{
    /// <summary>
    /// Movement never changes facing: a life may step to any legal cardinal
    /// while continuing to face wherever it last rotated. Rotation remains
    /// the only action that changes facing. This is the inert default —
    /// canonical contract bytes omit the field entirely for this value, so
    /// every contract authored before the capability existed keeps its exact
    /// fingerprints.
    /// </summary>
    PreserveFacing = 0,

    /// <summary>
    /// A movement action that resolves to <c>Success</c> sets the life's
    /// facing to the direction it moved, before the Movement event is
    /// emitted — so that event's facing payload is the new facing and is the
    /// authoritative evidence of the change. A movement that resolves to
    /// <c>Blocked</c> (wall, occupancy, reservation, joint-claim, or
    /// projectile contact) changes neither position nor facing. Every
    /// cardinal direction stays legal, so a step is still a one-action dodge
    /// — it just costs the aim the life was holding.
    /// </summary>
    FaceMovementDirection = 1,

    /// <summary>
    /// A life may only move in the direction it currently faces. The
    /// Direction constraint published for Movement-kind actions offers
    /// exactly the life's current facing (Rotation-kind actions keep all
    /// four cardinals), and movement resolution defensively Blocks any
    /// movement whose direction is not the mover's facing. Facing itself is
    /// unchanged by movement under this value: a step forward is a step, and
    /// turning stays a separate action.
    /// </summary>
    FacingLocked = 2,

    /// <summary>
    /// A successful move projects its eight-way travel heading back onto the
    /// four cardinal body facings. Cardinal travel faces that cardinal;
    /// diagonal travel keeps the current facing when it is one of the
    /// diagonal's components and otherwise flips to the opposite cardinal.
    /// Blocked movement changes neither position nor facing.
    /// </summary>
    FaceMovementHeadingProjected = 3,

    /// <summary>
    /// Forward, forward-diagonal, and exact lateral movement preserve facing,
    /// while a successful rear or rear-diagonal move flips facing by 180
    /// degrees. This permits combat strafing without making reverse kiting a
    /// free way to retain aim. Blocked movement never changes facing.
    /// </summary>
    CombatStrafe = 4,
}
