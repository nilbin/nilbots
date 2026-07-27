using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Canonical generation-3 projectile path generator. The launch heading has
/// already incorporated any initial aim offset; the optional program controls
/// only bends along the committed path.
/// </summary>
internal static class GenericActorProjectilePath
{
    public static ImmutableArray<Position> Trace(
        ActorMapDefinition map,
        Position origin,
        ProjectileHeading launchHeading,
        ActorAttackProfileDefinition profile,
        ShotProgram? program)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(profile);
        if (!Enum.IsDefined(launchHeading))
        {
            throw new ArgumentOutOfRangeException(
                nameof(launchHeading));
        }

        var path = ImmutableArray.CreateBuilder<Position>();
        Position position = origin;
        ProjectileHeading heading = launchHeading;
        int bends = 0;
        for (int tilesMoved = 0;
             tilesMoved < profile.Projectile.MaxTravelTiles;
             tilesMoved++)
        {
            if (program is ShotProgram curve
                && bends < curve.BendCount
                && tilesMoved >= curve.BendAfterTiles
                && (tilesMoved - curve.BendAfterTiles)
                    % curve.BendEveryTiles == 0)
            {
                heading = heading.Turned(curve.BendDirection);
                bends++;
            }
            var (dx, dy) = heading.Vector();
            Position next = position.Offset(dx, dy);
            if (map.IsWall(next)
                || dx != 0
                && dy != 0
                && profile.Projectile.DiagonalCornersMustBeClear
                && (map.IsWall(position.Offset(dx, 0))
                    || map.IsWall(position.Offset(0, dy))))
            {
                break;
            }
            position = next;
            path.Add(position);
        }
        return path.ToImmutable();
    }
}
