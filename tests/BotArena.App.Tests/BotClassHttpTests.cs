using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BotArena.App.Bots;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class BotClassHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ClassIdentity_FlowsThroughCreationCatalogAndBotReads()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migration = await database.CreateMigratedContextAsync())
            Assert.Empty(migration.ChangeTracker.Entries());
        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        await RegisterAsync(client, "class-create@example.test", "Class Creator");

        HttpResponseMessage createdResponse = await client.PostAsJsonAsync(
            "/api/bots/",
            new
            {
                name = "Classed Bot",
                accent = "#22d3ee",
                classId = " STRIKER ",
            });
        createdResponse.EnsureSuccessStatusCode();
        using JsonDocument created = await ReadJsonAsync(createdResponse);
        Guid botId = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("striker", created.RootElement.GetProperty("classId").GetString());

        using JsonDocument detail = await GetJsonAsync(client, $"/api/bots/{botId}");
        Assert.Equal("striker", detail.RootElement.GetProperty("classId").GetString());

        using JsonDocument roster = await GetJsonAsync(client, "/api/bots");
        Assert.Equal(
            "striker",
            roster.RootElement.EnumerateArray().Single().GetProperty("classId").GetString());

        using JsonDocument mine = await GetJsonAsync(client, "/api/bots/mine");
        Assert.Equal(
            "striker",
            mine.RootElement.EnumerateArray().Single().GetProperty("classId").GetString());

        using JsonDocument meta = await GetJsonAsync(client, "/api/meta");
        string[] classIds = meta.RootElement.GetProperty("botClasses")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("id").GetString()!)
            .Order()
            .ToArray();
        Assert.Equal(
            new[] { "bulwark", "fabricator", "striker" },
            classIds);

        HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/bots/",
            new
            {
                name = "Unknown Class Bot",
                accent = "#22d3ee",
                classId = "scout",
            });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(
            ApplicationErrorCodes.BotClassUnknown,
            await ProblemCodeAsync(unknown));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task LegacyAssignment_IsOwnerOnlyImmutableAndIdempotent()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migration = await database.CreateMigratedContextAsync())
            Assert.Empty(migration.ChangeTracker.Entries());
        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient owner = factory.CreateClient();
        await RegisterAsync(owner, "class-owner@example.test", "Class Owner");
        Guid botId = await CreateLegacyBotAsync(owner, "Legacy Class Bot");

        HttpResponseMessage missing = await owner.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(
            ApplicationErrorCodes.BotClassIdInvalid,
            await ProblemCodeAsync(missing));

        HttpResponseMessage malformed = await owner.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = "strike_team" });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(
            ApplicationErrorCodes.BotClassIdInvalid,
            await ProblemCodeAsync(malformed));

        HttpResponseMessage unknown = await owner.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = "scout" });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(
            ApplicationErrorCodes.BotClassUnknown,
            await ProblemCodeAsync(unknown));

        using HttpClient stranger = factory.CreateClient();
        await RegisterAsync(stranger, "class-stranger@example.test", "Class Stranger");
        HttpResponseMessage denied = await stranger.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = "bulwark" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(
            ApplicationErrorCodes.BotOwnershipRequired,
            await ProblemCodeAsync(denied));

        HttpResponseMessage assigned = await owner.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = " BULWARK " });
        assigned.EnsureSuccessStatusCode();
        using (JsonDocument payload = await ReadJsonAsync(assigned))
            Assert.Equal("bulwark", payload.RootElement.GetProperty("classId").GetString());

        HttpResponseMessage retry = await owner.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = "bulwark" });
        retry.EnsureSuccessStatusCode();

        HttpResponseMessage reclass = await owner.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = "striker" });
        Assert.Equal(HttpStatusCode.Conflict, reclass.StatusCode);
        Assert.Equal(
            ApplicationErrorCodes.BotClassAlreadyAssigned,
            await ProblemCodeAsync(reclass));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task CompetingFirstAssignments_OnlyOneValueWins()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migration = await database.CreateMigratedContextAsync())
            Assert.Empty(migration.ChangeTracker.Entries());
        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        await RegisterAsync(client, "class-race@example.test", "Class Race");
        Guid botId = await CreateLegacyBotAsync(client, "Class Race Bot");

        Task<HttpResponseMessage> striker = client.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = "striker" });
        Task<HttpResponseMessage> fabricator = client.PutAsJsonAsync(
            $"/api/bots/{botId}/class",
            new { classId = "fabricator" });
        HttpResponseMessage[] responses = await Task.WhenAll(striker, fabricator);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        HttpResponseMessage conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(
            ApplicationErrorCodes.BotClassAlreadyAssigned,
            await ProblemCodeAsync(conflict));

        await using AppDbContext db = database.CreateContext();
        string? stored = await db.Bots
            .Where(bot => bot.Id == botId)
            .Select(bot => bot.ClassId)
            .SingleAsync();
        Assert.Contains(stored, new[] { "striker", "fabricator" });
    }

    private static async Task<Guid> CreateLegacyBotAsync(
        HttpClient client,
        string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/bots/",
            new { name, accent = "#22d3ee" });
        response.EnsureSuccessStatusCode();
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("classId").ValueKind);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task RegisterAsync(
        HttpClient client,
        string email,
        string displayName)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                displayName,
                email,
                password = "correct-horse-battery-staple",
            });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string path)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    private static async Task<string?> ProblemCodeAsync(
        HttpResponseMessage response)
    {
        using JsonDocument problem = await ReadJsonAsync(response);
        return problem.RootElement.GetProperty("code").GetString();
    }
}
