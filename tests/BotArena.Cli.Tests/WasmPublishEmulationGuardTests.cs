using BotArena.Toolchain;

namespace BotArena.Cli.Tests;

/// <summary>
/// Pins the platform-matching and anti-stall shape of the Docker publish
/// (DECISIONS #145). The builder container must match the host CPU so the
/// compiler never runs emulated by default; when a platform override forces
/// emulation, MSBuild's multi-node fan-out intermittently deadlocks at 0% CPU,
/// so the emulated branch must run single-node with in-process compilation and
/// W^X disabled. The native Linux x64 branch keeps its parallel fan-out.
/// </summary>
public sealed class WasmPublishEmulationGuardTests
{
    private static string Script()
    {
        string path = Path.Combine(
            RepoPaths.ToolchainRoot(), "scripts", "run-wasm-publish.sh");
        Assert.True(File.Exists(path), $"missing {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void DockerPublish_MatchesContainerPlatformToHostCpu()
    {
        string script = Script();
        Assert.Contains(
            "container_platform=\"${BOTARENA_WASM_DOCKER_PLATFORM:-linux/$host_docker_arch}\"",
            script);
        // The cached builder image is keyed per architecture so an amd64 and an
        // arm64 builder can coexist on one machine.
        Assert.Contains("nilbots-wasm-builder:$builder_key-$container_arch", script);
    }

    [Fact]
    public void EmulatedPublish_RunsSingleNodeWithoutBuildServers()
    {
        string script = Script();
        int dockerCommand = script.IndexOf(
            "docker_command=(docker run", StringComparison.Ordinal);
        Assert.True(dockerCommand >= 0, "docker_command block not found");
        string dockerBranch = script[dockerCommand..];

        // Every anti-stall element is applied, and only under the emulation guard.
        Assert.Contains("DOTNET_EnableWriteXorExecute=0", dockerBranch);
        Assert.Contains("-p:UseSharedCompilation=false", dockerBranch);
        Assert.Contains("-maxcpucount:1", dockerBranch);
        Assert.Contains("-nodeReuse:false", dockerBranch);
        foreach (string marker in new[]
                 {
                     "DOTNET_EnableWriteXorExecute=0",
                     "-maxcpucount:1",
                 })
        {
            int position = script.IndexOf(marker, StringComparison.Ordinal);
            int guard = script.LastIndexOf(
                "if [ \"$emulated\" -eq 1 ]", position, StringComparison.Ordinal);
            Assert.True(guard >= 0, $"{marker} must sit inside an emulation guard");
        }
    }

    [Fact]
    public void NativePublish_KeepsParallelFanOut()
    {
        string script = Script();
        int nativeCommand = script.IndexOf(
            "native_command=(", StringComparison.Ordinal);
        int nativeExec = script.IndexOf(
            "exec \"${native_command[@]}\"", StringComparison.Ordinal);
        Assert.True(nativeCommand >= 0 && nativeExec > nativeCommand,
            "native_command block not found");
        string nativeBranch = script[nativeCommand..nativeExec];

        Assert.DoesNotContain("UseSharedCompilation", nativeBranch);
        Assert.DoesNotContain("maxcpucount", nativeBranch);
    }

    [Fact]
    public void CompilerHostPackages_AreConditionalOnBuildArchitecture()
    {
        // Both the generated player workspace (BotBuilder) and the built-in
        // guest reference the linux-x64 and linux-arm64 compiler hosts behind
        // architecture conditions, so one workspace builds correctly inside
        // either container.
        string builder = File.ReadAllText(Path.Combine(
            RepoPaths.ToolchainRoot(), "src", "BotArena.Toolchain", "BotBuilder.cs"));
        string guest = File.ReadAllText(Path.Combine(
            RepoPaths.ToolchainRoot(), "src", "BotArena.WasmGuest", "BotArena.WasmGuest.csproj"));
        foreach (string source in new[] { builder, guest })
        {
            Assert.Contains("runtime.linux-x64.Microsoft.DotNet.ILCompiler.LLVM", source);
            Assert.Contains("runtime.linux-arm64.Microsoft.DotNet.ILCompiler.LLVM", source);
            Assert.Contains("RuntimeInformation]::OSArchitecture)' == 'Arm64'", source);
        }
    }
}
