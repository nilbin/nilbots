using System.Diagnostics;
using System.Security.Cryptography;
using BotArena.App.ArcRelay;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// Hosted executor for identity-pinned generic actor matches. It deliberately
/// does not resolve a current playlist or route through the historical Duel
/// engine.
/// </summary>
public sealed class GenericActorMatchExecutor(
    AppDbContext db,
    IObjectStore objectStore,
    MatchReplayWriter replayWriter,
    HostedGenericMatchDefinitionRegistry definitions,
    ArcRelayLegacySnapshotCodec legacySheetCodec,
    ArcRelayClassCatalog classCatalog,
    MatchExecutionSettings settings,
    TimeProvider timeProvider,
    ILogger<GenericActorMatchExecutor> logger)
{
    public async Task<JobExecutionResult> HandleAsync(
        Match match,
        PlaylistVersion playlistVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(playlistVersion);

        match.Status = MatchStatus.Running;
        match.Error = null;
        await db.SaveChangesAsync(cancellationToken);

        Playlist playlist = await db.Playlists
            .AsNoTracking()
            .SingleAsync(
                candidate =>
                    candidate.Id == playlistVersion.PlaylistId,
                cancellationToken);
        IHostedGenericMatchDefinition expected =
            definitions.Resolve(
                playlist.Key,
                playlistVersion.Version);
        expected.Validate(playlist, playlistVersion);
        if (!string.Equals(
                expected.ExecutionEngineVersion,
                BotArenaVersions.GenericActorEngineVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Hosted generic playlist '{expected.PlaylistKey}' " +
                $"v{expected.Version} requires engine " +
                $"'{expected.ExecutionEngineVersion}', but this worker " +
                $"provides '{BotArenaVersions.GenericActorEngineVersion}'.");
        }
        List<MatchParticipant> participants =
            match.Participants
                .OrderBy(participant => participant.Slot)
                .ToList();

        var legacySheetCompilations =
            new ArcRelaySheetCompilation?[participants.Count];
        var tacticalSheetSnapshots =
            new TacticalSheetSnapshot?[participants.Count];
        bool tacticalSheets = expected is ArcRelayEntrantPlaylistDefinition
        {
            PlaylistVersion: ArcRelayEntrantPlaylistDefinition.Version,
        };
        HostedGenericParticipantInput[] participantInputs = participants
            .Select((participant, index) =>
            {
                IReadOnlyList<string> classes = [];
                if (expected.RuntimeModel == HostedGenericRuntimeModel.TrustedStockMind ||
                    expected.RuntimeModel == HostedGenericRuntimeModel.ArcRelayEntrants)
                {
                    if (string.Equals(participant.EntrantKindSnapshot, "mind", StringComparison.Ordinal))
                    {
                        if (participant.CompositionSnapshot is not { } compositionJson ||
                            participant.CompositionHashSnapshot is not { } compositionHash)
                            throw new InvalidOperationException("Custom mind has no composition snapshot.");
                        ArcRelayCompositionCompilation composition = ArcRelayComposition.Compile(
                            ArcRelayComposition.Read(compositionJson),
                            classCatalog,
                            classCatalog.All.Select(value => value.Id).ToHashSet(StringComparer.Ordinal));
                        if (!string.Equals(composition.ContentHash, compositionHash, StringComparison.Ordinal))
                            throw new InvalidOperationException("Custom mind composition snapshot failed verification.");
                        classes = composition.ClassIds;
                    }
                    else
                    {
                        if (tacticalSheets)
                        {
                            TacticalSheetSnapshot snapshot =
                                ValidateTacticalSheetSnapshot(participant);
                            tacticalSheetSnapshots[index] = snapshot;
                            classes = snapshot.Classes;
                        }
                        else
                        {
                            ArcRelaySheetCompilation compilation =
                                ValidateLegacySheetSnapshot(participant);
                            legacySheetCompilations[index] = compilation;
                            classes = compilation.Classes;
                        }
                    }
                }
                return new HostedGenericParticipantInput(
                    participant.Slot,
                    participant.TeamId
                        ?? throw new InvalidOperationException(
                            $"Participant {participant.Slot} has no team snapshot."),
                    classes);
            })
            .ToArray();
        ActorResolvedMatchDefinition resolvedDefinition =
            expected.ResolveMatch(participantInputs);
        ValidatePinnedMatch(match, playlistVersion, resolvedDefinition);
        ValidateParticipants(resolvedDefinition, participants);

        var versions = new List<BotVersion>(participants.Count);
        var modulePaths = new string?[participants.Count];
        foreach (MatchParticipant participant in participants)
        {
            BotVersion version = await db.BotVersions.SingleAsync(
                candidate =>
                    candidate.Id == participant.BotVersionId,
                cancellationToken);
            if (version.Status != BuildStatus.Built ||
                version.ArtifactKey is null ||
                version.ArtifactHash is null)
            {
                throw new InvalidOperationException(
                    $"Bot version {version.Id} has no built artifact.");
            }
            if (version.SupportedContractProfiles is null ||
                !version.SupportedContractProfiles.Contains(
                    expected.AdmissionPolicyId,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Bot version {version.Id} does not support required " +
                    $"contract profile '{expected.AdmissionPolicyId}'.");
            }
            if (!string.Equals(
                    version.ArtifactHash,
                    participant.ArtifactHashSnapshot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Participant {participant.Slot} artifact snapshot " +
                    "does not match its pinned bot version.");
            }

            versions.Add(version);
            if (expected.RuntimeModel == HostedGenericRuntimeModel.SubmittedActorWasm ||
                (expected.RuntimeModel == HostedGenericRuntimeModel.ArcRelayEntrants &&
                 string.Equals(participant.EntrantKindSnapshot, "mind", StringComparison.Ordinal)))
            {
                modulePaths[versions.Count - 1] = await objectStore.MaterializeAsync(
                    version.ArtifactKey,
                    version.ArtifactHash,
                    cancellationToken);
            }
        }

        var factories = new List<IDisposable>(participants.Count);
        GenericActorMatchResult result;
        ReadOnlyMemory<byte> replayBytes;
        string replayHash;
        int replayFormatVersion;
        TimeSpan simulationElapsed;
        TimeSpan replayElapsed;
        ArcRelayDegeneracyRead? degeneracy = null;
        try
        {
            GenericActorParticipantConfiguration[] configurations;
            if (expected.RuntimeModel == HostedGenericRuntimeModel.SubmittedActorWasm)
            {
                var actorFactories = new WasmGenericActorRuntimeFactory[participants.Count];
                for (int index = 0; index < participants.Count; index++)
                {
                    var factory = new WasmGenericActorRuntimeFactory(
                        new WasmRuntimeOptions
                        {
                            ModulePath = modulePaths[index]!,
                            BotName = versions[index].GuestBotName ?? "",
                        });
                    if (!string.Equals(
                        factory.ArtifactHash,
                        participants[index].ArtifactHashSnapshot,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        factory.Dispose();
                        throw new InvalidOperationException(
                            $"Materialized artifact for participant " +
                            $"{participants[index].Slot} does not match its " +
                            "pinned snapshot.");
                    }
                    actorFactories[index] = factory;
                    factories.Add(factory);
                }
                configurations = participants.Select((participant, index) =>
                    new GenericActorParticipantConfiguration
                    {
                        ParticipantId = participant.Slot,
                        TeamId = participant.TeamId!.Value,
                        Name = participant.NameSnapshot,
                        RuntimeFactory = actorFactories[index],
                        RuntimeKind = "wasm",
                        ArtifactHash = participant.ArtifactHashSnapshot,
                        Accent = participant.AccentSnapshot,
                        LookId = participant.LookIdSnapshot,
                        ProjectileLookId = participant.ProjectileLookIdSnapshot,
                    }).ToArray();
            }
            else if (expected.RuntimeModel == HostedGenericRuntimeModel.TrustedStockMind)
            {
                var mindFactories = new InProcessGenericMindRuntimeFactory[participants.Count];
                for (int index = 0; index < participants.Count; index++)
                {
                    var factory = new InProcessGenericMindRuntimeFactory(
                        static () => new global::ArcRelayStockMind(),
                        trustedArcRelayStockProjection: true);
                    mindFactories[index] = factory;
                    factories.Add(factory);
                }
                configurations = participants.Select((participant, index) =>
                {
                    ArcRelaySheetCompilation compilation = legacySheetCompilations[index]
                        ?? throw new InvalidOperationException("Trusted stock mind has no sheet snapshot.");
                    return new GenericActorParticipantConfiguration
                    {
                        ParticipantId = participant.Slot,
                        TeamId = participant.TeamId!.Value,
                        Name = participant.NameSnapshot,
                        MindRuntimeFactory = mindFactories[index],
                        RuntimeKind = "trusted-stock-in-process-v1",
                        ArtifactHash = participant.ArtifactHashSnapshot,
                        MindDataHash = compilation.ContentHash,
                        MindEvaluationData = [.. compilation.LinkedData],
                        Accent = participant.AccentSnapshot,
                        LookId = participant.LookIdSnapshot,
                        ProjectileLookId = participant.ProjectileLookIdSnapshot,
                    };
                }).ToArray();
            }
            else
            {
                bool strategyMind = expected is ArcRelayEntrantPlaylistDefinition
                {
                    PlaylistVersion: >= ArcRelayEntrantPlaylistDefinition.ForwardVersion
                        and <= ArcRelayEntrantPlaylistDefinition.PreviousVersion,
                };
                configurations = new GenericActorParticipantConfiguration[participants.Count];
                for (int index = 0; index < participants.Count; index++)
                {
                    MatchParticipant participant = participants[index];
                    if (string.Equals(participant.EntrantKindSnapshot, "mind", StringComparison.Ordinal))
                    {
                        var factory = new WasmGenericMindRuntimeFactory(new WasmMindRuntimeOptions
                        {
                            ModulePath = modulePaths[index]!,
                            BotName = versions[index].GuestBotName ?? "",
                        });
                        if (!string.Equals(factory.ArtifactHash, participant.ArtifactHashSnapshot, StringComparison.OrdinalIgnoreCase))
                        {
                            factory.Dispose();
                            throw new InvalidOperationException($"Materialized custom mind {participant.Slot} failed its artifact hash.");
                        }
                        factories.Add(factory);
                        configurations[index] = new GenericActorParticipantConfiguration
                        {
                            ParticipantId = participant.Slot,
                            TeamId = participant.TeamId!.Value,
                            Name = participant.NameSnapshot,
                            MindRuntimeFactory = factory,
                            RuntimeKind = "sandboxed-wasm-mind-v1",
                            ArtifactHash = participant.ArtifactHashSnapshot,
                            MindDataHash = participant.CompositionHashSnapshot,
                            Accent = participant.AccentSnapshot,
                            LookId = participant.LookIdSnapshot,
                            ProjectileLookId = participant.ProjectileLookIdSnapshot,
                        };
                    }
                    else
                    {
                        if (tacticalSheets)
                        {
                            var factory = new InProcessGenericMindRuntimeFactory(
                                static () => new global::ArcRelayTacticalPlaybookMind(),
                                trustedArcRelayStockProjection: true);
                            factories.Add(factory);
                            TacticalSheetSnapshot snapshot =
                                tacticalSheetSnapshots[index]
                                ?? throw new InvalidOperationException(
                                    "Tactical entrant has no sheet snapshot.");
                            configurations[index] =
                                new GenericActorParticipantConfiguration
                                {
                                    ParticipantId = participant.Slot,
                                    TeamId = participant.TeamId!.Value,
                                    Name = participant.NameSnapshot,
                                    MindRuntimeFactory = factory,
                                    RuntimeKind =
                                        "trusted-tactical-playbook-in-process-v1",
                                    ArtifactHash =
                                        participant.ArtifactHashSnapshot,
                                    MindDataHash = snapshot.ContentHash,
                                    MindEvaluationData = [.. snapshot.LinkedData],
                                    Accent = participant.AccentSnapshot,
                                    LookId = participant.LookIdSnapshot,
                                    ProjectileLookId =
                                        participant.ProjectileLookIdSnapshot,
                                };
                        }
                        else
                        {
                            var factory = new InProcessGenericMindRuntimeFactory(
                                strategyMind
                                    ? static () =>
                                        new global::ArcRelayStrategyMind()
                                    : static () =>
                                        new global::ArcRelayStockMind(),
                                trustedArcRelayStockProjection: true);
                            factories.Add(factory);
                            ArcRelaySheetCompilation compilation =
                                legacySheetCompilations[index]
                                ?? throw new InvalidOperationException(
                                    "Historical entrant has no sheet snapshot.");
                            configurations[index] =
                                new GenericActorParticipantConfiguration
                                {
                                    ParticipantId = participant.Slot,
                                    TeamId = participant.TeamId!.Value,
                                    Name = participant.NameSnapshot,
                                    MindRuntimeFactory = factory,
                                    RuntimeKind = "trusted-stock-in-process-v1",
                                    ArtifactHash =
                                        participant.ArtifactHashSnapshot,
                                    MindDataHash = compilation.ContentHash,
                                    MindEvaluationData =
                                        [.. compilation.LinkedData],
                                    Accent = participant.AccentSnapshot,
                                    LookId = participant.LookIdSnapshot,
                                    ProjectileLookId =
                                        participant.ProjectileLookIdSnapshot,
                                };
                        }
                    }
                }
            }
            using var session = new GenericActorMatchSession(
                resolvedDefinition,
                configurations,
                unchecked((ulong)match.Seed),
                // Arc Relay's compact broadcast is projected directly while the session
                // runs. Keeping the full evaluator chronology as well duplicates the
                // dominant allocation/tick cost and is neither part of the canonical
                // document nor needed for custom-mind fault accounting.
                recordChronology:
                    expected.RuntimeModel == HostedGenericRuntimeModel.SubmittedActorWasm);
            if (expected.RuntimeModel is HostedGenericRuntimeModel.TrustedStockMind or HostedGenericRuntimeModel.ArcRelayEntrants)
            {
                ArcRelayBroadcastDocument broadcast =
                    ArcRelayBroadcastDocument.CreateAndRun(
                        session,
                        expected.ReplayPresentation);
                result = broadcast.Result;
                replayBytes = broadcast.CanonicalUtf8;
                replayHash = broadcast.ReplayHash;
                replayFormatVersion = ArcRelayBroadcastDocument.FormatVersion;
                simulationElapsed = broadcast.SimulationElapsed;
                replayElapsed = broadcast.ProjectionElapsed;
                if (string.Equals(match.ArcRelayLane, ArcRelayMatchLane.Ranked, StringComparison.Ordinal))
                    degeneracy = ArcRelayFeltDegeneracyDetector.Analyze(replayBytes);
            }
            else
            {
                long simulationStarted = Stopwatch.GetTimestamp();
                result = session.Run();
                simulationElapsed = Stopwatch.GetElapsedTime(simulationStarted);
                long replayStarted = Stopwatch.GetTimestamp();
                GenericActorReplayDocument replay =
                    GenericActorReplayDocument.Create(
                        session,
                        expected.ReplayPresentation);
                replayElapsed = Stopwatch.GetElapsedTime(replayStarted);
                replayBytes = replay.CanonicalUtf8;
                replayHash = replay.ReplayHash;
                replayFormatVersion =
                    BotArenaVersions.GenericActorReplayFormatVersion;
            }
            // The session now owns and disposes every factory.
            factories.Clear();
        }
        finally
        {
            foreach (IDisposable factory in factories)
                factory.Dispose();
        }

        long writeStarted = Stopwatch.GetTimestamp();
        int storedReplayBytes;
        if (replayFormatVersion == ArcRelayBroadcastDocument.FormatVersion)
        {
            CompressedReplayWrite compressed =
                await replayWriter.WriteGzipJsonAsync(
                    match.Id,
                    replayBytes,
                    cancellationToken);
            match.ReplayKey = compressed.Key;
            storedReplayBytes = compressed.StoredBytes;
        }
        else
        {
            match.ReplayKey = await replayWriter.WriteCanonicalJsonAsync(
                match.Id,
                replayBytes,
                cancellationToken);
            storedReplayBytes = replayBytes.Length;
        }
        TimeSpan writeElapsed = Stopwatch.GetElapsedTime(writeStarted);
        match.ReplayHash = replayHash;
        match.ReplayFormatVersion = replayFormatVersion;
        GenericMatchResultPersistence.Apply(match, result);
        match.Status = MatchStatus.Completed;
        DateTime completedAt =
            timeProvider.GetUtcNow().UtcDateTime;
        match.CompletedAt = completedAt;
        match.BroadcastStartedAt = completedAt.AddSeconds(
            settings.BroadcastDelaySeconds);
        match.PresentationTicksPerSecond =
            expected.PresentationTicksPerSecond
            ?? settings.BroadcastTicksPerSecond;

        if (string.Equals(match.ArcRelayLane, ArcRelayMatchLane.Preflight, StringComparison.Ordinal))
        {
            MatchParticipant candidate = participants[0];
            if (candidate.EntrantIdSnapshot is not Guid entrantId)
                throw new InvalidOperationException("Preflight candidate has no entrant identity snapshot.");
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(
                value => value.Id == entrantId,
                cancellationToken);
            ArcRelayPreflightSettlement.ApplyIfCurrent(
                entrant,
                match.Id,
                candidate.EntrantRevisionSnapshot
                    ?? throw new InvalidOperationException("Preflight candidate has no revision snapshot."),
                candidate.Faults.GetValueOrDefault(),
                completedAt);
        }
        if (string.Equals(match.ArcRelayLane, ArcRelayMatchLane.Ranked, StringComparison.Ordinal))
        {
            foreach (MatchParticipant participant in participants)
            {
                int teamId = participant.TeamId!.Value;
                if (degeneracy?.Tripped(teamId) != true || participant.EntrantIdSnapshot is not Guid entrantId)
                    continue;
                ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(value => value.Id == entrantId, cancellationToken);
                ArcRelayEntrantSuspension.Apply(
                    entrant, match.Id, degeneracy.ReasonsByTeam[teamId], completedAt);
            }
            double seconds = settings.BroadcastDelaySeconds +
                ((match.EndTick.GetValueOrDefault() + 1) / match.PresentationTicksPerSecond);
            db.BackgroundJobs.Add(BackgroundJob.SettleArcRelayRating(
                match.Id,
                completedAt.AddSeconds(seconds)));
        }

        long persistStarted = Stopwatch.GetTimestamp();
        await db.SaveChangesAsync(cancellationToken);
        TimeSpan persistElapsed = Stopwatch.GetElapsedTime(persistStarted);
        logger.LogInformation(
            "Executed hosted generic match {MatchId} via {RuntimeModel}: simulation {SimulationMs:F1} ms, replay projection {ReplayMs:F1} ms ({ReplayBytes} bytes, {StoredReplayBytes} stored), object write {WriteMs:F1} ms, result persistence {PersistMs:F1} ms",
            match.Id,
            expected.RuntimeModel,
            simulationElapsed.TotalMilliseconds,
            replayElapsed.TotalMilliseconds,
            replayBytes.Length,
            storedReplayBytes,
            writeElapsed.TotalMilliseconds,
            persistElapsed.TotalMilliseconds);
        return new JobExecutionResult("completed");

        ArcRelaySheetCompilation ValidateLegacySheetSnapshot(
            MatchParticipant participant)
        {
            if (participant.SheetIdSnapshot is not Guid sheetId
                || participant.SheetRevisionSnapshot is not int revision
                || participant.SheetHashSnapshot is not { } sheetHash
                || participant.SheetCanonicalJsonSnapshot is not { } canonicalJson
                || participant.MindDataSnapshot is not { } linkedData)
            {
                throw new InvalidOperationException(
                    $"Trusted-stock participant {participant.Slot} has no complete immutable sheet snapshot.");
            }
            IReadOnlySet<string> everyClass = classCatalog.All
                .Select(value => value.Id)
                .ToHashSet(StringComparer.Ordinal);
            ArcRelaySheetCompilation compilation = legacySheetCodec.Compile(
                legacySheetCodec.Read(canonicalJson),
                everyClass,
                $"{sheetId}:r{revision}");
            if (!string.Equals(compilation.ContentHash, sheetHash, StringComparison.Ordinal)
                || !compilation.LinkedData.AsSpan().SequenceEqual(linkedData))
            {
                throw new InvalidOperationException(
                    $"Trusted-stock participant {participant.Slot} sheet snapshot failed verification.");
            }
            return compilation;
        }

        TacticalSheetSnapshot ValidateTacticalSheetSnapshot(
            MatchParticipant participant)
        {
            if (participant.SheetIdSnapshot is not Guid
                || participant.SheetRevisionSnapshot is not int
                || participant.SheetHashSnapshot is not { } sheetHash
                || participant.SheetCanonicalJsonSnapshot is null
                || participant.SheetLayoutJsonSnapshot is null
                || participant.MindDataSnapshot is not { } linkedData
                || participant.CompositionSnapshot is not { } compositionJson
                || participant.CompositionHashSnapshot is not { } compositionHash)
            {
                throw new InvalidOperationException(
                    $"Tactical participant {participant.Slot} has no complete immutable sheet snapshot.");
            }
            ArcRelayCompositionCompilation composition =
                ArcRelayComposition.Compile(
                    ArcRelayComposition.Read(compositionJson),
                    classCatalog,
                    classCatalog.All.Select(value => value.Id)
                        .ToHashSet(StringComparer.Ordinal));
            if (!string.Equals(
                    composition.ContentHash,
                    compositionHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Tactical participant {participant.Slot} composition snapshot failed verification.");
            }
            string linkedHash = Convert.ToHexStringLower(
                SHA256.HashData(linkedData));
            if (!string.Equals(linkedHash, sheetHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Tactical participant {participant.Slot} linked package failed verification.");
            }
            return new TacticalSheetSnapshot(
                sheetHash, linkedData, composition.ClassIds);
        }
    }

    private sealed record TacticalSheetSnapshot(
        string ContentHash,
        byte[] LinkedData,
        IReadOnlyList<string> Classes);

    private static void ValidatePinnedMatch(
        Match match,
        PlaylistVersion playlistVersion,
        ActorResolvedMatchDefinition definition)
    {
        if (match.PlaylistVersionId != playlistVersion.Id ||
            !string.Equals(
                match.GameRulesVersion,
                definition.Rules.RulesetId,
                StringComparison.Ordinal) ||
            !string.Equals(
                match.MapId,
                definition.Map.Id,
                StringComparison.Ordinal) ||
            match.MapVersion != definition.Map.Version ||
            !string.Equals(
                match.RuntimeConfigurationVersion,
                definition.CapabilityVersions
                    .RuntimeConfigurationVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Match {match.Id} contradicts its immutable hosted " +
                "generic playlist.");
        }
    }

    private static void ValidateParticipants(
        ActorResolvedMatchDefinition definition,
        IReadOnlyList<MatchParticipant> participants)
    {
        Dictionary<int, int> expected =
            definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant => participant.TeamId);
        if (participants.Count != expected.Count ||
            participants.Any(participant =>
                !expected.TryGetValue(
                    participant.Slot,
                    out int teamId) ||
                participant.TeamId != teamId))
        {
            throw new InvalidOperationException(
                "Persisted generic participants do not match the resolved " +
                "topology.");
        }
    }
}
