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
/// active non-owner bot sharing it — and advances on its configured cadence through
/// one or more ordered tile substeps until it hits a wall, a bot, or its range.</summary>
public sealed class ProjectileState
{
    /// <summary>Stable within one replay so presentation can interpolate ordered
    /// substeps even when several projectiles share an owner and direction.</summary>
    public required int Id { get; init; }
    public required Position Position { get; set; }
    public required Direction Direction { get; init; }
    /// <summary>Exact current eight-way heading for a programmed projectile.
    /// Null for legacy straight projectiles, whose <see cref="Direction"/> is exact.</summary>
    public ProjectileHeading? Heading { get; set; }
    /// <summary>Complete private immutable path. Present only for programmed shots;
    /// replay may expose it to spectators, bot observations never do.</summary>
    public IReadOnlyList<Position>? ProgrammedPath { get; init; }
    public int NextProgrammedPathIndex { get; set; }
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
    /// <summary>Signed shared objective pressure: positive favors slot 0, negative
    /// favors slot 1. Meaningful only under rules.ActiveZoneControl.</summary>
    public int ControlPressure { get; set; }
    public int NextProjectileId { get; set; }
    public int Tick { get; set; }
}
