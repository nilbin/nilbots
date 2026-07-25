using BotArena.Engine;
using BotArena.Toolchain;

namespace BotArena.Cli;

public static class NewCommand
{
    public static int Run(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9]*$"))
        {
            Console.Error.WriteLine("Bot name must be a valid C# identifier (letters and digits, starting with a letter).");
            return 1;
        }
        // C# warns for all-lowercase type names because they may become reserved;
        // solution-wide warnings-as-errors otherwise makes `new suppressor` generate
        // a project that cannot compile. Preserve the requested directory but emit a
        // conventional, valid entry type.
        string entryType = char.ToUpperInvariant(name[0]) + name[1..];
        string? templateDir = CliSupport.FindUpward(Path.Combine("templates", "botarena-bot"));
        if (templateDir is null)
        {
            Console.Error.WriteLine("Template not found (templates/botarena-bot).");
            return 1;
        }
        string targetDir = Path.GetFullPath(name);
        if (Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
        {
            Console.Error.WriteLine($"Directory {targetDir} already exists and is not empty.");
            return 1;
        }
        Directory.CreateDirectory(targetDir);

        string? sdkProject = CliSupport.FindUpward(
            Path.Combine("src", "BotArena.Sdk", "BotArena.Sdk.csproj"));
        string sdkReference;
        if (sdkProject is not null)
        {
            sdkReference =
                $"<ProjectReference Include=\"{Path.GetRelativePath(targetDir, sdkProject)}\" />";
        }
        else
        {
            string packagedSdk = Path.Combine(AppContext.BaseDirectory, "BotArena.Sdk.dll");
            if (!File.Exists(packagedSdk))
                throw new InvalidOperationException(
                    "The installed CLI is missing BotArena.Sdk.dll; reinstall the Nilbots tool.");
            string botLib = Path.Combine(targetDir, "lib");
            Directory.CreateDirectory(botLib);
            File.Copy(packagedSdk, Path.Combine(botLib, "BotArena.Sdk.dll"), overwrite: true);
            // The XML docs must travel WITH the dll or every SDK member is blank in
            // IntelliSense — the whole player API is this one assembly (player-test
            // finding). Missing docs must never fail `new`, so this is best-effort.
            string packagedDocs = Path.ChangeExtension(packagedSdk, ".xml");
            if (File.Exists(packagedDocs))
                File.Copy(packagedDocs, Path.Combine(botLib, "BotArena.Sdk.xml"), overwrite: true);
            sdkReference =
                "<Reference Include=\"BotArena.Sdk\"><HintPath>lib/BotArena.Sdk.dll</HintPath></Reference>";
        }
        foreach (var file in Directory.EnumerateFiles(templateDir))
        {
            string content = File.ReadAllText(file)
                .Replace("BOTNAME", entryType)
                .Replace("SDKVERSION", ToolchainInfo.SdkVersion)
                .Replace("<!--BOTARENA_SDK_REFERENCE-->", sdkReference);
            File.WriteAllText(
                Path.Combine(targetDir, Path.GetFileName(file).Replace("BOTNAME", entryType)),
                content);
        }
        Console.WriteLine($"Created bot project: {targetDir}");
        Console.WriteLine();
        Console.WriteLine($"  cd {name}");
        Console.WriteLine("  nilbots play --bot . --opponent hunter --seed 42");
        if (sdkProject is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Source checkout without `nilbots` on PATH:");
            string wrapper = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(sdkProject)!, "..", "..", "scripts", "botarena"));
            Console.WriteLine($"  {wrapper} play --bot . --opponent hunter --seed 42");
        }
        return 0;
    }
}

public static class BuildCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        var (directory, rest) = TakeDirectory(args);
        var options = CliSupport.ParseOptions(rest);
        var project = BotProject.Load(directory);
        var built = BotBuilder.EnsureBuilt(project, noCache: options.ContainsKey("no-cache"));
        // A tangible artifact in the project (gen-2 finding #10): lets you point
        // `play --opponent` at this build as a file, like champions/*/bot.wasm.
        string artifactCopy = Path.Combine(project.Directory, "out", "bot.wasm");
        Directory.CreateDirectory(Path.GetDirectoryName(artifactCopy)!);
        File.Copy(built.WasmPath, artifactCopy, overwrite: true);
        PrintSummary(project, built);
        Console.WriteLine($"Artifact:         {artifactCopy}");
        return 0;
    }

    public static void PrintSummary(BotProject project, BuiltBot built)
    {
        Console.WriteLine($"Bot:              {project.Manifest.Name} (entry {project.Manifest.EntryType})");
        Console.WriteLine($"Runtime:          WASM");
        // No "Game rules" line: the artifact is rules-agnostic — the ruleset is chosen
        // per match by play/set/server, not baked in at build time (gen-4 trial #4:
        // printing the default here read as "built for the wrong game" under a pin).
        Console.WriteLine($"Runtime protocol: {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"SDK:              {ToolchainInfo.SdkVersion}");
        Console.WriteLine($"Compiler:         NativeAOT-LLVM {ToolchainInfo.IlcLlvmVersion}");
        Console.WriteLine($"Cache:            {(built.FromCache ? "hit" : "miss (compiled)")} · key {built.CacheKey}");
        Console.WriteLine($"Artifact hash:    {built.ArtifactHash}");
    }

    public static (string Directory, IReadOnlyList<string> Remaining) TakeDirectory(IReadOnlyList<string> args) =>
        args.Count > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
            ? (args[0], args.Skip(1).ToArray())
            : (".", args);
}

public static class CacheCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        switch (args.FirstOrDefault() ?? "status")
        {
            case "status":
                if (!Directory.Exists(ToolchainInfo.CacheRoot))
                {
                    Console.WriteLine($"Cache empty ({ToolchainInfo.CacheRoot}).");
                    return 0;
                }
                var entries = Directory.GetDirectories(ToolchainInfo.CacheRoot);
                long bytes = entries.SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
                    .Sum(f => new FileInfo(f).Length);
                Console.WriteLine($"Cache: {entries.Length} artifact(s), {bytes / (1024.0 * 1024):F1} MB at {ToolchainInfo.CacheRoot}");
                return 0;
            case "clear":
                if (Directory.Exists(ToolchainInfo.CacheRoot))
                    Directory.Delete(ToolchainInfo.CacheRoot, recursive: true);
                Console.WriteLine("Cache cleared.");
                return 0;
            default:
                Console.Error.WriteLine("Usage: nilbots cache [status|clear]");
                return 1;
        }
    }
}

public static class DoctorCommand
{
    public static int Run()
    {
        Console.WriteLine($"CLI version:            {ToolchainInfo.CliVersion}");
        Console.WriteLine($"SDK version:            {ToolchainInfo.SdkVersion}");
        Console.WriteLine($"Compiler:               NativeAOT-LLVM {ToolchainInfo.IlcLlvmVersion}");
        Console.WriteLine($"Build pipeline:         {ToolchainInfo.BuildPipelineVersion}");
        Console.WriteLine($"WASM target:            wasi-wasm (p1 core module)");
        Console.WriteLine($"WASM build backend:     {WasmBuildPlatform.BackendDescription}");
        Console.WriteLine($"Runtime host:           Wasmtime {typeof(Wasmtime.Engine).Assembly.GetName().Version}");
        Console.WriteLine($"Runtime configuration:  {BotArenaVersions.RuntimeConfigurationVersion}");
        Console.WriteLine($"Runtime protocol:       {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"Game rules:             {BotArenaVersions.GameRulesVersion}");
        var rules = GameRules.Current;
        Console.WriteLine($"Fuel limit:             200000000 per tick (initial calibration)");
        Console.WriteLine($"Memory limit:           64 MB");
        Console.WriteLine($"Fault limit:            {rules.FaultLimit} per match");
        Console.WriteLine($"Build isolation:        {(!WasmBuildPlatform.NativeToolchainAvailable
            ? "Docker boundary (local development)"
            : BuildIsolation.Available
                ? "on — compiles run as the 'botbuild' user with ulimits"
                : "off — current user (set BOTARENA_BUILD_ISOLATION=off to force this)")}");

        if (WasmBuildPlatform.NativeToolchainAvailable)
        {
            string wasiSdk = ToolchainInfo.ResolveWasiSdkPath();
            Report("wasi-sdk toolchain", File.Exists(Path.Combine(wasiSdk, "bin", "clang")), wasiSdk,
                "run `sudo bash scripts/setup-wasi-sdk.sh` on Ubuntu 24.04");
        }
        else
        {
            Report("Docker WASM builder", WasmBuildPlatform.DockerAvailable(),
                "linux/amd64 image is prepared on first build",
                "install and start Docker; use --runtime in-process meanwhile");
        }
        string? builtin = CliSupport.FindUpward(Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
        Report("built-in bots (WASM)", builtin is not null, builtin ?? "",
            "run scripts/build-wasm-guest.sh");
        string? viewer = CliSupport.FindUpward(Path.Combine("web", "dist", "index.html"));
        Report("visual viewer", viewer is not null, viewer ?? "",
            "run `npm run build` in web/");
        string? maps = CliSupport.FindUpward("maps");
        Report("maps", maps is not null, maps ?? "", "missing maps/ directory");
        // Report the session we actually have, and check the server's build SDK against
        // ours. doctor previously hard-coded "not signed in" even seconds after whoami
        // succeeded, so the version-skew check that explains a parity mismatch never ran
        // (player-test round 2, S2-4).
        ServerCommands.ReportSessionHealth();
        return 0;

        static void Report(string label, bool ok, string detail, string fix) =>
            Console.WriteLine($"{label + ':',-24}{(ok ? "OK  " + detail : "MISSING — " + fix)}");
    }
}

public static class WatchCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        var (directory, rest) = BuildCommand.TakeDirectory(args);
        var restArray = rest.ToArray();
        if (!BotProject.LooksLikeProject(directory))
        {
            Console.Error.WriteLine($"{directory} is not a bot project (no botarena.json).");
            return 1;
        }
        Console.WriteLine($"Watching {Path.GetFullPath(directory)} — Ctrl+C to stop.");
        string lastKey = "";
        while (true)
        {
            string key;
            try
            {
                key = BotProject.Load(directory).ComputeCacheKey();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[watch] {ex.Message}");
                Thread.Sleep(1000);
                continue;
            }
            if (key != lastKey)
            {
                lastKey = key;
                Console.WriteLine();
                Console.WriteLine($"[watch] change detected at {DateTime.Now:HH:mm:ss} — rebuilding and replaying");
                try
                {
                    PlayCommand.Run(["--bot", directory, .. restArray]);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[watch] {ex.Message}");
                }
                Console.WriteLine("[watch] waiting for changes...");
            }
            Thread.Sleep(500);
        }
    }
}
