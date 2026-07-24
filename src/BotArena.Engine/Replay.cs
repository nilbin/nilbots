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
    /// <summary>True (else null, omitted) when sight is the directional cone rather than
    /// omnidirectional — lets the viewer draw each bot's facing wedge. Null under
    /// pre-cone rules keeps their hashes unchanged.</summary>
    public bool? VisionCone { get; init; }
    /// <summary>[x,y] pairs; null (omitted) under rules without zone control, so
    /// pre-0.3 replay hashes are unaffected. The viewer highlights these tiles.</summary>
    public IReadOnlyList<int[]>? ZoneTiles { get; init; }
    /// <summary>Absolute domination limit for the shared active-control meter; null
    /// under passive zone scoring.</summary>
    public int? ControlPressureLimit { get; init; }
    /// <summary>Tick and reduced pressure limit for a late active-control overtime;
    /// null when the rules have no overtime phase.</summary>
    public int? ControlOvertimeStartTick { get; init; }
    public int? ControlOvertimePressureLimit { get; init; }
    public int? ControlOvertimePressureGain { get; init; }
    /// <summary>True when nobody-holding pressure decay is disabled in overtime.</summary>
    public bool? ControlOvertimeStopsDecay { get; init; }
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
    /// <summary>Sounds this bot heard this tick; null when none (and always under
    /// rules without hearing), keeping pre-hearing replay hashes unaffected.</summary>
    public IReadOnlyList<ReplayHeardSound>? HeardSounds { get; init; }
}

/// <summary>Energy is null (omitted from canonical JSON) under rules without an energy
/// system, so pre-energy replay hashes are unaffected. ZoneTicks (cumulative) is
/// emitted only under <see cref="GameRules.ReplayZoneTallies"/> — viewers read the
/// tally instead of re-deriving accrual rules; null keeps official 0.4 bytes stable.</summary>
public sealed record ReplayBotState(
    int Slot, int X, int Y, Direction Facing, int Health, int Cooldown, BotStatus Status,
    int? Energy = null, int? ZoneTicks = null);

/// <summary>Null (omitted) under instant-shot rules, so pre-projectile replay hashes
/// are unaffected. TicksUntilAdvance/RemainingTiles mirror the bot observation (§H
/// item 2: dodge timing is data, not something viewers re-derive); their defaults let
/// pre-hardening bolt replays still deserialize.</summary>
public sealed record ReplayProjectile(
    int X, int Y, Direction Direction, int OwnerSlot,
    int TicksUntilAdvance = 0, int RemainingTiles = 0,
    int TilesPerAdvance = 1, int Id = 0);

/// <summary>Authoritative ordered path traversed by one projectile during this
/// visual tick. Path contains each entered tile in order, including the tile where
/// a first- or second-substep hit occurred. This lets presentation interpolate a
/// speed-two bolt through both tiles without changing discrete simulation.</summary>
public sealed record ReplayProjectileTraversal(
    int Id, int OwnerSlot, Direction Direction, int FromX, int FromY, IReadOnlyList<int[]> Path);

/// <summary>A redacted heard sound (see <see cref="Hearing"/>): bearing is the 8-way
/// octant 0=N..7=NW, distance the band 0=Near/1=Medium/2=Far.</summary>
public sealed record ReplayHeardSound(GameEventType Type, int Bearing, int Distance);

public sealed record ReplayTick
{
    public required int Tick { get; init; }
    public required IReadOnlyList<ReplayBotTick> Bots { get; init; }
    public required IReadOnlyList<GameEvent> Events { get; init; }
    public required IReadOnlyList<ReplayBotState> State { get; init; }
    /// <summary>Bolts in flight after this tick resolved; null under instant-shot rules.</summary>
    public IReadOnlyList<ReplayProjectile>? Projectiles { get; init; }
    /// <summary>Ordered projectile paths taken during this tick; null under
    /// instant-shot rules.</summary>
    public IReadOnlyList<ReplayProjectileTraversal>? ProjectileTraversals { get; init; }
    /// <summary>Signed shared objective pressure after this tick; null under passive
    /// zone scoring.</summary>
    public int? ControlPressure { get; init; }
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
