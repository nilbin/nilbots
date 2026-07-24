namespace BotArena.Toolchain;

public static class RepoPaths
{
    /// <summary>Finds a file or directory by walking up from the current directory and the
    /// application base directory. Returns the full path or null.</summary>
    public static string? FindUpward(string relativePath)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }
        return null;
    }

    /// <summary>Repo/toolchain root: the directory containing src/BotArena.Guest.
    /// Overridable with BOTARENA_ROOT for deployed servers.</summary>
    public static string ToolchainRoot()
    {
        if (Environment.GetEnvironmentVariable("BOTARENA_ROOT") is { Length: > 0 } root)
            return root;
        string guest = FindUpward(Path.Combine("src", "BotArena.Guest", "BotArena.Guest.csproj"))
            ?? throw new InvalidOperationException(
                "nilbots toolchain not found (src/BotArena.Guest missing above the current " +
                "directory) — set BOTARENA_ROOT to the checkout root.");
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(guest)!, "..", ".."));
    }
}
