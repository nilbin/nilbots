using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// The fast inner loop (DECISIONS #44): builds a bot project as a plain .NET assembly
/// (seconds, incremental) and runs it through the diagnostic in-process runtime — the
/// same engine and the same deterministic per-bot RNG as a real match, without paying
/// the NativeAOT-LLVM WASM compile on every iteration. NOT submission-equivalent:
/// fuel/memory limits and the WASI clock/entropy shims are not enforced here, which is
/// exactly why the plan calls in-process execution diagnostic-only. Verify in WASM
/// (default `botarena play`) before submitting.
/// </summary>
internal static class InProcessProject
{
    public static Func<Sdk.IBot> LoadFactory(BotProject project, bool quiet = false)
    {
        string dllPath = Build(project, quiet);
        var context = new BotLoadContext(dllPath);
        var assembly = context.LoadFromAssemblyPath(dllPath);
        var entryType = assembly.GetType(project.Manifest.EntryType, throwOnError: false)
            ?? Array.Find(assembly.GetTypes(), t => t.Name == project.Manifest.EntryType)
            ?? throw new InvalidOperationException(
                $"Entry type '{project.Manifest.EntryType}' not found in {Path.GetFileName(dllPath)}.");
        if (!typeof(Sdk.IBot).IsAssignableFrom(entryType))
            throw new InvalidOperationException(
                $"{entryType.FullName} does not implement BotArena.Sdk.IBot.");
        return () => (Sdk.IBot)Activator.CreateInstance(entryType)!;
    }

    private static string Build(BotProject project, bool quiet)
    {
        string? csproj = Directory.EnumerateFiles(project.Directory, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No .csproj in {project.Directory} — in-process mode builds the project file " +
                "(`botarena new` scaffolds one).");
        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(new ProcessStartInfo(
            "dotnet", "build -c Release -v q --nologo")
        {
            WorkingDirectory = project.Directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var errors = output.Split('\n')
                .Where(l => l.Contains(": error ", StringComparison.Ordinal))
                .Select(l => l.Trim()).Distinct().Take(20).ToList();
            throw new InvalidOperationException(errors.Count > 0
                ? $"Build failed:\n  {string.Join("\n  ", errors)}"
                : $"Build failed:\n{output}");
        }
        string dllPath = Path.Combine(project.Directory, "bin", "Release", "net10.0",
            Path.GetFileNameWithoutExtension(csproj) + ".dll");
        if (!File.Exists(dllPath))
            throw new InvalidOperationException($"Build succeeded but no assembly at {dllPath}.");
        if (!quiet)
            Console.WriteLine($"{project.Manifest.Name}: in-process build in {stopwatch.Elapsed.TotalSeconds:F1}s " +
                              "(diagnostic — fuel/memory limits NOT enforced; verify in WASM before submitting)");
        return dllPath;
    }

    /// <summary>
    /// Loads the bot assembly in its own context but lets BotArena.Sdk resolve to the
    /// host's already-loaded copy — otherwise the player's IBot implements a *different*
    /// IBot type and the cast fails.
    /// </summary>
    private sealed class BotLoadContext(string mainAssemblyPath)
        : AssemblyLoadContext($"bot:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}")
    {
        private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "BotArena.Sdk")
                return null; // defer to the default context: shared type identity
            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
