namespace BotArena.Engine;

/// <summary>
/// Shared authoritative and replay-reconstruction semantics for movement's
/// effect on cardinal body facing.
/// </summary>
internal static class ActorMovementFacingResolver
{
    internal static ActorMovementFacingCoupling EffectiveCoupling(
        ActorMovementProfileDefinition profile,
        ActorActionDefinition action) =>
        action.MovementFacingOverride ?? profile.FacingCoupling;

    internal static Direction AfterSuccessfulMove(
        Direction before,
        ProjectileHeading heading,
        ActorMovementFacingCoupling coupling) => coupling switch
        {
            ActorMovementFacingCoupling.PreserveFacing => before,
            ActorMovementFacingCoupling.FacingLocked => before,
            ActorMovementFacingCoupling.FaceMovementDirection =>
                Cardinal(heading) ?? before,
            ActorMovementFacingCoupling.FaceMovementHeadingProjected =>
                Projected(before, heading),
            ActorMovementFacingCoupling.CombatStrafe =>
                RelativeDistance(before, heading) >= 3
                    ? Opposite(before)
                    : before,
            _ => throw new ArgumentOutOfRangeException(
                nameof(coupling), coupling, null),
        };

    private static Direction Projected(
        Direction before,
        ProjectileHeading heading)
    {
        if (Cardinal(heading) is { } cardinal)
            return cardinal;

        int beforeSector = (int)before * 2;
        int headingSector = (int)heading;
        int clockwise = (headingSector - beforeSector + 8) % 8;
        return clockwise is 1 or 7 ? before : Opposite(before);
    }

    private static int RelativeDistance(
        Direction facing,
        ProjectileHeading heading)
    {
        int difference = Math.Abs((int)heading - ((int)facing * 2));
        return Math.Min(difference, 8 - difference);
    }

    private static Direction? Cardinal(ProjectileHeading heading) =>
        heading is ProjectileHeading.North
            or ProjectileHeading.East
            or ProjectileHeading.South
            or ProjectileHeading.West
            ? (Direction)((int)heading / 2)
            : null;

    private static Direction Opposite(Direction facing) =>
        (Direction)(((int)facing + 2) % 4);
}
