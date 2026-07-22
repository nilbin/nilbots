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

        string sdkProject = CliSupport.FindUpward(Path.Combine("src", "BotArena.Sdk", "BotArena.Sdk.csproj"))!;
        foreach (var file in Directory.EnumerateFiles(templateDir))
        {
            string content = File.ReadAllText(file)
                .Replace("BOTNAME", name)
                .Replace("<!--BOTARENA_SDK_REFERENCE-->",
                    $"<ProjectReference Include=\"{sdkProject}\" />");
            File.WriteAllText(
                Path.Combine(targetDir, Path.GetFileName(file).Replace("BOTNAME", name)),
                content);
        }
        Console.WriteLine($"Created bot project: {targetDir}");
        Console.WriteLine();
        Console.WriteLine($"  cd {name}");
        Console.WriteLine("  botarena play --bot . --opponent hunter --seed 42");
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
        Console.WriteLine($"Game rules:       {BotArenaVersions.GameRulesVersion}");
        Console.WriteLine($"Runtime protocol: {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"SDK:              {ToolchainInfo.SdkVersion}");
        Console.WriteLine($"Compiler:         NativeAOT-LLVM {ToolchainInfo.IlcLlvmVersion}");
        Console.WriteLine($"Cache:            {(built.FromCache ? "hit" : "miss (compiled)")} · key {built.CacheKey[..16]}…");
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
                Console.Error.WriteLine("Usage: botarena cache [status|clear]");
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
        Console.WriteLine($"WASM target:            wasi-wasm (p1 core module)");
        Console.WriteLine($"Runtime host:           Wasmtime {typeof(Wasmtime.Engine).Assembly.GetName().Version}");
        Console.WriteLine($"Runtime configuration:  {BotArenaVersions.RuntimeConfigurationVersion}");
        Console.WriteLine($"Runtime protocol:       {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"Game rules:             {BotArenaVersions.GameRulesVersion}");
        var rules = GameRules.V0_1;
        Console.WriteLine($"Fuel limit:             200000000 per tick (initial calibration)");
        Console.WriteLine($"Memory limit:           64 MB");
        Console.WriteLine($"Fault limit:            {rules.FaultLimit} per match");
        Console.WriteLine($"Build isolation:        {(BuildIsolation.Available
            ? "on — compiles run as the 'botbuild' user with ulimits"
            : "off — compiles run as the current user (needs root + setpriv + a botbuild account; BOTARENA_BUILD_ISOLATION=off forces off)")}");

        string wasiSdk = ToolchainInfo.ResolveWasiSdkPath();
        Report("wasi-sdk toolchain", File.Exists(Path.Combine(wasiSdk, "bin", "clang")), wasiSdk,
            "run scripts/setup-wasi-sdk.sh");
        string? builtin = CliSupport.FindUpward(Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
        Report("built-in bots (WASM)", builtin is not null, builtin ?? "",
            "run scripts/build-wasm-guest.sh");
        string? viewer = CliSupport.FindUpward(Path.Combine("web", "dist", "index.html"));
        Report("visual viewer", viewer is not null, viewer ?? "",
            "run `npm run build` in web/");
        string? maps = CliSupport.FindUpward("maps");
        Report("maps", maps is not null, maps ?? "", "missing maps/ directory");
        Console.WriteLine($"Authentication:         not signed in (no server yet)");
        Console.WriteLine($"Server compatibility:   n/a (no server yet)");
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
