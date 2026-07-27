using System.Globalization;
using System.Text;
using BotArena.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Host side of runtime protocol 0.1: a line-oriented UTF-8 text protocol between the host
/// and the WASM guest. Deliberately dependency-free and byte-stable — the encoding is part
/// of the runtime protocol version (plan §34.2). The guest-side twin lives in
/// BotArena.WasmGuest/GuestProtocol.cs; keep them in sync.
///
/// host -> guest:  I &lt;protocol&gt; &lt;slot&gt; &lt;botSeed&gt; &lt;rulesVersion&gt; &lt;botName&gt;
/// guest -> host:  R &lt;protocol&gt;
/// host -> guest:  T &lt;tick&gt; &lt;x&gt; &lt;y&gt; &lt;facing&gt; &lt;health&gt; &lt;cooldown&gt; &lt;prevResult&gt;
///                   NT &lt;n&gt; x:y:w ... NE &lt;n&gt; slot:x:y:facing:health ... NV &lt;n&gt; kind:slot:x:y ...
/// guest -> host:  A &lt;action&gt; [SP aim:bend:after:every:count] [D &lt;base64 debug&gt;]
///                  | F &lt;base64 fault message&gt;
/// </summary>
public static class WasmProtocol
{
    public const string ProtocolVersion = "0.1";

    public static string FormatInit(BotMatchStart start, string botName) =>
        string.Create(CultureInfo.InvariantCulture,
            $"I {ProtocolVersion} {start.Slot} {start.BotRandomSeed} {start.GameRulesVersion}")
        + (botName.Length > 0 ? " " + botName : "");

    public static string FormatObservation(BotObservation observation)
    {
        var builder = new StringBuilder(256);
        builder.Append(CultureInfo.InvariantCulture,
            $"T {observation.Tick} {observation.Position.X} {observation.Position.Y} " +
            $"{(int)observation.Facing} {observation.Health} {observation.Cooldown} " +
            $"{(int)observation.PreviousActionResult}");

        builder.Append(" NT ").Append(observation.VisibleTiles.Count);
        foreach (var tile in observation.VisibleTiles)
            builder.Append(' ').Append(tile.Position.X).Append(':').Append(tile.Position.Y)
                .Append(':').Append(tile.IsWall ? 1 : 0);

        builder.Append(" NE ").Append(observation.VisibleEnemies.Count);
        foreach (var enemy in observation.VisibleEnemies)
            builder.Append(' ').Append(enemy.Slot).Append(':').Append(enemy.Position.X)
                .Append(':').Append(enemy.Position.Y).Append(':').Append((int)enemy.Facing)
                .Append(':').Append(enemy.Health);

        builder.Append(" NV ").Append(observation.VisibleEvents.Count);
        foreach (var observedEvent in observation.VisibleEvents)
        {
            builder.Append(' ').Append((int)observedEvent.Type).Append(':')
                .Append(observedEvent.Slot ?? -1).Append(':')
                .Append(observedEvent.Position.X).Append(':')
                .Append(observedEvent.Position.Y);
        }
        // Optional trailing sections, in fixed order (E, M, Z, ZT, C, P, H, SP, PH).
        // Appended LAST
        // so protocol-0.1 guests — which parse exactly the tokens they expect and never
        // index further — remain compatible (same trick as the optional botName).
        // P grew 4→6 fields in the 0.5 hardening and 6→7 for ordered speed-two
        // substeps. Experiment adapters that parse an older P shape must rebuild;
        // pre-bolt adapters never read P at all.
        if (observation.Energy is int energy)
            builder.Append(" E ").Append(energy);
        if (observation.MapWidth > 0)
            builder.Append(" M ").Append(observation.MapWidth).Append(' ').Append(observation.MapHeight);
        if (observation.ZoneTiles is not null)
        {
            builder.Append(" Z ").Append(observation.ZoneTiles.Count);
            foreach (var tile in observation.ZoneTiles)
                builder.Append(' ').Append(tile.X).Append(':').Append(tile.Y);
            builder.Append(" ZT ").Append(observation.MyZoneTicks ?? -1)
                .Append(' ').Append(observation.EnemyZoneTicks ?? -1);
        }
        if (observation.ControlPressure is int pressure)
            builder.Append(" C ").Append(pressure)
                .Append(' ').Append(observation.ControlPressureLimit ?? 0);
        if (observation.VisibleProjectiles is not null)
        {
            builder.Append(" P ").Append(observation.VisibleProjectiles.Count);
            foreach (var bolt in observation.VisibleProjectiles)
                builder.Append(' ').Append(bolt.Position.X).Append(':').Append(bolt.Position.Y)
                    .Append(':').Append((int)bolt.Direction).Append(':').Append(bolt.OwnerSlot)
                    .Append(':').Append(bolt.TilesPerAdvance)
                    .Append(':').Append(bolt.TicksUntilAdvance).Append(':').Append(bolt.RemainingTiles);
        }
        if (observation.HeardSounds is not null)
        {
            builder.Append(" H ").Append(observation.HeardSounds.Count);
            foreach (var sound in observation.HeardSounds)
                builder.Append(' ').Append((int)sound.Type).Append(':').Append(sound.Bearing)
                    .Append(':').Append(sound.Distance);
        }
        if (observation.ShotPrograms is { } limits)
        {
            builder.Append(" SP ")
                .Append(limits.MaxInitialAimOctants).Append(':')
                .Append(limits.MaxBendAfterTiles).Append(':')
                .Append(limits.MaxBendEveryTiles).Append(':')
                .Append(limits.MaxBendCount).Append(':')
                .Append(limits.MaxPathTiles).Append(':')
                .Append(limits.LaunchTiles).Append(':')
                .Append(limits.TilesPerAdvance);
        }
        if (observation.VisibleProjectiles is { } projectiles
            && projectiles.Any(p => p.Heading is not null))
        {
            builder.Append(" PH ").Append(projectiles.Count);
            foreach (var bolt in projectiles)
                builder.Append(' ').Append(bolt.Heading is { } heading ? (int)heading : -1);
        }
        return builder.ToString();
    }

    public static BotDecision ParseReply(string reply)
    {
        var parts = reply.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0] == "F")
            return BotDecision.Fault(DecodeBase64(parts[1]));
        if (parts.Length >= 2 && parts[0] == "A" && int.TryParse(parts[1], CultureInfo.InvariantCulture, out int action))
        {
            string? debug = null;
            ShotProgram? program = null;
            int index = 2;
            while (index < parts.Length)
            {
                switch (parts[index++])
                {
                    case "SP" when index < parts.Length:
                        {
                            var fields = parts[index++].Split(':');
                            if (fields.Length != 5
                                || !fields.All(field => int.TryParse(
                                    field, CultureInfo.InvariantCulture, out _)))
                                return BotDecision.Fault("Malformed programmed-shot payload from guest.");
                            var values = fields
                                .Select(field => int.Parse(field, CultureInfo.InvariantCulture))
                                .ToArray();
                            program = new ShotProgram(values[0], values[1], values[2], values[3], values[4]);
                            break;
                        }
                    case "D" when index < parts.Length:
                        debug = DecodeBase64(parts[index++]);
                        break;
                    default:
                        return BotDecision.Fault($"Malformed guest reply: '{Truncate(reply, 80)}'");
                }
            }
            // 0-6: strafes (5/6) arrived with rules 0.3; the engine gates them by rules,
            // so accepting the wider range here is safe for every ruleset.
            if (action is < 0 or > 6)
                return BotDecision.Fault($"Invalid action value {action} from guest.");
            return new BotDecision
            {
                Action = (BotAction)action,
                ShotProgram = program,
                DebugMessage = debug,
            };
        }
        return BotDecision.Fault($"Malformed guest reply: '{Truncate(reply, 80)}'");
    }

    private static string DecodeBase64(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return "<malformed debug payload>";
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
