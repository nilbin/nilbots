using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Canonical generation-3 projectile path generator. The launch heading has
/// already incorporated any initial aim offset; the optional program controls
/// only bends along the committed path.
/// </summary>
internal static class GenericActorProjectilePath
{
    /// <param name="map">Gameplay map.</param>
    /// <param name="origin">Launch tile.</param>
    /// <param name="launchHeading">Launch heading.</param>
    /// <param name="profile">The firing form's declared attack profile.</param>
    /// <param name="program">Optional programmed bend.</param>
    /// <param name="extraTravelTiles">
    /// Tiles a mode currently adds to this body's declared gun reach. Zero for
    /// every contract that declares no such modifier, so the traced path is
    /// byte-identical to the historical one.
    /// </param>
    public static ImmutableArray<Position> Trace(
        ActorMapDefinition map,
        Position origin,
        ProjectileHeading launchHeading,
        ActorAttackProfileDefinition profile,
        ShotProgram? program,
        int extraTravelTiles = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(profile);
        if (!Enum.IsDefined(launchHeading))
        {
            throw new ArgumentOutOfRangeException(
                nameof(launchHeading));
        }
        if (extraTravelTiles < 0)
            throw new ArgumentOutOfRangeException(nameof(extraTravelTiles));

        int maxTravelTiles = checked(
            profile.Projectile.MaxTravelTiles + extraTravelTiles);
        var path = ImmutableArray.CreateBuilder<Position>();
        Position position = origin;
        ProjectileHeading heading = launchHeading;
        int bends = 0;
        for (int tilesMoved = 0;
             tilesMoved < maxTravelTiles;
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
