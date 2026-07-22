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
        ValidateSubmission(sources, entryType);
        string cacheKey = ComputeCacheKey(sources, entryType);
        string cacheDir = Path.Combine(ToolchainInfo.CacheRoot, cacheKey[..24]);
        string wasmPath = Path.Combine(cacheDir, "bot.wasm");
        if (!noCache && File.Exists(wasmPath))
            return new BuiltBot(wasmPath, Sha256File(wasmPath), FromCache: true, cacheKey);

        string repoRoot = RepoPaths.ToolchainRoot();
        string toolchainLibs = EnsureToolchainAssemblies(repoRoot);
        bool isolated = BuildIsolation.Available;
        string workspace = isolated
            ? Path.Combine(BuildIsolation.WorkRoot, cacheKey[..24])
            : Path.Combine(cacheDir, "build");
        if (Directory.Exists(workspace))
            Directory.Delete(workspace, recursive: true);
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(Path.Combine(workspace, "libs"));
        foreach (var lib in Directory.EnumerateFiles(toolchainLibs, "*.dll"))
            File.Copy(lib, Path.Combine(workspace, "libs", Path.GetFileName(lib)));

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
        string nugetConfig = Path.Combine(repoRoot, "nuget.config");
        if (File.Exists(nugetConfig))
            File.Copy(nugetConfig, Path.Combine(workspace, "nuget.config"));

        if (!quiet)
            Console.WriteLine($"Compiling {displayName} to WASM (cold cache{(isolated ? ", isolated" : "")})...");
        var startInfo = isolated
            ? BuildIsolation.WrapPublish(workspace)
            : new ProcessStartInfo("dotnet", "publish -c Release -v q")
            {
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        startInfo.Environment["WASI_SDK_PATH"] =
            Environment.GetEnvironmentVariable("WASI_SDK_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".wasi-sdk", "wasi-sdk-29.0");
        if (isolated)
            BuildIsolation.GrantWorkspace(workspace);
        using var process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            throw new BotBuildException("Build timed out after 5 minutes.", output);
        }
        File.WriteAllText(Path.Combine(cacheDir, "build.log"), output);
        if (process.ExitCode != 0)
        {
            var lines = output.Split('\n');
            string tail = string.Join('\n', lines[^Math.Min(15, lines.Length)..]);
            throw new BotBuildException($"Build failed:\n{tail}", output);
        }

        string produced = Path.Combine(workspace, "bin", "Release", "net10.0", "wasi-wasm", "native", "bot.wasm");
        if (!File.Exists(produced))
            throw new BotBuildException($"Build succeeded but no artifact at {produced}.", output);
        File.Copy(produced, wasmPath, overwrite: true);
        if (isolated)
            Directory.Delete(workspace, recursive: true);
        return new BuiltBot(wasmPath, Sha256File(wasmPath), FromCache: false, cacheKey);
    }

    /// <summary>
    /// Builds BotArena.Sdk/Guest once per toolchain version and returns the directory
    /// holding their assemblies. The controlled workspace references these DLLs, so
    /// submission builds never touch (or need write access to) the repo tree.
    /// </summary>
    public static string EnsureToolchainAssemblies(string repoRoot)
    {
        string libsDir = Path.Combine(ToolchainInfo.CacheRoot,
            "toolchain-" + ToolchainInfo.GuestAdapterVersion);
        string sdkDll = Path.Combine(libsDir, "BotArena.Sdk.dll");
        string guestDll = Path.Combine(libsDir, "BotArena.Guest.dll");
        if (File.Exists(sdkDll) && File.Exists(guestDll))
            return libsDir;

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
    /// lands with the public submission pipeline.</summary>
    private static void ValidateSubmission(IReadOnlyList<SourceFile> sources, string entryType)
    {
        if (sources.Count == 0)
            throw new BotBuildException("No source files.", "");
        if (sources.Count > MaxSourceFiles)
            throw new BotBuildException($"Too many source files (max {MaxSourceFiles}).", "");
        long total = 0;
        foreach (var source in sources)
        {
            if (!source.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                throw new BotBuildException($"Only .cs files are accepted ('{source.RelativePath}').", "");
            if (source.RelativePath.Contains("..") || Path.IsPathRooted(source.RelativePath))
                throw new BotBuildException($"Invalid source path '{source.RelativePath}'.", "");
            if (Path.GetFileName(source.RelativePath) == "__BotArenaMain.cs")
                throw new BotBuildException("Reserved file name __BotArenaMain.cs.", "");
            total += Encoding.UTF8.GetByteCount(source.Content);
        }
        if (total > MaxTotalSourceBytes)
            throw new BotBuildException($"Sources too large (max {MaxTotalSourceBytes / 1024} KB).", "");
        if (!System.Text.RegularExpressions.Regex.IsMatch(entryType, @"^[A-Za-z_][A-Za-z0-9_.]*$"))
            throw new BotBuildException($"Invalid entry type '{entryType}'.", "");
    }

    public static string ComputeCacheKey(IReadOnlyList<SourceFile> sources, string entryType)
    {
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
            BotArenaVersions.RuntimeProtocolVersion,
            BotArenaVersions.RuntimeConfigurationVersion)));
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
