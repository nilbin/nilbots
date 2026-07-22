namespace BotArena.Sdk;

/// <summary>Everything a bot can observe and use on one tick (plan §4.5, §7).</summary>
public sealed class BotContext
{
    public required int Tick { get; init; }
    public required Position Position { get; init; }
    public required Direction Facing { get; init; }
    public required int Health { get; init; }
    public required int Cooldown { get; init; }
    public required ActionResult PreviousActionResult { get; init; }
    public required IReadOnlyList<VisibleTile> VisibleTiles { get; init; }
    public required IReadOnlyList<VisibleEnemy> VisibleEnemies { get; init; }
    public required IReadOnlyList<VisibleEvent> VisibleEvents { get; init; }
    public required IBotRandom Random { get; init; }
    public required IBotDebug Debug { get; init; }

    private Dictionary<Position, bool>? _tileLookup;

    private Dictionary<Position, bool> TileLookup =>
        _tileLookup ??= VisibleTiles.ToDictionary(t => t.Position, t => t.IsWall);

    public bool CanSee(Position position) => TileLookup.ContainsKey(position);

    /// <summary>True when the tile is visibly a wall. Unseen tiles return false — bots do not get map knowledge for free.</summary>
    public bool IsWall(Position position) => TileLookup.TryGetValue(position, out bool wall) && wall;

    public Position Ahead()
    {
        var (dx, dy) = Facing.Vector();
        return Position.Offset(dx, dy);
    }

    public bool IsWallAhead() => IsWall(Ahead());

    public bool CanShoot => Cooldown == 0;
}
