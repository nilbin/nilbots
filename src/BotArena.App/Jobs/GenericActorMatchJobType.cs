namespace BotArena.App.Jobs;

/// <summary>
/// Durable queue capability for one immutable hosted generic playlist
/// version. Workers claim only the exact definitions registered in their
/// binary, so a mixed-version fleet cannot consume work it cannot execute.
/// The resulting value is persisted; changing its format requires an explicit
/// queue migration and mixed-version rollout plan.
/// </summary>
public static class GenericActorMatchJobType
{
    private const string Prefix = "ExecuteGenericActorMatch:";

    public static string ForPlaylist(
        string playlistKey,
        int playlistVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistKey);
        if (playlistVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(playlistVersion));

        return $"{Prefix}{Uri.EscapeDataString(playlistKey)}:v{playlistVersion}";
    }

    public static bool IsGenericActorMatch(string jobType) =>
        !string.IsNullOrEmpty(jobType) &&
        jobType.Length > Prefix.Length &&
        jobType.StartsWith(Prefix, StringComparison.Ordinal);
}
