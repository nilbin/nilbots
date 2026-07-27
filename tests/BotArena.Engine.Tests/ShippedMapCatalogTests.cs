namespace BotArena.Engine.Tests;

public class ShippedMapCatalogTests
{
    [Fact]
    public void TopLevelMapCatalog_ContainsOnlyPlayableFormat1Maps()
    {
        string mapsDirectory = Path.Combine(FindRepoRoot(), "maps");
        string[] shippedFiles = Directory
            .EnumerateFiles(mapsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(shippedFiles);
        Assert.All(
            shippedFiles,
            path => Assert.Equal(
                1,
                ArenaMap.FromJson(File.ReadAllText(path)).FormatVersion));

        string experimentalPath = Path.Combine(
            mapsDirectory,
            "experimental",
            "frontline-01.json");
        Assert.True(File.Exists(experimentalPath));
        Assert.Equal(
            2,
            ArenaMap.FromJson(File.ReadAllText(experimentalPath)).FormatVersion);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
