namespace BotArena.App.Storage;

public static class ObjectKeys
{
    public static string Artifact(string sha256)
    {
        if (sha256.Length != 64 || sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Artifact hash must be a 64-character SHA-256 hex string.", nameof(sha256));
        return $"artifacts/sha256/{sha256.ToLowerInvariant()}.wasm";
    }

    public static string Replay(Guid matchId) => $"replays/{matchId:N}.json";
}
