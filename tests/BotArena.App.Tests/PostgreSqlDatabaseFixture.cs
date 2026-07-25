using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BotArena.App.Tests;

/// <summary>
/// Creates one empty PostgreSQL database per integration test, which needs a role with
/// CREATEDB. Omitting BOTARENA_TEST_DB skips the suite — the local opt-out. SETTING it
/// commits to running: from there an unreachable server or an under-privileged role
/// fails rather than skips, so nobody gets a green run that tested nothing.
/// BOTARENA_POSTGRES_REQUIRED=true additionally makes a MISSING variable an error, which
/// is how CI catches its own misconfiguration.
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
        catch (Exception exception)
        {
            // Setting BOTARENA_TEST_DB IS the opt-in, so a server that cannot be reached
            // or a role that cannot CREATE DATABASE is a failure, not a skip. Swallowing
            // it meant a developer who asked for these tests got a green run that
            // exercised nothing, and the reason was only visible by also setting
            // BOTARENA_POSTGRES_REQUIRED (DECISIONS #101).
            throw new InvalidOperationException(
                $"BOTARENA_TEST_DB is set, but the PostgreSQL integration database could " +
                $"not be created: {exception.GetType().Name}: {exception.Message}. " +
                "The role needs CREATEDB — `ALTER ROLE <user> CREATEDB;` as a superuser.",
                exception);
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
