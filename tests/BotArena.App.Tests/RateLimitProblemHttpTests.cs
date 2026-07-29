using System.Net;
using System.Net.Http.Json;
using BotArena.App.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BotArena.App.Tests;

public sealed class RateLimitProblemHttpTests
{
    [Fact]
    public async Task NamedLimiterReturnsStableProblemEnvelope()
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddBotArenaRateLimiting();

        await using WebApplication app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/limited", () => Results.Ok())
            .RequireRateLimiting(RateLimitPolicies.Challenge);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        for (int request = 0; request < 20; request++)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync("/limited")).StatusCode);
        }

        HttpResponseMessage rejected =
            await client.GetAsync("/limited");
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejected.StatusCode);
        Assert.Equal(
            "application/problem+json",
            rejected.Content.Headers.ContentType?.MediaType);

        ApplicationProblemResponse? problem =
            await rejected.Content
                .ReadFromJsonAsync<ApplicationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal(
            ApplicationErrorCodes.RequestRateLimited,
            problem.Code);
        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.True(problem.RetryAfterSeconds > 0);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task GlobalLimiterReturnsStableProblemEnvelope()
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddBotArenaRateLimiting();

        await using WebApplication app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/globally-limited", () => Results.Ok());
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        for (int request = 0; request < 600; request++)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync("/globally-limited")).StatusCode);
        }

        HttpResponseMessage rejected =
            await client.GetAsync("/globally-limited");
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejected.StatusCode);
        ApplicationProblemResponse? problem =
            await rejected.Content
                .ReadFromJsonAsync<ApplicationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal(
            ApplicationErrorCodes.RequestRateLimited,
            problem.Code);
        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            problem.Status);
        Assert.True(problem.RetryAfterSeconds > 0);
    }
}
