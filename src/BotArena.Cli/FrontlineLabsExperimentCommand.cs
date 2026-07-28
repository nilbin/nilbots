using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Local authoring loop for the immutable hosted Frontline Labs v1 definition
/// and explicitly content-identified, local-only numeric experiment arms. It
/// bypasses App authentication and quotas, but uses the same resolved
/// contract, generic session, replay-v3 projection, and WASM runtime.
/// </summary>
public static class FrontlineLabsExperimentCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(
            options,
            "bot",
            "opponent",
            "seed",
            "seeds",
            "swap",
            "runtime",
            "out",
            "open",
            "capture-threshold",
            "capture-gain-phase",
            "mobilize-turrets",
            "remote-fabrication",
            "net-control",
            "one-bend-shots",
            "auto-companions",
            "duel-map",
            "classes",
            "print-candidate-contract");
        if (options.ContainsKey("seed") && options.ContainsKey("seeds"))
        {
            throw new InvalidOperationException(
                "Use either --seed or --seeds, not both.");
        }

        string runtimeKind = options
            .GetValueOrDefault("runtime", "wasm")
            .ToLowerInvariant();
        if (runtimeKind is not ("wasm" or "in-process"))
        {
            throw new InvalidOperationException(
                $"Unknown runtime '{runtimeKind}' " +
                "(use wasm or in-process).");
        }

        ulong[] seeds = ParseSeeds(options);
        if (options.ContainsKey("open") && seeds.Length != 1)
        {
            throw new InvalidOperationException(
                "--open requires a single --seed.");
        }

        int? captureThreshold = OptionalPositiveInt(
            options,
            "capture-threshold");
        (int StartsAtTick, int Gain)? captureGainPhase =
            OptionalCaptureGainPhase(options);
        bool mobilizeTurrets = OptionalFlag(
            options,
            "mobilize-turrets");
        bool remoteFabrication = OptionalFlag(
            options,
            "remote-fabrication");
        bool netControl = OptionalFlag(
            options,
            "net-control");
        bool oneBendShots = OptionalFlag(
            options,
            "one-bend-shots");
        bool automaticCompanions = OptionalFlag(
            options,
            "auto-companions");
        bool printCandidateContract = OptionalFlag(
            options,
            "print-candidate-contract");
        FrontlineLabsDuelMapArm? duelMapArm =
            OptionalDuelMapArm(options);
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classPair =
            OptionalClassPair(options);
        bool duelExperiment = oneBendShots
            || automaticCompanions
            || (duelMapArm is not null && classPair is null);
        int experimentCount =
            (captureThreshold is null ? 0 : 1)
            + (captureGainPhase is null ? 0 : 1)
            + (mobilizeTurrets ? 1 : 0)
            + (remoteFabrication ? 1 : 0)
            + (netControl ? 1 : 0)
            + (duelExperiment ? 1 : 0)
            + (classPair is null ? 0 : 1);
        if (experimentCount > 1)
        {
            throw new InvalidOperationException(
                "Use one Frontline Labs experiment option at a time.");
        }
        ActorResolvedMatchDefinition definition;
        if (captureThreshold is int threshold)
        {
            definition = FrontlineLabsDefinition
                .CreateCaptureThresholdExperiment(threshold);
        }
        else if (captureGainPhase is { } phase)
        {
            definition = FrontlineLabsDefinition
                .CreateCaptureGainPhaseExperiment(
                    phase.StartsAtTick,
                    phase.Gain);
        }
        else if (mobilizeTurrets)
        {
            definition = FrontlineLabsDefinition.CreateMobilizeExperiment();
        }
        else if (remoteFabrication)
        {
            definition =
                FrontlineLabsDefinition.CreateRemoteFabricationExperiment();
        }
        else if (netControl)
        {
            definition = FrontlineLabsDefinition.CreateNetControlExperiment();
        }
        else if (classPair is { } selectedClasses)
        {
            definition = FrontlineLabsDefinition.CreateClassesExperiment(
                selectedClasses.TeamZero,
                selectedClasses.TeamOne,
                duelMapArm ?? FrontlineLabsDuelMapArm.Current);
        }
        else if (automaticCompanions)
        {
            definition = FrontlineLabsDefinition
                .CreateAutomaticCompanionsExperiment(
                    duelMapArm ?? FrontlineLabsDuelMapArm.Current);
        }
        else if (duelMapArm is { } mapArm)
        {
            definition =
                FrontlineLabsDefinition.CreateOneBendShotsExperiment(mapArm);
        }
        else if (oneBendShots)
        {
            definition =
                FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        }
        else
        {
            definition = FrontlineLabsDefinition.Create();
        }
        if (printCandidateContract)
        {
            PrintCandidateContract(definition);
            return 0;
        }

        string botSpec = RequiredOption(options, "bot");
        string opponentSpec = RequiredOption(options, "opponent");
        if (options.ContainsKey("swap"))
            (botSpec, opponentSpec) = (opponentSpec, botSpec);
        Console.WriteLine(
            experimentCount == 0
                ? "LOCAL LABS: exact hosted Frontline Labs v1 contract; " +
                  "unranked and quota-free."
                : "LOCAL LABS: content-identified experiment; " +
                  "unranked, quota-free, and not the hosted v1 ruleset.");
        Console.WriteLine($"Runtime:           {runtimeKind}");
        Console.WriteLine($"Rules:             {definition.Rules.RulesetId}");
        Console.WriteLine(
            $"Contract profile:  " +
            $"{definition.CapabilityVersions.ContractProfileId}");
        Console.WriteLine(
            $"Map:               {definition.Map.Id} " +
            $"v{definition.Map.Version} " +
            $"({definition.Map.Width}x{definition.Map.Height})");
        Console.WriteLine(
            $"Rules fingerprint: {ActorContractFingerprint.ComputeRules(
                definition.Rules)}");
        Console.WriteLine(
            $"Map fingerprint:   {ActorContractFingerprint.ComputeMap(
                definition.Map)}");
        Console.WriteLine(
            $"Match fingerprint: {ActorContractFingerprint.ComputeMatch(
                definition)}");
        Console.WriteLine();

        int wins = 0;
        int losses = 0;
        int draws = 0;
        for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            ulong seed = seeds[seedIndex];
            using ResolvedGenericActorBot bot0 =
                ResolvedGenericActorBot.Resolve(
                    botSpec,
                    runtimeKind,
                    quiet: seeds.Length > 1);
            using ResolvedGenericActorBot bot1 =
                ResolvedGenericActorBot.Resolve(
                    opponentSpec,
                    runtimeKind,
                    quiet: seeds.Length > 1);
            if (seedIndex == 0)
            {
                Console.WriteLine(
                    $"Participants:      {bot0.Name} [{bot0.RuntimeKind}] " +
                    $"vs {bot1.Name} [{bot1.RuntimeKind}]");
                Console.WriteLine();
            }

            var participants = new[]
            {
                bot0.ToParticipant(participantId: 0, teamId: 0),
                bot1.ToParticipant(participantId: 1, teamId: 1),
            };
            GenericActorMatchResult result;
            GenericActorReplayDocument replay;
            using (var session = new GenericActorMatchSession(
                       definition,
                       participants,
                       seed))
            {
                result = session.Run();
                replay = GenericActorReplayDocument.Create(session);
            }

            string outDir = OutputDirectory(
                options.GetValueOrDefault("out"),
                bot0.Name,
                bot1.Name,
                seed,
                seeds.Length > 1);
            WrittenReplay written = ReplayOutput.WriteJson(
                replay.CanonicalJson,
                outDir);
            _ = result.WinnerTeamId switch
            {
                0 => wins++,
                1 => losses++,
                _ => draws++,
            };

            if (seeds.Length == 1)
            {
                PrintResult(
                    result,
                    replay.ReplayHash,
                    bot0.Name,
                    bot1.Name,
                    seed);
                Console.WriteLine($"Replay:  {written.ReplayPath}");
                Console.WriteLine(
                    written.ViewerPath is null
                        ? "Viewer:  unavailable (build web/dist-cli first)"
                        : $"Viewer:  {written.ViewerPath}");
                if (options.ContainsKey("open")
                    && written.ViewerPath is not null)
                {
                    TryOpen(written.ViewerPath);
                }
            }
            else
            {
                string verdict = result.WinnerTeamId switch
                {
                    0 => $"{bot0.Name} wins",
                    1 => $"{bot1.Name} wins",
                    _ => "draw",
                };
                Console.WriteLine(
                    $"seed {seed,-12} {verdict,-28} " +
                    $"{Reason(result),-18} " +
                    $"t{result.EndTick ?? -1,-4} {written.ReplayPath}");
            }

            if (result.EligibleTeamIds.Length != 2)
            {
                Console.Error.WriteLine(
                    "Frontline Labs participant faulted or was " +
                    $"disqualified; preserved replay: {written.ReplayPath}");
                PrintWasmDiagnostics(bot0);
                PrintWasmDiagnostics(bot1);
                return 2;
            }
        }

        if (seeds.Length > 1)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Total ({seeds.Length} seeds, W = slot-0 bot wins): " +
                $"{wins}W {losses}L {draws}D");
        }
        return 0;
    }

    private static void PrintCandidateContract(
        ActorResolvedMatchDefinition definition)
    {
        var contract = new
        {
            modeId = definition.Rules.GameMode.ModeId,
            rulesetId = definition.Rules.RulesetId,
            rulesFingerprint =
                ActorContractFingerprint.ComputeRules(definition.Rules),
            seedProfileId =
                definition.Rules.SeedMechanics.SeedProfileId,
            mapId = definition.Map.Id,
            mapVersion = definition.Map.Version,
            mapFingerprint =
                ActorContractFingerprint.ComputeMap(definition.Map),
            formatId = definition.Format.FormatId,
            formatFingerprint =
                ActorContractFingerprint.ComputeFormat(definition.Format),
            topologyProfileId =
                FrontlineLabsDefinition.TopologyProfileId,
            topologyFingerprint =
                ActorContractFingerprint.ComputeTopology(
                    definition.Topology),
            contractProfileId =
                definition.CapabilityVersions.ContractProfileId,
            matchContractFingerprint =
                ActorContractFingerprint.ComputeMatch(definition),
        };
        Console.WriteLine(
            JsonSerializer.Serialize(
                contract,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void PrintWasmDiagnostics(ResolvedGenericActorBot bot)
    {
        foreach (
            BotArena.Runtime.Wasm.WasmGenericActorRuntimeFactory
                .RuntimeDiagnostic diagnostic
            in bot.WasmDiagnostics.Where(value =>
                value.FailureReason is not null))
        {
            string actor = diagnostic.ActorId?.ToString() ?? "startup";
            double peakFuelMillions =
                diagnostic.MaxFuelUsedPerTick / 1_000_000.0;
            double budgetMillions = diagnostic.FuelPerTick / 1_000_000.0;
            Console.Error.WriteLine(
                $"  {bot.Name} {actor}: {diagnostic.FailureReason} " +
                $"(peak completed tick fuel {peakFuelMillions:F1}M/" +
                $"{budgetMillions:F1}M)");
        }
    }

    private static string RequiredOption(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"nilbots experiment frontline-labs requires --{name} " +
                "<project|wasm>.");
        }
        return value;
    }

    private static int? OptionalPositiveInt(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? raw))
            return null;
        if (!int.TryParse(
                raw,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int value)
            || value <= 0)
        {
            throw new InvalidOperationException(
                $"--{name} must be a positive integer.");
        }
        return value;
    }

    private static (int StartsAtTick, int Gain)?
        OptionalCaptureGainPhase(
            IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("capture-gain-phase", out string? raw))
            return null;
        string[] parts = raw.Split(
            ':',
            StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int startsAtTick)
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int gain)
            || startsAtTick <= 0
            || gain <= 0)
        {
            throw new InvalidOperationException(
                "--capture-gain-phase must be <positive-start-tick>:<positive-gain>.");
        }
        return (startsAtTick, gain);
    }

    private static bool OptionalFlag(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value))
            return false;
        if (!string.Equals(
                value,
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"--{name} does not accept a value.");
        }
        return true;
    }

    private static FrontlineLabsDuelMapArm? OptionalDuelMapArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("duel-map", out string? value))
            return null;
        return value.ToLowerInvariant() switch
        {
            "current" => FrontlineLabsDuelMapArm.Current,
            "thin-fronts" => FrontlineLabsDuelMapArm.ThinFronts,
            "outer-shoulder-bypass" =>
                FrontlineLabsDuelMapArm.OuterShoulderBypass,
            _ => throw new InvalidOperationException(
                $"Unknown --duel-map '{value}' " +
                "(use current, thin-fronts, or outer-shoulder-bypass)."),
        };
    }

    private static (FrontlineLabsClassDefinition TeamZero,
        FrontlineLabsClassDefinition TeamOne)? OptionalClassPair(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("classes", out string? value))
            return null;

        string[] parts = value.Split("-vs-");
        if (parts.Length != 2
            || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "Use --classes <class>-vs-<class>, for example "
                + "--classes bulwark-vs-striker.");
        }

        FrontlineLabsClassDefinition teamZero;
        FrontlineLabsClassDefinition teamOne;
        try
        {
            teamZero = FrontlineLabsClassDefinition.Parse(parts[0]);
            teamOne = FrontlineLabsClassDefinition.Parse(parts[1]);
        }
        catch (ArgumentException error)
        {
            throw new InvalidOperationException(error.Message);
        }

        if (string.CompareOrdinal(teamZero.Id, teamOne.Id) > 0)
        {
            throw new InvalidOperationException(
                $"Class pairs are canonical: use --classes "
                + $"{teamOne.Id}-vs-{teamZero.Id} and swap bot assignments "
                + "with --swap instead of swapping teams.");
        }

        return (teamZero, teamOne);
    }

    private static ulong[] ParseSeeds(
        IReadOnlyDictionary<string, string> options)
    {
        string raw = options.GetValueOrDefault(
            options.ContainsKey("seeds") ? "seeds" : "seed",
            "42");
        string[] values = raw.Split(
            ',',
            StringSplitOptions.TrimEntries
            | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0)
            throw new InvalidOperationException("At least one seed is required.");

        var seeds = new ulong[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            if (!ulong.TryParse(
                    values[index],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out seeds[index]))
            {
                throw new InvalidOperationException(
                    $"Invalid unsigned 64-bit seed '{values[index]}'.");
            }
        }
        if (seeds.Distinct().Count() != seeds.Length)
        {
            throw new InvalidOperationException(
                "--seeds values must be distinct so replay paths do not collide.");
        }
        return seeds;
    }

    private static string OutputDirectory(
        string? overrideDirectory,
        string bot0,
        string bot1,
        ulong seed,
        bool batch)
    {
        if (overrideDirectory is not null)
        {
            return batch
                ? Path.Combine(overrideDirectory, $"s{seed}")
                : overrideDirectory;
        }
        return Path.Combine(
            "out",
            "frontline-labs",
            $"{Slug(bot0)}-vs-{Slug(bot1)}-s{seed}");
    }

    private static void PrintResult(
        GenericActorMatchResult result,
        string replayHash,
        string bot0,
        string bot1,
        ulong seed)
    {
        string verdict = result.WinnerTeamId switch
        {
            0 => $"{bot0} (team 0) wins",
            1 => $"{bot1} (team 1) wins",
            _ => "draw",
        };
        Console.WriteLine($"Seed:    {seed}");
        Console.WriteLine($"Match:   {bot0} vs {bot1}");
        Console.WriteLine(
            $"Result:  {verdict} — {Reason(result)} " +
            $"at tick {result.EndTick?.ToString(
                CultureInfo.InvariantCulture) ?? "pre-tick"}");
        foreach (TeamStanding standing in result.Standings.Standings
                     .OrderBy(standing => standing.TeamId))
        {
            string name = standing.TeamId == 0 ? bot0 : bot1;
            string scores = string.Join(
                ", ",
                standing.Scores.Select(score =>
                    $"{score.Channel}={score.Value}"));
            Console.WriteLine(
                $"  team {standing.TeamId} {name,-22} " +
                $"{standing.Outcome,-5} rank {standing.Rank} {scores}");
        }
        Console.WriteLine(
            $"Ticks:   {(result.EndTick ?? -1) + 1}");
        Console.WriteLine($"Hash:    {replayHash}");
        Console.WriteLine();
    }

    private static string Reason(GenericActorMatchResult result) =>
        result.Mode is GenericActorMatchModeResult.Frontline frontline
            ? frontline.Reason switch
            {
                GenericFrontlineEndReason.BaseBreach => "base-breach",
                GenericFrontlineEndReason.MaxTicks => "max-ticks",
                GenericFrontlineEndReason.FaultEligibility =>
                    "fault-eligibility",
                _ => frontline.Reason.ToString(),
            }
            : result.CompletionReason;

    private static string Slug(string value)
    {
        string slug = new(
            value.ToLowerInvariant()
                .Select(character =>
                    char.IsAsciiLetterOrDigit(character)
                        ? character
                        : '-')
                .ToArray());
        slug = slug.Trim('-');
        return slug.Length == 0 ? "bot" : slug;
    }

    private static void TryOpen(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(
                    new ProcessStartInfo(path)
                    {
                        UseShellExecute = true,
                    });
            }
            else
            {
                Process.Start(
                    OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                    path);
            }
        }
        catch (Exception exception)
            when (exception is InvalidOperationException
                  or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine(
                $"Could not open the viewer automatically: " +
                $"{exception.Message}");
        }
    }
}
