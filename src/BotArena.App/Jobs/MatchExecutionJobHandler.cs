using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
using BotArena.Runtime.Wasm;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

public sealed class MatchExecutionJobHandler(
    AppDbContext db,
    IObjectStore objectStore,
    MatchReplayWriter replayWriter,
    RankedMatchSetFinalizer setFinalizer,
    LegacyCompetitionIdentityResolver identityResolver,
    CosmeticEntitlementService entitlements,
    MatchExecutionSettings settings,
    TimeProvider timeProvider)
{
    public async Task<JobExecutionResult> HandleAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        Match match = await db.Matches
            .Include(candidate => candidate.Participants)
            .SingleAsync(candidate => candidate.Id == matchId, cancellationToken);
        PlaylistVersion? pinnedPlaylistVersion = null;
        if (match.PlaylistVersionId is Guid pinnedPlaylistVersionId)
        {
            pinnedPlaylistVersion =
                await db.PlaylistVersions
                    .AsNoTracking()
                    .SingleAsync(
                        candidate =>
                            candidate.Id == pinnedPlaylistVersionId,
                        cancellationToken);
            if (!string.Equals(
                    pinnedPlaylistVersion.ExecutionPolicyId,
                    PlaylistExecutionPolicyIds.LegacyDuel,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Legacy ExecuteMatch job for match {match.Id} cannot " +
                    $"execute playlist policy " +
                    $"'{pinnedPlaylistVersion.ExecutionPolicyId}'.");
            }
            if (!string.Equals(
                    pinnedPlaylistVersion.ExecutionEngineVersion,
                    BotArenaVersions.EngineVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Legacy ExecuteMatch job for match {match.Id} requires " +
                    $"engine '{pinnedPlaylistVersion.ExecutionEngineVersion}', " +
                    $"but this worker provides '{BotArenaVersions.EngineVersion}'.");
            }
        }

        if (match.Status is MatchStatus.Completed or MatchStatus.Failed)
        {
            await ApplyTerminalEffectsAsync(
                match,
                pinnedPlaylistVersion,
                cancellationToken);
            return new JobExecutionResult(
                match.Status == MatchStatus.Completed
                    ? "already_completed"
                    : "already_failed");
        }

        match.Status = MatchStatus.Running;
        match.Error = null;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            List<MatchParticipant> participants =
                match.Participants.OrderBy(participant => participant.Slot).ToList();
            List<BotVersion> versions = [];
            foreach (MatchParticipant participant in participants)
            {
                versions.Add(await db.BotVersions.SingleAsync(
                    version => version.Id == participant.BotVersionId,
                    cancellationToken));
            }

            string? pinnedRulesName = null;
            MatchSet? owningSet = null;
            if (match.MatchSetId is Guid rulesSetId)
            {
                owningSet = await db.MatchSets.SingleAsync(
                    set => set.Id == rulesSetId,
                    cancellationToken);
                pinnedRulesName = owningSet.RulesName;
            }

            GameRules rules;
            if (match.PlaylistVersionId is Guid playlistVersionId)
            {
                PlaylistVersion playlistVersion =
                    pinnedPlaylistVersion
                    ?? throw new InvalidOperationException(
                        $"Match {match.Id} has no loaded playlist " +
                        $"{playlistVersionId}.");
                if (!string.Equals(
                        playlistVersion.RulesetId,
                        match.GameRulesVersion,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Match {match.Id} playlist rules " +
                        $"'{playlistVersion.RulesetId}' contradict stored rules " +
                        $"'{match.GameRulesVersion}'.");
                }
                if (owningSet is not null &&
                    (owningSet.PlaylistVersionId != playlistVersionId ||
                     !string.Equals(
                         owningSet.GameRulesVersion,
                         match.GameRulesVersion,
                         StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Match {match.Id} competition identity contradicts " +
                        $"MatchSet {owningSet.Id}.");
                }

                rules = pinnedRulesName is { Length: > 0 }
                    ? GameRules.Resolve(pinnedRulesName)
                    : ResolveStoredRulesVersion(match.GameRulesVersion);
                if (!string.Equals(
                        rules.RulesVersion,
                        match.GameRulesVersion,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Match {match.Id} resolved rules " +
                        $"'{rules.RulesVersion}' contradict its pinned playlist " +
                        $"rules '{match.GameRulesVersion}'.");
                }
            }
            else
            {
                // Compatibility path for work inserted by an old image during
                // the additive migration window. Resolve its historical
                // execution behavior first, then repair the nullable mirrors
                // before it can reach settlement.
                rules = pinnedRulesName is { Length: > 0 }
                    ? GameRules.Resolve(pinnedRulesName)
                    : settings.MatchRules;
                LegacyCompetitionIdentity identity =
                    await identityResolver.ResolveOrCreateAsync(
                        rules.RulesVersion,
                        settings.MatchRules.RulesVersion,
                        cancellationToken);
                RepairCompetitionIdentity(
                    match,
                    owningSet,
                    rules.RulesVersion,
                    identity);
            }

            List<string> modulePaths = [];
            foreach (BotVersion version in versions)
            {
                if (!BotContractProfiles.Supports(
                        version.SupportedContractProfiles,
                        BotContractProfiles.LegacyDuel))
                {
                    throw new InvalidOperationException(
                        $"Bot version {version.Id} cannot execute a Duel " +
                        $"match because it does not support contract profile " +
                        $"'{BotContractProfiles.LegacyDuel}'.");
                }
                if (version.ArtifactKey is null || version.ArtifactHash is null)
                {
                    throw new InvalidOperationException(
                        $"Bot version {version.Id} has no built artifact.");
                }
                modulePaths.Add(await objectStore.MaterializeAsync(
                    version.ArtifactKey,
                    version.ArtifactHash,
                    cancellationToken));
            }

            List<WasmBotRuntime> runtimes = versions
                .Select((version, index) =>
                    new WasmBotRuntime(new WasmRuntimeOptions
                    {
                        ModulePath = modulePaths[index],
                        BotName = version.GuestBotName ?? "",
                    }))
                .ToList();
            try
            {
                MatchRunResult run = new MatchEngine().Run(new MatchConfiguration
                {
                    Map = ArenaMapLoader.Load(match.MapId, rules),
                    Rules = rules,
                    Seed = unchecked((ulong)match.Seed),
                    Participants = participants
                        .Select((participant, slot) => new MatchParticipantConfig
                        {
                            Name = participant.NameSnapshot,
                            Runtime = runtimes[slot],
                            RuntimeKind = "wasm",
                            ArtifactHash = participant.ArtifactHashSnapshot,
                            Accent = participant.AccentSnapshot,
                            LookId = participant.LookIdSnapshot,
                            ProjectileLookId =
                                participant.ProjectileLookIdSnapshot,
                        })
                        .ToArray(),
                });

                match.ReplayKey = await replayWriter.WriteAsync(
                    match.Id,
                    run.Replay,
                    cancellationToken);
                match.ReplayHash = run.ReplayHash;
                match.ReplayFormatVersion =
                    BotArenaVersions.ReplayFormatVersion;
                match.GameRulesVersion = rules.RulesVersion;
                match.WinnerSlot = run.Result.WinnerSlot;
                match.EndReason = run.Result.Reason.ToString();
                match.EndTick = run.Result.EndTick;
                match.Status = MatchStatus.Completed;
                DateTime completedAt = timeProvider.GetUtcNow().UtcDateTime;
                match.CompletedAt = completedAt;
                match.BroadcastStartedAt = completedAt.AddSeconds(
                    settings.BroadcastDelaySeconds);
                match.PresentationTicksPerSecond =
                    settings.BroadcastTicksPerSecond;
                foreach (MatchParticipant participant in participants)
                {
                    BotMatchResult botResult = run.Result.Bots.Single(
                        result => result.Slot == participant.Slot);
                    participant.Outcome = botResult.Outcome.ToString();
                    participant.FinalHealth = botResult.FinalHealth;
                    participant.DamageDealt = botResult.DamageDealt;
                    participant.Faults = botResult.Faults;
                }
            }
            finally
            {
                foreach (WasmBotRuntime runtime in runtimes)
                    runtime.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            match.Status = MatchStatus.Failed;
            match.Error = exception.Message;
            match.CompletedAt = timeProvider.GetUtcNow().UtcDateTime;
        }

        ScheduleResultAnnouncement(match);

        await db.SaveChangesAsync(cancellationToken);
        await ApplyTerminalEffectsAsync(
            match,
            pinnedPlaylistVersion,
            cancellationToken);
        return new JobExecutionResult(
            match.Status == MatchStatus.Completed
                ? "completed"
                : "match_failed");
    }

    /// <summary>
    /// Queue the announcement for the instant this match stops withholding its result.
    /// <para>
    /// Enqueued in the same SaveChanges as the completed match, so a result can never
    /// exist without something scheduled to announce it.
    /// </para>
    /// <para>
    /// Setless matches only. A ranked set announces once, when the last of its games has
    /// finished broadcasting — a boundary the set's finalizer knows and a single game's
    /// worker does not.
    /// </para>
    /// </summary>
    private void ScheduleResultAnnouncement(Match match)
    {
        if (match.Status != MatchStatus.Completed || match.MatchSetId is not null)
            return;
        if (BroadcastSchedule.AnnounceAt(match) is not DateTime announceAt)
            return;
        db.BackgroundJobs.Add(BackgroundJob.AnnounceMatchResult(match.Id, announceAt));
    }

    private async Task ApplyTerminalEffectsAsync(
        Match match,
        PlaylistVersion? playlistVersion,
        CancellationToken cancellationToken)
    {
        if (match.Status == MatchStatus.Completed &&
            playlistVersion?.Visibility != PlaylistVisibilityIds.Labs &&
            match.MatchSetId is null &&
            match.InitiatedByUserId is Guid challengerId)
        {
            await entitlements.GrantForEventAsync(
                challengerId,
                CosmeticUnlockEvents.Challenge,
                CosmeticUnlockEvents.FirstUnrankedMatch,
                new { matchId = match.Id },
                cancellationToken);
        }
        if (match.MatchSetId is Guid setId)
            await setFinalizer.TryFinalizeAsync(setId, cancellationToken);
    }

    private static GameRules ResolveStoredRulesVersion(string rulesVersion)
    {
        foreach (string name in GameRules.KnownNames)
        {
            GameRules candidate = GameRules.Resolve(name);
            if (string.Equals(
                    candidate.RulesVersion,
                    rulesVersion,
                    StringComparison.Ordinal))
            {
                return candidate;
            }
        }
        throw new InvalidOperationException(
            $"Stored rules version '{rulesVersion}' is not executable by this image.");
    }

    private static void RepairCompetitionIdentity(
        Match match,
        MatchSet? set,
        string executedRulesVersion,
        LegacyCompetitionIdentity identity)
    {
        match.GameRulesVersion = executedRulesVersion;
        Repair(
            match.PlaylistVersionId,
            identity.PlaylistVersionId,
            value => match.PlaylistVersionId = value,
            nameof(Match),
            match.Id,
            nameof(Match.PlaylistVersionId));
        if (set is null)
            return;
        if (!string.Equals(
                set.GameRulesVersion,
                executedRulesVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"MatchSet {set.Id} rules '{set.GameRulesVersion}' contradict " +
                $"executed legacy rules '{executedRulesVersion}'.");
        }
        Repair(
            set.PlaylistVersionId,
            identity.PlaylistVersionId,
            value => set.PlaylistVersionId = value,
            nameof(MatchSet),
            set.Id,
            nameof(MatchSet.PlaylistVersionId));
        Repair(
            set.LadderId,
            identity.LadderId,
            value => set.LadderId = value,
            nameof(MatchSet),
            set.Id,
            nameof(MatchSet.LadderId));
    }

    private static void Repair(
        Guid? actual,
        Guid expected,
        Action<Guid> fill,
        string rowKind,
        Guid rowId,
        string field)
    {
        if (actual is null)
        {
            fill(expected);
            return;
        }
        if (actual.Value != expected)
        {
            throw new InvalidOperationException(
                $"Competition identity contradiction for {rowKind} {rowId}: " +
                $"{field} is {actual.Value:D}, expected {expected:D}.");
        }
    }
}
