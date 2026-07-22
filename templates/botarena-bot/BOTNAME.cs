using BotArena.Sdk;

/// <summary>
/// Your bot. One instance is created per match; fields keep their values
/// between ticks, so you can remember what you have seen.
/// </summary>
public sealed class BOTNAME : IBot
{
    private int _ticksSinceEnemySeen;

    public BotAction Tick(BotContext context)
    {
        var enemy = context.VisibleEnemies.Count > 0 ? context.VisibleEnemies[0] : null;
        if (enemy is not null)
        {
            _ticksSinceEnemySeen = 0;
            bool aligned =
                (context.Position.X == enemy.Position.X || context.Position.Y == enemy.Position.Y);
            if (aligned && context.CanShoot)
            {
                context.Debug.Write("Enemy at {0}; firing!", enemy.Position);
                return Actions.Shoot();
            }
        }

        _ticksSinceEnemySeen++;
        if (context.PreviousActionResult == ActionResult.Blocked || context.IsWallAhead())
            return context.Random.NextBool() ? Actions.TurnLeft() : Actions.TurnRight();
        if (_ticksSinceEnemySeen % 6 == 0)
            return Actions.TurnRight();
        return Actions.MoveForward();
    }
}
