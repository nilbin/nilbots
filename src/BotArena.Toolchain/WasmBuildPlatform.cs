using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BotArena.Toolchain;

/// <summary>
/// Selects the host used by the pinned NativeAOT-LLVM compiler. Its compiler
/// executable is distributed only as a Linux x64 package, so non-Linux-x64
/// developer machines publish through the repository's cached Docker builder.
/// The produced module is still the same WASI p1 target consumed by Wasmtime.
/// </summary>
public static class WasmBuildPlatform
{
    public static bool NativeCompilerSupported =>
        OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64;

    public static bool NativeToolchainAvailable =>
        NativeCompilerSupported
        && File.Exists(Path.Combine(ToolchainInfo.ResolveWasiSdkPath(), "bin", "clang"));

    public static string BackendDescription => NativeToolchainAvailable
        ? "native Linux x64"
        : "Docker linux/amd64";

    public static bool DockerAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null || !process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                process?.Kill(entireProcessTree: true);
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static ProcessStartInfo DockerPublish(
        string repoRoot,
        string workspace,
        params string[] dotnetArguments)
    {
        string script = Path.Combine(repoRoot, "scripts", "run-wasm-publish.sh");
        if (!File.Exists(script))
            throw new InvalidOperationException($"Cross-platform WASM build script missing: {script}");

        var startInfo = new ProcessStartInfo("bash")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("--docker");
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(workspace);
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("bot.csproj");
        startInfo.ArgumentList.Add("--");
        foreach (string argument in dotnetArguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }
}
