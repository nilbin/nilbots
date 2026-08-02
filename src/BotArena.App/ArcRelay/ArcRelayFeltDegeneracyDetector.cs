using System.Text.Json;

namespace BotArena.App.ArcRelay;

public sealed record ArcRelayDegeneracyRead(
    IReadOnlyDictionary<int, IReadOnlyList<string>> ReasonsByTeam)
{
    public bool Tripped(int teamId) => ReasonsByTeam.TryGetValue(teamId, out var reasons) && reasons.Count > 0;
}

/// <summary>
/// Hosted port of the frozen v3 cohort bars. It reads only the public compact
/// broadcast, so suspension never depends on private observations.
/// </summary>
public static class ArcRelayFeltDegeneracyDetector
{
    private const int Window = 75;
    private const int WindowTrip = 60;
    private const int StuckTrip = 30;
    private const int HomeProgressTrip = 30;

    public static ArcRelayDegeneracyRead Analyze(ReadOnlyMemory<byte> canonicalBroadcast)
    {
        using JsonDocument document = JsonDocument.Parse(canonicalBroadcast);
        JsonElement root = document.RootElement;
        JsonElement worlds = root.GetProperty("worlds");
        JsonElement turns = root.GetProperty("turns");
        JsonElement events = root.GetProperty("events");
        JsonElement initial = root.GetProperty("initial");
        var reasons = new Dictionary<int, HashSet<string>>
        {
            [0] = new(StringComparer.Ordinal),
            [1] = new(StringComparer.Ordinal),
        };

        int firstBirth = initial[7].GetProperty("wells").EnumerateArray()
            .Select(value => value.GetProperty("nextScheduledBirthTick").GetInt32()).Min();
        bool[][] quiet = [new bool[worlds.GetArrayLength()], new bool[worlds.GetArrayLength()]];
        bool[][] highWait = [new bool[worlds.GetArrayLength()], new bool[worlds.GetArrayLength()]];

        var stuck = new Dictionary<string, StuckState>(StringComparer.Ordinal);
        var home = new Dictionary<string, HomeState>(StringComparer.Ordinal);
        Dictionary<int, (int X, int Y)> reactors = initial[7].GetProperty("reactors")
            .EnumerateArray().ToDictionary(value => value.GetProperty("teamId").GetInt32(),
                value => Position(value.GetProperty("position")));
        string[] rows = root.GetProperty("header").GetProperty("contract").GetProperty("map")
            .GetProperty("tileRows").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Dictionary<int, Dictionary<(int, int), int>> fields = reactors.ToDictionary(
            value => value.Key, value => Distances(rows, value.Value));

        for (int tick = 0; tick < worlds.GetArrayLength(); tick++)
        {
            JsonElement world = worlds[tick];
            Life[] lives = world[4].EnumerateArray().Select(LifeFrom).ToArray();
            JsonElement[] cores = world[7].GetProperty("visibleCores").EnumerateArray().ToArray();
            for (int team = 0; team < 2; team++)
            {
                Life[] own = lives.Where(value => value.Team == team).ToArray();
                JsonElement[] teamTurns = turns[tick].EnumerateArray()
                    .Where(value => value[0][0].GetInt32() == team).ToArray();
                int waits = teamTurns.Count(value =>
                    value[4].ValueKind == JsonValueKind.Array &&
                    value[4].GetArrayLength() > 0 &&
                    string.Equals(value[4][0].GetString(), "wait", StringComparison.Ordinal));
                bool waiting = teamTurns.Length > 0 && waits / (double)teamTurns.Length >= .75;
                bool owns = cores.Any(value => Carrier(value) is Actor actor && actor.Team == team);
                bool near = own.Any(life => cores.Any(core => Chebyshev(life.Position, Position(core.GetProperty("position"))) <= 4));
                highWait[team][tick] = tick >= firstBirth && own.Length > 0 && waiting;
                quiet[team][tick] = highWait[team][tick] && !owns && !near;
            }

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement core in cores)
            {
                string key = CoreKey(core.GetProperty("coreId"));
                present.Add(key);
                Actor? carrier = Carrier(core);
                (int X, int Y) position = Position(core.GetProperty("position"));
                if (carrier is null || !string.Equals(core.GetProperty("disposition").GetString(), "carried", StringComparison.Ordinal))
                {
                    stuck.Remove(key); home.Remove(key); continue;
                }
                Actor actor = carrier.Value;
                if (!stuck.TryGetValue(key, out StuckState? prior) || prior.Carrier != actor || prior.Position != position)
                    stuck[key] = prior = new StuckState(actor, position, 1);
                else
                    stuck[key] = prior = prior with { Count = prior.Count + 1 };
                if (prior.Count >= StuckTrip) reasons[actor.Team].Add("stuck carrier");

                int distance = fields[actor.Team].GetValueOrDefault(position, int.MaxValue);
                if (!home.TryGetValue(key, out HomeState? progress) || progress.Team != actor.Team)
                    home[key] = progress = new HomeState(actor.Team, distance, 0);
                if (distance < progress.BestDistance)
                    home[key] = progress = progress with { BestDistance = distance, QuietTicks = 0 };
                if (distance <= 6)
                {
                    bool contested = lives.Any(value => value.Team != actor.Team && Chebyshev(value.Position, position) <= 2);
                    if (!contested)
                        home[key] = progress = progress with { QuietTicks = progress.QuietTicks + 1 };
                    if (progress.QuietTicks >= HomeProgressTrip)
                        reasons[actor.Team].Add("home carrier non-progress");
                }
            }
            foreach (string key in stuck.Keys.Where(key => !present.Contains(key)).ToArray()) stuck.Remove(key);
            foreach (string key in home.Keys.Where(key => !present.Contains(key)).ToArray()) home.Remove(key);
        }

        for (int team = 0; team < 2; team++)
        {
            if (MaximumInWindow(quiet[team], Window) >= WindowTrip)
                reasons[team].Add("sustained passivity");
            if (MaximumInWindow(highWait[team], Window) >= WindowTrip)
                reasons[team].Add("formation freeze");
        }
        DetectPingPong(events, reasons);
        return new ArcRelayDegeneracyRead(reasons.ToDictionary(
            value => value.Key,
            value => (IReadOnlyList<string>)value.Value.Order(StringComparer.Ordinal).ToArray()));
    }

    private static void DetectPingPong(JsonElement events, Dictionary<int, HashSet<string>> reasons)
    {
        var histories = new Dictionary<string, List<Handoff>>(StringComparer.Ordinal);
        var epochs = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int tick = 0; tick < events.GetArrayLength(); tick++)
        foreach (JsonElement value in events[tick].EnumerateArray())
        {
            if (!string.Equals(value.GetProperty("kind").GetString(), "arc-relay", StringComparison.Ordinal)) continue;
            JsonElement fact = value.GetProperty("payload").GetProperty("fact");
            string kind = fact.GetProperty("kind").GetString()!;
            if (!fact.TryGetProperty("coreId", out JsonElement coreId)) continue;
            string key = CoreKey(coreId);
            if (kind is "core-born" or "core-picked-up" or "core-dropped" or "core-banked")
                epochs[key] = epochs.GetValueOrDefault(key) + 1;
            if (kind != "core-handed-off") continue;
            Actor source = ActorFrom(fact.GetProperty("sourceActorId"));
            Actor target = ActorFrom(fact.GetProperty("targetActorId"));
            if (!histories.TryGetValue(key, out List<Handoff>? history)) histories[key] = history = [];
            history.Add(new Handoff(tick, epochs.GetValueOrDefault(key), source, target));
        }
        foreach (List<Handoff> history in histories.Values)
        {
            int reversals = 0;
            Handoff? prior = null;
            (Actor, Actor)? pair = null;
            foreach (Handoff current in history)
            {
                bool reverse = prior is Handoff p && current.Epoch == p.Epoch && current.Tick - p.Tick <= 2 && current.Source == p.Target && current.Target == p.Source;
                (Actor, Actor) currentPair = current.Source.GetHashCode() <= current.Target.GetHashCode()
                    ? (current.Source, current.Target) : (current.Target, current.Source);
                if (reverse && pair == currentPair) reversals++;
                else if (reverse) { pair = currentPair; reversals = 1; }
                else { pair = null; reversals = 0; }
                if (reversals >= 3) reasons[current.Source.Team].Add("handoff ping-pong");
                prior = current;
            }
        }
    }

    private static int MaximumInWindow(bool[] flags, int window)
    {
        int running = 0, maximum = 0;
        for (int index = 0; index < flags.Length; index++)
        {
            if (flags[index]) running++;
            if (index >= window && flags[index - window]) running--;
            if (index >= window - 1) maximum = Math.Max(maximum, running);
        }
        return maximum;
    }

    private static Dictionary<(int, int), int> Distances(string[] rows, (int X, int Y) start)
    {
        var result = new Dictionary<(int, int), int> { [start] = 0 };
        var queue = new Queue<(int, int)>(); queue.Enqueue(start);
        int[] steps = [-1, 0, 1];
        while (queue.TryDequeue(out var current))
        foreach (int dx in steps) foreach (int dy in steps)
        {
            if (dx == 0 && dy == 0) continue;
            var next = (current.Item1 + dx, current.Item2 + dy);
            if (next.Item2 < 0 || next.Item2 >= rows.Length || next.Item1 < 0 || next.Item1 >= rows[0].Length || rows[next.Item2][next.Item1] == '#' || result.ContainsKey(next)) continue;
            result[next] = result[current] + 1; queue.Enqueue(next);
        }
        return result;
    }

    private static Actor? Carrier(JsonElement core) =>
        core.TryGetProperty("carrierActorId", out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? ActorFrom(value) : null;
    private static Actor ActorFrom(JsonElement value) => value.ValueKind == JsonValueKind.Array
        ? new(value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt64())
        : new(value.GetProperty("teamId").GetInt32(), value.GetProperty("unitId").GetInt32(), value.GetProperty("lifeId").GetInt64());
    private static Life LifeFrom(JsonElement value) => new(value[0].GetInt32(), (value[6].GetInt32(), value[7].GetInt32()));
    private static (int X, int Y) Position(JsonElement value) => value.ValueKind == JsonValueKind.Array
        ? (value[0].GetInt32(), value[1].GetInt32())
        : (value.GetProperty("x").GetInt32(), value.GetProperty("y").GetInt32());
    private static int Chebyshev((int X, int Y) a, (int X, int Y) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private static string CoreKey(JsonElement value) => $"{value.GetProperty("sourceWellId").GetString()}:{value.GetProperty("sourceOrdinal").GetInt64()}";

    private readonly record struct Actor(int Team, int Unit, long Life);
    private readonly record struct Life(int Team, (int X, int Y) Position);
    private sealed record StuckState(Actor Carrier, (int X, int Y) Position, int Count);
    private sealed record HomeState(int Team, int BestDistance, int QuietTicks);
    private readonly record struct Handoff(int Tick, int Epoch, Actor Source, Actor Target);
}
