using BotArena.Engine;
using BotArena.Toolchain;

namespace BotArena.App.Matches;

internal static class ArenaMapLoader
{
    public static ArenaMap Load(string mapId, GameRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (!IsCatalogMapId(mapId))
            throw new InvalidOperationException($"Map '{mapId}' is not available.");

        string? path = RepoPaths.FindUpward(Path.Combine("maps", mapId + ".json"));
        if (path is null)
            throw new InvalidOperationException($"Map '{mapId}' not found.");
        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(path));
        ResolvedMatchDefinition definition =
            MatchDefinitionResolver.Resolve(rules, map);
        if (definition.IsFrontline)
        {
            throw new NotSupportedException(
                "Frontline maps are not playable before the dedicated session ships.");
        }
        return map;
    }

    private static bool IsCatalogMapId(string? value) =>
        value is { Length: > 0 and <= 64 }
        && value[0] is >= 'a' and <= 'z'
        && value.All(character =>
            character is >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '-');
}
