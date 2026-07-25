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
        string replayKey = ObjectKeys.Replay(matchId);
        byte[] replayBytes = Encoding.UTF8.GetBytes(ReplaySerializer.ToJson(replay));
        await using var stream = new MemoryStream(replayBytes, writable: false);
        await objectStore.PutAsync(
            replayKey,
            stream,
            expectedSha256: null,
            cancellationToken);
        return replayKey;
    }
}
