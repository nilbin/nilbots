using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace BotArena.App.Storage;

/// <summary>
/// Private S3-compatible immutable-blob backend. Uploads and downloads are
/// staged locally so their recorded SHA-256 can be verified before use.
/// </summary>
public sealed class S3ObjectStore : IObjectStore, IDisposable
{
    private const int BufferSize = 81_920;
    private const string Sha256MetadataKey = "sha256";

    private readonly S3ObjectStoreOptions options;
    private readonly IAmazonS3 client;
    private readonly SemaphoreSlim writeProbeGate = new(1, 1);
    private bool writeProbeCompleted;

    public S3ObjectStore(S3ObjectStoreOptions options)
        : this(options, CreateClient(options))
    {
    }

    public S3ObjectStore(S3ObjectStoreOptions options, IAmazonS3 client)
    {
        this.options = options;
        this.client = client;
    }

    public async Task PutAsync(
        string key,
        Stream source,
        string? expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ObjectKeys.Validate(key);
        string? normalizedExpected = expectedSha256 is null
            ? null
            : NormalizeSha256(expectedSha256);

        if (normalizedExpected is not null &&
            await RemoteHashMatches(key, normalizedExpected, cancellationToken))
        {
            return;
        }

        Directory.CreateDirectory(options.CacheRoot);
        string temporary = Path.Combine(options.CacheRoot, $".upload-{Guid.NewGuid():N}.tmp");
        try
        {
            string actual = await CopyAndHashAsync(source, temporary, cancellationToken);
            if (normalizedExpected is not null &&
                !actual.Equals(normalizedExpected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Object '{key}' hash mismatch: expected {normalizedExpected}, got {actual}.");
            }

            await using var input = new FileStream(
                temporary,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var request = new PutObjectRequest
            {
                BucketName = options.Bucket,
                Key = key,
                InputStream = input,
                AutoCloseStream = false,
                ContentType = "application/octet-stream",
            };
            request.Metadata[Sha256MetadataKey] = actual;
            await client.PutObjectAsync(request, cancellationToken);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public async Task<Stream?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ObjectKeys.Validate(key);
        try
        {
            GetObjectResponse response = await client.GetObjectAsync(
                options.Bucket,
                key,
                cancellationToken);
            return new OwnedReadStream(response.ResponseStream, response);
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ObjectKeys.Validate(key);
        try
        {
            await client.GetObjectMetadataAsync(
                options.Bucket,
                key,
                cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return false;
        }
    }

    public async Task<string> MaterializeAsync(
        string key,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ObjectKeys.Validate(key);
        string normalizedHash = NormalizeSha256(expectedSha256);
        string directory = Path.Combine(options.CacheRoot, "sha256", normalizedHash[..2]);
        string path = Path.Combine(directory, normalizedHash);
        if (File.Exists(path) &&
            await HashMatchesAsync(path, normalizedHash, cancellationToken))
        {
            return path;
        }

        Directory.CreateDirectory(directory);
        string temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using Stream? remote = await OpenReadAsync(key, cancellationToken);
            if (remote is null)
                throw new FileNotFoundException($"Object '{key}' does not exist.");

            string actual = await CopyAndHashAsync(remote, temporary, cancellationToken);
            if (!actual.Equals(normalizedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Object '{key}' does not match its recorded SHA-256 hash.");
            }

            File.Move(temporary, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public async Task<bool> IsReadyAsync(
        bool requireWrite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.HeadBucketAsync(
                new HeadBucketRequest { BucketName = options.Bucket },
                cancellationToken);
            if (!requireWrite || writeProbeCompleted)
                return true;

            await writeProbeGate.WaitAsync(cancellationToken);
            try
            {
                if (!writeProbeCompleted)
                {
                    await using var probe = new MemoryStream([0x6f, 0x6b]);
                    await PutAsync(
                        ".health/write-probe",
                        probe,
                        expectedSha256: null,
                        cancellationToken);
                    writeProbeCompleted = true;
                }
            }
            finally
            {
                writeProbeGate.Release();
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        writeProbeGate.Dispose();
        client.Dispose();
    }

    private async Task<bool> RemoteHashMatches(
        string key,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            GetObjectMetadataResponse response = await client.GetObjectMetadataAsync(
                options.Bucket,
                key,
                cancellationToken);
            string? actual = response.Metadata[Headers.Sha256MetadataHeader]
                ?? response.Metadata[Sha256MetadataKey];
            return actual?.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return false;
        }
    }

    private static AmazonS3Client CreateClient(S3ObjectStoreOptions options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint.ToString().TrimEnd('/'),
            AuthenticationRegion = options.Region,
            ForcePathStyle = true,
            UseHttp = options.Endpoint.Scheme == "http",
            MaxErrorRetry = 3,
            Timeout = TimeSpan.FromSeconds(30),
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            config);
    }

    private static async Task<string> CopyAndHashAsync(
        Stream source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        await output.FlushAsync(cancellationToken);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task<bool> HashMatchesAsync(
        string path,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] actual = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(actual).Equals(
            expectedSha256,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSha256(string value)
    {
        if (value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)))
        {
            throw new ArgumentException(
                "Expected SHA-256 must be a 64-character hexadecimal string.",
                nameof(value));
        }
        return value.ToLowerInvariant();
    }

    private static bool IsNotFound(AmazonS3Exception exception) =>
        exception.StatusCode == HttpStatusCode.NotFound ||
        exception.ErrorCode is "NoSuchKey" or "NotFound";

    private static class Headers
    {
        public const string Sha256MetadataHeader = "x-amz-meta-sha256";
    }
}
