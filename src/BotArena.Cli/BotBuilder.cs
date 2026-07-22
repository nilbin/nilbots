using System.Diagnostics;
using BotArena.Engine;

namespace BotArena.Cli;

public sealed record BuiltBot(string WasmPath, string ArtifactHash, bool FromCache, string CacheKey);

/// <summary>
/// Compiles a bot project to WASM through the controlled build project (mirroring the
/// server flow of plan §14/§15.1: player sources are dropped into a generated project;
/// the player's own csproj is never trusted). Results are cached deterministically (§12).
/// </summary>
public static class BotBuilder
{
    public static BuiltBot EnsureBuilt(BotProject project, bool noCache = false, bool quiet = false)
    {
        string cacheKey = project.ComputeCacheKey();
        string cacheDir = Path.Combine(Toolchain.CacheRoot, cacheKey[..24]);
        string wasmPath = Path.Combine(cacheDir, "bot.wasm");
        if (!noCache && File.Exists(wasmPath))
            return new BuiltBot(wasmPath, Sha256File(wasmPath), FromCache: true, cacheKey);

        string repoRoot = Path.GetDirectoryName(
            CliSupport.FindUpward(Path.Combine("src", "BotArena.Guest", "BotArena.Guest.csproj"))
            ?? throw new InvalidOperationException(
                "Bot Arena toolchain not found (src/BotArena.Guest missing above the current directory)."))!;
        repoRoot = Path.GetFullPath(Path.Combine(repoRoot, "..", ".."));

        string workspace = Path.Combine(cacheDir, "build");
        if (Directory.Exists(workspace))
            Directory.Delete(workspace, recursive: true);
        Directory.CreateDirectory(workspace);

        // Only .cs sources cross into the controlled project (plan §15.1).
        foreach (var source in project.SourceFiles)
        {
            string target = Path.Combine(workspace, Path.GetRelativePath(project.Directory, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
        File.WriteAllText(Path.Combine(workspace, "__BotArenaMain.cs"),
            $"return BotArena.Guest.GuestHost.Run(() => new {project.Manifest.EntryType}());\n");
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
                <PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM" Version="{Toolchain.IlcLlvmVersion}" />
                <PackageReference Include="runtime.linux-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="{Toolchain.IlcLlvmVersion}" />
                <ProjectReference Include="{repoRoot}/src/BotArena.Sdk/BotArena.Sdk.csproj" />
                <ProjectReference Include="{repoRoot}/src/BotArena.Guest/BotArena.Guest.csproj" />
              </ItemGroup>
            </Project>
            """);
        string nugetConfig = Path.Combine(repoRoot, "nuget.config");
        if (File.Exists(nugetConfig))
            File.Copy(nugetConfig, Path.Combine(workspace, "nuget.config"));

        if (!quiet)
            Console.WriteLine($"Compiling {project.Manifest.Name} to WASM (cold cache)...");
        var startInfo = new ProcessStartInfo("dotnet", "publish -c Release -v q")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["WASI_SDK_PATH"] =
            Environment.GetEnvironmentVariable("WASI_SDK_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".wasi-sdk", "wasi-sdk-29.0");
        using var process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        File.WriteAllText(Path.Combine(cacheDir, "build.log"), output);
        if (process.ExitCode != 0)
        {
            string tail = string.Join('\n', output.Split('\n')[^Math.Min(15, output.Split('\n').Length)..]);
            throw new InvalidOperationException(
                $"Build failed (full log: {Path.Combine(cacheDir, "build.log")}):\n{tail}");
        }

        string produced = Path.Combine(workspace, "bin", "Release", "net10.0", "wasi-wasm", "native", "bot.wasm");
        if (!File.Exists(produced))
            throw new InvalidOperationException($"Build succeeded but no artifact at {produced}.");
        File.Copy(produced, wasmPath, overwrite: true);
        return new BuiltBot(wasmPath, Sha256File(wasmPath), FromCache: false, cacheKey);
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
    }

    public static void PrintSummary(BotProject project, BuiltBot built)
    {
        Console.WriteLine($"Bot:              {project.Manifest.Name} (entry {project.Manifest.EntryType})");
        Console.WriteLine($"Runtime:          WASM");
        Console.WriteLine($"Game rules:       {BotArenaVersions.GameRulesVersion}");
        Console.WriteLine($"Runtime protocol: {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"SDK:              {Toolchain.SdkVersion}");
        Console.WriteLine($"Compiler:         NativeAOT-LLVM {Toolchain.IlcLlvmVersion}");
        Console.WriteLine($"Cache:            {(built.FromCache ? "hit" : "miss (compiled)")} · key {built.CacheKey[..16]}…");
        Console.WriteLine($"Artifact hash:    {built.ArtifactHash}");
    }
}
