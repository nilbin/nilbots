using System.Data;
using System.Text.Json.Nodes;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BotArena.App.Competition;

/// <summary>
/// Resolves the additive opaque identity for the existing rules-version keyed
/// Duel behavior. Creation is serialized with the migration backfiller; reads
/// used while finalizing an already queued set never take that global lock.
/// </summary>
public sealed class LegacyCompetitionIdentityResolver(AppDbContext db)
{
    // "NILBOTCL" as a signed PostgreSQL bigint. This lock protects only legacy
    // competition identity creation/backfill and is held for the transaction.
    internal const long AdvisoryLockKey = 0x4E494C424F54434C;

    public async Task<LegacyCompetitionIdentity> ResolveOrCreateAsync(
        string rulesVersion,
        string currentRulesVersion,
        CancellationToken cancellationToken = default)
    {
        LegacyCompetitionDefinition.ValidateRulesVersion(
            rulesVersion,
            nameof(rulesVersion));
        LegacyCompetitionDefinition.ValidateRulesVersion(
            currentRulesVersion,
            nameof(currentRulesVersion));

        IDbContextTransaction? ownedTransaction = null;
        try
        {
            if (db.Database.CurrentTransaction is null)
            {
                ownedTransaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
            }

            await AcquireAdvisoryLockAsync(cancellationToken);
            LegacyCompetitionIdentity identity =
                await ResolveOrCreateLockedAsync(
                    rulesVersion,
                    currentRulesVersion,
                    cancellationToken);

            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);
            return identity;
        }
        catch
        {
            if (ownedTransaction is not null)
                await ownedTransaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }
    }

    /// <summary>
    /// Resolves an identity that migration or admission has already created.
    /// This intentionally does not acquire the global backfill lock: finalizers
    /// call it after locking a series, and reversing those locks would deadlock
    /// with a migration repairing the same row.
    /// </summary>
    public async Task<LegacyCompetitionIdentity> ResolveExistingAsync(
        string rulesVersion,
        CancellationToken cancellationToken = default)
    {
        LegacyCompetitionDefinition expected =
            LegacyCompetitionDefinition.Create(rulesVersion);
        Ladder ladder = await db.Ladders.SingleOrDefaultAsync(
                candidate => candidate.LegacyRulesVersion == rulesVersion,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"No legacy competition identity exists for rules version " +
                $"'{rulesVersion}'. Run the migrate role before finalizing this set.");
        PlaylistVersion version = await db.PlaylistVersions.SingleAsync(
            candidate => candidate.Id == ladder.PlaylistVersionId,
            cancellationToken);
        Playlist playlist = await db.Playlists.SingleAsync(
            candidate => candidate.Id == version.PlaylistId,
            cancellationToken);
        Season season = await db.Seasons.SingleAsync(
            candidate => candidate.Id == ladder.SeasonId,
            cancellationToken);

        ValidateExisting(
            expected,
            playlist,
            version,
            season,
            ladder);
        return Identity(expected, playlist, version, season, ladder);
    }

    internal Task AcquireAdvisoryLockAsync(
        CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({AdvisoryLockKey})",
            cancellationToken);

    internal async Task<LegacyCompetitionIdentity> ResolveOrCreateLockedAsync(
        string rulesVersion,
        string currentRulesVersion,
        CancellationToken cancellationToken)
    {
        LegacyCompetitionDefinition expected =
            LegacyCompetitionDefinition.Create(rulesVersion);

        Season? season = await db.Seasons.SingleOrDefaultAsync(
            candidate => candidate.Key == LegacyCompetitionDefinition.SeasonKey,
            cancellationToken);
        if (season is null)
        {
            season = new Season
            {
                Key = LegacyCompetitionDefinition.SeasonKey,
                DisplayName =
                    LegacyCompetitionDefinition.SeasonDisplayName,
            };
            db.Seasons.Add(season);
            await db.SaveChangesAsync(cancellationToken);
        }

        Playlist? playlist = await db.Playlists.SingleOrDefaultAsync(
            candidate => candidate.Key == expected.PlaylistKey,
            cancellationToken);
        if (playlist is null)
        {
            playlist = new Playlist
            {
                Key = expected.PlaylistKey,
                DisplayName = expected.PlaylistDisplayName,
            };
            db.Playlists.Add(playlist);
            await db.SaveChangesAsync(cancellationToken);
        }

        PlaylistVersion? version = await db.PlaylistVersions
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.PlaylistId == playlist.Id &&
                    candidate.Version ==
                        LegacyCompetitionDefinition.PlaylistVersion,
                cancellationToken);
        if (version is null)
        {
            version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = LegacyCompetitionDefinition.PlaylistVersion,
                GameModeId = LegacyCompetitionDefinition.GameModeId,
                RulesetId = rulesVersion,
                MatchFormatId =
                    LegacyCompetitionDefinition.MatchFormatId,
                MapPoolId =
                    LegacyCompetitionDefinition.UnknownDefinitionId,
                SeriesPolicyId =
                    LegacyCompetitionDefinition.UnknownDefinitionId,
                MatchmakingPolicyId =
                    LegacyCompetitionDefinition.UnknownDefinitionId,
                AdmissionPolicyId =
                    LegacyCompetitionDefinition.UnknownDefinitionId,
                ExecutionPolicyId =
                    PlaylistExecutionPolicyIds.LegacyDuel,
                ExecutionEngineVersion =
                    BotArenaVersions.EngineVersion,
                CanonicalDefinition = expected.CanonicalDefinition,
                DefinitionFingerprint = expected.DefinitionFingerprint,
                Provenance = expected.Provenance,
                Visibility = LegacyCompetitionDefinition.Visibility,
            };
            db.PlaylistVersions.Add(version);
            await db.SaveChangesAsync(cancellationToken);
        }

        Ladder? aliasLadder = await db.Ladders.SingleOrDefaultAsync(
            candidate => candidate.LegacyRulesVersion == rulesVersion,
            cancellationToken);
        Ladder? populationLadder = await db.Ladders.SingleOrDefaultAsync(
            candidate =>
                candidate.PlaylistVersionId == version.Id &&
                candidate.SeasonId == season.Id,
            cancellationToken);
        if (aliasLadder is not null &&
            populationLadder is not null &&
            aliasLadder.Id != populationLadder.Id)
        {
            throw Contradiction(
                rulesVersion,
                "the legacy alias and playlist/season population resolve to " +
                "different ladders");
        }

        Ladder? ladder = aliasLadder ?? populationLadder;
        if (ladder is null)
        {
            ladder = new Ladder
            {
                PlaylistVersionId = version.Id,
                SeasonId = season.Id,
                Status = DesiredStatus(rulesVersion, currentRulesVersion),
                RatingPolicyId = DuelEloV1.Id,
                LegacyRulesVersion = rulesVersion,
                IsListed = IsListed(rulesVersion, currentRulesVersion),
                AwardsAchievements = AwardsAchievements(rulesVersion),
            };
            db.Ladders.Add(ladder);
        }
        else
        {
            if (ladder.PlaylistVersionId != version.Id ||
                ladder.SeasonId != season.Id)
            {
                throw Contradiction(
                    rulesVersion,
                    "the ladder points at a different playlist version or season");
            }
            if (ladder.LegacyRulesVersion is null)
                ladder.LegacyRulesVersion = rulesVersion;
            else if (!string.Equals(
                         ladder.LegacyRulesVersion,
                         rulesVersion,
                         StringComparison.Ordinal))
            {
                throw Contradiction(
                    rulesVersion,
                    "the ladder carries a different legacy rules alias");
            }
            if (!string.Equals(
                    ladder.RatingPolicyId,
                    DuelEloV1.Id,
                    StringComparison.Ordinal))
            {
                throw Contradiction(
                    rulesVersion,
                    $"the ladder uses rating policy '{ladder.RatingPolicyId}'");
            }

            ladder.Status = DesiredStatus(
                rulesVersion,
                currentRulesVersion);
            ladder.IsListed = IsListed(
                rulesVersion,
                currentRulesVersion);
            ladder.AwardsAchievements =
                AwardsAchievements(rulesVersion);
        }

        ValidateExisting(expected, playlist, version, season, ladder);
        await db.SaveChangesAsync(cancellationToken);
        return Identity(expected, playlist, version, season, ladder);
    }

    private static void ValidateExisting(
        LegacyCompetitionDefinition expected,
        Playlist playlist,
        PlaylistVersion version,
        Season season,
        Ladder ladder)
    {
        if (!string.Equals(
                playlist.Key,
                expected.PlaylistKey,
                StringComparison.Ordinal))
        {
            throw Contradiction(
                expected.RulesVersion,
                "the playlist key is not the deterministic import key");
        }
        if (!string.Equals(
                season.Key,
                LegacyCompetitionDefinition.SeasonKey,
                StringComparison.Ordinal) ||
            season.StartsAt is not null ||
            season.EndsAt is not null)
        {
            throw Contradiction(
                expected.RulesVersion,
                "the imported season identity or time window differs");
        }

        AssertEqual(
            expected,
            nameof(version.PlaylistId),
            playlist.Id,
            version.PlaylistId);
        AssertEqual(
            expected,
            nameof(version.Version),
            LegacyCompetitionDefinition.PlaylistVersion,
            version.Version);
        AssertEqual(
            expected,
            nameof(version.GameModeId),
            LegacyCompetitionDefinition.GameModeId,
            version.GameModeId);
        AssertEqual(
            expected,
            nameof(version.RulesetId),
            expected.RulesVersion,
            version.RulesetId);
        AssertEqual(
            expected,
            nameof(version.MatchFormatId),
            LegacyCompetitionDefinition.MatchFormatId,
            version.MatchFormatId);
        AssertEqual(
            expected,
            nameof(version.MapPoolId),
            LegacyCompetitionDefinition.UnknownDefinitionId,
            version.MapPoolId);
        AssertEqual(
            expected,
            nameof(version.SeriesPolicyId),
            LegacyCompetitionDefinition.UnknownDefinitionId,
            version.SeriesPolicyId);
        AssertEqual(
            expected,
            nameof(version.MatchmakingPolicyId),
            LegacyCompetitionDefinition.UnknownDefinitionId,
            version.MatchmakingPolicyId);
        AssertEqual(
            expected,
            nameof(version.AdmissionPolicyId),
            LegacyCompetitionDefinition.UnknownDefinitionId,
            version.AdmissionPolicyId);
        AssertEqual(
            expected,
            nameof(version.ExecutionPolicyId),
            PlaylistExecutionPolicyIds.LegacyDuel,
            version.ExecutionPolicyId);
        AssertEqual(
            expected,
            nameof(version.ExecutionEngineVersion),
            BotArenaVersions.EngineVersion,
            version.ExecutionEngineVersion);
        AssertEqual(
            expected,
            nameof(version.DefinitionFingerprint),
            expected.DefinitionFingerprint,
            version.DefinitionFingerprint);
        AssertEqual(
            expected,
            nameof(version.Visibility),
            LegacyCompetitionDefinition.Visibility,
            version.Visibility);
        AssertJsonEqual(
            expected,
            nameof(version.CanonicalDefinition),
            expected.CanonicalDefinition,
            version.CanonicalDefinition);
        AssertJsonEqual(
            expected,
            nameof(version.Provenance),
            expected.Provenance,
            version.Provenance);

        if (ladder.PlaylistVersionId != version.Id ||
            ladder.SeasonId != season.Id ||
            !string.Equals(
                ladder.LegacyRulesVersion,
                expected.RulesVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                ladder.RatingPolicyId,
                DuelEloV1.Id,
                StringComparison.Ordinal))
        {
            throw Contradiction(
                expected.RulesVersion,
                "the ladder metadata does not match the imported population");
        }
    }

    private static LegacyCompetitionIdentity Identity(
        LegacyCompetitionDefinition expected,
        Playlist playlist,
        PlaylistVersion version,
        Season season,
        Ladder ladder) =>
        new(
            expected.RulesVersion,
            playlist.Id,
            version.Id,
            season.Id,
            ladder.Id);

    private static LadderStatus DesiredStatus(
        string rulesVersion,
        string currentRulesVersion) =>
        string.Equals(
            rulesVersion,
            currentRulesVersion,
            StringComparison.Ordinal)
            ? LadderStatus.Open
            : LadderStatus.Closed;

    private static bool IsListed(
        string rulesVersion,
        string currentRulesVersion) =>
        string.Equals(
            rulesVersion,
            currentRulesVersion,
            StringComparison.Ordinal) ||
        Engine.GameRules.ShippedNames.Contains(
            rulesVersion,
            StringComparer.Ordinal);

    private static bool AwardsAchievements(string rulesVersion) =>
        !rulesVersion.Contains("-exp-", StringComparison.Ordinal);

    private static void AssertEqual<T>(
        LegacyCompetitionDefinition expected,
        string field,
        T expectedValue,
        T actualValue)
    {
        if (!EqualityComparer<T>.Default.Equals(expectedValue, actualValue))
        {
            throw Contradiction(
                expected.RulesVersion,
                $"immutable playlist field {field} differs");
        }
    }

    private static void AssertJsonEqual(
        LegacyCompetitionDefinition expected,
        string field,
        string expectedValue,
        string actualValue)
    {
        JsonNode? expectedJson = JsonNode.Parse(expectedValue);
        JsonNode? actualJson = JsonNode.Parse(actualValue);
        if (!JsonNode.DeepEquals(expectedJson, actualJson))
        {
            throw Contradiction(
                expected.RulesVersion,
                $"immutable playlist JSON field {field} differs");
        }
    }

    private static InvalidOperationException Contradiction(
        string rulesVersion,
        string detail) =>
        new(
            $"Legacy competition identity contradiction for rules version " +
            $"'{rulesVersion}': {detail}.");
}
