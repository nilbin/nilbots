using System.Security.Cryptography;
using System.Text;
using BotArena.App.Storage;

namespace BotArena.App.Tests;

public class LocalObjectStoreTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), "botarena-object-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PutAndMaterialize_VerifiesContentAddressedObject()
    {
        var store = new LocalObjectStore(root);
        byte[] payload = Encoding.UTF8.GetBytes("deterministic wasm bytes");
        string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string key = ObjectKeys.Artifact(hash);

        await using (var source = new MemoryStream(payload))
            await store.PutAsync(key, source, hash);

        Assert.True(await store.ExistsAsync(key));
        string path = await store.MaterializeAsync(key, hash);
        Assert.Equal(payload, await File.ReadAllBytesAsync(path));

        await using Stream? stored = await store.OpenReadAsync(key);
        Assert.NotNull(stored);
        using var copy = new MemoryStream();
        await stored.CopyToAsync(copy);
        Assert.Equal(payload, copy.ToArray());
    }

    [Fact]
    public async Task Put_HashMismatchDoesNotPublishObject()
    {
        var store = new LocalObjectStore(root);
        byte[] payload = Encoding.UTF8.GetBytes("wrong bytes");
        string expected = new('0', 64);
        string key = ObjectKeys.Artifact(expected);

        await using var source = new MemoryStream(payload);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.PutAsync(key, source, expected));

        Assert.False(await store.ExistsAsync(key));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("nested/../../escape")]
    [InlineData("windows\\escape")]
    public async Task Keys_CannotEscapeStore(string key)
    {
        var store = new LocalObjectStore(root);

        await Assert.ThrowsAsync<ArgumentException>(() => store.ExistsAsync(key));
    }

    [Fact]
    public async Task Readiness_DistinguishesReadOnlyAndWritableRoles()
    {
        var store = new LocalObjectStore(root);

        Assert.False(await store.IsReadyAsync(requireWrite: false));
        Assert.True(await store.IsReadyAsync(requireWrite: true));
        Assert.True(await store.IsReadyAsync(requireWrite: false));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
