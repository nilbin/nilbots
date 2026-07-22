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
/// guest -> host:  A &lt;action&gt; [D &lt;base64 debug&gt;]   |   F &lt;base64 fault message&gt;
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
        foreach (var gameEvent in observation.VisibleEvents)
        {
            Position position = default;
            foreach (var p in gameEvent.ReferencePositions())
            {
                position = p;
                break;
            }
            builder.Append(' ').Append((int)gameEvent.Type).Append(':')
                .Append(gameEvent.Slot ?? -1).Append(':').Append(position.X).Append(':').Append(position.Y);
        }
        // Optional trailing section: energy (rules with MaxEnergy > 0 only). Appended
        // LAST so protocol-0.1 guests — which parse exactly the tokens they expect and
        // never index further — remain compatible (same trick as the optional botName).
        if (observation.Energy is int energy)
            builder.Append(" E ").Append(energy);
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
            if (parts.Length >= 4 && parts[2] == "D")
                debug = DecodeBase64(parts[3]);
            if (action is < 0 or > 4)
                return BotDecision.Fault($"Invalid action value {action} from guest.");
            return BotDecision.Of((BotAction)action, debug);
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
