using System.Text;
using BotArena.App.Storage;
using BotArena.Engine;

namespace BotArena.App.Jobs;

/// <summary>
/// Persists deterministic replay content under the match's stable object key.
/// Repeating this write after a lease expiry is safe.
/// </summary>
public sealed class MatchReplayWriter(IObjectStore objectStore)
{
    public async Task<string> WriteAsync(
        Guid matchId,
        Replay replay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replay);
        return await WriteCanonicalJsonAsync(
            matchId,
            ReplaySerializer.ToJson(replay),
            cancellationToken);
    }

    public async Task<string> WriteCanonicalJsonAsync(
        Guid matchId,
        string canonicalJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalJson);
        string replayKey = ObjectKeys.Replay(matchId);
        byte[] replayBytes = Encoding.UTF8.GetBytes(canonicalJson);
        await using var stream = new MemoryStream(replayBytes, writable: false);
        await objectStore.PutAsync(
            replayKey,
            stream,
            expectedSha256: null,
            cancellationToken);
        return replayKey;
    }
}
