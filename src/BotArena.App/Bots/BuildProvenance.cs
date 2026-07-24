namespace BotArena.App.Bots;

public sealed record BuildProvenance(
    string CompilerImageReference,
    string GitCommit)
{
    public static BuildProvenance FromConfiguration(IConfiguration configuration) => new(
        configuration["BOTARENA_COMPILER_IMAGE_REFERENCE"] ?? "local",
        configuration["BOTARENA_RELEASE_GIT_SHA"] ?? "local");
}

