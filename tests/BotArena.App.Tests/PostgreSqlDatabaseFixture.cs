using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BotArena.App.Tests;

/// <summary>
/// Creates one empty PostgreSQL database per integration test. Local developers may
/// omit BOTARENA_TEST_DB and skip this opt-in suite; CI sets
/// BOTARENA_POSTGRES_REQUIRED=true, so a missing or unreachable server is a failure.
/// </summary>
public sealed class PostgreSqlDatabaseFixture : IAsyncDisposable
{
    public const string Category = "PostgreSql";

    private readonly string databaseName;
    private readonly string administrationConnectionString;

    private PostgreSqlDatabaseFixture(
        string connectionString,
        string databaseName,
        string administrationConnectionString)
    {
        ConnectionString = connectionString;
        this.databaseName = databaseName;
        this.administrationConnectionString = administrationConnectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgreSqlDatabaseFixture> CreateAsync()
    {
        string? configured = Environment.GetEnvironmentVariable("BOTARENA_TEST_DB");
        bool required = string.Equals(
            Environment.GetEnvironmentVariable("BOTARENA_POSTGRES_REQUIRED"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(configured))
        {
            if (required)
            {
                throw new InvalidOperationException(
                    "BOTARENA_TEST_DB is required when BOTARENA_POSTGRES_REQUIRED=true.");
            }

            Skip.If(true, "Set BOTARENA_TEST_DB to run PostgreSQL integration tests.");
        }

        var testBuilder = new NpgsqlConnectionStringBuilder(configured!);
        string baseName = string.IsNullOrWhiteSpace(testBuilder.Database)
            ? "botarena_test"
            : testBuilder.Database;
        string databaseName = $"{baseName}_{Guid.NewGuid():N}";
        if (databaseName.Length > 63)
            databaseName = $"botarena_test_{Guid.NewGuid():N}";

        var adminBuilder = new NpgsqlConnectionStringBuilder(testBuilder.ConnectionString)
        {
            Database = "postgres",
            Pooling = false,
        };

        try
        {
            await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"CREATE DATABASE {QuoteIdentifier(databaseName)} TEMPLATE template0";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception) when (!required)
        {
            Skip.If(
                true,
                $"PostgreSQL is unavailable: {exception.GetType().Name}: {exception.Message}");
        }

        testBuilder.Database = databaseName;
        testBuilder.Pooling = false;
        return new PostgreSqlDatabaseFixture(
            testBuilder.ConnectionString,
            databaseName,
            adminBuilder.ConnectionString);
    }

    public AppDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseOpenIddict();
        if (interceptors.Length > 0)
            builder.AddInterceptors(interceptors);
        return new AppDbContext(builder.Options);
    }

    public async Task<AppDbContext> CreateMigratedContextAsync()
    {
        AppDbContext db = CreateContext();
        await db.Database.MigrateAsync();
        return db;
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(administrationConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)} WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string identifier) =>
        '"' + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
