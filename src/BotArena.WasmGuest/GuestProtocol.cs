using System.Globalization;
using System.Text;
using BotArena.Sdk;

namespace BotArena.WasmGuest;

/// <summary>
/// Guest side of runtime protocol 0.1. Twin of BotArena.Runtime.Wasm/WasmProtocol.cs —
/// keep both in sync; the encoding is pinned by the runtime protocol version.
/// </summary>
internal static class GuestProtocol
{
    public const string ProtocolVersion = "0.1";

    public sealed record InitMessage(int Slot, ulong Seed, string RulesVersion, string BotName);

    public static InitMessage ParseInit(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6 || parts[0] != "I")
            throw new FormatException("Malformed init line.");
        if (parts[1] != ProtocolVersion)
            throw new FormatException($"Protocol mismatch: host {parts[1]}, guest {ProtocolVersion}.");
        return new InitMessage(
            int.Parse(parts[2], CultureInfo.InvariantCulture),
            ulong.Parse(parts[3], CultureInfo.InvariantCulture),
            parts[4],
            parts[5]);
    }

    public static ParsedObservation ParseObservation(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8 || parts[0] != "T")
            throw new FormatException("Malformed tick line.");
        int index = 1;
        int Next() => int.Parse(parts[index++], CultureInfo.InvariantCulture);

        int tick = Next();
        int x = Next(), y = Next(), facing = Next(), health = Next(), cooldown = Next(), previous = Next();

        Expect("NT");
        int tileCount = Next();
        var tiles = new VisibleTile[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            var f = Fields(3);
            tiles[i] = new VisibleTile(new Position(f[0], f[1]), f[2] == 1);
        }

        Expect("NE");
        int enemyCount = Next();
        var enemies = new VisibleEnemy[enemyCount];
        for (int i = 0; i < enemyCount; i++)
        {
            var f = Fields(5);
            enemies[i] = new VisibleEnemy(f[0], new Position(f[1], f[2]), (Direction)f[3], f[4]);
        }

        Expect("NV");
        int eventCount = Next();
        var events = new VisibleEvent[eventCount];
        for (int i = 0; i < eventCount; i++)
        {
            var f = Fields(4);
            events[i] = new VisibleEvent((VisibleEventKind)f[0], f[1] < 0 ? null : f[1], new Position(f[2], f[3]));
        }

        return new ParsedObservation(
            tick, new Position(x, y), (Direction)facing, health, cooldown,
            (ActionResult)previous, tiles, enemies, events);

        void Expect(string marker)
        {
            if (parts[index++] != marker)
                throw new FormatException($"Expected '{marker}' in tick line.");
        }

        int[] Fields(int count)
        {
            var raw = parts[index++].Split(':');
            if (raw.Length != count)
                throw new FormatException("Malformed tick field.");
            var values = new int[count];
            for (int i = 0; i < count; i++)
                values[i] = int.Parse(raw[i], CultureInfo.InvariantCulture);
            return values;
        }
    }

    public sealed record ParsedObservation(
        int Tick,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        ActionResult PreviousActionResult,
        IReadOnlyList<VisibleTile> VisibleTiles,
        IReadOnlyList<VisibleEnemy> VisibleEnemies,
        IReadOnlyList<VisibleEvent> VisibleEvents);

    public static string FormatDecision(BotAction action, string? debug)
    {
        string reply = "A " + (int)action.Kind;
        if (!string.IsNullOrEmpty(debug))
            reply += " D " + Convert.ToBase64String(Encoding.UTF8.GetBytes(debug));
        return reply;
    }

    public static string FormatFault(string message) =>
        "F " + Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
}
