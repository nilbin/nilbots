namespace BotArena.App.Shared;

/// <summary>Local persistent and disposable paths used by application adapters.</summary>
public static class DataPaths
{
    public static string Root =>
        Environment.GetEnvironmentVariable("BOTARENA_DATA") is { Length: > 0 } data
            ? Path.GetFullPath(data)
            : Path.Combine(Directory.GetCurrentDirectory(), "var");

    public static string Objects =>
        Environment.GetEnvironmentVariable("BOTARENA_OBJECTS") is { Length: > 0 } objects
            ? Path.GetFullPath(objects)
            : Path.Combine(Root, "objects");

    public static string Keys => Ensure(Path.Combine(Root, "keys"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
