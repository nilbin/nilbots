using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BotArena.Engine;

namespace BotArena.Toolchain;

public sealed record SourceFile(string RelativePath, string Content);

public sealed record BuiltBot(string WasmPath, string ArtifactHash, bool FromCache, string CacheKey);

/// <summary>
/// Compiles bot sources to WASM through the controlled build project (plan §14/§15.1:
/// player sources are dropped into a generated project; a player's own csproj is never
/// trusted). The CLI and the server submission pipeline share this exact code path, and
/// results are cached deterministically (§12).
/// </summary>
public static class BotBuilder
{
    public const int MaxSourceFiles = 16;
    public const int MaxTotalSourceBytes = 256 * 1024;
    public const int MaxBuildLogCharacters = 1024 * 1024;
    public const int MaxArtifactBytes = 16 * 1024 * 1024;

    // Parallel compile lanes (DECISIONS #42) make this class multi-threaded: builds of
    // the same cache key share a workspace and must serialize; distinct keys don't.
    // This lock handles threads in one process; BuildCacheLock below handles a CLI
    // and server (or two CLI processes) sharing the same content cache.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> BuildLocks = new();
    private static readonly object ToolchainAssembliesLock = new();

    /// <summary>The complete assembly closure the generated bot project compiles
    /// against: BotArena.Guest references BotArena.Sdk, and Sdk is a leaf. Staging
    /// anything else makes the build depend on WHICH host started it.</summary>
    private static readonly string[] ToolchainAssemblyFileNames =
        ["BotArena.Sdk.dll", "BotArena.Guest.dll"];

    public static BuiltBot EnsureBuilt(BotProject project, bool noCache = false, bool quiet = false)
    {
        var sources = project.SourceFiles
            .Select(f => new SourceFile(
                Path.GetRelativePath(project.Directory, f), File.ReadAllText(f)))
            .ToArray();
        return BuildFromSources(sources, project.Manifest.EntryType, project.Manifest.Name, noCache, quiet);
    }

    public static BuiltBot BuildFromSources(
        IReadOnlyList<SourceFile> sources, string entryType, string displayName,
        bool noCache = false, bool quiet = false)
    {
        string cacheKey = ComputeCacheKey(sources, entryType);
        string cacheDir = Path.Combine(ToolchainInfo.CacheRoot, cacheKey[..24]);
        string wasmPath = Path.Combine(cacheDir, "bot.wasm");
        if (!noCache && File.Exists(wasmPath))
            return new BuiltBot(wasmPath, Sha256File(wasmPath), FromCache: true, cacheKey);

        lock (BuildLocks.GetOrAdd(cacheKey, static _ => new object()))
        {
            using var cacheLock = BuildCacheLock.Acquire(cacheDir, displayName, quiet);
            // A concurrent identical submission may have finished while we waited.
            if (!noCache && File.Exists(wasmPath))
                return new BuiltBot(wasmPath, Sha256File(wasmPath), FromCache: true, cacheKey);
            return BuildLocked(sources, entryType, displayName, cacheKey, cacheDir, wasmPath, quiet);
        }
    }

    private static BuiltBot BuildLocked(
        IReadOnlyList<SourceFile> sources, string entryType, string displayName,
        string cacheKey, string cacheDir, string wasmPath, bool quiet)
    {
        string repoRoot = RepoPaths.ToolchainRoot();
        Directory.CreateDirectory(cacheDir);
        string toolchainLibs = EnsureToolchainAssemblies(repoRoot);
        bool docker = !WasmBuildPlatform.NativeToolchainAvailable;
        bool isolated = !docker && BuildIsolation.Available;
        string workspace = isolated
            ? Path.Combine(BuildIsolation.WorkRoot, cacheKey[..24])
            : Path.Combine(cacheDir, "build");
        if (Directory.Exists(workspace))
            Directory.Delete(workspace, recursive: true);
        Directory.CreateDirectory(workspace);
        // Stage EXACTLY the assemblies the generated project references, never "every dll
        // next to whoever invoked us" (DECISIONS #84). That copied the host's whole output
        // directory in: 9 assemblies from the CLI but 74 from the server — AWS SDK, EF
        // Core, OpenIddict, BotArena.App itself — so a local build and a server build of
        // identical sources compiled against different assembly closures and could not
        // produce equal bytes. It also put the server's dependency closure inside a
        // sandboxed player build for no reason. BotArena.Guest -> BotArena.Sdk is the
        // entire closure; Sdk is a leaf.
        string libsTarget = Path.Combine(workspace, "libs");
        Directory.CreateDirectory(libsTarget);
        foreach (string assembly in ToolchainAssemblyFileNames)
        {
            string source = Path.Combine(toolchainLibs, assembly);
            if (!File.Exists(source))
                throw new InvalidOperationException(
                    $"Toolchain assembly missing: {source}. Reinstall the Nilbots tool.");
            File.Copy(source, Path.Combine(libsTarget, assembly));
        }

        // Only .cs sources cross into the controlled project (plan §15.1).
        foreach (var source in sources)
        {
            string target = Path.Combine(workspace, source.RelativePath);
            if (!Path.GetFullPath(target).StartsWith(Path.GetFullPath(workspace), StringComparison.Ordinal))
                throw new InvalidOperationException($"Source path escapes the workspace: {source.RelativePath}");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, source.Content);
        }
        File.WriteAllText(Path.Combine(workspace, "__BotArenaMain.cs"),
            $"return BotArena.Guest.GuestHost.Run(() => new {entryType}());\n");
        File.WriteAllText(Path.Combine(workspace, "bot.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RuntimeIdentifier>wasi-wasm</RuntimeIdentifier>
                <UseAppHost>false</UseAppHost>
                <PublishTrimmed>true</PublishTrimmed>
                <SelfContained>true</SelfContained>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <AssemblyName>bot</AssemblyName>
                <InvariantGlobalization>true</InvariantGlobalization>
                <!-- Reproducibility (DECISIONS #81/#83). Three build paths reach this
                     project: Docker (macOS and arm64, via run-wasm-publish.sh), the
                     isolated setpriv build, and a plain local publish. Only the Docker
                     one passed -p:PathMap and -p:ContinuousIntegrationBuild, so a Mac
                     build and a Linux build of the same source embedded DIFFERENT roots
                     and could never match. Setting both here makes every path agree on
                     /src — the value the Docker command line already uses, so it stays
                     consistent whether the command line overrides this or not. -->
                <PathMap>$(MSBuildProjectDirectory)=/src</PathMap>
                <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
                <Deterministic>true</Deterministic>
                <!-- Debug info carried absolute repo paths into every player artifact
                     and dominated its size (2.54 MB -> 998 KB without it). -->
                <DebugType>none</DebugType>
              </PropertyGroup>
              <ItemGroup>
                <DirectPInvoke Include="botarena" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM" Version="{ToolchainInfo.IlcLlvmVersion}" />
                <PackageReference Include="runtime.linux-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="{ToolchainInfo.IlcLlvmVersion}" />
                <Reference Include="BotArena.Sdk"><HintPath>libs/BotArena.Sdk.dll</HintPath></Reference>
                <Reference Include="BotArena.Guest"><HintPath>libs/BotArena.Guest.dll</HintPath></Reference>
              </ItemGroup>
            </Project>
            """);
        string nugetConfig =
            Environment.GetEnvironmentVariable("BOTARENA_NUGET_CONFIG") is { Length: > 0 } configured
                ? configured
                : Path.Combine(repoRoot, "nuget.config");
        if (File.Exists(nugetConfig))
            File.Copy(nugetConfig, Path.Combine(workspace, "nuget.config"));

        string buildLogPath = Path.Combine(cacheDir, "build.log");
        if (!quiet)
            Console.WriteLine($"Compiling {displayName} to WASM (cold cache, " +
                              $"{(docker ? "Docker linux/amd64" : isolated ? "native isolated" : "native")}, " +
                              $"~10 s idle / longer under load — tail {buildLogPath})...");
        var startInfo = docker
            ? WasmBuildPlatform.DockerPublish(repoRoot, workspace, "-c", "Release", "-v", "q")
            : isolated
            ? BuildIsolation.WrapPublish(workspace)
            : new ProcessStartInfo("dotnet", "publish -c Release -v q")
            {
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        if (!docker)
            startInfo.Environment["WASI_SDK_PATH"] = ToolchainInfo.ResolveWasiSdkPath();
        if (isolated)
            BuildIsolation.GrantWorkspace(workspace);

        // Stream the compiler output into build.log as it happens, so a slow build is
        // tailable instead of looking like a hang (gen-2 report, Rampart #9).
        var outputBuffer = new StringBuilder();
        using (var logWriter = new StreamWriter(buildLogPath, append: false) { AutoFlush = true })
        {
            using var process = Process.Start(startInfo)!;
            bool logTruncated = false;
            void OnLine(object sender, DataReceivedEventArgs e)
            {
                if (e.Data is null)
                    return;
                lock (outputBuffer)
                {
                    int remaining = MaxBuildLogCharacters - outputBuffer.Length;
                    if (remaining > 0)
                    {
                        string line = e.Data.Length <= remaining ? e.Data : e.Data[..remaining];
                        outputBuffer.AppendLine(line);
                        logWriter.WriteLine(line);
                    }
                    else if (!logTruncated)
                    {
                        const string marker = "[build log truncated at 1 MiB]";
                        outputBuffer.AppendLine(marker);
                        logWriter.WriteLine(marker);
                        logTruncated = true;
                    }
                }
            }
            process.OutputDataReceived += OnLine;
            process.ErrorDataReceived += OnLine;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(BuildTimeout()))
            {
                // Killing the local `docker run` client does not necessarily stop
                // its daemon-owned container (observed on Docker Desktop). Remove
                // the named build container first so it cannot keep mutating the
                // shared workspace after this process reports a timeout.
                if (docker)
                    WasmBuildPlatform.AbortDockerPublish(startInfo);
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                throw new BotBuildException(
                    $"Build timed out after {BuildTimeout().TotalMinutes:0.#} minutes.",
                    outputBuffer.ToString());
            }
            process.WaitForExit(); // drain the async output streams
            if (process.ExitCode != 0)
            {
                string failureOutput = outputBuffer.ToString();
                throw new BotBuildException(BuildFailureMessage(failureOutput, workspace, buildLogPath), failureOutput);
            }
        }
        string output = outputBuffer.ToString();

        string produced = Path.Combine(workspace, "bin", "Release", "net10.0", "wasi-wasm", "native", "bot.wasm");
        if (!File.Exists(produced))
            throw new BotBuildException($"Build succeeded but no artifact at {produced}.", output);
        if (new FileInfo(produced).Length > MaxArtifactBytes)
            throw new BotBuildException(
                $"Compiled artifact exceeds the {MaxArtifactBytes / 1024 / 1024} MiB limit.",
                output);
        File.Copy(produced, wasmPath, overwrite: true);
        if (isolated)
            Directory.Delete(workspace, recursive: true);
        return new BuiltBot(wasmPath, Sha256File(wasmPath), FromCache: false, cacheKey);
    }

    /// <summary>
    /// A failed build must read like a compiler run, not a crash dump: pull the actual
    /// diagnostic lines out of the MSBuild/ILC/linker noise and show sources by the
    /// player's own relative paths. Falls back to the log tail when nothing matches
    /// (toolchain misconfiguration rather than bad player code).
    /// </summary>
    private static string BuildFailureMessage(string output, string workspace, string buildLogPath)
    {
        string prefix = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var diagnostics = output.Split('\n')
            .Select(l => l.TrimEnd())
            .Where(l => DiagnosticPattern.IsMatch(l))
            .Select(l => System.Text.RegularExpressions.Regex.Replace(
                l.Replace(prefix, ""), @" \[[^\[\]]+\.csproj\]$", ""))
            .Distinct()
            .ToList();
        var errors = diagnostics.Where(d => !d.Contains(": warning ", StringComparison.Ordinal)).ToList();
        if (errors.Count > 0)
            return $"Build failed:\n  {string.Join("\n  ", errors.Take(20))}" +
                   (errors.Count > 20 ? $"\n  … {errors.Count - 20} more error(s)" : "") +
                   $"\nFull log: {buildLogPath}";
        var lines = output.Split('\n');
        string tail = string.Join('\n', lines[^Math.Min(15, lines.Length)..]);
        return $"Build failed (no compiler diagnostics found — likely a toolchain problem):\n{tail}\nFull log: {buildLogPath}";
    }

    private static readonly System.Text.RegularExpressions.Regex DiagnosticPattern = new(
        @"(:\s(error|warning)\s[A-Z]+\d+\s*:)|(\b(clang|wasm-ld|wasm-component-ld|ilc)\b.*\berror\b)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Builds BotArena.Sdk/Guest once per toolchain version and returns the directory
    /// holding their assemblies. The controlled workspace references these DLLs, so
    /// submission builds never touch (or need write access to) the repo tree.
    /// </summary>
    public static string EnsureToolchainAssemblies(string repoRoot)
    {
        if (Environment.GetEnvironmentVariable("BOTARENA_TOOLCHAIN_LIBS") is { Length: > 0 } provisioned)
        {
            string sdk = Path.Combine(provisioned, "BotArena.Sdk.dll");
            string guest = Path.Combine(provisioned, "BotArena.Guest.dll");
            if (!File.Exists(sdk) || !File.Exists(guest))
                throw new InvalidOperationException(
                    $"Provisioned toolchain assemblies are incomplete at {provisioned}.");
            return provisioned;
        }

        string packagedSdk = Path.Combine(AppContext.BaseDirectory, "BotArena.Sdk.dll");
        string packagedGuest = Path.Combine(AppContext.BaseDirectory, "BotArena.Guest.dll");
        if (File.Exists(packagedSdk) && File.Exists(packagedGuest))
            return AppContext.BaseDirectory;

        // Keyed by the SOURCES, not by a hand-maintained version. Keying it by
        // GuestAdapterVersion alone meant an SDK-only change (0.8.0 -> 0.8.1) left a
        // stale Sdk.dll here, which this host then staged into player builds while a
        // freshly built host staged the new one (DECISIONS #84).
        string libsDir = Path.Combine(ToolchainInfo.CacheRoot,
            "toolchain-" + ToolchainAssemblySourceHash(repoRoot)[..16]);
        string sdkDll = Path.Combine(libsDir, "BotArena.Sdk.dll");
        string guestDll = Path.Combine(libsDir, "BotArena.Guest.dll");
        if (File.Exists(sdkDll) && File.Exists(guestDll))
            return libsDir;
        lock (ToolchainAssembliesLock)
        {
            if (File.Exists(sdkDll) && File.Exists(guestDll))
                return libsDir;
            return BuildToolchainAssemblies(repoRoot, libsDir, sdkDll, guestDll);
        }
    }

    private static string? _stagedAssemblyIdentity;

    /// <summary>SHA-256 of each assembly this host would stage, in declaration order.
    /// Resolved once per process; a redeployed host is a new process.</summary>
    public static string StagedAssemblyIdentity()
    {
        if (_stagedAssemblyIdentity is not null)
            return _stagedAssemblyIdentity;
        lock (ToolchainAssembliesLock)
        {
            // Only reach for the repo (which may build them, and may not exist) when the
            // assemblies are not already sitting somewhere this host can see.
            string libs = ToolchainAssembliesIn(
                    Environment.GetEnvironmentVariable("BOTARENA_TOOLCHAIN_LIBS"))
                ?? ToolchainAssembliesIn(AppContext.BaseDirectory)
                ?? EnsureToolchainAssemblies(RepoPaths.ToolchainRoot());
            return _stagedAssemblyIdentity = string.Join('|', ToolchainAssemblyFileNames
                .Select(a => Sha256File(Path.Combine(libs, a))));
        }
    }

    private static string? ToolchainAssembliesIn(string? directory) =>
        directory is { Length: > 0 } &&
        ToolchainAssemblyFileNames.All(a => File.Exists(Path.Combine(directory, a)))
            ? directory
            : null;

    /// <summary>Content identity of the two staged assemblies' inputs: every .cs file
    /// and .csproj under BotArena.Sdk and BotArena.Guest, plus the shared props that
    /// pins how they compile.</summary>
    private static string ToolchainAssemblySourceHash(string repoRoot)
    {
        var inputs = new List<string>();
        foreach (string project in new[] { "BotArena.Sdk", "BotArena.Guest" })
        {
            string dir = Path.Combine(repoRoot, "src", project);
            foreach (string pattern in new[] { "*.cs", "*.csproj" })
                inputs.AddRange(Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories)
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")));
        }
        string shared = Path.Combine(repoRoot, "src", "ToolchainAssembly.props");
        if (File.Exists(shared))
            inputs.Add(shared);
        using var sha = SHA256.Create();
        foreach (string file in inputs.OrderBy(f => f, StringComparer.Ordinal))
        {
            var header = Encoding.UTF8.GetBytes($"\n--{Path.GetRelativePath(repoRoot, file)}--\n");
            sha.TransformBlock(header, 0, header.Length, null, 0);
            byte[] content = File.ReadAllBytes(file);
            sha.TransformBlock(content, 0, content.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(sha.Hash!);
    }

    private static string BuildToolchainAssemblies(
        string repoRoot, string libsDir, string sdkDll, string guestDll)
    {
        Directory.CreateDirectory(libsDir);
        string guestProject = Path.Combine(repoRoot, "src", "BotArena.Guest");
        using var process = Process.Start(new ProcessStartInfo(
            "dotnet", "build -c Release -v q")
        {
            WorkingDirectory = guestProject,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        string log = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new BotBuildException("Toolchain assembly build failed.", log);
        string binDir = Path.Combine(guestProject, "bin", "Release", "net10.0");
        File.Copy(Path.Combine(binDir, "BotArena.Sdk.dll"), sdkDll, overwrite: true);
        File.Copy(Path.Combine(binDir, "BotArena.Guest.dll"), guestDll, overwrite: true);
        return libsDir;
    }

    /// <summary>First line of submission hardening (plan §15.2); the full limits list
    /// lands with the public submission pipeline. Public because callers that only want
    /// to reject a bad submission should not go through ComputeCacheKey — that also
    /// resolves the staged assemblies, and a toolchain problem there is the server's
    /// fault, not the player's.</summary>
    public static void ValidateSubmission(IReadOnlyList<SourceFile> sources, string entryType)
    {
        if (sources.Count == 0)
            throw new BotBuildException("No source files.", "");
        if (sources.Count > MaxSourceFiles)
            throw new BotBuildException($"Too many source files (max {MaxSourceFiles}).", "");
        long total = 0;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!source.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                throw new BotBuildException($"Only .cs files are accepted ('{source.RelativePath}').", "");
            if (source.RelativePath.Length > 160 ||
                !System.Text.RegularExpressions.Regex.IsMatch(
                    source.RelativePath,
                    @"^[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*$") ||
                source.RelativePath.Split('/').Any(part => part is "." or "..") ||
                Path.IsPathRooted(source.RelativePath))
                throw new BotBuildException($"Invalid source path '{source.RelativePath}'.", "");
            if (Path.GetFileName(source.RelativePath) == "__BotArenaMain.cs")
                throw new BotBuildException("Reserved file name __BotArenaMain.cs.", "");
            if (!paths.Add(source.RelativePath))
                throw new BotBuildException($"Duplicate source path '{source.RelativePath}'.", "");
            total += Encoding.UTF8.GetByteCount(source.Content);
        }
        if (total > MaxTotalSourceBytes)
            throw new BotBuildException($"Sources too large (max {MaxTotalSourceBytes / 1024} KB).", "");
        if (entryType.Length > 200 ||
            !System.Text.RegularExpressions.Regex.IsMatch(entryType, @"^[A-Za-z_][A-Za-z0-9_.]*$"))
            throw new BotBuildException($"Invalid entry type '{entryType}'.", "");
    }

    private static TimeSpan BuildTimeout()
    {
        int seconds = int.TryParse(
            Environment.GetEnvironmentVariable("BOTARENA_BUILD_TIMEOUT_SECONDS"),
            out int configured)
            ? Math.Clamp(configured, 30, 900)
            : 300;
        return TimeSpan.FromSeconds(seconds);
    }

    public static string ComputeCacheKey(IReadOnlyList<SourceFile> sources, string entryType)
    {
        ValidateSubmission(sources, entryType);
        using var sha = SHA256.Create();
        void Add(string label, byte[] content)
        {
            var header = Encoding.UTF8.GetBytes($"\n--{label}--\n");
            sha.TransformBlock(header, 0, header.Length, null, 0);
            sha.TransformBlock(content, 0, content.Length, null, 0);
        }
        Add("toolchain", Encoding.UTF8.GetBytes(string.Join('|',
            ToolchainInfo.SdkVersion,
            ToolchainInfo.IlcLlvmVersion,
            ToolchainInfo.GuestAdapterVersion,
            ToolchainInfo.BuildPipelineVersion,
            BotArenaVersions.RuntimeProtocolVersion,
            BotArenaVersions.RuntimeConfigurationVersion,
            BotArenaVersions.ActorRuntimeProtocolVersion,
            BotArenaVersions.ActorRuntimeConfigurationVersion)));
        // The two staged assemblies are compiled into the artifact, so they belong in
        // its identity. Version strings alone were a promise the code did not keep: an
        // Sdk edit without a GuestAdapterVersion bump kept serving the old artifact, and
        // two hosts holding different Sdk builds agreed on a cache key while producing
        // different bytes (DECISIONS #84). Hashing the DLLs makes a framework change
        // rebuild by itself, and makes a host mismatch show up as a different key
        // instead of a different artifact under the same key.
        Add("assemblies", Encoding.UTF8.GetBytes(StagedAssemblyIdentity()));
        Add("entry", Encoding.UTF8.GetBytes(entryType));
        foreach (var source in sources.OrderBy(s => s.RelativePath, StringComparer.Ordinal))
            Add(source.RelativePath, Encoding.UTF8.GetBytes(source.Content));
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(sha.Hash!);
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}

public sealed class BotBuildException(string message, string buildLog) : Exception(message)
{
    public string BuildLog { get; } = buildLog;
}
