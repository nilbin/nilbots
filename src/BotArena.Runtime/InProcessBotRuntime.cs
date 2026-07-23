using System.Text;
using BotArena.Engine;

namespace BotArena.Runtime;

/// <summary>
/// Diagnostic/internal runtime (plan §3.1, §17): runs an SDK bot in-process. Not the canonical
/// player environment — WASM is — but it powers engine tests, built-in bot development, and
/// runtime contract tests. Translates between SDK-facing and engine-facing models (plan §8).
/// </summary>
public sealed class InProcessBotRuntime : IBotRuntime
{
    private readonly Func<Sdk.IBot> _botFactory;
    private Sdk.IBot? _bot;
    private DeterministicRandom? _random;
    private int _slot;

    public InProcessBotRuntime(Func<Sdk.IBot> botFactory) => _botFactory = botFactory;

    public void StartMatch(BotMatchStart start)
    {
        // A fresh instance every match: bot state must never leak across matches (plan §7).
        _bot = _botFactory();
        _random = new DeterministicRandom(start.BotRandomSeed);
        _slot = start.Slot;
    }

    public BotDecision ExecuteTick(BotObservation observation)
    {
        if (_bot is null || _random is null)
            throw new InvalidOperationException("StartMatch must be called before ExecuteTick.");
        var debug = new DebugCollector();
        var context = SdkModelMapper.ToContext(observation, _slot, new SdkRandom(_random), debug);
        try
        {
            var action = _bot.Tick(context);
            return BotDecision.Of(SdkModelMapper.ToEngineAction(action), debug.TextOrNull);
        }
        catch (Exception ex)
        {
            return BotDecision.Fault($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private sealed class SdkRandom(DeterministicRandom inner) : Sdk.IBotRandom
    {
        public int NextInt(int minimumInclusive, int maximumExclusive) =>
            inner.NextInt(minimumInclusive, maximumExclusive);

        public bool NextBool() => inner.NextBool();

        public double NextDouble() => inner.NextDouble();
    }

    private sealed class DebugCollector : Sdk.IBotDebug
    {
        private StringBuilder? _text;

        public string? TextOrNull => _text?.ToString();

        public void Write(string message)
        {
            _text ??= new StringBuilder();
            if (_text.Length > 0)
                _text.Append('\n');
            _text.Append(message);
        }

        public void Write(string format, params object?[] arguments) =>
            Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, arguments));
    }
}

/// <summary>Maps engine observation/action models to the SDK's and back (plan §8).</summary>
internal static class SdkModelMapper
{
    public static Sdk.BotContext ToContext(BotObservation observation, int slot, Sdk.IBotRandom random, Sdk.IBotDebug debug) =>
        new()
        {
            Tick = observation.Tick,
            Slot = slot,
            Position = new Sdk.Position(observation.Position.X, observation.Position.Y),
            Facing = ToSdkDirection(observation.Facing),
            Health = observation.Health,
            Cooldown = observation.Cooldown,
            Energy = observation.Energy,
            MapWidth = observation.MapWidth,
            MapHeight = observation.MapHeight,
            ZoneTiles = observation.ZoneTiles?.Select(p => new Sdk.Position(p.X, p.Y)).ToArray(),
            MyZoneTicks = observation.MyZoneTicks,
            EnemyZoneTicks = observation.EnemyZoneTicks,
            VisibleProjectiles = observation.VisibleProjectiles
                ?.Select(p => new Sdk.VisibleProjectile(
                    new Sdk.Position(p.Position.X, p.Position.Y), ToSdkDirection(p.Direction), p.OwnerSlot,
                    p.TicksUntilAdvance, p.RemainingTiles))
                .ToArray(),
            HeardSounds = observation.HeardSounds
                ?.Select(h => new Sdk.HeardSound(
                    ToSdkEventKind(h.Type), (Sdk.SoundBearing)h.Bearing, (Sdk.SoundDistance)h.Distance))
                .ToArray(),
            PreviousActionResult = ToSdkResult(observation.PreviousActionResult),
            VisibleTiles = observation.VisibleTiles
                .Select(t => new Sdk.VisibleTile(new Sdk.Position(t.Position.X, t.Position.Y), t.IsWall))
                .ToArray(),
            VisibleEnemies = observation.VisibleEnemies
                .Select(e => new Sdk.VisibleEnemy(
                    e.Slot, new Sdk.Position(e.Position.X, e.Position.Y), ToSdkDirection(e.Facing), e.Health))
                .ToArray(),
            VisibleEvents = observation.VisibleEvents
                .Select(ToSdkEvent)
                .Where(e => e is not null)
                .Select(e => e!)
                .ToArray(),
            Random = random,
            Debug = debug,
        };

    public static Engine.BotAction ToEngineAction(Sdk.BotAction action) => action.Kind switch
    {
        Sdk.BotActionKind.Wait => Engine.BotAction.Wait,
        Sdk.BotActionKind.MoveForward => Engine.BotAction.MoveForward,
        Sdk.BotActionKind.TurnLeft => Engine.BotAction.TurnLeft,
        Sdk.BotActionKind.TurnRight => Engine.BotAction.TurnRight,
        Sdk.BotActionKind.Shoot => Engine.BotAction.Shoot,
        Sdk.BotActionKind.StrafeLeft => Engine.BotAction.StrafeLeft,
        Sdk.BotActionKind.StrafeRight => Engine.BotAction.StrafeRight,
        _ => Engine.BotAction.Wait,
    };

    private static Sdk.Direction ToSdkDirection(Direction direction) => direction switch
    {
        Direction.North => Sdk.Direction.North,
        Direction.East => Sdk.Direction.East,
        Direction.South => Sdk.Direction.South,
        Direction.West => Sdk.Direction.West,
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    private static Sdk.ActionResult ToSdkResult(ActionResult result) => result switch
    {
        ActionResult.None => Sdk.ActionResult.None,
        ActionResult.Success => Sdk.ActionResult.Success,
        ActionResult.Blocked => Sdk.ActionResult.Blocked,
        ActionResult.OnCooldown => Sdk.ActionResult.OnCooldown,
        ActionResult.Faulted => Sdk.ActionResult.Faulted,
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };

    private static Sdk.VisibleEventKind ToSdkEventKind(GameEventType type) => type switch
    {
        GameEventType.Turn => Sdk.VisibleEventKind.Turn,
        GameEventType.Move => Sdk.VisibleEventKind.Move,
        GameEventType.MoveBlocked => Sdk.VisibleEventKind.MoveBlocked,
        GameEventType.Shot => Sdk.VisibleEventKind.Shot,
        GameEventType.Damage => Sdk.VisibleEventKind.Damage,
        GameEventType.Destroyed => Sdk.VisibleEventKind.Destroyed,
        GameEventType.Fault => Sdk.VisibleEventKind.Fault,
        GameEventType.Disqualified => Sdk.VisibleEventKind.Disqualified,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static Sdk.VisibleEvent? ToSdkEvent(GameEvent gameEvent)
    {
        var position = gameEvent.ReferencePositions().Cast<Position?>().FirstOrDefault();
        if (position is null)
            return null;
        return new Sdk.VisibleEvent(
            ToSdkEventKind(gameEvent.Type), gameEvent.Slot, new Sdk.Position(position.Value.X, position.Value.Y));
    }
}
