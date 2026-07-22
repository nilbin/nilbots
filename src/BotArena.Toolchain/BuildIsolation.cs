using System.Diagnostics;

namespace BotArena.Toolchain;

/// <summary>
/// First pass at plan §15.3: submission compilation runs as a dedicated unprivileged
/// user with CPU/process/file-size ulimits when the host allows it (root + setpriv +
/// a 'botbuild' account, as provisioned by the Dockerfile). Falls back to running as
/// the current user otherwise — acceptable for local development, not for strangers.
/// Full network-less container isolation remains on the roadmap.
/// </summary>
public static class BuildIsolation
{
    public const string BuildUser = "botbuild";
    public const string BuildHome = "/var/lib/botbuild";

    public static bool Available =>
        Environment.GetEnvironmentVariable("BOTARENA_BUILD_ISOLATION") != "off"
        && OperatingSystem.IsLinux()
        && Environment.IsPrivilegedProcess
        && File.Exists("/usr/bin/setpriv")
        && Directory.Exists(BuildHome);

    /// <summary>Workspace root readable/writable by the build user.</summary>
    public static string WorkRoot => Path.Combine(BuildHome, "work");

    public static ProcessStartInfo WrapPublish(string workspace)
    {
        // ulimits are inherited across setpriv: 15 CPU-minutes, bounded process count,
        // 1 GB max file size. Memory is bounded operationally (container limits), not here.
        const string limits = "ulimit -t 900 -u 512 -f 1048576";
        var startInfo = new ProcessStartInfo("/bin/bash",
            $"-c \"{limits}; exec setpriv --reuid {BuildUser} --regid {BuildUser} --clear-groups " +
            "dotnet publish -c Release -v q\"")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["HOME"] = BuildHome;
        startInfo.Environment["DOTNET_CLI_HOME"] = BuildHome;
        startInfo.Environment["NUGET_PACKAGES"] = Path.Combine(BuildHome, ".nuget", "packages");
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        return startInfo;
    }

    public static void GrantWorkspace(string workspace)
    {
        Run("chown", $"-R {BuildUser}:{BuildUser} \"{workspace}\"");
        Run("chmod", $"-R u+rwX \"{workspace}\"");
    }

    private static void Run(string file, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(file, arguments))!;
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{file} {arguments} exited {process.ExitCode}.");
    }
}
