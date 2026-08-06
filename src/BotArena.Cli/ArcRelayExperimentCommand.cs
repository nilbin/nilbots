using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            "loop-profile",
            "screen",
            "print-contract");

        bool printContract = OptionalFlag(options, "print-contract");
        bool screen = OptionalFlag(options, "screen");
        ArcRelayLoopProfile loopProfile = ArcRelayLoopProfile.Resolve(
            options.GetValueOrDefault("loop-profile", "h0"));
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
            teamOne.Classes,
            loopProfile: loopProfile);
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
            first.ToParticipant(
                participantId: 0,
                teamId: 0,
                mindEvaluationData: teamZero.Data,
                displayName: SheetDisplayName(first.Name, teamZero)),
            second.ToParticipant(
                participantId: 1,
                teamId: 1,
                mindEvaluationData: teamOne.Data,
                displayName: SheetDisplayName(second.Name, teamOne)),
        ];

        bool perfDiagnostics = string.Equals(
            Environment.GetEnvironmentVariable("BOTARENA_PERF_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);
        PerfSnapshot perfStart = PerfSnapshot.Capture();
        GenericActorMatchResult result;
        GenericActorReplayDocument? replay;
        (result, replay) =
            MatchRun.Guard(
                MatchRun.Cell(first.Name, second.Name, seed),
                () =>
                {
                    using var session = new GenericActorMatchSession(
                        definition,
                        participants,
                        seed);
                    result = session.Run();
                    PerfSnapshot afterMatch = PerfSnapshot.Capture();
                    ReportPerf(
                        perfDiagnostics,
                        "match",
                        perfStart,
                        afterMatch);
                    replay = screen
                        ? null
                        : GenericActorReplayDocument.Create(
                            session,
                            ArcRelayH0ReplayPresentation.Create(definition));
                    if (!screen)
                    {
                        ReportPerf(
                            perfDiagnostics,
                            "replay",
                            afterMatch,
                            PerfSnapshot.Capture());
                    }
                    return (result, replay);
                });

        if (screen)
        {
            Directory.CreateDirectory(output);
            var screenReceipt = new ArcRelayScreenReceipt(
                SchemaVersion: 1,
                ScreenOnly: true,
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
                Runtime: runtimeKind,
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
                Scores: ArcRelayScores(result),
                CanonicalReplayProduced: false);
            string screenPath = Path.Combine(output, "screen.json");
            File.WriteAllText(
                screenPath,
                JsonSerializer.Serialize(
                    screenReceipt,
                    new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(
                $"Arc Relay screen {loopProfile.Id}: "
                + $"{SideLabel(first.Name, 0, teamZero)} vs "
                + $"{SideLabel(second.Name, 1, teamOne)}, seed {seed}");
            Console.WriteLine(
                $"Result: {Verdict(result, SideLabel(first.Name, 0, teamZero), SideLabel(second.Name, 1, teamOne))} — "
                + $"{Reason(result)} at tick {result.EndTick ?? -1}");
            Console.WriteLine($"Screen: {Path.GetFullPath(screenPath)}");
            return result.EligibleTeamIds.Length == 2 ? 0 : 2;
        }

        GenericActorReplayDocument completedReplay = replay
            ?? throw new InvalidOperationException(
                "Completed evaluation did not produce its canonical replay.");
        PerfSnapshot beforeWrite = PerfSnapshot.Capture();
        WrittenReplay written = ReplayOutput.WriteGzipJson(
            completedReplay.CanonicalUtf8,
            output);
        ReportPerf(
            perfDiagnostics,
            "gzip+verify",
            beforeWrite,
            PerfSnapshot.Capture());
        ArcRelayRunReceipt receipt = Receipt(
            definition,
            seed,
            first,
            second,
            teamZero,
            teamOne,
            result,
            completedReplay,
            written);
        string receiptPath = Path.Combine(output, "run.json");
        File.WriteAllText(
            receiptPath,
            JsonSerializer.Serialize(
                receipt,
                new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine(
            $"Arc Relay {loopProfile.Id}: {SideLabel(first.Name, 0, teamZero)} "
            + $"vs {SideLabel(second.Name, 1, teamOne)}, seed {seed}");
        Console.WriteLine(
            $"Result: {Verdict(result, SideLabel(first.Name, 0, teamZero), SideLabel(second.Name, 1, teamOne))} — "
            + $"{Reason(result)} at tick {result.EndTick ?? -1}");
        Console.WriteLine($"Hash:   {completedReplay.ReplayHash}");
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

    private static ArcRelayScreenScoreReceipt[] ArcRelayScores(
        GenericActorMatchResult result)
    {
        if (result.Mode is not GenericActorMatchModeResult.ArcRelay arcRelay)
            return [];

        Dictionary<int, ArcRelayReactorState> reactors = arcRelay.State.Reactors
            .ToDictionary(value => value.TeamId);
        return reactors.Values
            .OrderBy(value => value.TeamId)
            .Select(value =>
            {
                ArcRelayReactorState opponent = reactors.Values.Single(
                    candidate => candidate.TeamId != value.TeamId);
                return new ArcRelayScreenScoreReceipt(
                    value.TeamId,
                    (3 - opponent.IntegritySegments) * 3 + value.ChargePips,
                    value.ChargePips,
                    value.IntegritySegments);
            })
            .ToArray();
    }

    private static void ReportPerf(
        bool enabled,
        string phase,
        PerfSnapshot start,
        PerfSnapshot end)
    {
        if (!enabled)
            return;
        Console.Error.WriteLine(
            $"PERF {phase}: wall={(end.Wall - start.Wall).TotalMilliseconds:F1}ms "
            + $"cpu={(end.Cpu - start.Cpu).TotalMilliseconds:F1}ms "
            + $"allocated={(end.AllocatedBytes - start.AllocatedBytes) / 1_048_576.0:F1}MiB");
    }

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
            sheet.Classes)
        {
            LayoutHash = sheet.LayoutHash,
            LayoutPath = sheet.LayoutPath,
        };

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
            if (root.TryGetProperty("schema", out JsonElement schema)
                && string.Equals(
                    schema.GetString(),
                    ArcRelayTacticalPlaybookCompiler.PlaybookSchema,
                    StringComparison.Ordinal))
            {
                TacticalPlaybookCompilation compilation =
                    ArcRelayTacticalPlaybookCompiler.Compile(fullPath);
                return new SheetSelection(
                    compilation.Composition,
                    compilation.PlaybookSha256,
                    compilation.PlaybookPath,
                    compilation.LinkedData,
                    compilation.LayoutSha256,
                    compilation.LayoutPath);
            }
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
            string sheetHash = Convert.ToHexStringLower(
                SHA256.HashData(bytes));
            return new SheetSelection(
                classes,
                sheetHash,
                fullPath,
                HasExecutableEvaluationData(root)
                    ? EncodeEvaluationSheet(root, sheetHash)
                    : null,
                LayoutHash: null,
                LayoutPath: null);
        }

        string[] selected = hasClasses
            ? ParseClasses(raw!, classesOption)
            : fallback;
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(selected);
        return new SheetSelection(
            selected,
            Convert.ToHexStringLower(SHA256.HashData(identity)),
            Path: null,
            Data: null,
            LayoutHash: null,
            LayoutPath: null);
    }

    private static bool HasExecutableEvaluationData(JsonElement root) =>
        (root.TryGetProperty("schema", out JsonElement schema)
        && string.Equals(
            schema.GetString(),
            "arc-relay-evaluation-sheet-v3",
            StringComparison.Ordinal)
        && root.TryGetProperty("standingStrategy", out _))
        || (root.TryGetProperty("slots", out _)
        && root.TryGetProperty("zones", out _)
        && root.TryGetProperty("rallyLines", out _)
        && root.TryGetProperty("policies", out _)
        && root.TryGetProperty("gambits", out _));

    private static byte[] EncodeEvaluationSheet(
        JsonElement root,
        string sourceSha256)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(
            stream,
            System.Text.Encoding.UTF8,
            leaveOpen: true);
        writer.Write(0x31535241); // ARS1, little-endian.
        writer.Write(sourceSha256);
        string schema = RequiredSheetString(root, "schema");
        if (schema == "arc-relay-evaluation-sheet-v3")
        {
            writer.Write(schema);
            byte[] json = System.Text.Encoding.UTF8.GetBytes(root.GetRawText());
            writer.Write(json.Length);
            writer.Write(json);
            writer.Flush();
            if (stream.Length > 64 * 1024)
            {
                throw new InvalidDataException(
                    "Evaluation sheet data exceeds 64 KiB.");
            }
            return stream.ToArray();
        }
        if (schema is not "arc-relay-evaluation-sheet-v0"
            and not "arc-relay-evaluation-sheet-v1"
            and not "arc-relay-evaluation-sheet-v2")
        {
            throw new InvalidDataException(
                $"Unsupported evaluation sheet schema '{schema}'.");
        }
        writer.Write(schema);
        writer.Write(RequiredSheetString(root, "sheetId"));
        writer.Write(RequiredSheetString(root, "mapId"));

        JsonElement[] composition = root.GetProperty("composition")
            .EnumerateArray().ToArray();
        writer.Write(composition.Length);
        foreach (JsonElement value in composition)
            writer.Write(value.GetString() ?? throw SheetError("composition"));

        JsonElement[] slots = root.GetProperty("slots").EnumerateArray()
            .OrderBy(value => value.GetProperty("unitId").GetInt32())
            .ToArray();
        writer.Write(slots.Length);
        foreach (JsonElement slot in slots)
        {
            writer.Write(slot.GetProperty("unitId").GetInt32());
            writer.Write(RequiredSheetString(slot, "theater"));
            writer.Write(RequiredSheetString(slot, "role"));
            writer.Write(slot.GetProperty("partnerUnitId").GetInt32());
            WritePositions(writer, slot.GetProperty("outboundPath"));
            WritePositions(writer, slot.GetProperty("returnPath"));
            // Veterancy build order (owner direction 2026-08-06): the sheet
            // owns skill-point selection. Optional and empty for older
            // sheets, whose bodies keep the mind's class defaults; entries
            // beyond the list repeat its last track.
            JsonElement[] build = slot.TryGetProperty(
                    "build",
                    out JsonElement buildValue)
                ? buildValue.EnumerateArray().ToArray()
                : [];
            writer.Write(build.Length);
            foreach (JsonElement track in build)
                writer.Write(track.GetString() ?? throw SheetError("build"));
        }

        JsonProperty[] zones = root.GetProperty("zones").EnumerateObject()
            .OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
        writer.Write(zones.Length);
        foreach (JsonProperty zone in zones)
        {
            writer.Write(zone.Name);
            int[] values = zone.Value.EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (values.Length != 4)
                throw SheetError($"zones.{zone.Name}");
            foreach (int value in values)
                writer.Write(value);
        }

        JsonProperty[] rally = root.GetProperty("rallyLines").EnumerateObject()
            .OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
        writer.Write(rally.Length);
        foreach (JsonProperty line in rally)
        {
            writer.Write(line.Name);
            WritePositions(writer, line.Value);
        }

        JsonElement policies = root.GetProperty("policies");
        JsonElement carrier = policies.GetProperty("carrier");
        writer.Write(carrier.GetProperty("handoffHealthAtOrBelow").GetInt32());
        writer.Write(carrier.GetProperty("preferAssignedTheater").GetBoolean());
        writer.Write(carrier.GetProperty("routeFailureTicks").GetInt32());
        JsonElement escort = policies.GetProperty("escort");
        writer.Write(escort.GetProperty("followDistance").GetInt32());
        writer.Write(escort.GetProperty("focusEnemyCarrier").GetBoolean());
        JsonElement interception = policies.GetProperty("interception");
        writer.Write(interception.GetProperty("focusEnemyCarrier").GetBoolean());
        writer.Write(interception.GetProperty("looseCoreFallback").GetBoolean());

        if (schema is "arc-relay-evaluation-sheet-v1"
            or "arc-relay-evaluation-sheet-v2")
        {
            WriteStrategySheetV1(writer, root, slots);
            if (schema == "arc-relay-evaluation-sheet-v2")
            {
                WriteIntelligentOperationsV2(writer, root);
                if (root.TryGetProperty(
                        "attackCoordination", out JsonElement coordination))
                {
                    WriteAttackCoordination(writer, coordination);
                }
            }
        }
        else
            WriteEvaluationGambitsV0(writer, root);
        writer.Flush();
        if (stream.Length > 64 * 1024)
            throw new InvalidDataException("Evaluation sheet data exceeds 64 KiB.");
        return stream.ToArray();
    }

    private static void WriteAttackCoordination(
        BinaryWriter writer,
        JsonElement coordination)
    {
        writer.Write(0x31434143); // CAC1, little-endian.
        writer.Write(RequiredSheetString(coordination, "mode"));
        WriteStrings(writer, coordination.GetProperty("targetPriorities"));
        WriteStrings(writer, coordination.GetProperty("tieBreakers"));
        writer.Write(coordination.GetProperty(
            "maximumAttackersPerTarget").GetInt32());
        writer.Write(coordination.GetProperty("overkillDamage").GetInt32());
        writer.Write(coordination.GetProperty("lockTicks").GetInt32());
    }

    private static void WriteEvaluationGambitsV0(
        BinaryWriter writer,
        JsonElement root)
    {
        JsonElement[] gambits = root.GetProperty("gambits").EnumerateArray()
            .OrderBy(value => value.GetProperty("priority").GetInt32())
            .ToArray();
        writer.Write(gambits.Length);
        foreach (JsonElement gambit in gambits)
        {
            writer.Write(gambit.GetProperty("priority").GetInt32());
            writer.Write(RequiredSheetString(gambit, "id"));
            writer.Write(RequiredSheetString(gambit, "trigger"));
            writer.Write(gambit.GetProperty("durationTicks").GetInt32());
            writer.Write(gambit.GetProperty("cooldownTicks").GetInt32());
            WriteStrings(writer, gambit.GetProperty("scopeRoles"));
            writer.Write(RequiredSheetString(gambit, "roleOverride"));
            writer.Write(RequiredSheetString(gambit, "rallyLine"));
        }
    }

    private static void WriteStrategySheetV1(
        BinaryWriter writer,
        JsonElement root,
        IReadOnlyList<JsonElement> slots)
    {
        JsonProperty[] paths = root.GetProperty("paths").EnumerateObject()
            .OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
        writer.Write(paths.Length);
        foreach (JsonProperty path in paths)
        {
            writer.Write(path.Name);
            WritePositions(writer, path.Value);
        }

        writer.Write(slots.Count);
        foreach (JsonElement slot in slots)
        {
            writer.Write(slot.GetProperty("unitId").GetInt32());
            if (slot.TryGetProperty("defaultIntent", out JsonElement intent))
            {
                WritePositionIntent(writer, intent.GetProperty("position"));
                writer.Write(OptionalString(
                    intent, "engagementIntent", "normal"));
                writer.Write(OptionalString(
                    intent, "signatureIntent", "normal"));
            }
            else
            {
                WriteBasePositionIntent(writer);
                writer.Write("normal");
                writer.Write("normal");
            }
        }

        JsonElement[] gambits = root.GetProperty("gambits").EnumerateArray()
            .OrderBy(value => value.GetProperty("priority").GetInt32())
            .ThenBy(value => RequiredSheetString(value, "id"),
                StringComparer.Ordinal)
            .ToArray();
        writer.Write(gambits.Length);
        foreach (JsonElement gambit in gambits)
        {
            writer.Write(gambit.GetProperty("priority").GetInt32());
            writer.Write(RequiredSheetString(gambit, "id"));
            writer.Write(OptionalString(
                gambit, "activation", "rising-edge"));
            writer.Write(gambit.GetProperty("minimumTicks").GetInt32());
            writer.Write(gambit.GetProperty("maximumTicks").GetInt32());
            writer.Write(gambit.GetProperty("cooldownTicks").GetInt32());

            JsonElement scope = gambit.GetProperty("scope");
            WriteIntegers(writer, scope, "unitIds");
            WriteStrings(writer, scope, "roles");
            WriteClauses(writer, gambit.GetProperty("enterAll"));
            if (gambit.TryGetProperty("exitAny", out JsonElement exitAny))
                WriteClauses(writer, exitAny);
            else
                writer.Write(0);

            JsonElement overlay = gambit.GetProperty("overlay");
            writer.Write(OptionalString(overlay, "roleOverride", ""));
            bool hasPosition = overlay.TryGetProperty(
                "position", out JsonElement position);
            writer.Write(hasPosition);
            if (hasPosition)
                WritePositionIntent(writer, position);
            if (overlay.TryGetProperty(
                    "formationOffsets", out JsonElement formation))
            {
                WritePositions(writer, formation);
            }
            else
            {
                writer.Write(0);
            }

            JsonElement policies = overlay.TryGetProperty(
                    "policies", out JsonElement policyValue)
                ? policyValue
                : default;
            JsonElement carrier = ChildOrDefault(policies, "carrier");
            writer.Write(OptionalInt(carrier, "handoffHealthAtOrBelow"));
            writer.Write(OptionalBool(carrier, "preferAssignedTheater"));
            writer.Write(OptionalInt(carrier, "routeFailureTicks"));
            JsonElement escort = ChildOrDefault(policies, "escort");
            writer.Write(OptionalInt(escort, "followDistance"));
            writer.Write(OptionalBool(escort, "focusEnemyCarrier"));
            JsonElement interception = ChildOrDefault(
                policies, "interception");
            writer.Write(OptionalBool(interception, "focusEnemyCarrier"));
            writer.Write(OptionalBool(interception, "looseCoreFallback"));
            writer.Write(OptionalString(
                overlay, "engagementIntent", "normal"));
            writer.Write(OptionalString(
                overlay, "signatureIntent", "normal"));
            writer.Write(overlay.TryGetProperty(
                    "appliesWhileCarrying", out JsonElement carrying)
                && carrying.GetBoolean());
        }
    }

    private static void WriteIntelligentOperationsV2(
        BinaryWriter writer,
        JsonElement root)
    {
        JsonElement[] operations = root.GetProperty("operations")
            .EnumerateArray()
            .OrderBy(value => value.GetProperty("priority").GetInt32())
            .ThenBy(value => RequiredSheetString(value, "id"),
                StringComparer.Ordinal)
            .ToArray();
        writer.Write(operations.Length);
        foreach (JsonElement operation in operations)
        {
            writer.Write(operation.GetProperty("priority").GetInt32());
            writer.Write(RequiredSheetString(operation, "id"));
            writer.Write(operation.GetProperty(
                "prepareDeadlineTicks").GetInt32());
            writer.Write(operation.GetProperty("cooldownTicks").GetInt32());
            JsonElement prepare = operation.GetProperty("prepare");
            WriteOperationConditionGroup(writer, prepare.GetProperty("when"));
            WriteOperationConditions(writer, prepare, "abortAny");
            WriteOperationTasks(writer, prepare.GetProperty("tasks"));

            JsonElement[] branches = operation.GetProperty("branches")
                .EnumerateArray().ToArray();
            writer.Write(branches.Length);
            foreach (JsonElement branch in branches)
            {
                writer.Write(RequiredSheetString(branch, "id"));
                WriteOperationConditionGroup(
                    writer, branch.GetProperty("commitWhen"));
                WriteOperationTasks(writer, branch.GetProperty("tasks"));
                WriteOperationConditions(writer, branch, "successAny");
                WriteOperationConditions(writer, branch, "abortAny");
                writer.Write(branch.GetProperty("deadlineTicks").GetInt32());
            }

            JsonElement recovery = operation.GetProperty("recovery");
            writer.Write(recovery.GetProperty("deadlineTicks").GetInt32());
            WriteOperationConditions(writer, recovery, "completeAll");
            WriteOperationTasks(writer, recovery.GetProperty("onSuccess"));
            WriteOperationTasks(writer, recovery.GetProperty("onAbort"));
        }
    }

    private static void WriteOperationConditionGroup(
        BinaryWriter writer,
        JsonElement group)
    {
        WriteOperationConditions(writer, group, "all");
        WriteOperationConditions(writer, group, "any");
    }

    private static void WriteOperationConditions(
        BinaryWriter writer,
        JsonElement parent,
        string property)
    {
        JsonElement[] conditions = parent.TryGetProperty(
                property, out JsonElement array)
            ? array.EnumerateArray().ToArray()
            : [];
        writer.Write(conditions.Length);
        foreach (JsonElement condition in conditions)
        {
            writer.Write(RequiredSheetString(condition, "fact"));
            writer.Write(OptionalString(condition, "operator", "at-least"));
            writer.Write(condition.TryGetProperty(
                    "value", out JsonElement value)
                ? value.GetInt32()
                : 1);
            writer.Write(OptionalString(condition, "zone", ""));
            writer.Write(OptionalString(condition, "subject", ""));
            writer.Write(condition.TryGetProperty(
                    "freshnessTicks", out JsonElement freshness)
                ? freshness.GetInt32()
                : 0);
            if (condition.TryGetProperty(
                    "classIds", out JsonElement classIds))
                WriteStrings(writer, classIds);
            else
                writer.Write(0);
        }
    }

    private static void WriteOperationTasks(
        BinaryWriter writer,
        JsonElement tasks)
    {
        JsonElement[] values = tasks.EnumerateArray().ToArray();
        writer.Write(values.Length);
        foreach (JsonElement task in values)
        {
            writer.Write(RequiredSheetString(task, "id"));
            writer.Write(RequiredSheetString(task, "resilience"));
            writer.Write(task.GetProperty("minimum").GetInt32());
            WriteIntegers(writer, task, "candidateUnitIds");
            WriteStrings(writer, task, "candidateRoles");
            WriteStrings(writer, task, "candidateClassIds");
            writer.Write(task.TryGetProperty(
                    "permitsCarrying", out JsonElement permits)
                && permits.GetBoolean());
            writer.Write(task.TryGetProperty(
                    "requiresCarrying", out JsonElement requires)
                && requires.GetBoolean());
            WritePositionIntent(writer, task.GetProperty("position"));
            writer.Write(OptionalString(task, "roleOverride", ""));
            writer.Write(OptionalString(
                task, "engagementIntent", "opportunistic"));
            writer.Write(OptionalString(
                task, "signatureIntent", "normal"));
        }
    }

    private static void WritePositionIntent(
        BinaryWriter writer,
        JsonElement intent)
    {
        writer.Write(RequiredSheetString(intent, "kind"));
        writer.Write(OptionalString(intent, "target", ""));
        int[] offset = intent.TryGetProperty("offset", out JsonElement value)
            ? value.EnumerateArray().Select(item => item.GetInt32()).ToArray()
            : [0, 0];
        if (offset.Length != 2)
            throw SheetError("position.offset");
        writer.Write(offset[0]);
        writer.Write(offset[1]);
        writer.Write(OptionalString(intent, "arrival", "hold"));
        writer.Write(OptionalString(intent, "fallbackZone", ""));
    }

    private static void WriteBasePositionIntent(BinaryWriter writer)
    {
        writer.Write("base-assignment");
        writer.Write("");
        writer.Write(0);
        writer.Write(0);
        writer.Write("base-assignment");
        writer.Write("");
    }

    private static void WriteClauses(BinaryWriter writer, JsonElement values)
    {
        JsonElement[] clauses = values.EnumerateArray().ToArray();
        writer.Write(clauses.Length);
        foreach (JsonElement clause in clauses)
        {
            writer.Write(RequiredSheetString(clause, "fact"));
            writer.Write(OptionalString(clause, "operator", "at-least"));
            writer.Write(clause.TryGetProperty("value", out JsonElement value)
                ? value.GetInt32()
                : 1);
            writer.Write(OptionalString(clause, "zone", ""));
        }
    }

    private static void WriteIntegers(
        BinaryWriter writer,
        JsonElement parent,
        string property)
    {
        int[] values = parent.TryGetProperty(property, out JsonElement array)
            ? array.EnumerateArray().Select(value => value.GetInt32()).ToArray()
            : [];
        writer.Write(values.Length);
        foreach (int value in values)
            writer.Write(value);
    }

    private static void WriteStrings(BinaryWriter writer, JsonElement values)
    {
        string[] strings = values.EnumerateArray()
            .Select(value => value.GetString()
                ?? throw SheetError("string-array"))
            .ToArray();
        writer.Write(strings.Length);
        foreach (string value in strings)
            writer.Write(value);
    }

    private static void WriteStrings(
        BinaryWriter writer,
        JsonElement parent,
        string property)
    {
        if (parent.TryGetProperty(property, out JsonElement values))
            WriteStrings(writer, values);
        else
            writer.Write(0);
    }

    private static JsonElement ChildOrDefault(
        JsonElement parent,
        string property) => parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(property, out JsonElement value)
            ? value
            : default;

    private static string OptionalString(
        JsonElement parent,
        string property,
        string fallback) => parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(property, out JsonElement value)
            ? value.GetString() ?? throw SheetError(property)
            : fallback;

    private static int OptionalInt(JsonElement parent, string property) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(property, out JsonElement value)
            ? value.GetInt32()
            : -1;

    private static int OptionalBool(JsonElement parent, string property) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(property, out JsonElement value)
            ? value.GetBoolean() ? 1 : 0
            : -1;

    private static void WritePositions(
        BinaryWriter writer,
        JsonElement values)
    {
        JsonElement[] positions = values.EnumerateArray().ToArray();
        writer.Write(positions.Length);
        foreach (JsonElement position in positions)
        {
            int[] coordinates = position.EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (coordinates.Length != 2)
                throw SheetError("position");
            writer.Write(coordinates[0]);
            writer.Write(coordinates[1]);
        }
    }

    private static string RequiredSheetString(JsonElement value, string name) =>
        value.GetProperty(name).GetString() ?? throw SheetError(name);

    private static InvalidDataException SheetError(string field) =>
        new($"Evaluation sheet field '{field}' is invalid.");

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

    /// <summary>
    /// Winner text names the team id and sheet, not just the mind: in a
    /// mirror match both sides print the same mind name, which has caused
    /// a misattributed result during evaluation.
    /// </summary>
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

    // The viewer's team labels come from the replay's provenance participant
    // names. Sheet-vs-sheet cells run one mind on both sides, so the mind name
    // labels both teams identically; the sheet is the identity a spectator
    // needs.
    private static string SheetDisplayName(
        string name,
        SheetSelection sheet) =>
        sheet.Path is null
            ? name
            : Path.GetFileNameWithoutExtension(sheet.Path);

    private static string SideLabel(
        string name,
        int teamId,
        SheetSelection sheet) =>
        sheet.Path is null
            ? $"{name} [team {teamId}]"
            : $"{name} [team {teamId}, "
              + $"{Path.GetFileNameWithoutExtension(sheet.Path)}]";

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
        string? Path,
        byte[]? Data,
        string? LayoutHash,
        string? LayoutPath);

    private readonly record struct PerfSnapshot(
        TimeSpan Wall,
        TimeSpan Cpu,
        long AllocatedBytes)
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        public static PerfSnapshot Capture()
        {
            using Process process = Process.GetCurrentProcess();
            return new PerfSnapshot(
                Clock.Elapsed,
                process.TotalProcessorTime,
                GC.GetTotalAllocatedBytes(precise: false));
        }
    }
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

public sealed record ArcRelayScreenReceipt(
    int SchemaVersion,
    bool ScreenOnly,
    string RulesetId,
    string RulesFingerprint,
    string MapId,
    string MapFingerprint,
    string TopologyFingerprint,
    string MatchContractFingerprint,
    string Seed,
    string Runtime,
    ArcRelayParticipantReceipt[] Participants,
    ArcRelayRunResultReceipt Result,
    ArcRelayScreenScoreReceipt[] Scores,
    bool CanonicalReplayProduced);

public sealed record ArcRelayScreenScoreReceipt(
    int TeamId,
    int Score,
    int ChargePips,
    int IntegritySegments);

public sealed record ArcRelayParticipantReceipt(
    int ParticipantId,
    int TeamId,
    string Name,
    string ArtifactHash,
    string RuntimeKind,
    string SheetHash,
    string? SheetPath,
    string[] Classes)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LayoutHash { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LayoutPath { get; init; }
}

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
