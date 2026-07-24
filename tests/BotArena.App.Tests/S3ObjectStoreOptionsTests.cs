using BotArena.App.Storage;
using Microsoft.Extensions.Configuration;

namespace BotArena.App.Tests;

public class S3ObjectStoreOptionsTests
{
    [Fact]
    public void FromConfiguration_AcceptsExplicitPrivateHttpEndpoint()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BOTARENA_S3_ENDPOINT"] = "http://garage-gateway:3900",
            ["BOTARENA_S3_ALLOW_HTTP"] = "true",
            ["BOTARENA_S3_REGION"] = "garage",
            ["BOTARENA_S3_BUCKET"] = "nilbots",
            ["BOTARENA_S3_ACCESS_KEY"] = "GKtest",
            ["BOTARENA_S3_SECRET_KEY"] = "secret",
            ["BOTARENA_OBJECT_CACHE"] = Path.Combine(Path.GetTempPath(), "nilbots-cache"),
        });

        S3ObjectStoreOptions options = S3ObjectStoreOptions.FromConfiguration(configuration);

        Assert.Equal("garage-gateway", options.Endpoint.Host);
        Assert.Equal("garage", options.Region);
        Assert.Equal("nilbots", options.Bucket);
    }

    [Fact]
    public void FromConfiguration_RejectsHttpWithoutExplicitOptIn()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BOTARENA_S3_ENDPOINT"] = "http://garage-gateway:3900",
            ["BOTARENA_S3_REGION"] = "garage",
            ["BOTARENA_S3_BUCKET"] = "nilbots",
            ["BOTARENA_S3_ACCESS_KEY"] = "GKtest",
            ["BOTARENA_S3_SECRET_KEY"] = "secret",
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => S3ObjectStoreOptions.FromConfiguration(configuration));

        Assert.Contains("BOTARENA_S3_ALLOW_HTTP=true", exception.Message);
    }

    [Fact]
    public void FromConfiguration_RequiresEveryCredential()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BOTARENA_S3_ENDPOINT"] = "https://s3.example.test",
            ["BOTARENA_S3_REGION"] = "test",
            ["BOTARENA_S3_BUCKET"] = "nilbots",
            ["BOTARENA_S3_ACCESS_KEY"] = "GKtest",
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => S3ObjectStoreOptions.FromConfiguration(configuration));

        Assert.Contains("BOTARENA_S3_SECRET_KEY", exception.Message);
    }

    private static IConfiguration BuildConfiguration(
        IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
