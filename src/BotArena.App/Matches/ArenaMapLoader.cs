using BotArena.Engine;
using BotArena.Toolchain;

namespace BotArena.App.Matches;

internal static class ArenaMapLoader
{
    public static ArenaMap Load(string mapId)
    {
        string? path = RepoPaths.FindUpward(Path.Combine("maps", mapId + ".json"));
        if (path is null)
            throw new InvalidOperationException($"Map '{mapId}' not found.");
        return ArenaMap.FromJson(File.ReadAllText(path));
    }
}
