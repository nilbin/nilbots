using System.Security.Cryptography;
using System.Text;
using BotArena.App.Storage;

namespace BotArena.App.Tests;

public class S3ObjectStoreIntegrationTests
{
    [SkippableFact]
    public async Task PutReadAndMaterialize_RoundTripsThroughS3()
    {
        string? endpoint = Environment.GetEnvironmentVariable("BOTARENA_TEST_S3_ENDPOINT");
        string? accessKey = Environment.GetEnvironmentVariable("BOTARENA_TEST_S3_ACCESS_KEY");
        string? secretKey = Environment.GetEnvironmentVariable("BOTARENA_TEST_S3_SECRET_KEY");
        Skip.If(
            endpoint is null || accessKey is null || secretKey is null,
            "S3 integration credentials were not provided.");

        string cache = Path.Combine(
            Path.GetTempPath(),
            "nilbots-s3-integration",
            Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new S3ObjectStore(new S3ObjectStoreOptions
            {
                Endpoint = new Uri(endpoint),
                Region = Environment.GetEnvironmentVariable("BOTARENA_TEST_S3_REGION")
                    ?? "garage",
                Bucket = Environment.GetEnvironmentVariable("BOTARENA_TEST_S3_BUCKET")
                    ?? "nilbots-tests",
                AccessKey = accessKey,
                SecretKey = secretKey,
                CacheRoot = cache,
            });

            byte[] payload = Encoding.UTF8.GetBytes($"s3-roundtrip-{Guid.NewGuid():N}");
            string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            string key = $"tests/sha256/{hash}";
            await using (var input = new MemoryStream(payload))
                await store.PutAsync(key, input, hash);

            Assert.True(await store.ExistsAsync(key));
            Assert.True(await store.IsReadyAsync(requireWrite: true));
            await using Stream? remote = await store.OpenReadAsync(key);
            Assert.NotNull(remote);
            using var copy = new MemoryStream();
            await remote.CopyToAsync(copy);
            Assert.Equal(payload, copy.ToArray());

            string materialized = await store.MaterializeAsync(key, hash);
            Assert.Equal(payload, await File.ReadAllBytesAsync(materialized));
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }
}
