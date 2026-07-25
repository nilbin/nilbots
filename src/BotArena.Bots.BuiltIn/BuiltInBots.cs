using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>Always waits. Baseline runtime test target (plan §26).</summary>
public sealed class IdleBot : IBot
{
    public BotAction Tick(BotContext context)
    {
        context.Debug.Write("Idling.");
        return Actions.Wait();
    }
}

/// <summary>Moves forward, turns when blocked, occasionally turns at random (plan §26).</summary>
public sealed class WanderBot : IBot
{
    public BotAction Tick(BotContext context)
    {
        if (context.PreviousActionResult == ActionResult.Blocked || context.IsWallAhead())
        {
            context.Debug.Write("Blocked ahead; turning.");
            return context.Random.NextBool() ? Actions.TurnLeft() : Actions.TurnRight();
        }
        if (context.Random.NextInt(0, 8) == 0)
        {
            context.Debug.Write("Random turn.");
            return Actions.TurnRight();
        }
        return Actions.MoveForward();
    }
}

/// <summary>Turns toward visible enemies and shoots when lined up (plan §26).</summary>
public sealed class HunterBot : IBot
{
    public BotAction Tick(BotContext context)
    {
        var enemy = context.VisibleEnemies.Count > 0 ? context.VisibleEnemies[0] : null;
        if (enemy is null)
        {
            if (context.PreviousActionResult == ActionResult.Blocked || context.IsWallAhead())
                return Actions.TurnRight();
            if (context.Random.NextInt(0, 10) == 0)
                return Actions.TurnLeft();
            return Actions.MoveForward();
        }

        var target = enemy.Position;
        var mine = context.Position;
        var toward = BotMath.DirectionToward(mine, target);
        bool aligned = (mine.X == target.X || mine.Y == target.Y) && toward == context.Facing;

        if (aligned && context.CanShoot)
        {
            context.Debug.Write("Enemy at {0}; firing.", target);
            return Actions.Shoot();
        }
        if (context.Facing != toward)
        {
            context.Debug.Write("Enemy at {0}; turning to face.", target);
            return BotMath.TurnToward(context.Facing, toward);
        }
        context.Debug.Write("Enemy at {0}; advancing.", target);
        return context.IsWallAhead() ? Actions.TurnRight() : Actions.MoveForward();
    }
}

/// <summary>Avoids line of sight and keeps distance (plan §26).</summary>
public sealed class CowardBot : IBot
{
    public BotAction Tick(BotContext context)
    {
        var enemy = context.VisibleEnemies.Count > 0 ? context.VisibleEnemies[0] : null;
        if (enemy is null)
        {
            if (context.PreviousActionResult == ActionResult.Blocked || context.IsWallAhead())
                return context.Random.NextBool() ? Actions.TurnLeft() : Actions.TurnRight();
            return Actions.MoveForward();
        }

        var away = BotMath.DirectionToward(enemy.Position, context.Position);
        context.Debug.Write("Enemy at {0}; fleeing {1}.", enemy.Position, away);
        if (context.Facing != away)
            return BotMath.TurnToward(context.Facing, away);
        if (context.IsWallAhead())
            return Actions.TurnRight();
        return Actions.MoveForward();
    }
}

internal static class BotMath
{
    /// <summary>Cardinal direction from one position toward another; the axis with the larger
    /// distance wins, X on ties, so the choice is deterministic.</summary>
    public static Direction DirectionToward(Position from, Position to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
            return dx > 0 ? Direction.East : Direction.West;
        if (dy != 0)
            return dy > 0 ? Direction.South : Direction.North;
        return dx > 0 ? Direction.East : dx < 0 ? Direction.West : Direction.North;
    }

    public static BotAction TurnToward(Direction current, Direction desired)
    {
        int diff = (((int)desired - (int)current) % 4 + 4) % 4;
        return diff == 3 ? Actions.TurnLeft() : Actions.TurnRight();
    }
}

/// <summary>Built-in opponent catalog used by the CLI and tests.</summary>
public static class BuiltInBotCatalog
{
    public static readonly IReadOnlyList<string> Names = ["idle", "wander", "hunter", "coward"];

    public static IBot Create(string name) => name.ToLowerInvariant() switch
    {
        "idle" => new IdleBot(),
        "wander" => new WanderBot(),
        "hunter" => new HunterBot(),
        "coward" => new CowardBot(),
        _ => throw new ArgumentException(
            $"Unknown built-in bot '{name}'. Available: {string.Join(", ", Names)}."),
    };

    public static string Accent(string name) => name.ToLowerInvariant() switch
    {
        "idle" => "#94a3b8",
        "wander" => "#34d399",
        "hunter" => "#f97316",
        "coward" => "#a78bfa",
        _ => "#38bdf8",
    };

    /// <summary>One line per opponent, so a newcomer can pick a training target
    /// instead of reverse-engineering behaviour from replays (player-test finding).
    /// Describe what each DOES, including the weakness worth exploiting — these are
    /// sparring partners, not opponents to be surprised by.</summary>
    public static string Describe(string name) => name.ToLowerInvariant() switch
    {
        "idle" => "never moves or shoots — a fixed target for checking your aim and pathing",
        "wander" => "walks randomly, shoots when something is in front of it; no tactics",
        "hunter" => "closes on you and fires when lined up, but never dodges — the standard bar",
        "coward" => "keeps its distance and flees when hurt; punishes slow, telegraphed approaches",
        _ => "",
    };
}
