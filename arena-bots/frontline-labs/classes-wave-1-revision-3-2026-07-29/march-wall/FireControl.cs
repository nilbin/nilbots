using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Builds the exact set of tiles this body can put a projectile on this tick.
/// Every attack action in the current legality mask is expanded through its own
/// declared argument domain: an absolute-heading turret gun, a facing-relative
/// gun with an aim/bend envelope, or a payload-free straight gun. Paths are
/// replayed with the SDK's own preview of the engine rule, so range, wall
/// termination and strict diagonal corners come from the contract instead of an
/// assumption.
/// </summary>
internal static class FireControl
{
    private const int MaxProgramsPerAction = 768;

    internal sealed record Shot(
        string ActionId,
        int ActionCode,
        ImmutableArray<GenericActorActionArgument> Arguments,
        int PathLength,
        int Bends);

    /// <summary>
    /// Maps every reachable tile to the cheapest shot that reaches it. A shot is
    /// consumed by the first enemy body on its path, so a tile beyond an enemy
    /// is deliberately absent.
    /// </summary>
    /// <param name="facingOverride">
    /// Hypothetical facing used to ask what a rotation would open up. Omit it to
    /// solve for the body's current facing.
    /// </param>
    public static Dictionary<Position, Shot> Solutions(
        ContractView view,
        GenericActorContext context,
        Direction? facingOverride = null)
    {
        Dictionary<Position, Shot> reachable = [];
        GenericActorRulesContract.AttackProfile? attack =
            view.Attack(context.Self.FormId);
        if (attack is null)
            return reachable;

        HashSet<string> attackIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.Attack);
        HashSet<Position> enemyTiles = context.Enemies
            .Select(enemy => enemy.Position)
            .ToHashSet();
        Func<Position, bool> isWall = view.IsWall;
        Position origin = context.Self.Position;
        Direction facing = facingOverride ?? context.Self.Facing;
        int maxTravel = Math.Max(1, attack.Projectile.MaxTravelTiles);

        foreach (GenericActorActionLegality action in context.ActionLegalities
                     .Where(entry =>
                         entry.Available && attackIds.Contains(entry.ActionId))
                     .OrderBy(entry => entry.ActionId, StringComparer.Ordinal))
        {
            GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint? headings = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
            if (headings is not null)
            {
                foreach (ProjectileHeading heading in headings.AllowedValues)
                {
                    Trace(
                        reachable,
                        isWall,
                        enemyTiles,
                        origin,
                        Direction.North,
                        new ShotProgram((int)heading, 0, 0, 1, 0),
                        maxTravel,
                        new Shot(
                            action.ActionId,
                            action.ActionCode,
                            [
                                new GenericActorActionArgument
                                    .ProjectileHeadingArgument(heading),
                            ],
                            0,
                            0));
                }
                continue;
            }

            GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
                programs = action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint>()
                    .SingleOrDefault();
            bool payloadAllowed =
                programs is { Allowed: true } && attack.ShotProgram.Enabled;

            // A gun with no payload always fires straight along current facing.
            if (!payloadAllowed)
            {
                Trace(
                    reachable,
                    isWall,
                    enemyTiles,
                    origin,
                    facing,
                    ShotProgram.Straight,
                    maxTravel,
                    new Shot(action.ActionId, action.ActionCode, [], 0, 0));
                continue;
            }

            EnumerateProgrammed(
                reachable,
                view,
                context,
                action,
                attack,
                enemyTiles,
                maxTravel,
                facing);
        }
        return reachable;
    }

    private static void EnumerateProgrammed(
        Dictionary<Position, Shot> reachable,
        ContractView view,
        GenericActorContext context,
        GenericActorActionLegality action,
        GenericActorRulesContract.AttackProfile attack,
        HashSet<Position> enemyTiles,
        int maxTravel,
        Direction facing)
    {
        GenericActorRulesContract.ShotProgramDefinition definition =
            attack.ShotProgram;
        GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
            definition.AimOnlyProgram;
        Func<Position, bool> isWall = view.IsWall;
        Position origin = context.Self.Position;
        int emitted = 0;

        int minAim = definition.MinInitialAimSteps;
        int maxAim = definition.MaxInitialAimSteps;
        if (maxAim < minAim)
            (minAim, maxAim) = (maxAim, minAim);
        maxAim = Math.Min(maxAim, minAim + 8);

        for (int aim = minAim; aim <= maxAim && emitted < MaxProgramsPerAction; aim++)
        {
            var straight = new ShotProgram(
                aim,
                aimOnly.BendDirection,
                aimOnly.BendAfterTiles,
                aimOnly.BendEveryTiles,
                aimOnly.BendCount);
            bool omitPayload = aim == 0 && definition.PayloadOptional;
            Trace(
                reachable,
                isWall,
                enemyTiles,
                origin,
                facing,
                straight,
                maxTravel,
                new Shot(
                    action.ActionId,
                    action.ActionCode,
                    omitPayload
                        ? []
                        : [new GenericActorActionArgument.ShotProgramArgument(
                            straight)],
                    0,
                    0));
            emitted++;

            foreach (int bendDirection in definition.AllowedCurvedBendDirections)
            {
                for (int after = Math.Max(1, definition.MinBendAfterTiles);
                     after <= definition.MaxBendAfterTiles;
                     after++)
                {
                    for (int every = Math.Max(1, definition.MinBendEveryTiles);
                         every <= definition.MaxBendEveryTiles;
                         every++)
                    {
                        for (int count = Math.Max(1, definition.MinBendCount);
                             count <= definition.MaxBendCount;
                             count++)
                        {
                            if (emitted >= MaxProgramsPerAction)
                                return;
                            var curved = new ShotProgram(
                                aim,
                                bendDirection,
                                after,
                                every,
                                count);
                            Trace(
                                reachable,
                                isWall,
                                enemyTiles,
                                origin,
                                facing,
                                curved,
                                maxTravel,
                                new Shot(
                                    action.ActionId,
                                    action.ActionCode,
                                    [
                                        new GenericActorActionArgument
                                            .ShotProgramArgument(curved),
                                    ],
                                    0,
                                    count));
                            emitted++;
                        }
                    }
                }
            }
        }
    }

    private static void Trace(
        Dictionary<Position, Shot> reachable,
        Func<Position, bool> isWall,
        HashSet<Position> enemyTiles,
        Position origin,
        Direction facing,
        ShotProgram program,
        int maxTravel,
        Shot template)
    {
        IReadOnlyList<Position> path;
        try
        {
            path = ShotPaths.Preview(
                origin,
                facing,
                program,
                maxTravel,
                isWall);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        for (int index = 0; index < path.Count; index++)
        {
            Position tile = path[index];
            Shot candidate = template with { PathLength = index + 1 };
            if (!reachable.TryGetValue(tile, out Shot? existing)
                || Cheaper(candidate, existing))
            {
                reachable[tile] = candidate;
            }
            if (enemyTiles.Contains(tile))
                return;
        }
    }

    private static bool Cheaper(Shot candidate, Shot existing) =>
        candidate.Bends != existing.Bends
            ? candidate.Bends < existing.Bends
            : candidate.PathLength != existing.PathLength
                ? candidate.PathLength < existing.PathLength
                : string.CompareOrdinal(
                    candidate.ActionId,
                    existing.ActionId) < 0;

    public static GenericActorDecision Decision(Shot shot, string reason) =>
        new(shot.ActionId, shot.ActionCode, shot.Arguments, reason);

    /// <summary>
    /// Ticks from launch until the bolt sweeps the tile this shot reaches, from
    /// the declared launch distance and per-advance travel. Firing at a tile the
    /// bolt arrives at before the target could is a wasted commitment.
    /// </summary>
    public static int ArrivalOffset(
        GenericActorRulesContract.Projectile projectile,
        int pathLength)
    {
        int index = Math.Max(0, pathLength - 1);
        int launch = Math.Max(1, projectile.LaunchTiles);
        int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
        return index < launch ? 0 : 1 + (index - launch) / perAdvance;
    }
}
