using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotArena.Engine;

public sealed record ReplayParticipant(
    int Slot,
    string Name,
    string RuntimeKind,
    string ArtifactHash,
    string Accent,
    int SpawnX,
    int SpawnY,
    Direction SpawnFacing);

public sealed record ReplayHeader
{
    public required int ReplayVersion { get; init; }
    public required string EngineVersion { get; init; }
    public required string GameRulesVersion { get; init; }
    public required string RuntimeProtocolVersion { get; init; }
    public required string RuntimeConfigurationVersion { get; init; }
    public required string MapId { get; init; }
    public required int MapVersion { get; init; }
    public required int MapWidth { get; init; }
    public required int MapHeight { get; init; }
    /// <summary>Included so the viewer is self-contained; small for MVP-sized maps.</summary>
    public required IReadOnlyList<string> MapTiles { get; init; }
    public required ulong Seed { get; init; }
    public required int MaxTicks { get; init; }
    public required int VisionRange { get; init; }
    /// <summary>[x,y] pairs; null (omitted) under rules without zone control, so
    /// pre-0.3 replay hashes are unaffected. The viewer highlights these tiles.</summary>
    public IReadOnlyList<int[]>? ZoneTiles { get; init; }
    public required IReadOnlyList<ReplayParticipant> Participants { get; init; }
}

public sealed record ReplayVisibleEnemy(int Slot, int X, int Y, Direction Facing, int Health);

public sealed record ReplayBotTick
{
    public required int Slot { get; init; }
    public required BotAction ChosenAction { get; init; }
    public required BotAction ValidatedAction { get; init; }
    public required ActionResult Result { get; init; }
    public bool Faulted { get; init; }
    public string? Debug { get; init; }
    /// <summary>[x,y] pairs; wall/floor is derivable from the header map.</summary>
    public required int[][] VisibleTiles { get; init; }
    public required IReadOnlyList<ReplayVisibleEnemy> VisibleEnemies { get; init; }
}

/// <summary>Energy is null (omitted from canonical JSON) under rules without an energy
/// system, so pre-energy replay hashes are unaffected.</summary>
public sealed record ReplayBotState(
    int Slot, int X, int Y, Direction Facing, int Health, int Cooldown, BotStatus Status,
    int? Energy = null);

public sealed record ReplayTick
{
    public required int Tick { get; init; }
    public required IReadOnlyList<ReplayBotTick> Bots { get; init; }
    public required IReadOnlyList<GameEvent> Events { get; init; }
    public required IReadOnlyList<ReplayBotState> State { get; init; }
}

public sealed record Replay
{
    public required ReplayHeader Header { get; init; }
    public required IReadOnlyList<ReplayTick> Ticks { get; init; }
    public required MatchResultInfo Result { get; init; }
}

public sealed record ReplayDocument
{
    public required ReplayHeader Header { get; init; }
    public required IReadOnlyList<ReplayTick> Ticks { get; init; }
    public required MatchResultInfo Result { get; init; }
    public required string ReplayHash { get; init; }
}

/// <summary>
/// Canonical replay serialization. The replay hash is SHA-256 over the canonical JSON
/// (camelCase, enums as strings, nulls omitted, no whitespace, declaration property order)
/// of {header, ticks, result}. Any change here is a replay-format version change.
/// </summary>
public static class ReplaySerializer
{
    public static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToCanonicalJson(Replay replay) =>
        JsonSerializer.Serialize(replay, Canonical);

    public static string ComputeHash(Replay replay) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalJson(replay))));

    public static string ToJson(Replay replay)
    {
        var document = new ReplayDocument
        {
            Header = replay.Header,
            Ticks = replay.Ticks,
            Result = replay.Result,
            ReplayHash = ComputeHash(replay),
        };
        return JsonSerializer.Serialize(document, Canonical);
    }

    public static ReplayDocument FromJson(string json) =>
        JsonSerializer.Deserialize<ReplayDocument>(json, Canonical)
        ?? throw new InvalidOperationException("Empty replay document.");
}
