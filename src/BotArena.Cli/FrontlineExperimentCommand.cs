using System.Diagnostics;
using System.Globalization;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Local-only playable Frontline loop. It is intentionally separate from
/// <c>play</c>, ranked rules resolution, server admission, and shipped map
/// catalogs while the game and replay-v2 contract are still experimental.
/// </summary>
public static class FrontlineExperimentCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(
            options,
            "bot",
            "opponent",
            "map",
            "rules",
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

        string botSpec = options.GetValueOrDefault(
            "bot",
            "frontline-rusher");
        string opponentSpec = options.GetValueOrDefault(
            "opponent",
            "frontline-bastion");
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

        string rulesName = options.GetValueOrDefault(
            "rules",
            ExperimentalFrontlineRules.DefaultName);
        GameRules rules = ExperimentalFrontlineRules.Resolve(rulesName);
        ArenaMap map = LoadMap(
            options.GetValueOrDefault("map", "frontline-01"));
        ulong[] seeds = ParseSeeds(options);
        if (options.ContainsKey("open") && seeds.Length != 1)
        {
            throw new InvalidOperationException(
                "--open requires a single --seed.");
        }

        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(rules, map);

        Console.WriteLine(
            "LOCAL EXPERIMENT: Frontline is not ranked or server-admitted.");
        Console.WriteLine($"Runtime:          {runtimeKind}");
        Console.WriteLine($"Rules:            {rules.RulesVersion}");
        Console.WriteLine(
            $"Actor protocol:   {BotArenaVersions.ActorRuntimeProtocolVersion}");
        Console.WriteLine(
            $"Map:              {map.Id} v{map.Version} " +
            $"({map.Width}x{map.Height})");
        Console.WriteLine(
            $"Rules fingerprint: {contract.Rules.RulesFingerprint}");
        Console.WriteLine(
            $"Map fingerprint:   {contract.Map.MapFingerprint}");
        Console.WriteLine(
            $"Match fingerprint: {contract.MatchContractFingerprint}");
        Console.WriteLine();

        using ResolvedActorBot bot0 = ResolvedActorBot.Resolve(
            botSpec,
            runtimeKind,
            quiet: seeds.Length > 1);
        using ResolvedActorBot bot1 = ResolvedActorBot.Resolve(
            opponentSpec,
            runtimeKind,
            quiet: seeds.Length > 1);
        Console.WriteLine(
            $"Participants:     {bot0.Name} [{bot0.RuntimeKind}] vs " +
            $"{bot1.Name} [{bot1.RuntimeKind}]");
        Console.WriteLine();
        var participants = new[]
        {
            bot0.ToParticipant(participantId: 0, teamId: 0),
            bot1.ToParticipant(participantId: 1, teamId: 1),
        };

        int wins = 0;
        int losses = 0;
        int draws = 0;
        foreach (ulong seed in seeds)
        {
            string outDir = OutputDirectory(
                options.GetValueOrDefault("out"),
                bot0.Name,
                bot1.Name,
                map.Id,
                seed,
                seeds.Length > 1);
            FrontlineActorMatchAttempt attempt =
                new FrontlineActorMatchEngine().RunAttempt(
                    new FrontlineActorMatchConfiguration
                    {
                        Map = map,
                        Rules = rules,
                        Seed = seed,
                        Participants = participants,
                    });

            if (attempt is FrontlineActorMatchFailed failed)
            {
                WrittenReplay partial = ReplayOutput.WriteJson(
                    failed.Failure.PartialReplayJson,
                    outDir,
                    map.ThemeId);
                Console.Error.WriteLine(
                    $"Frontline artifact is not eligible or failed: " +
                    $"participant {failed.Failure.Fault.ParticipantId}, " +
                    $"actor {FormatActor(failed.Failure.Fault.ActorId)}, " +
                    $"stage {failed.Failure.Fault.Stage}, " +
                    $"code {failed.Failure.Fault.Code}, " +
                    $"tick {failed.Failure.Fault.Tick}.");
                Console.Error.WriteLine(
                    $"Partial replay: {partial.ReplayPath}");
                return 2;
            }

            FrontlineActorMatchRunResult run =
                ((FrontlineActorMatchCompleted)attempt).Run;
            WrittenReplay written = ReplayOutput.WriteJson(
                run.ReplayJson,
                outDir,
                map.ThemeId);
            int? winner = run.Result.WinnerTeamId;
            _ = winner switch
            {
                0 => wins++,
                1 => losses++,
                _ => draws++,
            };

            if (seeds.Length == 1)
            {
                PrintResult(run, bot0.Name, bot1.Name, seed);
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
                string verdict = winner switch
                {
                    0 => $"{bot0.Name} wins",
                    1 => $"{bot1.Name} wins",
                    _ => "draw",
                };
                Console.WriteLine(
                    $"seed {seed,-12} {verdict,-28} " +
                    $"{Reason(run.Result.Reason),-12} " +
                    $"t{run.Result.EndTick,-4} {written.ReplayPath}");
            }
        }

        if (seeds.Length > 1)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Total ({seeds.Length} seeds, W = {bot0.Name} wins): " +
                $"{wins}W {losses}L {draws}D");
        }
        return 0;
    }

    private static ArenaMap LoadMap(string idOrPath)
    {
        string fileName = idOrPath.EndsWith(
            ".json",
            StringComparison.OrdinalIgnoreCase)
            ? idOrPath
            : idOrPath + ".json";
        string? path = File.Exists(idOrPath)
            ? Path.GetFullPath(idOrPath)
            : CliSupport.FindUpward(
                Path.Combine("maps", "experimental", fileName));
        if (path is null)
        {
            throw new InvalidOperationException(
                $"Experimental Frontline map '{idOrPath}' not found " +
                $"(looked under maps/experimental/).");
        }

        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(path));
        if (map.FormatVersion != 2 || map.Frontline is null)
        {
            throw new InvalidOperationException(
                $"Map '{map.Id}' is not a format-v2 Frontline map.");
        }
        return map;
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
        string mapId,
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
            "experimental-frontline",
            $"{Slug(bot0)}-vs-{Slug(bot1)}-{Slug(mapId)}-s{seed}");
    }

    private static void PrintResult(
        FrontlineActorMatchRunResult run,
        string bot0,
        string bot1,
        ulong seed)
    {
        string verdict = run.Result.WinnerTeamId switch
        {
            0 => $"{bot0} (team 0) wins",
            1 => $"{bot1} (team 1) wins",
            _ => "draw",
        };
        Console.WriteLine($"Seed:    {seed}");
        Console.WriteLine($"Match:   {bot0} vs {bot1}");
        Console.WriteLine(
            $"Result:  {verdict} — {Reason(run.Result.Reason)} " +
            $"at tick {run.Result.EndTick}");
        Console.WriteLine(
            $"Score:   {run.Result.TerritorialScore:+#;-#;0} " +
            "(signed team-0 territory)");
        foreach (FrontlineTeamMatchResult team in run.Result.Teams
                     .OrderBy(team => team.TeamId))
        {
            string name = team.TeamId == 0 ? bot0 : bot1;
            Console.WriteLine(
                $"  team {team.TeamId} {name,-22} " +
                $"{team.Outcome,-5} health {team.ActiveHealth} " +
                $"damage {team.DamageDealt} units {team.Units.Count}");
        }
        Console.WriteLine($"Ticks:   {run.Result.EndTick + 1}");
        Console.WriteLine($"Hash:    {run.ReplayHash}");
        Console.WriteLine();
    }

    private static string Reason(FrontlineMatchEndReason reason) =>
        reason switch
        {
            FrontlineMatchEndReason.BaseBreach => "base-breach",
            FrontlineMatchEndReason.MaxTicks => "max-ticks",
            _ => reason.ToString(),
        };

    private static string FormatActor(ActorIdentity actorId) =>
        $"{actorId.TeamId}:{actorId.UnitId}:{actorId.LifeId}";

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
