namespace BotArena.Engine;

public sealed class BotState
{
    public required int Slot { get; init; }
    public Position Position { get; set; }
    public Direction Facing { get; set; }
    public int Health { get; set; }
    public int Cooldown { get; set; }
    public int Faults { get; set; }
    public int DamageDealt { get; set; }
    public BotStatus Status { get; set; } = BotStatus.Active;
    public ActionResult LastActionResult { get; set; } = ActionResult.None;

    public bool IsActive => Status == BotStatus.Active;
}

public sealed class GameState
{
    public required ArenaMap Map { get; init; }
    public required GameRules Rules { get; init; }
    public required IReadOnlyList<BotState> Bots { get; init; }
    public int Tick { get; set; }
}
