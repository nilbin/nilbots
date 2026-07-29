using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Toolchain;

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
            "ignore-declared-classes",
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

        // Class is bot identity (DECISIONS #154): a classed project always
        // plays its declared chassis. Declared classes select the arm when
        // --classes is absent, must agree with it when present, and always
        // bind each bot to its class's canonical team side.
        // --ignore-declared-classes runs classed projects on the explicit or
        // base contract instead — the path qualification exercises.
        bool ignoreDeclaredClasses = OptionalFlag(
            options,
            "ignore-declared-classes");
        string? botSpec = null;
        string? opponentSpec = null;
        if (printCandidateContract
            && !ignoreDeclaredClasses
            && classPair is null
            && options.ContainsKey("bot")
            && options.ContainsKey("opponent"))
        {
            // Print mode takes no bots normally, but when specs are given
            // their declared classes resolve the printed identity exactly as
            // a run would — the one command whose job is "show the resolved
            // contract" must not silently show a different one.
            FrontlineLabsClassDefinition? printDeclared0 =
                DeclaredClass(options["bot"]);
            FrontlineLabsClassDefinition? printDeclared1 =
                DeclaredClass(options["opponent"]);
            if (printDeclared0 is not null && printDeclared1 is not null)
            {
                classPair = string.CompareOrdinal(
                        printDeclared0.Id, printDeclared1.Id) <= 0
                    ? (printDeclared0, printDeclared1)
                    : (printDeclared1, printDeclared0);
            }
        }
        if (!printCandidateContract)
        {
            botSpec = RequiredOption(options, "bot");
            opponentSpec = RequiredOption(options, "opponent");
            if (options.ContainsKey("swap"))
                (botSpec, opponentSpec) = (opponentSpec, botSpec);
            FrontlineLabsClassDefinition? declared0 =
                ignoreDeclaredClasses ? null : DeclaredClass(botSpec);
            FrontlineLabsClassDefinition? declared1 =
                ignoreDeclaredClasses ? null : DeclaredClass(opponentSpec);
            if (classPair is null
                && declared0 is not null
                && declared1 is not null)
            {
                if (string.CompareOrdinal(declared0.Id, declared1.Id) > 0)
                {
                    (botSpec, opponentSpec) = (opponentSpec, botSpec);
                    (declared0, declared1) = (declared1, declared0);
                }
                classPair = (declared0, declared1);
                Console.WriteLine(
                    "Classes resolved from bot manifests: "
                    + $"{declared0.Id}-vs-{declared1.Id}.");
            }
            else if (classPair is { } requested)
            {
                if (declared0 is not null
                    && declared0.Id != requested.TeamZero.Id)
                {
                    throw new InvalidOperationException(
                        $"--bot declares class '{declared0.Id}' but the "
                        + $"requested pair puts '{requested.TeamZero.Id}' "
                        + "on team 0. A classed bot always plays its "
                        + "declared chassis.");
                }
                if (declared1 is not null
                    && declared1.Id != requested.TeamOne.Id)
                {
                    throw new InvalidOperationException(
                        $"--opponent declares class '{declared1.Id}' but "
                        + $"the requested pair puts '{requested.TeamOne.Id}' "
                        + "on team 1. A classed bot always plays its "
                        + "declared chassis.");
                }
            }
            else if ((declared0 is null) != (declared1 is null))
            {
                throw new InvalidOperationException(
                    "One entrant declares a class and the other does not. "
                    + "Declare both (or pass --classes explicitly with "
                    + "class-agnostic bots).");
            }
        }
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

        string resolvedBotSpec = botSpec!;
        string resolvedOpponentSpec = opponentSpec!;
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
                    resolvedBotSpec,
                    runtimeKind,
                    quiet: seeds.Length > 1);
            using ResolvedGenericActorBot bot1 =
                ResolvedGenericActorBot.Resolve(
                    resolvedOpponentSpec,
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

    /// <summary>Reads a project spec's declared class from botarena.json.
    /// Raw WASM artifacts declare no class; the Lab's entrant metadata covers
    /// them.</summary>
    private static FrontlineLabsClassDefinition? DeclaredClass(string spec)
    {
        if (!Directory.Exists(spec) || !BotProject.LooksLikeProject(spec))
            return null;
        string? declared = BotProject.Load(spec).Manifest.Class;
        if (declared is null)
            return null;
        try
        {
            return FrontlineLabsClassDefinition.Parse(declared);
        }
        catch (ArgumentException error)
        {
            throw new InvalidOperationException(
                $"{spec}: {error.Message}");
        }
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
