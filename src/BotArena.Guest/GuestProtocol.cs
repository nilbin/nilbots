using System.Globalization;
using System.Text;
using BotArena.Sdk;

namespace BotArena.Guest;

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
        if (parts.Length < 5 || parts[0] != "I")
            throw new FormatException("Malformed init line.");
        if (parts[1] != ProtocolVersion)
            throw new FormatException($"Protocol mismatch: host {parts[1]}, guest {ProtocolVersion}.");
        return new InitMessage(
            int.Parse(parts[2], CultureInfo.InvariantCulture),
            ulong.Parse(parts[3], CultureInfo.InvariantCulture),
            parts[4],
            parts.Length > 5 ? parts[5] : "");
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

        // Optional trailing sections (additive, protocol 0.1-compatible), fixed order:
        // "E <energy>", "M <w> <h>", "Z <n> x:y ... ZT <mine> <theirs>",
        // "C <signedPressure> <limit>",
        // "P <n> x:y:dir:owner:tilesPerAdvance:ticksUntilAdvance:remainingTiles ...",
        // "H <n> kind:bearing:band ..." (redacted heard sounds),
        // "SP maxAim:maxAfter:maxEvery:maxCount:path:launch:speed" (program limits),
        // "PH <n> heading ..." (exact current headings aligned to P; -1 = legacy).
        int? energy = null;
        if (index < parts.Length && parts[index] == "E")
        {
            index++;
            energy = Next();
        }
        int mapWidth = 0, mapHeight = 0;
        if (index < parts.Length && parts[index] == "M")
        {
            index++;
            mapWidth = Next();
            mapHeight = Next();
        }
        Position[]? zone = null;
        int? myZoneTicks = null, enemyZoneTicks = null;
        if (index < parts.Length && parts[index] == "Z")
        {
            index++;
            int zoneCount = Next();
            zone = new Position[zoneCount];
            for (int i = 0; i < zoneCount; i++)
            {
                var f = Fields(2);
                zone[i] = new Position(f[0], f[1]);
            }
            Expect("ZT");
            int rawMyZoneTicks = Next();
            int rawEnemyZoneTicks = Next();
            myZoneTicks = rawMyZoneTicks < 0 ? null : rawMyZoneTicks;
            enemyZoneTicks = rawEnemyZoneTicks < 0 ? null : rawEnemyZoneTicks;
        }
        int? controlPressure = null, controlPressureLimit = null;
        if (index < parts.Length && parts[index] == "C")
        {
            index++;
            controlPressure = Next();
            controlPressureLimit = Next();
        }
        VisibleProjectile[]? projectiles = null;
        if (index < parts.Length && parts[index] == "P")
        {
            index++;
            int boltCount = Next();
            projectiles = new VisibleProjectile[boltCount];
            for (int i = 0; i < boltCount; i++)
            {
                var f = Fields(7);
                projectiles[i] = new VisibleProjectile(
                    new Position(f[0], f[1]), (Direction)f[2], f[3], f[4], f[5], f[6]);
            }
        }
        HeardSound[]? heardSounds = null;
        if (index < parts.Length && parts[index] == "H")
        {
            index++;
            int soundCount = Next();
            heardSounds = new HeardSound[soundCount];
            for (int i = 0; i < soundCount; i++)
            {
                var f = Fields(3);
                heardSounds[i] = new HeardSound(
                    (VisibleEventKind)f[0], (SoundBearing)f[1], (SoundDistance)f[2]);
            }
        }
        ShotProgramLimits? shotPrograms = null;
        if (index < parts.Length && parts[index] == "SP")
        {
            index++;
            var f = Fields(7);
            shotPrograms = new ShotProgramLimits(
                f[0], f[1], f[2], f[3], f[4], f[5], f[6]);
        }
        if (index < parts.Length && parts[index] == "PH")
        {
            index++;
            int headingCount = Next();
            if (projectiles is null || projectiles.Length != headingCount)
                throw new FormatException("Projectile heading count does not match projectile count.");
            for (int i = 0; i < headingCount; i++)
            {
                int rawHeading = Next();
                if (rawHeading is < -1 or > 7)
                    throw new FormatException("Malformed projectile heading.");
                if (rawHeading >= 0)
                    projectiles[i] = projectiles[i] with
                    {
                        ProgrammedHeading = (ProjectileHeading)rawHeading,
                    };
            }
        }

        return new ParsedObservation(
            tick, new Position(x, y), (Direction)facing, health, cooldown,
            (ActionResult)previous, tiles, enemies, events, energy,
            mapWidth, mapHeight, zone, myZoneTicks, enemyZoneTicks,
            controlPressure, controlPressureLimit, projectiles, heardSounds, shotPrograms);

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
        IReadOnlyList<VisibleEvent> VisibleEvents,
        int? Energy,
        int MapWidth,
        int MapHeight,
        IReadOnlyList<Position>? ZoneTiles,
        int? MyZoneTicks,
        int? EnemyZoneTicks,
        int? ControlPressure,
        int? ControlPressureLimit,
        IReadOnlyList<VisibleProjectile>? VisibleProjectiles = null,
        IReadOnlyList<HeardSound>? HeardSounds = null,
        ShotProgramLimits? ShotPrograms = null);

    public static string FormatDecision(BotAction action, string? debug)
    {
        string reply = "A " + (int)action.Kind;
        if (action.ShotProgram is { } program)
        {
            reply += string.Create(
                CultureInfo.InvariantCulture,
                $" SP {program.InitialAimOffset}:{program.BendDirection}:" +
                $"{program.BendAfterTiles}:{program.BendEveryTiles}:{program.BendCount}");
        }
        if (!string.IsNullOrEmpty(debug))
            reply += " D " + Convert.ToBase64String(Encoding.UTF8.GetBytes(debug));
        return reply;
    }

    public static string FormatFault(string message) =>
        "F " + Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
}
