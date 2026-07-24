using BotArena.App.Shared;

namespace BotArena.App.Tests;

public class ApplicationModeTests
{
    [Theory]
    [InlineData(null, "all", true, true, false, true, false)]
    [InlineData("all", "all", true, true, false, true, false)]
    [InlineData("web", "web", true, false, false, false, false)]
    [InlineData("compile-worker", "compile-worker", false, true, false, false, false)]
    [InlineData("compiler-runner", "compiler-runner", false, false, true, false, false)]
    [InlineData("match-worker", "match-worker", false, false, false, true, false)]
    [InlineData("migrate", "migrate", false, false, false, false, true)]
    public void Parse_MapsSupportedRoles(
        string? value,
        string name,
        bool web,
        bool compile,
        bool compilerRunner,
        bool match,
        bool migrate)
    {
        var mode = ApplicationMode.Parse(value);

        Assert.Equal(name, mode.Name);
        Assert.Equal(web, mode.RunsWeb);
        Assert.Equal(compile, mode.RunsCompileWorker);
        Assert.Equal(compilerRunner, mode.RunsCompilerRunner);
        Assert.Equal(match, mode.RunsMatchWorker);
        Assert.Equal(migrate, mode.RunsMigrations);
    }

    [Fact]
    public void Parse_RejectsUnknownRole()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ApplicationMode.Parse("api"));

        Assert.Contains("BOTARENA_ROLE", error.Message);
    }
}
