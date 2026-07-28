using System.Diagnostics;
using System.Globalization;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Exact local authoring loop for the immutable hosted Frontline Labs v1
/// definition. It bypasses App authentication and quotas, but uses the same
/// resolved contract, generic session, replay-v3 projection, and WASM runtime.
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
            "open");
        if (options.ContainsKey("seed") && options.ContainsKey("seeds"))
        {
            throw new InvalidOperationException(
                "Use either --seed or --seeds, not both.");
        }

        string botSpec = RequiredOption(options, "bot");
        string opponentSpec = RequiredOption(options, "opponent");
        if (options.ContainsKey("swap"))
            (botSpec, opponentSpec) = (opponentSpec, botSpec);

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

        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        Console.WriteLine(
            "LOCAL LABS: exact hosted Frontline Labs v1 contract; " +
            "unranked and quota-free.");
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
