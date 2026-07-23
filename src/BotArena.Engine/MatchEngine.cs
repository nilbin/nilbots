using System.Text;

namespace BotArena.Engine;

public sealed class MatchParticipantConfig
{
    public required string Name { get; init; }
    public required IBotRuntime Runtime { get; init; }
    public string RuntimeKind { get; init; } = "in-process";
    public string ArtifactHash { get; init; } = "";
    public string Accent { get; init; } = "#38bdf8";
}

public sealed class MatchConfiguration
{
    public required ArenaMap Map { get; init; }
    public required GameRules Rules { get; init; }
    public required ulong Seed { get; init; }
    public required IReadOnlyList<MatchParticipantConfig> Participants { get; init; }
}

public sealed class MatchRunResult
{
    public required Replay Replay { get; init; }
    public required MatchResultInfo Result { get; init; }
    public required string ReplayHash { get; init; }
}

/// <summary>
/// Runs a complete match: derives per-bot random seeds, drives runtimes against observations,
/// steps the session, enforces diagnostic budgets, and assembles the authoritative replay.
/// </summary>
public sealed class MatchEngine
{
    public MatchRunResult Run(MatchConfiguration configuration)
    {
        var map = configuration.Map;
        var rules = configuration.Rules;
        var participants = configuration.Participants;
        if (participants.Count != map.Spawns.Count)
            throw new ArgumentException(
                $"Map '{map.Id}' has {map.Spawns.Count} spawns but {participants.Count} participants were supplied.");

        var spawns = SpawnVariation.Resolve(map, rules, configuration.Seed);
        var session = new MatchSession(map, rules, spawns);
        int n = participants.Count;
        var debugBudgetRemaining = new int[n];
        for (int slot = 0; slot < n; slot++)
        {
            debugBudgetRemaining[slot] = rules.MaxDebugBytesPerMatch;
            participants[slot].Runtime.StartMatch(new BotMatchStart
            {
                Slot = slot,
                BotRandomSeed = SeedDerivation.DeriveBotSeed(configuration.Seed, slot, rules.RulesVersion),
                GameRulesVersion = rules.RulesVersion,
                RuntimeProtocolVersion = BotArenaVersions.RuntimeProtocolVersion,
            });
        }

        var replayTicks = new List<ReplayTick>();
        while (!session.IsCompleted)
        {
            var observations = new BotObservation[n];
            var decisions = new BotDecision[n];
            for (int slot = 0; slot < n; slot++)
            {
                observations[slot] = session.BuildObservation(slot);
                try
                {
                    decisions[slot] = participants[slot].Runtime.ExecuteTick(observations[slot]);
                }
                catch (Exception ex)
                {
                    decisions[slot] = BotDecision.Fault($"{ex.GetType().Name}: {ex.Message}");
                }
                decisions[slot] = ApplyDebugBudget(decisions[slot], rules, ref debugBudgetRemaining[slot]);
            }

            var tickResult = session.Step(decisions);
            replayTicks.Add(BuildReplayTick(tickResult, observations, decisions, session.State));
        }

        var replay = new Replay
        {
            Header = BuildHeader(configuration, spawns),
            Ticks = replayTicks,
            Result = session.Result!,
        };
        return new MatchRunResult
        {
            Replay = replay,
            Result = session.Result!,
            ReplayHash = ReplaySerializer.ComputeHash(replay),
        };
    }

    private static BotDecision ApplyDebugBudget(BotDecision decision, GameRules rules, ref int remaining)
    {
        if (decision.DebugMessage is null)
            return decision;
        if (remaining <= 0)
            return decision with { DebugMessage = null };
        string message = TruncateUtf8(decision.DebugMessage, Math.Min(rules.MaxDebugBytesPerTick, remaining));
        remaining -= Encoding.UTF8.GetByteCount(message);
        return decision with { DebugMessage = message };
    }

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
            return value;
        var bytes = Encoding.UTF8.GetBytes(value);
        int length = maxBytes;
        // Do not split a multi-byte sequence.
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
            length--;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private static ReplayTick BuildReplayTick(
        TickResult tickResult,
        BotObservation[] observations,
        BotDecision[] decisions,
        GameState state)
    {
        var bots = new List<ReplayBotTick>();
        for (int slot = 0; slot < observations.Length; slot++)
        {
            var resolution = tickResult.Bots[slot];
            bots.Add(new ReplayBotTick
            {
                Slot = slot,
                ChosenAction = resolution.ChosenAction,
                ValidatedAction = resolution.ValidatedAction,
                Result = resolution.Result,
                Faulted = resolution.Faulted,
                Debug = decisions[slot].Faulted ? decisions[slot].FaultMessage : decisions[slot].DebugMessage,
                VisibleTiles = observations[slot].VisibleTiles
                    .Select(t => new[] { t.Position.X, t.Position.Y })
                    .ToArray(),
                VisibleEnemies = observations[slot].VisibleEnemies
                    .Select(e => new ReplayVisibleEnemy(e.Slot, e.Position.X, e.Position.Y, e.Facing, e.Health))
                    .ToArray(),
            });
        }
        var snapshots = state.Bots
            .Select(b => new ReplayBotState(b.Slot, b.Position.X, b.Position.Y, b.Facing, b.Health, b.Cooldown, b.Status,
                state.Rules.MaxEnergy > 0 ? b.Energy : null))
            .ToArray();
        return new ReplayTick
        {
            Tick = tickResult.Tick,
            Bots = bots,
            Events = tickResult.Events,
            State = snapshots,
            Projectiles = state.Rules.ProjectileTicksPerTile > 0
                ? state.Projectiles
                    .Select(p => new ReplayProjectile(p.Position.X, p.Position.Y, p.Direction, p.OwnerSlot))
                    .ToArray()
                : null,
        };
    }

    private static ReplayHeader BuildHeader(MatchConfiguration configuration, IReadOnlyList<Spawn> spawns)
    {
        var map = configuration.Map;
        return new ReplayHeader
        {
            ReplayVersion = BotArenaVersions.ReplayFormatVersion,
            EngineVersion = BotArenaVersions.EngineVersion,
            GameRulesVersion = configuration.Rules.RulesVersion,
            RuntimeProtocolVersion = BotArenaVersions.RuntimeProtocolVersion,
            RuntimeConfigurationVersion = BotArenaVersions.RuntimeConfigurationVersion,
            MapId = map.Id,
            MapVersion = map.Version,
            MapWidth = map.Width,
            MapHeight = map.Height,
            MapTiles = map.TileRows,
            Seed = configuration.Seed,
            MaxTicks = configuration.Rules.MaxTicks,
            VisionRange = configuration.Rules.VisionRange,
            VisionCone = configuration.Rules.VisionCone ? true : null,
            ZoneTiles = configuration.Rules.ZoneControl
                ? map.EffectiveZone().Select(p => new[] { p.X, p.Y }).ToArray()
                : null,
            Participants = configuration.Participants
                .Select((p, slot) => new ReplayParticipant(
                    slot, p.Name, p.RuntimeKind, p.ArtifactHash, p.Accent,
                    spawns[slot].X, spawns[slot].Y, spawns[slot].Facing))
                .ToArray(),
        };
    }
}
