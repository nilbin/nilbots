namespace BotArena.App.Shared;

/// <summary>
/// Operational roles of the modular monolith. Roles share the same code,
/// database, object store, and domain model; they are not network services.
/// </summary>
public sealed class ApplicationMode
{
    private ApplicationMode(
        string name,
        bool runsWeb,
        bool runsCompileWorker,
        bool runsCompilerRunner,
        bool runsMatchWorker,
        bool runsMigrations)
    {
        Name = name;
        RunsWeb = runsWeb;
        RunsCompileWorker = runsCompileWorker;
        RunsCompilerRunner = runsCompilerRunner;
        RunsMatchWorker = runsMatchWorker;
        RunsMigrations = runsMigrations;
    }

    public string Name { get; }
    public bool RunsWeb { get; }
    public bool RunsCompileWorker { get; }
    public bool RunsCompilerRunner { get; }
    public bool RunsMatchWorker { get; }
    public bool RunsMigrations { get; }
    public bool RunsAnyWorker => RunsCompileWorker || RunsMatchWorker;
    public bool IsAll => Name == "all";

    public static ApplicationMode Parse(string? value)
    {
        string role = string.IsNullOrWhiteSpace(value)
            ? "all"
            : value.Trim().ToLowerInvariant();
        return role switch
        {
            "all" => new("all", runsWeb: true, runsCompileWorker: true, runsCompilerRunner: false,
                runsMatchWorker: true, runsMigrations: false),
            "web" => new("web", runsWeb: true, runsCompileWorker: false, runsCompilerRunner: false,
                runsMatchWorker: false, runsMigrations: false),
            "compile-worker" => new("compile-worker", runsWeb: false,
                runsCompileWorker: true, runsCompilerRunner: false,
                runsMatchWorker: false, runsMigrations: false),
            "compiler-runner" => new("compiler-runner", runsWeb: false,
                runsCompileWorker: false, runsCompilerRunner: true,
                runsMatchWorker: false, runsMigrations: false),
            "match-worker" => new("match-worker", runsWeb: false,
                runsCompileWorker: false, runsCompilerRunner: false,
                runsMatchWorker: true, runsMigrations: false),
            "migrate" => new("migrate", runsWeb: false, runsCompileWorker: false,
                runsCompilerRunner: false,
                runsMatchWorker: false, runsMigrations: true),
            _ => throw new InvalidOperationException(
                $"Unknown BOTARENA_ROLE '{value}'. Expected all, web, " +
                "compile-worker, compiler-runner, match-worker, or migrate."),
        };
    }
}
