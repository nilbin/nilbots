namespace BotArena.Sdk;

/// <summary>Everything a bot can observe and use on one tick (plan §4.5, §7).</summary>
public sealed class BotContext
{
    /// <summary>The current tick number, starting at 0.</summary>
    public required int Tick { get; init; }

    /// <summary>Your slot in this match (0 or 1). Use it to attribute
    /// <see cref="VisibleEvents"/>: an event whose <c>Slot</c> equals yours is your own.</summary>
    public required int Slot { get; init; }

    public required Position Position { get; init; }

    /// <summary>Which way you face. Movement and shooting always follow it; under rules
    /// with directional vision your sight does too (see <see cref="VisibleTiles"/>) —
    /// turning is also looking.</summary>
    public required Direction Facing { get; init; }

    public required int Health { get; init; }

    /// <summary>Ticks until you may shoot again. Counts 2 → 1 → 0 on the ticks after a
    /// shot, so with a 2-tick cooldown you can fire every 3rd tick. See <see cref="CanShoot"/>.</summary>
    public required int Cooldown { get; init; }

    /// <summary>Your current energy, or null when these rules have no energy system.
    /// With energy on, each shot costs energy and regeneration is slow — a Shoot without
    /// enough energy becomes Wait with an OnCooldown result. Enemy energy is not
    /// observable; count their shots to estimate it.</summary>
    public int? Energy { get; init; }

    /// <summary>Arena width in tiles (0 on servers older than SDK 0.4). Static per match —
    /// use it to bound pathfinding instead of discovering edges by walking into them.</summary>
    public int MapWidth { get; init; }

    /// <summary>Arena height in tiles (0 on servers older than SDK 0.4).</summary>
    public int MapHeight { get; init; }

    /// <summary>Zone-control tiles, or null when these rules have no zone control.
    /// The full list from tick 0, not gated by vision; a map's zone may be split
    /// into disconnected pads. At the end of every tick you are alive on zone tiles
    /// you accrue 1 zone-tick — under exclusive accrual (the shipped form) only a
    /// SOLE occupant accrues, so a contested zone pays nobody. The tick-limit
    /// tiebreak is zone → health → damage, and holding for the domination threshold
    /// wins outright. The zone is the objective — ignoring it forfeits ties.</summary>
    public IReadOnlyList<Position>? ZoneTiles { get; init; }

    /// <summary>Your accrued zone-ticks (null when zone control is off). Public score.</summary>
    public int? MyZoneTicks { get; init; }

    /// <summary>The enemy's accrued zone-ticks (null when zone control is off).</summary>
    public int? EnemyZoneTicks { get; init; }

    public required ActionResult PreviousActionResult { get; init; }

    /// <summary>Every tile you can currently see (Chebyshev range 6, corner-strict wall
    /// blocking). WARNING: even a diagonally adjacent wall can be outside this list when
    /// the sight line clips another wall's corner — remember the map yourself if you need
    /// terrain knowledge beyond the current cone. Under rules with directional vision,
    /// sight is the 90° quadrant toward your facing plus the adjacent ring — turning is
    /// also looking, and your back is genuinely blind.</summary>
    public required IReadOnlyList<VisibleTile> VisibleTiles { get; init; }

    public required IReadOnlyList<VisibleEnemy> VisibleEnemies { get; init; }

    /// <summary>Bolts in flight on tiles you can see, or null when these rules have
    /// instant shots (no projectile system). When non-null, dodge them by arithmetic,
    /// not observation: a bolt's tile is lethal, <see cref="VisibleProjectile.TicksUntilAdvance"/>
    /// says exactly when it moves, and <see cref="VisibleProjectile.RemainingTiles"/>
    /// says how far it can still reach.</summary>
    public IReadOnlyList<VisibleProjectile>? VisibleProjectiles { get; init; }

    /// <summary>What happened LAST tick, filtered by your CURRENT vision: an event is
    /// delivered when any of its reference positions is on a tile you can see now. Shots
    /// are delivered even when the shooter itself is beyond your vision, as long as part
    /// of the ray's start/end is visible — muzzle flashes carry information. Events you
    /// only HEAR are not here: they arrive redacted in <see cref="HeardSounds"/>.</summary>
    public required IReadOnlyList<VisibleEvent> VisibleEvents { get; init; }

    /// <summary>Sounds from LOUD events (shots, damage, destruction) LAST tick that were
    /// beyond your sight but within hearing, or null when these rules have no hearing.
    /// Each is redacted to kind + coarse bearing + coarse distance band — enough to know
    /// someone is fighting north of you, not enough to aim. An event you saw appears in
    /// <see cref="VisibleEvents"/> instead, never in both.</summary>
    public IReadOnlyList<HeardSound>? HeardSounds { get; init; }

    /// <summary>The only permitted randomness: seeded from the match seed and your slot,
    /// so replays are exact. System clocks and System.Random are neutralized in the sandbox.</summary>
    public required IBotRandom Random { get; init; }

    public required IBotDebug Debug { get; init; }

    private Dictionary<Position, bool>? _tileLookup;

    private Dictionary<Position, bool> TileLookup =>
        _tileLookup ??= VisibleTiles.ToDictionary(t => t.Position, t => t.IsWall);

    /// <summary>True when the tile is in your current vision (see <see cref="VisibleTiles"/>).</summary>
    public bool CanSee(Position position) => TileLookup.ContainsKey(position);

    /// <summary>True when the tile is visibly a wall. Unseen tiles return false — bots do
    /// not get map knowledge for free, and corner-strict vision can hide even adjacent
    /// walls (see <see cref="VisibleTiles"/>). Track seen walls yourself for pathfinding.</summary>
    public bool IsWall(Position position) => TileLookup.TryGetValue(position, out bool wall) && wall;

    public Position Ahead()
    {
        var (dx, dy) = Facing.Vector();
        return Position.Offset(dx, dy);
    }

    public bool IsWallAhead() => IsWall(Ahead());

    public bool CanShoot => Cooldown == 0;
}
