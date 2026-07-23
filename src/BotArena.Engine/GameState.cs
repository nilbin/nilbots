namespace BotArena.Engine;

public sealed class BotState
{
    public required int Slot { get; init; }
    public Position Position { get; set; }
    public Direction Facing { get; set; }
    public int Health { get; set; }
    public int Cooldown { get; set; }
    /// <summary>Current energy; meaningful only when rules.MaxEnergy > 0.</summary>
    public int Energy { get; set; }
    /// <summary>Accrued zone-control ticks; meaningful only when rules.ZoneControl.</summary>
    public int ZoneTicks { get; set; }
    public int Faults { get; set; }
    public int DamageDealt { get; set; }
    public BotStatus Status { get; set; } = BotStatus.Active;
    public ActionResult LastActionResult { get; set; } = ActionResult.None;

    public bool IsActive => Status == BotStatus.Active;
}

/// <summary>A bolt in flight (RULES-0.5-DESIGN §B): occupies its tile — lethal to any
/// active non-owner bot sharing it — and advances one tile every
/// rules.ProjectileTicksPerTile ticks until it hits a wall, a bot, or travels
/// rules.ShotRange tiles.</summary>
public sealed class ProjectileState
{
    public required Position Position { get; set; }
    public required Direction Direction { get; init; }
    public required int OwnerSlot { get; init; }
    public int TilesTraveled { get; set; }
    /// <summary>Ticks since the last advance; advances when it reaches
    /// rules.ProjectileTicksPerTile.</summary>
    public int Phase { get; set; }
}

public sealed class GameState
{
    public required ArenaMap Map { get; init; }
    public required GameRules Rules { get; init; }
    public required IReadOnlyList<BotState> Bots { get; init; }
    /// <summary>Bolts in flight, in spawn order (deterministic); empty unless
    /// rules.ProjectileTicksPerTile > 0.</summary>
    public List<ProjectileState> Projectiles { get; } = [];
    public int Tick { get; set; }
}
