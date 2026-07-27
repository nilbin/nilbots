using System.Collections.Immutable;
using System.Text;

namespace BotArena.Engine;

/// <summary>
/// Inputs for the experimental actor-runtime Frontline host. A supplied
/// topology is resolved and canonicalized before any runtime is created; when
/// omitted, the rules-owned default topology is used.
/// </summary>
public sealed record FrontlineActorMatchConfiguration
{
    public required ArenaMap Map { get; init; }
    public required GameRules Rules { get; init; }
    public required ulong Seed { get; init; }
    public required IReadOnlyList<ActorParticipantConfiguration> Participants
    {
        get;
        init;
    }

    public PublicMatchTopology? Topology { get; init; }
}

/// <summary>
/// Completed experimental Frontline run. Replay v2 remains internal while its
/// future entity-action variants are still evolving, so callers receive the
/// canonical wire document and hash rather than a public mutable DTO graph.
/// </summary>
public sealed record FrontlineActorMatchRunResult
{
    public required FrontlineMatchResult Result { get; init; }
    public required string ReplayJson { get; init; }
    public required string ReplayHash { get; init; }
    internal ReplayV2 Replay { get; init; } = null!;
}

/// <summary>
/// Stable actor-host failure codes. The envelope deliberately excludes raw
/// exception text so artifact-controlled diagnostics never become contract
/// data.
/// </summary>
public static class FrontlineActorHostFaultCodes
{
    public const string RuntimeCreateFailed = "runtime-create-failed";
    public const string RuntimeInstanceReused = "runtime-instance-reused";
    public const string RuntimeStartFailed = "runtime-start-failed";
    public const string RuntimeExecuteFailed = "runtime-execute-failed";
    public const string DecisionRejected = "decision-rejected";
}

public enum FrontlineActorHostStage
{
    CreateRuntime = 0,
    StartLife = 1,
    ExecuteTick = 2,
    ValidateDecision = 3,
}

/// <summary>Deterministic attribution for one aborted actor-host operation.</summary>
public sealed record FrontlineActorHostFault
{
    public required int SchemaVersion { get; init; }
    public required string Code { get; init; }
    public required FrontlineActorHostStage Stage { get; init; }
    public required int ParticipantId { get; init; }
    public required ActorIdentity ActorId { get; init; }
    public required int Tick { get; init; }
}

/// <summary>
/// Diagnostic evidence for an aborted match. The replay is a hashless prefix
/// containing only ticks that fully resolved before the failing operation.
/// </summary>
public sealed record FrontlineActorMatchFailure
{
    public required FrontlineActorHostFault Fault { get; init; }
    public required string PartialReplayJson { get; init; }
}

public abstract record FrontlineActorMatchAttempt;

public sealed record FrontlineActorMatchCompleted(
    FrontlineActorMatchRunResult Run) : FrontlineActorMatchAttempt;

public sealed record FrontlineActorMatchFailed(
    FrontlineActorMatchFailure Failure) : FrontlineActorMatchAttempt
{
    internal FrontlineActorHostException? DiagnosticException { get; init; }
}

/// <summary>
/// A rejected experimental runtime operation. Package 4 deliberately aborts
/// instead of silently turning malformed output into Wait; a deterministic
/// unit/team fault policy is frozen with replication before public routing.
/// </summary>
public sealed class FrontlineActorHostException : Exception
{
    public FrontlineActorHostException(
        ActorIdentity actorId,
        int tick,
        FrontlineActorHostStage stage,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ActorId = actorId;
        Tick = tick;
        Stage = stage;
    }

    internal FrontlineActorHostException(
        ActorIdentity actorId,
        int participantId,
        int tick,
        FrontlineActorHostStage stage,
        string code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ActorId = actorId;
        ParticipantId = participantId;
        Tick = tick;
        Stage = stage;
        Code = code;
    }

    internal FrontlineActorHostException(
        FrontlineActorMatchFailure failure,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
        ActorId = failure.Fault.ActorId;
        ParticipantId = failure.Fault.ParticipantId;
        Tick = failure.Fault.Tick;
        Stage = failure.Fault.Stage;
        Code = failure.Fault.Code;
    }

    public ActorIdentity ActorId { get; }
    public int? ParticipantId { get; }
    public int Tick { get; }
    public FrontlineActorHostStage Stage { get; }
    public string? Code { get; }
    public FrontlineActorMatchFailure? Failure { get; }
}

/// <summary>
/// Prime-only vertical slice joining prepared Frontline ticks, canonical actor
/// observations, one isolated runtime per life, and canonical replay v2.
/// Runtime factories remain caller-owned; every life instance created from
/// them is disposed by this host.
/// </summary>
public sealed class FrontlineActorMatchEngine
{
    public FrontlineActorMatchRunResult Run(
        FrontlineActorMatchConfiguration configuration)
    {
        FrontlineActorMatchAttempt attempt = RunAttempt(configuration);
        return attempt switch
        {
            FrontlineActorMatchCompleted completed => completed.Run,
            FrontlineActorMatchFailed failed =>
                throw failed.DiagnosticException
                    ?? new FrontlineActorHostException(
                        failed.Failure,
                        "Frontline actor match aborted.",
                        innerException: null),
            _ => throw new InvalidOperationException(
                "Unknown Frontline actor match attempt result."),
        };
    }

    /// <summary>
    /// Executes an actor match while retaining deterministic diagnostic
    /// evidence for actor-host failures. Invalid configuration and engine
    /// invariant failures still throw normally.
    /// </summary>
    public FrontlineActorMatchAttempt RunAttempt(
        FrontlineActorMatchConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.Map);
        ArgumentNullException.ThrowIfNull(configuration.Rules);
        ArgumentNullException.ThrowIfNull(configuration.Participants);

        ActorParticipantConfiguration[] participants =
            configuration.Participants.ToArray();
        ResolvedMatchDefinition definition = configuration.Topology is null
            ? MatchDefinitionResolver.Resolve(
                configuration.Rules,
                configuration.Map)
            : MatchDefinitionResolver.Resolve(
                configuration.Rules,
                configuration.Map,
                configuration.Topology);
        if (!definition.IsFrontline)
        {
            throw new ArgumentException(
                "FrontlineActorMatchEngine requires Frontline rules and map geometry.",
                nameof(configuration));
        }
        if (definition.Rules.MaxDebugBytesPerTick < 0
            || definition.Rules.MaxDebugBytesPerMatch < 0)
        {
            throw new ArgumentException(
                "Frontline debug byte limits cannot be negative.",
                nameof(configuration));
        }

        Dictionary<int, ActorParticipantConfiguration> participantsById =
            ValidateParticipants(definition.Topology, participants);
        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(
                definition.Rules,
                definition.Map,
                definition.Topology);
        ReplayV2Header header = ReplayV2Projection.Header(
            configuration.Seed,
            contract,
            definition.Map.ThemeId,
            definition.Map.Presentation,
            participants);
        var session = new FrontlineMatchSession(definition);
        var observationProjector = new FrontlineObservationProjector();
        var replayTicks = ImmutableArray.CreateBuilder<ReplayV2Tick>();
        IReadOnlyList<FrontlineMatchEvent> priorResolvedEvents =
            Array.Empty<FrontlineMatchEvent>();
        var liveRuntimes =
            new Dictionary<ActorIdentity, LiveRuntime>();
        var issuedRuntimeInstances = new HashSet<IActorRuntime>(
            ReferenceEqualityComparer.Instance);
        Dictionary<int, int> debugBudgets = participantsById.Keys
            .ToDictionary(
                participantId => participantId,
                _ => definition.Rules.MaxDebugBytesPerMatch);

        try
        {
            while (!session.IsCompleted)
            {
                FrontlineTickStart tickStart = session.PrepareTick();
                ActorObservationFrame frame =
                    observationProjector.Project(
                        session.State,
                        tickStart,
                        priorResolvedEvents,
                        contract);

                // Both replay projections happen before any artifact code runs.
                // The replay therefore snapshots the same immutable information
                // delivered to runtimes, plus a separate omniscient pre-state.
                ReplayV2TickStart replayTickStart =
                    ReplayV2Projection.TickStart(tickStart, session.State);
                Dictionary<ActorIdentity, ActorObservation> observations =
                    frame.Actors.ToDictionary(
                        observation => observation.Self.ActorId);
                Dictionary<ActorIdentity, ReplayV2ActorObservation>
                    replayObservations = frame.Actors.ToDictionary(
                        observation => observation.Self.ActorId,
                        ReplayV2Projection.Observation);
                Dictionary<ActorIdentity, ReplayV2ObservationAliases>
                    replayAliases = frame.ReplayAliases.ToDictionary(
                        aliases => aliases.ActorId,
                        ReplayV2Projection.ObservationAliases);
                Dictionary<ActorIdentity, ActorMatchStart> lifeStarts =
                    PrepareLifeStarts(
                        definition,
                        contract,
                        configuration.Seed,
                        tickStart,
                        observations.Keys,
                        participantsById,
                        liveRuntimes);
                Dictionary<ActorIdentity, ReplayV2LifeStart>
                    replayLifeStarts = lifeStarts.ToDictionary(
                        pair => pair.Key,
                        pair => ReplayV2Projection.LifeStart(pair.Value));

                EnsureLifeRuntimes(
                    tickStart,
                    observations.Keys,
                    lifeStarts,
                    participantsById,
                    liveRuntimes,
                    issuedRuntimeInstances);

                var runtimeReplies = new Dictionary<
                    ActorIdentity,
                    ActorDecision>();
                var acceptedDecisions = new Dictionary<
                    ActorIdentity,
                    ActorDecision>();
                var primeDecisions = new Dictionary<
                    FrontlineActorId,
                    BotDecision>();
                foreach (ActorIdentity actorId in observations.Keys.Order())
                {
                    ActorDecision raw;
                    try
                    {
                        raw = liveRuntimes[actorId]
                            .Runtime
                            .ExecuteTick(observations[actorId])
                            ?? throw new InvalidOperationException(
                                "Actor runtime returned a null decision.");
                    }
                    catch (Exception exception)
                    {
                        throw HostFailure(
                            actorId,
                            liveRuntimes[actorId].ParticipantId,
                            frame.Tick,
                            FrontlineActorHostStage.ExecuteTick,
                            FrontlineActorHostFaultCodes
                                .RuntimeExecuteFailed,
                            exception);
                    }

                    ActorDecision canonical;
                    int remainingDiagnosticBytesThisTick =
                        definition.Rules.MaxDebugBytesPerTick;
                    try
                    {
                        canonical = ActorDecisionAdapter.Normalize(
                            raw,
                            contract);
                        canonical = ApplyDebugBudget(
                            canonical,
                            debugBudgets,
                            liveRuntimes[actorId].ParticipantId,
                            ref remainingDiagnosticBytesThisTick);
                        primeDecisions.Add(
                            actorId.ToFrontline(),
                            ActorDecisionAdapter.ToPrimeDecision(
                                canonical,
                                contract));
                    }
                    catch (Exception exception)
                    {
                        throw HostFailure(
                            actorId,
                            liveRuntimes[actorId].ParticipantId,
                            frame.Tick,
                            FrontlineActorHostStage.ValidateDecision,
                            FrontlineActorHostFaultCodes.DecisionRejected,
                            exception);
                    }

                    // The replay preserves the runtime's selector/payload
                    // shape separately from the accepted canonical decision.
                    // Diagnostic text crosses the host byte-budget boundary
                    // before capture so an in-process runtime cannot inflate
                    // replay through either debug or stale fault text.
                    runtimeReplies.Add(
                        actorId,
                        raw with
                        {
                            DebugMessage = canonical.DebugMessage,
                            FaultMessage = ApplyTextBudget(
                                raw.FaultMessage,
                                debugBudgets,
                                liveRuntimes[actorId].ParticipantId,
                                ref remainingDiagnosticBytesThisTick),
                        });
                    acceptedDecisions.Add(actorId, canonical);
                }

                FrontlineStepResult step = session.Step(primeDecisions);
                Dictionary<ActorIdentity, FrontlineActionResolution>
                    resolutions = step.ActionResolutions.ToDictionary(
                        resolution =>
                            ActorIdentity.FromFrontline(resolution.ActorId));
                ImmutableArray<ReplayV2ActorTurn> actorTurns =
                    observations.Keys
                        .Order()
                        .Select(actorId => new ReplayV2ActorTurn(
                            ReplayActorId(actorId),
                            replayLifeStarts.GetValueOrDefault(actorId),
                            replayObservations[actorId],
                            replayAliases[actorId],
                            ReplayV2Projection.Decision(
                                runtimeReplies[actorId]),
                            ReplayV2Projection.Decision(
                                acceptedDecisions[actorId]),
                            ReplayV2Projection.ActionResolution(
                                resolutions[actorId])))
                        .ToImmutableArray();
                replayTicks.Add(new ReplayV2Tick(
                    step.Tick,
                    replayTickStart,
                    actorTurns,
                    ReplayV2Projection.Resolution(step),
                    ReplayV2Projection.WorldState(session.State)));

                DisposeInactiveLives(session.State, liveRuntimes);
                priorResolvedEvents = step.Events;
            }

            FrontlineMatchResult result = session.Result
                ?? throw new InvalidOperationException(
                    "Completed Frontline session has no result.");
            var replay = new ReplayV2(
                header,
                replayTicks.ToImmutable(),
                ReplayV2Projection.Result(result));
            var run = new FrontlineActorMatchRunResult
            {
                Replay = replay,
                Result = result,
                ReplayHash = ReplayV2Serializer.ComputeHash(replay),
                ReplayJson = ReplayV2Serializer.ToJson(replay),
            };
            return new FrontlineActorMatchCompleted(run);
        }
        catch (FrontlineActorHostException exception)
        {
            var failure = new FrontlineActorMatchFailure
            {
                Fault = new FrontlineActorHostFault
                {
                    SchemaVersion =
                        BotArenaVersions.ActorHostFaultSchemaVersion,
                    Code = exception.Code
                        ?? FaultCode(exception.Stage),
                    Stage = exception.Stage,
                    ParticipantId = exception.ParticipantId
                        ?? ParticipantForActor(
                            definition.Topology,
                            exception.ActorId),
                    ActorId = exception.ActorId,
                    Tick = exception.Tick,
                },
                PartialReplayJson = ReplayV2Serializer.ToPartialJson(
                    header,
                    replayTicks.ToImmutable()),
            };
            return new FrontlineActorMatchFailed(failure)
            {
                DiagnosticException = new FrontlineActorHostException(
                    failure,
                    exception.Message,
                    exception),
            };
        }
        finally
        {
            foreach (LiveRuntime life in liveRuntimes.Values)
                SafeDispose(life.Runtime);
            liveRuntimes.Clear();
        }
    }

    private static Dictionary<int, ActorParticipantConfiguration>
        ValidateParticipants(
            PublicMatchTopology topology,
            IReadOnlyList<ActorParticipantConfiguration> participants)
    {
        if (participants.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Actor participant entries cannot be null.",
                nameof(participants));
        }

        Dictionary<int, ActorParticipantConfiguration> actual;
        try
        {
            actual = participants.ToDictionary(
                participant => participant.ParticipantId);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "Actor participant IDs must be unique.",
                nameof(participants),
                exception);
        }

        int[] expectedIds = topology.Participants
            .Select(participant => participant.ParticipantId)
            .Order()
            .ToArray();
        if (!actual.Keys.Order().SequenceEqual(expectedIds))
        {
            throw new ArgumentException(
                "Actor participants must exactly match the resolved topology.",
                nameof(participants));
        }

        foreach (PublicParticipant expected in topology.Participants)
        {
            ActorParticipantConfiguration participant =
                actual[expected.ParticipantId];
            if (participant.TeamId != expected.TeamId)
            {
                throw new ArgumentException(
                    $"Participant {expected.ParticipantId} must belong to team " +
                    $"{expected.TeamId}.",
                    nameof(participants));
            }
            if (participant.RuntimeFactory is null)
            {
                throw new ArgumentException(
                    $"Participant {expected.ParticipantId} has no runtime factory.",
                    nameof(participants));
            }
            if (string.IsNullOrWhiteSpace(participant.Name)
                || string.IsNullOrWhiteSpace(participant.RuntimeKind)
                || string.IsNullOrWhiteSpace(participant.ArtifactHash)
                || string.IsNullOrWhiteSpace(participant.Accent))
            {
                throw new ArgumentException(
                    $"Participant {expected.ParticipantId} has invalid replay provenance.",
                    nameof(participants));
            }
        }

        return actual;
    }

    private static Dictionary<ActorIdentity, ActorMatchStart>
        PrepareLifeStarts(
        ResolvedMatchDefinition definition,
        PublicMatchContractManifest contract,
        ulong matchSeed,
        FrontlineTickStart tickStart,
        IEnumerable<ActorIdentity> actorIds,
        IReadOnlyDictionary<int, ActorParticipantConfiguration>
            participantsById,
        IReadOnlyDictionary<ActorIdentity, LiveRuntime> liveRuntimes)
    {
        HashSet<FrontlineActorId> respawned =
            tickStart.RespawnedActors.ToHashSet();
        var starts = new Dictionary<ActorIdentity, ActorMatchStart>();
        foreach (ActorIdentity actorId in actorIds.Order())
        {
            if (liveRuntimes.ContainsKey(actorId))
                continue;

            PublicUnitSlot unit = definition.Topology.UnitSlots.Single(
                candidate =>
                    candidate.TeamId == actorId.TeamId
                    && candidate.UnitId == actorId.UnitId);
            ActorParticipantConfiguration participant =
                participantsById[unit.ControllerParticipantId];
            ActorSpawnReason reason =
                respawned.Contains(actorId.ToFrontline())
                    ? ActorSpawnReason.Respawn
                    : actorId.LifeId == 0 && tickStart.Tick == 0
                        ? ActorSpawnReason.Initial
                        : throw new InvalidOperationException(
                            "An active life has no matching spawn event.");
            starts.Add(actorId, new ActorMatchStart
            {
                SchemaVersion =
                    BotArenaVersions.ActorMatchStartSchemaVersion,
                RuntimeContractVersion =
                    BotArenaVersions.ActorRuntimeContractVersion,
                ActorId = actorId,
                ParticipantId = participant.ParticipantId,
                ActorRandomSeed = SeedDerivation.DeriveActorSeed(
                    matchSeed,
                    actorId,
                    definition.Rules.SeedProfile
                    ?? definition.Rules.RulesVersion),
                SpawnReason = reason,
                Contract = contract,
            });
        }
        return starts;
    }

    private static void EnsureLifeRuntimes(
        FrontlineTickStart tickStart,
        IEnumerable<ActorIdentity> actorIds,
        IReadOnlyDictionary<ActorIdentity, ActorMatchStart> lifeStarts,
        IReadOnlyDictionary<int, ActorParticipantConfiguration>
            participantsById,
        IDictionary<ActorIdentity, LiveRuntime> liveRuntimes,
        ISet<IActorRuntime> issuedRuntimeInstances)
    {
        foreach (ActorIdentity actorId in actorIds.Order())
        {
            if (liveRuntimes.ContainsKey(actorId))
                continue;

            if (!lifeStarts.TryGetValue(
                    actorId,
                    out ActorMatchStart? start))
            {
                throw new InvalidOperationException(
                    $"Active actor {actorId} has neither a live runtime nor a prepared life start.");
            }
            ActorParticipantConfiguration participant =
                participantsById[start.ParticipantId];
            IActorRuntime runtime;
            try
            {
                runtime = participant.RuntimeFactory.CreateRuntime()
                    ?? throw new InvalidOperationException(
                        "Actor runtime factory returned null.");
            }
            catch (Exception exception)
            {
                throw HostFailure(
                    actorId,
                    participant.ParticipantId,
                    tickStart.Tick,
                    FrontlineActorHostStage.CreateRuntime,
                    FrontlineActorHostFaultCodes.RuntimeCreateFailed,
                    exception);
            }

            if (!issuedRuntimeInstances.Add(runtime))
            {
                throw new FrontlineActorHostException(
                    actorId,
                    participant.ParticipantId,
                    tickStart.Tick,
                    FrontlineActorHostStage.CreateRuntime,
                    FrontlineActorHostFaultCodes.RuntimeInstanceReused,
                    "A runtime factory reused an actor-life instance.");
            }

            try
            {
                runtime.StartLife(start);
                liveRuntimes.Add(
                    actorId,
                    new LiveRuntime(
                        participant.ParticipantId,
                        runtime));
            }
            catch (Exception exception)
            {
                SafeDispose(runtime);
                throw HostFailure(
                    actorId,
                    participant.ParticipantId,
                    tickStart.Tick,
                    FrontlineActorHostStage.StartLife,
                    FrontlineActorHostFaultCodes.RuntimeStartFailed,
                    exception);
            }
        }
    }

    private static void DisposeInactiveLives(
        FrontlineMatchState state,
        IDictionary<ActorIdentity, LiveRuntime> liveRuntimes)
    {
        HashSet<ActorIdentity> active = state.Teams
            .SelectMany(team => team.Units)
            .Where(unit => unit.ActiveLife is not null)
            .Select(unit =>
                ActorIdentity.FromFrontline(unit.ActiveLife!.ActorId))
            .ToHashSet();
        foreach (ActorIdentity actorId in liveRuntimes.Keys
                     .Where(actorId => !active.Contains(actorId))
                     .ToArray())
        {
            SafeDispose(liveRuntimes[actorId].Runtime);
            liveRuntimes.Remove(actorId);
        }
    }

    private static ActorDecision ApplyDebugBudget(
        ActorDecision decision,
        IDictionary<int, int> remainingByParticipant,
        int participantId,
        ref int remainingThisTick)
    {
        string? message = ApplyTextBudget(
            decision.DebugMessage,
            remainingByParticipant,
            participantId,
            ref remainingThisTick);
        return decision with { DebugMessage = message };
    }

    private static string? ApplyTextBudget(
        string? value,
        IDictionary<int, int> remainingByParticipant,
        int participantId,
        ref int remainingThisTick)
    {
        if (value is null)
            return null;

        int remaining = remainingByParticipant[participantId];
        if (remaining <= 0 || remainingThisTick <= 0)
            return null;

        string bounded = TruncateUtf8(
            value,
            Math.Min(remainingThisTick, remaining));
        int used = Encoding.UTF8.GetByteCount(bounded);
        remainingByParticipant[participantId] = remaining - used;
        remainingThisTick -= used;
        return bounded;
    }

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
            return value;

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        int length = maxBytes;
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
            length--;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private static FrontlineActorHostException HostFailure(
        ActorIdentity actorId,
        int participantId,
        int tick,
        FrontlineActorHostStage stage,
        string code,
        Exception exception) =>
        new(
            actorId,
            participantId,
            tick,
            stage,
            code,
            $"Actor {actorId} failed during {stage} at tick {tick}: " +
            $"{exception.GetType().Name}: {exception.Message}",
            exception);

    private static string FaultCode(FrontlineActorHostStage stage) =>
        stage switch
        {
            FrontlineActorHostStage.CreateRuntime =>
                FrontlineActorHostFaultCodes.RuntimeCreateFailed,
            FrontlineActorHostStage.StartLife =>
                FrontlineActorHostFaultCodes.RuntimeStartFailed,
            FrontlineActorHostStage.ExecuteTick =>
                FrontlineActorHostFaultCodes.RuntimeExecuteFailed,
            FrontlineActorHostStage.ValidateDecision =>
                FrontlineActorHostFaultCodes.DecisionRejected,
            _ => throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "Unknown actor-host stage."),
        };

    private static int ParticipantForActor(
        PublicMatchTopology topology,
        ActorIdentity actorId) =>
        topology.UnitSlots.Single(
            unit =>
                unit.TeamId == actorId.TeamId
                && unit.UnitId == actorId.UnitId)
            .ControllerParticipantId;

    private static ReplayV2ActorId ReplayActorId(ActorIdentity actorId) =>
        new(actorId.TeamId, actorId.UnitId, actorId.LifeId);

    private static void SafeDispose(IDisposable value)
    {
        try
        {
            value.Dispose();
        }
        catch
        {
            // Disposal cannot change an already resolved deterministic match.
        }
    }

    private sealed record LiveRuntime(
        int ParticipantId,
        IActorRuntime Runtime);
}
