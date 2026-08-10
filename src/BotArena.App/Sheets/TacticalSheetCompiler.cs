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
    public const int MaximumPlaybookBytes = 192 * 1024;
    public const int MaximumLayoutBytes = 96 * 1024;

    public ValidatedTacticalSheet Compile(
        string playbookJson,
        string layoutJson,
        IReadOnlySet<string> unlockedClassIds,
        string diagnosticName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playbookJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutJson);
        byte[] playbook = Encoding.UTF8.GetBytes(playbookJson);
        byte[] layout = Encoding.UTF8.GetBytes(layoutJson);
        if (playbook.Length > MaximumPlaybookBytes)
            throw new InvalidDataException(
                $"Playbook exceeds {MaximumPlaybookBytes} UTF-8 bytes.");
        if (layout.Length > MaximumLayoutBytes)
            throw new InvalidDataException(
                $"Layout exceeds {MaximumLayoutBytes} UTF-8 bytes.");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(
                playbook,
                layout,
                $"{diagnosticName}/playbook.json",
                $"{diagnosticName}/layout.json");
        string mapId = LayoutMapId(compilation.NormalizedLayout);
        if (!string.Equals(
                mapId,
                ArcRelayLoopProfile.AmbushWarren11.MapId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Layout targets '{mapId}'. Hosted Arc Relay v17 requires "
                + $"the native '{ArcRelayLoopProfile.AmbushWarren11.MapId}' "
                + "Warren layout; legacy Counterflow geometry is not migrated.");
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

    public static bool IsAdmissibleToCurrentPlaylist(string layoutJson)
    {
        try
        {
            return string.Equals(
                LayoutMapId(Encoding.UTF8.GetBytes(layoutJson)),
                ArcRelayLoopProfile.AmbushWarren11.MapId,
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

    private static string LayoutMapId(ReadOnlyMemory<byte> layoutJson)
    {
        using JsonDocument document = JsonDocument.Parse(layoutJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(
                "mapId", out JsonElement mapId)
            || mapId.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(mapId.GetString()))
        {
            throw new InvalidDataException("Layout has no mapId.");
        }
        return mapId.GetString()!;
    }
}
