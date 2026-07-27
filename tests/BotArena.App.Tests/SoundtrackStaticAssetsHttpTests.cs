using System.Net;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class SoundtrackStaticAssetsHttpTests
{
    [Fact]
    public async Task SoundtrackAssets_HaveFormatAndAddressAwareCacheHeaders()
    {
        string webDist = CreateWebDist();
        try
        {
            using var factory = new BotArenaApplicationFactory(UnusedDatabase, webDist);
            using HttpClient client = factory.CreateClient();

            using HttpResponseMessage catalog =
                await client.GetAsync("/soundtracks/index.json");
            Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
            Assert.Equal(
                "application/json",
                catalog.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no-cache", catalog.Headers.CacheControl?.ToString());

            using HttpResponseMessage manifest = await client.GetAsync(
                "/soundtracks/neon-protocol/v1-0123456789abcdef/manifest.json");
            AssertImmutable(manifest, "application/json");

            using HttpResponseMessage m4a = await client.GetAsync(
                "/soundtracks/neon-protocol/v1-0123456789abcdef/stem.m4a");
            AssertImmutable(m4a, "audio/mp4");

            using HttpResponseMessage ogg = await client.GetAsync(
                "/soundtracks/neon-protocol/v1-0123456789abcdef/stem.ogg");
            AssertImmutable(ogg, "audio/ogg");
        }
        finally
        {
            Directory.Delete(webDist, recursive: true);
        }
    }

    [Fact]
    public async Task MissingSoundtrackPath_DoesNotFallBackToSpa()
    {
        string webDist = CreateWebDist();
        try
        {
            using var factory = new BotArenaApplicationFactory(UnusedDatabase, webDist);
            using HttpClient client = factory.CreateClient();

            using HttpResponseMessage missing =
                await client.GetAsync("/soundtracks/neon-protocol/not-here");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.DoesNotContain("SPA SHELL", await missing.Content.ReadAsStringAsync());

            using var head = new HttpRequestMessage(
                HttpMethod.Head,
                "/soundtracks/neon-protocol/not-here");
            using HttpResponseMessage missingHead = await client.SendAsync(head);
            Assert.Equal(HttpStatusCode.NotFound, missingHead.StatusCode);

            using HttpResponseMessage clientRoute = await client.GetAsync("/matches/example");
            Assert.Equal(HttpStatusCode.OK, clientRoute.StatusCode);
            Assert.Contains("SPA SHELL", await clientRoute.Content.ReadAsStringAsync());
        }
        finally
        {
            Directory.Delete(webDist, recursive: true);
        }
    }

    private const string UnusedDatabase =
        "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";

    private static string CreateWebDist()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-soundtrack-static-{Guid.NewGuid():N}");
        string pack = Path.Combine(
            root,
            "soundtracks",
            "neon-protocol",
            "v1-0123456789abcdef");
        Directory.CreateDirectory(pack);
        File.WriteAllText(Path.Combine(root, "index.html"), "<p>SPA SHELL</p>");
        File.WriteAllText(
            Path.Combine(root, "soundtracks", "index.json"),
            """{"schemaVersion":1,"packs":[]}""");
        File.WriteAllText(Path.Combine(pack, "manifest.json"), """{"schemaVersion":1}""");
        File.WriteAllBytes(Path.Combine(pack, "stem.m4a"), [0, 1, 2, 3]);
        File.WriteAllBytes(Path.Combine(pack, "stem.ogg"), [4, 5, 6, 7]);
        return root;
    }

    private static void AssertImmutable(
        HttpResponseMessage response,
        string mediaType)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            response.Headers.CacheControl?.ToString());
    }
}
