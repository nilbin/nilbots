using System.Text;
using System.IO.Compression;
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

    /// <summary>
    /// Stores a compact replay with deterministic-content hash semantics under
    /// gzip. The HTTP replay endpoint advertises the encoding for complete
    /// broadcasts and inflates it only when it must build a secrecy-safe
    /// partial prefix.
    /// </summary>
    public async Task<CompressedReplayWrite> WriteGzipJsonAsync(
        Guid matchId,
        ReadOnlyMemory<byte> canonicalUtf8,
        CancellationToken cancellationToken)
    {
        if (canonicalUtf8.IsEmpty)
            throw new ArgumentException("Canonical replay bytes cannot be empty.", nameof(canonicalUtf8));
        using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(
                         compressed,
                         CompressionLevel.SmallestSize,
                         leaveOpen: true))
        {
            await gzip.WriteAsync(canonicalUtf8, cancellationToken);
        }
        string replayKey = ObjectKeys.Replay(matchId);
        await using var source = new MemoryStream(
            compressed.GetBuffer(),
            0,
            checked((int)compressed.Length),
            writable: false,
            publiclyVisible: true);
        await objectStore.PutAsync(
            replayKey,
            source,
            expectedSha256: null,
            cancellationToken);
        return new CompressedReplayWrite(replayKey, checked((int)compressed.Length));
    }

    /// <summary>
    /// Byte-oriented replay path for large generic replays. It avoids the
    /// UTF-8 → UTF-16 → UTF-8 round trip and its second full-size allocation.
    /// </summary>
    public async Task<string> WriteCanonicalJsonAsync(
        Guid matchId,
        ReadOnlyMemory<byte> canonicalUtf8,
        CancellationToken cancellationToken)
    {
        if (canonicalUtf8.IsEmpty)
            throw new ArgumentException("Canonical replay bytes cannot be empty.", nameof(canonicalUtf8));
        string replayKey = ObjectKeys.Replay(matchId);
        await using var stream = new ReadOnlyMemoryStream(canonicalUtf8);
        await objectStore.PutAsync(
            replayKey,
            stream,
            expectedSha256: null,
            cancellationToken);
        return replayKey;
    }

    private sealed class ReadOnlyMemoryStream(ReadOnlyMemory<byte> value) : Stream
    {
        private int position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => value.Length;
        public override long Position
        {
            get => position;
            set => position = checked((int)value);
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int length = Math.Min(count, value.Length - position);
            value.Span.Slice(position, length).CopyTo(buffer.AsSpan(offset, length));
            position += length;
            return length;
        }
        public override int Read(Span<byte> buffer)
        {
            int length = Math.Min(buffer.Length, value.Length - position);
            value.Span.Slice(position, length).CopyTo(buffer);
            position += length;
            return length;
        }
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            long next = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => value.Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (next < 0 || next > value.Length)
                throw new IOException("Attempted to seek outside replay bytes.");
            position = checked((int)next);
            return position;
        }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public sealed record CompressedReplayWrite(string Key, int StoredBytes);
