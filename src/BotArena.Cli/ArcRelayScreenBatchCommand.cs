using System.Diagnostics;
using System.Text.Json;

namespace BotArena.Cli;

/// <summary>
/// Persistent-process, in-process Arc Relay screening. It deliberately emits
/// no canonical replay and makes no audit claim: candidates screened here must
/// still pass the ordinary WASM + canonical-replay path before entering a
/// balance read or gallery.
/// </summary>
public static class ArcRelayScreenBatchCommand
{
    private const string Schema = "arc-relay-screen-batch-v1";

    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(
            options,
            "plan",
            "sweep-plan",
            "bot",
            "opponent",
            "limit",
            "out");
        string outputRoot = Path.GetFullPath(Required(options, "out"));
        ArcRelayScreenBatchPlan plan = LoadPlan(options);
        Validate(plan);

        Directory.CreateDirectory(outputRoot);
        var completed = new List<ArcRelayScreenBatchCellResult>(
            plan.Cells.Length);
        var clock = Stopwatch.StartNew();
        foreach (ArcRelayScreenBatchCell cell in plan.Cells)
        {
            string cellOutput = Path.Combine(outputRoot, cell.CellId);
            long cellStart = Stopwatch.GetTimestamp();
            int exitCode = ArcRelayExperimentCommand.Run(
            [
                "--bot", plan.Bot,
                "--opponent", plan.Opponent,
                "--sheet0", cell.Sheet0,
                "--sheet1", cell.Sheet1,
                "--seed", cell.Seed,
                "--runtime", "in-process",
                "--loop-profile", plan.LoopProfile,
                "--out", cellOutput,
                "--screen",
            ]);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Screen cell '{cell.CellId}' exited {exitCode}.");
            }
            string receiptPath = Path.Combine(cellOutput, "screen.json");
            ArcRelayScreenReceipt receipt = JsonSerializer.Deserialize<
                    ArcRelayScreenReceipt>(
                    File.ReadAllBytes(receiptPath),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    })
                ?? throw new InvalidDataException(
                    $"{receiptPath}: screen receipt deserialized to null.");
            completed.Add(new ArcRelayScreenBatchCellResult(
                cell.CellId,
                receipt.Seed,
                receipt.Result.WinnerTeamId,
                receipt.Result.Reason,
                receipt.Result.EndTick,
                Path.GetRelativePath(outputRoot, receiptPath),
                Stopwatch.GetElapsedTime(cellStart).TotalMilliseconds));
        }

        clock.Stop();
        var batch = new ArcRelayScreenBatchReceipt(
            SchemaVersion: 1,
            ScreenOnly: true,
            CanonicalReplayProduced: false,
            Runtime: "in-process",
            Cells: completed.ToArray(),
            ElapsedMilliseconds: clock.ElapsedMilliseconds,
            MeanMillisecondsPerCell:
                clock.Elapsed.TotalMilliseconds / completed.Count);
        string batchPath = Path.Combine(outputRoot, "batch.json");
        File.WriteAllText(
            batchPath,
            JsonSerializer.Serialize(
                batch,
                new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(
            $"Screened {completed.Count} Arc Relay cells in "
            + $"{clock.Elapsed.TotalSeconds:F3}s "
            + $"({batch.MeanMillisecondsPerCell:F1}ms/cell).");
        Console.WriteLine(
            "SCREEN ONLY: confirm selected cells through WASM, canonical "
            + "replay verification, and felt-degeneracy scorecards.");
        return 0;
    }

    private static ArcRelayScreenBatchPlan LoadPlan(
        IReadOnlyDictionary<string, string> options)
    {
        bool custom = options.TryGetValue("plan", out string? planPath);
        bool sweep = options.TryGetValue(
            "sweep-plan",
            out string? sweepPlanPath);
        if (custom == sweep)
        {
            throw new InvalidOperationException(
                "Use exactly one of --plan or --sweep-plan.");
        }
        if (custom)
        {
            return JsonSerializer.Deserialize<ArcRelayScreenBatchPlan>(
                    File.ReadAllBytes(Path.GetFullPath(planPath!)),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    })
                ?? throw new InvalidDataException(
                    "Arc Relay screen batch plan deserialized to null.");
        }

        string bot = Required(options, "bot");
        string opponent = options.GetValueOrDefault("opponent", bot);
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(Path.GetFullPath(sweepPlanPath!)));
        JsonElement root = document.RootElement;
        JsonElement entrants = root.GetProperty("entrants");
        int limit = int.MaxValue;
        if (options.TryGetValue("limit", out string? rawLimit))
        {
            if (!int.TryParse(rawLimit, out limit) || limit <= 0)
            {
                throw new InvalidOperationException(
                    "arc-relay-screen-batch requires --limit to be a "
                    + "positive integer.");
            }
        }
        ArcRelayScreenBatchCell[] cells = root.GetProperty("cells")
            .EnumerateArray()
            .Take(limit)
            .Select(cell =>
            {
                string team0 = cell.GetProperty("team0").GetString()
                    ?? throw new InvalidDataException(
                        "Sweep cell team0 must be a string.");
                string team1 = cell.GetProperty("team1").GetString()
                    ?? throw new InvalidDataException(
                        "Sweep cell team1 must be a string.");
                return new ArcRelayScreenBatchCell(
                    cell.GetProperty("cellId").GetString()
                        ?? throw new InvalidDataException(
                            "Sweep cellId must be a string."),
                    entrants.GetProperty(team0).GetProperty("sheet").GetString()
                        ?? throw new InvalidDataException(
                            "Sweep entrant sheet must be a string."),
                    entrants.GetProperty(team1).GetProperty("sheet").GetString()
                        ?? throw new InvalidDataException(
                            "Sweep entrant sheet must be a string."),
                    cell.GetProperty("seed").ToString());
            })
            .ToArray();
        return new ArcRelayScreenBatchPlan(
            Schema,
            bot,
            opponent,
            root.TryGetProperty("loopProfile", out JsonElement loopProfile)
                ? loopProfile.GetString() ?? "h0"
                : "h0",
            cells);
    }

    private static void Validate(ArcRelayScreenBatchPlan plan)
    {
        if (!string.Equals(plan.Schema, Schema, StringComparison.Ordinal))
            throw new InvalidDataException($"Expected plan schema '{Schema}'.");
        if (string.IsNullOrWhiteSpace(plan.Bot)
            || string.IsNullOrWhiteSpace(plan.Opponent)
            || string.IsNullOrWhiteSpace(plan.LoopProfile)
            || plan.Cells is not { Length: > 0 })
        {
            throw new InvalidDataException(
                "Screen plan needs bot, opponent, loopProfile and cells.");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ArcRelayScreenBatchCell cell in plan.Cells)
        {
            if (string.IsNullOrWhiteSpace(cell.CellId)
                || !ids.Add(cell.CellId)
                || string.IsNullOrWhiteSpace(cell.Sheet0)
                || string.IsNullOrWhiteSpace(cell.Sheet1)
                || string.IsNullOrWhiteSpace(cell.Seed))
            {
                throw new InvalidDataException(
                    "Screen cells need unique IDs, two sheets and a seed.");
            }
        }
    }

    private static string Required(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"arc-relay-screen-batch requires --{name} <value>.");
        }
        return value;
    }
}

public sealed record ArcRelayScreenBatchPlan(
    string Schema,
    string Bot,
    string Opponent,
    string LoopProfile,
    ArcRelayScreenBatchCell[] Cells);

public sealed record ArcRelayScreenBatchCell(
    string CellId,
    string Sheet0,
    string Sheet1,
    string Seed);

public sealed record ArcRelayScreenBatchCellResult(
    string CellId,
    string Seed,
    int? WinnerTeamId,
    string Reason,
    int? EndTick,
    string Receipt,
    double ElapsedMilliseconds);

public sealed record ArcRelayScreenBatchReceipt(
    int SchemaVersion,
    bool ScreenOnly,
    bool CanonicalReplayProduced,
    string Runtime,
    ArcRelayScreenBatchCellResult[] Cells,
    long ElapsedMilliseconds,
    double MeanMillisecondsPerCell);
