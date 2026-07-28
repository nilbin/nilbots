using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
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
    MatchExecutionSettings settings,
    TimeProvider timeProvider)
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
        ValidatePinnedMatch(match, playlistVersion, expected.Match);

        List<MatchParticipant> participants =
            match.Participants
                .OrderBy(participant => participant.Slot)
                .ToList();
        ValidateParticipants(expected.Match, participants);

        var versions = new List<BotVersion>(participants.Count);
        var modulePaths = new List<string>(participants.Count);
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
            modulePaths.Add(
                await objectStore.MaterializeAsync(
                    version.ArtifactKey,
                    version.ArtifactHash,
                    cancellationToken));
        }

        var factories =
            new List<WasmGenericActorRuntimeFactory>(
                participants.Count);
        GenericActorMatchResult result;
        GenericActorReplayDocument replay;
        try
        {
            for (int index = 0;
                 index < participants.Count;
                 index++)
            {
                var factory =
                    new WasmGenericActorRuntimeFactory(
                        new WasmRuntimeOptions
                        {
                            ModulePath = modulePaths[index],
                            BotName =
                                versions[index].GuestBotName ?? "",
                        });
                if (!string.Equals(
                        factory.ArtifactHash,
                        participants[index]
                            .ArtifactHashSnapshot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    factory.Dispose();
                    throw new InvalidOperationException(
                        $"Materialized artifact for participant " +
                        $"{participants[index].Slot} does not match its " +
                        "pinned snapshot.");
                }
                factories.Add(factory);
            }

            GenericActorParticipantConfiguration[] configurations =
                participants
                    .Select((participant, index) =>
                        new GenericActorParticipantConfiguration
                        {
                            ParticipantId = participant.Slot,
                            TeamId = participant.TeamId!.Value,
                            Name = participant.NameSnapshot,
                            RuntimeFactory = factories[index],
                            RuntimeKind = "wasm",
                            ArtifactHash =
                                participant.ArtifactHashSnapshot,
                            Accent = participant.AccentSnapshot,
                            LookId = participant.LookIdSnapshot,
                            ProjectileLookId =
                                participant
                                    .ProjectileLookIdSnapshot,
                        })
                    .ToArray();
            using var session = new GenericActorMatchSession(
                expected.Match,
                configurations,
                unchecked((ulong)match.Seed));
            result = session.Run();
            replay = GenericActorReplayDocument.Create(session);
            // The session now owns and disposes every factory.
            factories.Clear();
        }
        finally
        {
            foreach (WasmGenericActorRuntimeFactory factory in factories)
                factory.Dispose();
        }

        match.ReplayKey =
            await replayWriter.WriteCanonicalJsonAsync(
                match.Id,
                replay.CanonicalJson,
                cancellationToken);
        match.ReplayHash = replay.ReplayHash;
        match.ReplayFormatVersion =
            BotArenaVersions.GenericActorReplayFormatVersion;
        GenericMatchResultPersistence.Apply(match, result);
        match.Status = MatchStatus.Completed;
        DateTime completedAt =
            timeProvider.GetUtcNow().UtcDateTime;
        match.CompletedAt = completedAt;
        match.BroadcastStartedAt = completedAt.AddSeconds(
            settings.BroadcastDelaySeconds);
        match.PresentationTicksPerSecond =
            settings.BroadcastTicksPerSecond;

        await db.SaveChangesAsync(cancellationToken);
        return new JobExecutionResult("completed");
    }

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
