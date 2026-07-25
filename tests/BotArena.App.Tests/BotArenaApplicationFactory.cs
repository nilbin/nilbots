using BotArena.App.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BotArena.App.Tests;

/// <summary>A reusable real-application HTTP host backed by an isolated PostgreSQL database.</summary>
public sealed class BotArenaApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;
    private readonly string? previousRole;
    private readonly string? previousDatabase;
    private readonly string? previousNetworkHashKey;
    private readonly string objectRoot =
        Path.Combine(Path.GetTempPath(), $"nilbots-app-tests-{Guid.NewGuid():N}");

    public BotArenaApplicationFactory(string connectionString)
    {
        this.connectionString = connectionString;
        previousRole = Environment.GetEnvironmentVariable("BOTARENA_ROLE");
        previousDatabase = Environment.GetEnvironmentVariable("BOTARENA_DB");
        previousNetworkHashKey =
            Environment.GetEnvironmentVariable("BOTARENA_NETWORK_HASH_KEY");
        // Minimal-hosting entry-point code reads configuration before
        // WebApplicationFactory replays ConfigureWebHost callbacks.
        Environment.SetEnvironmentVariable("BOTARENA_ROLE", "web");
        Environment.SetEnvironmentVariable("BOTARENA_DB", connectionString);
        Environment.SetEnvironmentVariable(
            "BOTARENA_NETWORK_HASH_KEY",
            "test-only-network-hmac-key-at-least-32-characters");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOTARENA_ROLE"] = "web",
                ["BOTARENA_NETWORK_HASH_KEY"] =
                    "test-only-network-hmac-key-at-least-32-characters",
                ["ConnectionStrings:BotArena"] = connectionString,
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStore>();
            services.AddSingleton<IObjectStore>(new LocalObjectStore(objectRoot));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Environment.SetEnvironmentVariable("BOTARENA_ROLE", previousRole);
            Environment.SetEnvironmentVariable("BOTARENA_DB", previousDatabase);
            Environment.SetEnvironmentVariable(
                "BOTARENA_NETWORK_HASH_KEY",
                previousNetworkHashKey);
            if (Directory.Exists(objectRoot))
                Directory.Delete(objectRoot, recursive: true);
        }
    }
}
