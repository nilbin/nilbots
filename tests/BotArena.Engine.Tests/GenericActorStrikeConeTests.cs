using System.Collections.Immutable;
using BotArena.Engine;
using Xunit;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the declared strike's REAL-cone geometry (owner ruling 2026-08-06):
/// the filled 90° wedge with inclusive 45° boundaries, Chebyshev reach,
/// wall occlusion through the canonical strike line, and the canonical
/// ring-then-row tile order the frozen telegraph serializes in.
/// </summary>
public sealed class GenericActorStrikeConeTests
{
    private static ActorMapDefinition Map(params string[] rows) =>
        new(
            "strike-cone-test",
            1,
            [.. rows],
            [
                new ActorMapSpawnAnchorDefinition(
                    new InitialSpawnDefinition(
                        "anchor",
                        new Position(0, 0),
                        Direction.East),
                    [ActorMovementLayer.Ground]),
            ],
            [],
            []);

    [Fact]
    public void SectorMembershipIsInclusiveAtTheFortyFiveDegreeBoundary()
    {
        // East heading (1, 0): straight ahead and both diagonals are in;
        // anything steeper than 45° or behind the shooter is out.
        Assert.True(GenericActorStrikeCone.WithinSector(3, 0, 1, 0));
        Assert.True(GenericActorStrikeCone.WithinSector(3, 3, 1, 0));
        Assert.True(GenericActorStrikeCone.WithinSector(3, -3, 1, 0));
        Assert.False(GenericActorStrikeCone.WithinSector(2, 3, 1, 0));
        Assert.False(GenericActorStrikeCone.WithinSector(0, 2, 1, 0));
        Assert.False(GenericActorStrikeCone.WithinSector(-1, 0, 1, 0));
        // Diagonal heading (1, 1): the wedge is the quadrant between East
        // and South, boundaries inclusive.
        Assert.True(GenericActorStrikeCone.WithinSector(2, 0, 1, 1));
        Assert.True(GenericActorStrikeCone.WithinSector(0, 2, 1, 1));
        Assert.True(GenericActorStrikeCone.WithinSector(2, 1, 1, 1));
        Assert.False(GenericActorStrikeCone.WithinSector(2, -1, 1, 1));
        Assert.False(GenericActorStrikeCone.WithinSector(-1, 2, 1, 1));
    }

    [Fact]
    public void OpenGroundConeIsTheFilledWedgeInCanonicalOrder()
    {
        ActorMapDefinition map = Map(
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......");
        ImmutableArray<Position> tiles = GenericActorStrikeCone.Tiles(
            map,
            new Position(2, 3),
            ProjectileHeading.East,
            2,
            diagonalCornersMustBeClear: true);
        Assert.Equal(
            [
                new Position(3, 2),
                new Position(3, 3),
                new Position(3, 4),
                new Position(4, 1),
                new Position(4, 2),
                new Position(4, 3),
                new Position(4, 4),
                new Position(4, 5),
            ],
            tiles.ToArray());
    }

    [Fact]
    public void WallDeadAheadShadowsTheWholeCloseCone()
    {
        // With clear-corner diagonals, a wall directly ahead blocks the
        // straight line AND both first diagonal steps, so the entire wedge
        // goes dark — exactly what the three spokes did before the fill.
        ActorMapDefinition map = Map(
            ".....",
            ".....",
            "..#..",
            ".....",
            ".....");
        ImmutableArray<Position> tiles = GenericActorStrikeCone.Tiles(
            map,
            new Position(1, 2),
            ProjectileHeading.East,
            2,
            diagonalCornersMustBeClear: true);
        Assert.Empty(tiles);
    }

    [Fact]
    public void SideWallOccludesOnlyItsOwnShadow()
    {
        ActorMapDefinition map = Map(
            ".....",
            "...#.",
            ".....",
            ".....",
            ".....");
        ImmutableArray<Position> tiles = GenericActorStrikeCone.Tiles(
            map,
            new Position(1, 2),
            ProjectileHeading.East,
            2,
            diagonalCornersMustBeClear: true);
        // The wall at (3,1) removes itself; the straight lane and the
        // lower diagonal stay lit, and (2,1) before the wall stays lit.
        Assert.Contains(new Position(2, 1), tiles);
        Assert.Contains(new Position(2, 3), tiles);
        Assert.Contains(new Position(3, 2), tiles);
        Assert.Contains(new Position(3, 4), tiles);
        Assert.DoesNotContain(new Position(3, 1), tiles);
    }

    [Fact]
    public void StrikeLineMatchesTheEightWayRayOnSpokes()
    {
        ActorMapDefinition map = Map(
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......");
        Position origin = new(1, 3);
        // Straight spoke.
        Assert.Equal(
            [new Position(2, 3), new Position(3, 3), new Position(4, 3)],
            GenericActorStrikeCone.LineTo(
                map, origin, new Position(4, 3), true).ToArray());
        // Diagonal spoke.
        Assert.Equal(
            [new Position(2, 4), new Position(3, 5)],
            GenericActorStrikeCone.LineTo(
                map, origin, new Position(3, 5), true).ToArray());
        // Off-axis line stays 8-connected and ends on the target.
        ImmutableArray<Position> offAxis = GenericActorStrikeCone.LineTo(
            map, origin, new Position(5, 5), true);
        Assert.Equal(new Position(5, 5), offAxis[^1]);
        Position previous = origin;
        foreach (Position step in offAxis)
        {
            Assert.Equal(1, previous.ChebyshevDistance(step));
            previous = step;
        }
    }
}
