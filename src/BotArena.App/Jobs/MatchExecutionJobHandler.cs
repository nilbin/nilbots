using BotArena.App.Bots;
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
        if (match.Status is MatchStatus.Completed or MatchStatus.Failed)
        {
            await ApplyTerminalEffectsAsync(match, cancellationToken);
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

            GameRules rules = settings.MatchRules;
            if (match.MatchSetId is Guid rulesSetId &&
                await db.MatchSets
                    .Where(set => set.Id == rulesSetId)
                    .Select(set => set.RulesName)
                    .SingleAsync(cancellationToken) is { Length: > 0 } pinned)
            {
                rules = GameRules.Resolve(pinned);
            }

            List<string> modulePaths = [];
            foreach (BotVersion version in versions)
            {
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
                    Map = ArenaMapLoader.Load(match.MapId),
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

        await db.SaveChangesAsync(cancellationToken);
        await ApplyTerminalEffectsAsync(match, cancellationToken);
        return new JobExecutionResult(
            match.Status == MatchStatus.Completed
                ? "completed"
                : "match_failed");
    }

    private async Task ApplyTerminalEffectsAsync(
        Match match,
        CancellationToken cancellationToken)
    {
        if (match.Status == MatchStatus.Completed &&
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
}
