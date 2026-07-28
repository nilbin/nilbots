using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// The fast inner loop (DECISIONS #44): builds a bot project as a plain .NET assembly
/// (seconds, incremental) and runs it through the diagnostic in-process runtime — the
/// same engine and the same deterministic per-bot RNG as a real match, without paying
/// the NativeAOT-LLVM WASM compile on every iteration. NOT submission-equivalent:
/// fuel/memory limits and the WASI clock/entropy shims are not enforced here, which is
/// exactly why the plan calls in-process execution diagnostic-only. Verify in WASM
/// (default `nilbots play`) before submitting.
/// </summary>
internal static class InProcessProject
{
    private sealed record CachedFactory(long SourceStamp, Func<Sdk.IBot> Factory);
    internal sealed record LoadedActorFactory(
        Func<Sdk.IActorBot> Factory,
        string ProvenanceHash);
    internal sealed record LoadedGenericActorFactory(
        Func<Sdk.IGenericActorBot> Factory,
        string ProvenanceHash);
    private sealed record CachedActorFactory(
        long SourceStamp,
        LoadedActorFactory Loaded);
    private sealed record CachedGenericActorFactory(
        long SourceStamp,
        LoadedGenericActorFactory Loaded);

    private static readonly Dictionary<string, CachedFactory> Factories =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, CachedActorFactory> ActorFactories =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, CachedGenericActorFactory>
        GenericActorFactories = new(StringComparer.Ordinal);

    public static Func<Sdk.IBot> LoadFactory(BotProject project, bool quiet = false)
    {
        string projectDirectory = Path.GetFullPath(project.Directory);
        long sourceStamp = project.SourceFiles
            .Append(Path.Combine(projectDirectory, "botarena.json"))
            .Concat(Directory.EnumerateFiles(projectDirectory, "*.csproj"))
            .Where(File.Exists)
            .Max(file => File.GetLastWriteTimeUtc(file).Ticks);
        if (Factories.TryGetValue(projectDirectory, out var cached)
            && cached.SourceStamp == sourceStamp)
            return cached.Factory;

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
        Func<Sdk.IBot> factory = () => (Sdk.IBot)Activator.CreateInstance(entryType)!;
        Factories[projectDirectory] = new CachedFactory(sourceStamp, factory);
        return factory;
    }

    /// <summary>
    /// Actor equivalent of <see cref="LoadFactory"/>. Every invocation creates
    /// a new policy object; <c>InProcessActorRuntimeFactory</c> then gives each
    /// Frontline life an isolated instance and deterministic random stream.
    /// </summary>
    public static LoadedActorFactory LoadActorFactory(
        BotProject project,
        bool quiet = false)
    {
        string projectDirectory = Path.GetFullPath(project.Directory);
        long sourceStamp = SourceStamp(project, projectDirectory);
        if (ActorFactories.TryGetValue(
                projectDirectory,
                out CachedActorFactory? cached)
            && cached.SourceStamp == sourceStamp)
        {
            return cached.Loaded;
        }

        string dllPath = Build(project, quiet);
        var context = new BotLoadContext(dllPath);
        Assembly assembly = context.LoadFromAssemblyPath(dllPath);
        Type entryType = FindEntryType(project, dllPath, assembly);
        if (!typeof(Sdk.IActorBot).IsAssignableFrom(entryType))
        {
            throw new InvalidOperationException(
                $"{entryType.FullName} does not implement " +
                "BotArena.Sdk.IActorBot (required by Frontline).");
        }

        var loaded = new LoadedActorFactory(
            () => (Sdk.IActorBot)Activator.CreateInstance(entryType)!,
            AssemblyClosureHash(dllPath));
        ActorFactories[projectDirectory] =
            new CachedActorFactory(sourceStamp, loaded);
        return loaded;
    }

    /// <summary>
    /// Generic actor equivalent of <see cref="LoadActorFactory"/>. The loaded
    /// factory still creates a fresh SDK object for every runtime life.
    /// </summary>
    public static LoadedGenericActorFactory LoadGenericActorFactory(
        BotProject project,
        bool quiet = false)
    {
        string projectDirectory = Path.GetFullPath(project.Directory);
        long sourceStamp = SourceStamp(project, projectDirectory);
        if (GenericActorFactories.TryGetValue(
                projectDirectory,
                out CachedGenericActorFactory? cached)
            && cached.SourceStamp == sourceStamp)
        {
            return cached.Loaded;
        }

        string dllPath = Build(project, quiet);
        var context = new BotLoadContext(dllPath);
        Assembly assembly = context.LoadFromAssemblyPath(dllPath);
        Type entryType = FindEntryType(project, dllPath, assembly);
        if (!typeof(Sdk.IGenericActorBot).IsAssignableFrom(entryType))
        {
            throw new InvalidOperationException(
                $"{entryType.FullName} does not implement " +
                "BotArena.Sdk.IGenericActorBot " +
                "(required by Frontline Labs).");
        }

        var loaded = new LoadedGenericActorFactory(
            () => (Sdk.IGenericActorBot)Activator.CreateInstance(entryType)!,
            AssemblyClosureHash(dllPath));
        GenericActorFactories[projectDirectory] =
            new CachedGenericActorFactory(sourceStamp, loaded);
        return loaded;
    }

    private static long SourceStamp(
        BotProject project,
        string projectDirectory) =>
        project.SourceFiles
            .Append(Path.Combine(projectDirectory, "botarena.json"))
            .Concat(Directory.EnumerateFiles(projectDirectory, "*.csproj"))
            .Where(File.Exists)
            .Max(file => File.GetLastWriteTimeUtc(file).Ticks);

    private static Type FindEntryType(
        BotProject project,
        string dllPath,
        Assembly assembly) =>
        assembly.GetType(project.Manifest.EntryType, throwOnError: false)
        ?? Array.Find(
            assembly.GetTypes(),
            type => type.Name == project.Manifest.EntryType)
        ?? throw new InvalidOperationException(
            $"Entry type '{project.Manifest.EntryType}' not found in " +
            $"{Path.GetFileName(dllPath)}.");

    /// <summary>
    /// Identifies the bytes the diagnostic runtime actually executes. This is
    /// intentionally distinct from the controlled-WASM build cache key: a
    /// player csproj can change references and compile options that the
    /// submission builder ignores, while the in-process loop honors them.
    /// </summary>
    private static string AssemblyClosureHash(string mainAssemblyPath)
    {
        string outputDirectory = Path.GetDirectoryName(mainAssemblyPath)
            ?? throw new InvalidOperationException(
                $"Assembly '{mainAssemblyPath}' has no output directory.");
        string dependencyManifest = Path.ChangeExtension(
            mainAssemblyPath,
            ".deps.json");
        string[] files = Directory
            .EnumerateFiles(
                outputDirectory,
                "*.dll",
                SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).Equals(
                "BotArena.Sdk.dll",
                StringComparison.OrdinalIgnoreCase))
            .Append(dependencyManifest)
            .Where(File.Exists)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        using SHA256 sha = SHA256.Create();
        void Add(string label, byte[] content)
        {
            byte[] header = Encoding.UTF8.GetBytes(
                $"\n--{label}--\n");
            sha.TransformBlock(header, 0, header.Length, null, 0);
            sha.TransformBlock(content, 0, content.Length, null, 0);
        }

        foreach (string file in files)
        {
            Add(
                Path.GetFileName(file),
                File.ReadAllBytes(file));
        }
        string sdkPath = typeof(Sdk.IActorBot).Assembly.Location;
        Add("host/BotArena.Sdk.dll", File.ReadAllBytes(sdkPath));
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(sha.Hash!);
    }

    private static string Build(BotProject project, bool quiet)
    {
        using var projectBuildLock = ProjectBuildLock.Acquire(project.Directory, project.Manifest.Name, quiet);
        string? csproj = Directory.EnumerateFiles(project.Directory, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No .csproj in {project.Directory} — in-process mode builds the project file " +
                "(`nilbots new` scaffolds one).");
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
