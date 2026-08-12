using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.ArcRelay;
using BotArena.Engine;
using BotArena.TacticalPlaybooks;

namespace BotArena.App.Sheets;

public sealed record ValidatedTacticalSheet(
    TacticalPlaybookCompilation Compilation,
    string ContentHash,
    ArcRelayCompositionCompilation Composition);

/// <summary>The only hosted validation path for tactical-sheet source.</summary>
public sealed class TacticalSheetCompiler(ArcRelayClassCatalog classCatalog)
{
    public const int MaximumOrdersBytes = 192 * 1024;
    public const int MaximumMapPlanBytes = 96 * 1024;

    public ValidatedTacticalSheet Compile(
        string ordersJson,
        string mapPlanJson,
        IReadOnlySet<string> unlockedClassIds,
        string diagnosticName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ordersJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapPlanJson);
        byte[] orders = Encoding.UTF8.GetBytes(ordersJson);
        byte[] mapPlan = Encoding.UTF8.GetBytes(mapPlanJson);
        if (orders.Length > MaximumOrdersBytes)
            throw new InvalidDataException(
                $"Orders document exceeds {MaximumOrdersBytes} UTF-8 bytes.");
        if (mapPlan.Length > MaximumMapPlanBytes)
            throw new InvalidDataException(
                $"Map plan exceeds {MaximumMapPlanBytes} UTF-8 bytes.");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(
                orders,
                mapPlan,
                $"{diagnosticName}/orders.json",
                $"{diagnosticName}/map-plan.json");
        string mapId = MapPlanMapId(compilation.NormalizedLayout);
        if (!string.Equals(
                mapId,
                ArcRelayLoopProfile.Current.MapId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Map plan targets '{mapId}'. Hosted Arc Relay requires "
                + $"the native '{ArcRelayLoopProfile.Current.MapId}' "
                + "Tapline plan; geometry from older maps is never migrated.");
        }
        ArcRelayCompositionCompilation composition = ArcRelayComposition.Compile(
            new ArcRelayCompositionDeclaration(compilation.Composition),
            classCatalog,
            unlockedClassIds);
        return new ValidatedTacticalSheet(
            compilation,
            Convert.ToHexStringLower(SHA256.HashData(compilation.LinkedData)),
            composition);
    }

    public static bool IsAdmissibleToCurrentPlaylist(string mapPlanJson)
    {
        try
        {
            return string.Equals(
                MapPlanMapId(Encoding.UTF8.GetBytes(mapPlanJson)),
                ArcRelayLoopProfile.Current.MapId,
                StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static string MapPlanMapId(ReadOnlyMemory<byte> mapPlanJson)
    {
        using JsonDocument document = JsonDocument.Parse(mapPlanJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(
                "mapId", out JsonElement mapId)
            || mapId.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(mapId.GetString()))
        {
            throw new InvalidDataException("Map plan has no mapId.");
        }
        return mapId.GetString()!;
    }
}
