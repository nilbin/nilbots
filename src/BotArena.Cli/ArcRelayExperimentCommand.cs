using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Local, quota-free Arc Relay H0 runner for native mind artifacts. It emits a
/// gzip-only canonical replay plus a small run receipt suitable for the
/// regenerate-don't-store evaluation harness.
/// </summary>
public static class ArcRelayExperimentCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(
            options,
            "bot",
            "opponent",
            "seed",
            "runtime",
            "out",
            "sheet0",
            "sheet1",
            "classes0",
            "classes1",
            "print-contract");

        bool printContract = OptionalFlag(options, "print-contract");
        SheetSelection teamZero = Sheet(
            options,
            "sheet0",
            "classes0",
            DefaultTeamZero());
        SheetSelection teamOne = Sheet(
            options,
            "sheet1",
            "classes1",
            DefaultTeamOne());
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            teamZero.Classes,
            teamOne.Classes);
        if (printContract)
        {
            Console.WriteLine(
                ActorContractManifestSerializer.ToCanonicalJson(definition));
            return 0;
        }

        string botSpec = Required(options, "bot");
        string opponentSpec = Required(options, "opponent");
        string runtimeKind = options
            .GetValueOrDefault("runtime", "wasm")
            .ToLowerInvariant();
        if (runtimeKind is not ("wasm" or "in-process"))
        {
            throw new InvalidOperationException(
                $"Unknown runtime '{runtimeKind}' (use wasm or in-process).");
        }
        ulong seed = ParseSeed(options.GetValueOrDefault("seed", "42"));
        string output = Path.GetFullPath(
            options.GetValueOrDefault(
                "out",
                Path.Combine("out", "arc-relay", $"s{seed}")));

        using ResolvedLabsEntrant first = ResolvedLabsEntrant.Resolve(
            botSpec,
            runtimeKind,
            mindProfile: true,
            quiet: false);
        using ResolvedLabsEntrant second = ResolvedLabsEntrant.Resolve(
            opponentSpec,
            runtimeKind,
            mindProfile: true,
            quiet: false);
        GenericActorParticipantConfiguration[] participants =
        [
            first.ToParticipant(participantId: 0, teamId: 0),
            second.ToParticipant(participantId: 1, teamId: 1),
        ];

        (GenericActorMatchResult result, GenericActorReplayDocument replay) =
            MatchRun.Guard(
                MatchRun.Cell(first.Name, second.Name, seed),
                () =>
                {
                    using var session = new GenericActorMatchSession(
                        definition,
                        participants,
                        seed);
                    GenericActorMatchResult ran = session.Run();
                    return (
                        ran,
                        GenericActorReplayDocument.Create(
                            session,
                            ArcRelayH0ReplayPresentation.Create(definition)));
                });

        WrittenReplay written = ReplayOutput.WriteGzipJson(
            replay.CanonicalJson,
            output);
        ArcRelayRunReceipt receipt = Receipt(
            definition,
            seed,
            first,
            second,
            teamZero,
            teamOne,
            result,
            replay,
            written);
        string receiptPath = Path.Combine(output, "run.json");
        File.WriteAllText(
            receiptPath,
            JsonSerializer.Serialize(
                receipt,
                new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine(
            $"Arc Relay H0: {first.Name} vs {second.Name}, seed {seed}");
        Console.WriteLine(
            $"Result: {Verdict(result, first.Name, second.Name)} — "
            + $"{Reason(result)} at tick {result.EndTick ?? -1}");
        Console.WriteLine($"Hash:   {replay.ReplayHash}");
        Console.WriteLine($"Replay: {written.ReplayPath}");
        Console.WriteLine($"Run:    {Path.GetFullPath(receiptPath)}");

        if (result.EligibleTeamIds.Length != 2)
        {
            Console.Error.WriteLine(
                "Arc Relay participant faulted or was disqualified.");
            PrintWasmDiagnostics(first);
            PrintWasmDiagnostics(second);
            return 2;
        }
        return 0;
    }

    private static ArcRelayRunReceipt Receipt(
        ActorResolvedMatchDefinition definition,
        ulong seed,
        ResolvedLabsEntrant first,
        ResolvedLabsEntrant second,
        SheetSelection teamZero,
        SheetSelection teamOne,
        GenericActorMatchResult result,
        GenericActorReplayDocument replay,
        WrittenReplay written) =>
        new(
            SchemaVersion: 1,
            RulesetId: definition.Rules.RulesetId,
            RulesFingerprint: ActorContractFingerprint.ComputeRules(
                definition.Rules),
            MapId: definition.Map.Id,
            MapFingerprint: ActorContractFingerprint.ComputeMap(
                definition.Map),
            TopologyFingerprint: ActorContractFingerprint.ComputeTopology(
                definition.Topology),
            MatchContractFingerprint: ActorContractFingerprint.ComputeMatch(
                definition),
            Seed: seed.ToString(CultureInfo.InvariantCulture),
            Participants:
            [
                ParticipantReceipt(0, 0, first, teamZero),
                ParticipantReceipt(1, 1, second, teamOne),
            ],
            Result: new ArcRelayRunResultReceipt(
                result.WinnerTeamId,
                Reason(result),
                result.EndTick,
                result.EligibleTeamIds.ToArray()),
            Replay: new ArcRelayReplayReceipt(
                BotArenaVersions.GenericActorReplayFormatVersion,
                replay.ReplayHash,
                Path.GetFileName(written.ReplayPath),
                new FileInfo(written.ReplayPath).Length));

    private static ArcRelayParticipantReceipt ParticipantReceipt(
        int participantId,
        int teamId,
        ResolvedLabsEntrant entrant,
        SheetSelection sheet) =>
        new(
            participantId,
            teamId,
            entrant.Name,
            entrant.ArtifactHash,
            entrant.RuntimeKind,
            sheet.Hash,
            sheet.Path,
            sheet.Classes);

    private static SheetSelection Sheet(
        IReadOnlyDictionary<string, string> options,
        string sheetOption,
        string classesOption,
        string[] fallback)
    {
        bool hasSheet = options.TryGetValue(sheetOption, out string? sheetPath);
        bool hasClasses = options.TryGetValue(classesOption, out string? raw);
        if (hasSheet && hasClasses)
        {
            throw new InvalidOperationException(
                $"Use either --{sheetOption} or --{classesOption}, not both.");
        }
        if (hasSheet)
        {
            if (string.IsNullOrWhiteSpace(sheetPath)
                || string.Equals(
                    sheetPath,
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"--{sheetOption} requires a JSON path.");
            }
            string fullPath = Path.GetFullPath(sheetPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            JsonElement composition = root.TryGetProperty(
                    "composition",
                    out JsonElement value)
                ? value
                : throw new InvalidDataException(
                    $"{fullPath}: sheet needs a composition array.");
            string[] classes = composition
                .EnumerateArray()
                .Select(entry => entry.GetString()
                    ?? throw new InvalidDataException(
                        $"{fullPath}: composition entries must be strings."))
                .ToArray();
            return new SheetSelection(
                classes,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                fullPath);
        }

        string[] selected = hasClasses
            ? ParseClasses(raw!, classesOption)
            : fallback;
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(selected);
        return new SheetSelection(
            selected,
            Convert.ToHexStringLower(SHA256.HashData(identity)),
            Path: null);
    }

    private static string[] ParseClasses(string raw, string option)
    {
        string[] classes = raw.Split(
            ',',
            StringSplitOptions.TrimEntries
            | StringSplitOptions.RemoveEmptyEntries);
        if (classes.Length == 0)
        {
            throw new InvalidOperationException(
                $"--{option} requires a comma-separated composition.");
        }
        return classes;
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
                $"nilbots experiment arc-relay requires --{name} "
                + "<project|wasm>.");
        }
        return value;
    }

    private static ulong ParseSeed(string value) =>
        ulong.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out ulong seed)
            ? seed
            : throw new InvalidOperationException(
                $"Invalid unsigned 64-bit seed '{value}'.");

    private static bool OptionalFlag(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value))
            return false;
        if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"--{name} does not accept a value.");
        }
        return true;
    }

    private static string Verdict(
        GenericActorMatchResult result,
        string first,
        string second) =>
        result.WinnerTeamId switch
        {
            0 => $"{first} wins",
            1 => $"{second} wins",
            _ => "draw",
        };

    private static string Reason(GenericActorMatchResult result) =>
        result.Mode is GenericActorMatchModeResult.ArcRelay arc
            ? arc.Reason switch
            {
                GenericArcRelayEndReason.ReactorDestroyed =>
                    "reactor-destroyed",
                GenericArcRelayEndReason.MaxTicks => "max-ticks",
                GenericArcRelayEndReason.FaultEligibility =>
                    "fault-eligibility",
                _ => arc.Reason.ToString(),
            }
            : result.CompletionReason;

    private static void PrintWasmDiagnostics(ResolvedLabsEntrant entrant)
    {
        foreach ((string subject, ulong peak, ulong budget, string reason)
                 in entrant.SandboxFailures)
        {
            Console.Error.WriteLine(
                $"  {entrant.Name} {subject}: {reason} "
                + $"(peak {peak / 1_000_000.0:F1}M/"
                + $"{budget / 1_000_000.0:F1}M fuel)");
        }
    }

    private static string[] DefaultTeamZero() =>
    [
        ArcRelayLaunchClassIds.Kestrel,
        ArcRelayLaunchClassIds.Palisade,
        ArcRelayLaunchClassIds.Towline,
        ArcRelayLaunchClassIds.Patchbay,
        ArcRelayLaunchClassIds.Lantern,
        ArcRelayLaunchClassIds.Mortar,
        ArcRelayLaunchClassIds.Minesmith,
        ArcRelayLaunchClassIds.Hush,
    ];

    private static string[] DefaultTeamOne() =>
    [
        ArcRelayLaunchClassIds.Relay,
        ArcRelayLaunchClassIds.Switchback,
        ArcRelayLaunchClassIds.Longshot,
        ArcRelayLaunchClassIds.Mason,
        ArcRelayLaunchClassIds.Sunder,
        ArcRelayLaunchClassIds.Repulsor,
        ArcRelayLaunchClassIds.Veil,
        ArcRelayLaunchClassIds.Nest,
    ];

    private sealed record SheetSelection(
        string[] Classes,
        string Hash,
        string? Path);
}

public sealed record ArcRelayRunReceipt(
    int SchemaVersion,
    string RulesetId,
    string RulesFingerprint,
    string MapId,
    string MapFingerprint,
    string TopologyFingerprint,
    string MatchContractFingerprint,
    string Seed,
    ArcRelayParticipantReceipt[] Participants,
    ArcRelayRunResultReceipt Result,
    ArcRelayReplayReceipt Replay);

public sealed record ArcRelayParticipantReceipt(
    int ParticipantId,
    int TeamId,
    string Name,
    string ArtifactHash,
    string RuntimeKind,
    string SheetHash,
    string? SheetPath,
    string[] Classes);

public sealed record ArcRelayRunResultReceipt(
    int? WinnerTeamId,
    string Reason,
    int? EndTick,
    int[] EligibleTeamIds);

public sealed record ArcRelayReplayReceipt(
    int FormatVersion,
    string Hash,
    string File,
    long GzipBytes);
