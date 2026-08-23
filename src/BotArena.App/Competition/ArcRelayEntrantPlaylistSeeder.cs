using BotArena.App.ArcRelay;
using BotArena.App.Sheets;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>
/// Seeds the current immutable entrant lane. Each promoted rules profile starts
/// a fresh rating book when public gameplay changes; immutable v8-v35 ladders
/// remain readable.
/// </summary>
public sealed class ArcRelayEntrantPlaylistSeeder(
    AppDbContext db,
    TimeProvider timeProvider,
    ArcRelaySeasonLifecycleService seasons)
{
    // Tapline-9 was corrected locally before publication. This exact
    // fingerprint is the rejected 14-wall candidate and is the only persisted
    // v26 definition the seeder may repair in place.
    public const string UnpublishedMistakenTapline9DefinitionFingerprint =
        "75c909ff904d8c985559f868968644380316929c16b56dfe0d582d6113cd5e6b";

    public async Task<ArcRelayEntrantSeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            cancellationToken);
        ArcRelayEntrantPlaylistDefinition expected =
            ArcRelayEntrantPlaylistDefinition.Create();
        Playlist playlist = await db.Playlists.SingleAsync(
            value => value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            cancellationToken);
        PlaylistVersion? historicalVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.HistoricalVersion,
                cancellationToken);
        if (historicalVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV2()
                .Validate(playlist, historicalVersion);
        }
        PlaylistVersion? counterflowVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.CounterflowVersion,
                cancellationToken);
        if (counterflowVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV3()
                .Validate(playlist, counterflowVersion);
        }
        PlaylistVersion? tacticalVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.TacticalVersion,
                cancellationToken);
        if (tacticalVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV6()
                .Validate(playlist, tacticalVersion);
        }
        PlaylistVersion? ambushVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.AmbushVersion,
                cancellationToken);
        if (ambushVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV7()
                .Validate(playlist, ambushVersion);
        }
        PlaylistVersion? wasmRepairVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.WasmRepairVersion,
                cancellationToken);
        if (wasmRepairVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV8()
                .Validate(playlist, wasmRepairVersion);
        }
        PlaylistVersion? directLadderVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.DirectLadderVersion,
                cancellationToken);
        if (directLadderVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV9()
                .Validate(playlist, directLadderVersion);
        }
        PlaylistVersion? semanticIntentVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.SemanticIntentVersion,
                cancellationToken);
        if (semanticIntentVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV10()
                .Validate(playlist, semanticIntentVersion);
        }
        PlaylistVersion? boundedMovementVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.BoundedMovementVersion,
                cancellationToken);
        if (boundedMovementVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV11()
                .Validate(playlist, boundedMovementVersion);
        }
        PlaylistVersion? relativeWellVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.RelativeWellVersion,
                cancellationToken);
        if (relativeWellVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV12()
                .Validate(playlist, relativeWellVersion);
        }
        PlaylistVersion? stickyRouteVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.StickyRouteVersion,
                cancellationToken);
        if (stickyRouteVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV13()
                .Validate(playlist, stickyRouteVersion);
        }
        PlaylistVersion? carrierClearanceVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.CarrierClearanceVersion,
                cancellationToken);
        if (carrierClearanceVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV14()
                .Validate(playlist, carrierClearanceVersion);
        }
        PlaylistVersion? trafficDetourVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.TrafficDetourVersion,
                cancellationToken);
        if (trafficDetourVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV15()
                .Validate(playlist, trafficDetourVersion);
        }
        PlaylistVersion? stableCarrierVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.StableCarrierVersion,
                cancellationToken);
        if (stableCarrierVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV16()
                .Validate(playlist, stableCarrierVersion);
        }
        PlaylistVersion? looseCoreMemoryVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.LooseCoreMemoryVersion,
                cancellationToken);
        if (looseCoreMemoryVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV17()
                .Validate(playlist, looseCoreMemoryVersion);
        }
        PlaylistVersion? claimableCoreVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.ClaimableCoreVersion,
                cancellationToken);
        if (claimableCoreVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV18()
                .Validate(playlist, claimableCoreVersion);
        }
        PlaylistVersion? taplineV1Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.TaplineV1Version,
                cancellationToken);
        if (taplineV1Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV19()
                .Validate(playlist, taplineV1Version);
        }
        PlaylistVersion? ordersV2Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.OrdersV2Version,
                cancellationToken);
        if (ordersV2Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV20()
                .Validate(playlist, ordersV2Version);
        }
        PlaylistVersion? tapline3Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline3Version,
                cancellationToken);
        if (tapline3Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV21()
                .Validate(playlist, tapline3Version);
        }
        PlaylistVersion? tapline4Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline4Version,
                cancellationToken);
        if (tapline4Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV22()
                .Validate(playlist, tapline4Version);
        }
        PlaylistVersion? tapline5Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline5Version,
                cancellationToken);
        if (tapline5Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV23()
                .Validate(playlist, tapline5Version);
        }
        PlaylistVersion? tapline6Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline6Version,
                cancellationToken);
        if (tapline6Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV24()
                .Validate(playlist, tapline6Version);
        }
        PlaylistVersion? tapline8Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline8Version,
                cancellationToken);
        if (tapline8Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV25()
                .Validate(playlist, tapline8Version);
        }
        PlaylistVersion? tapline9Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline9Version,
                cancellationToken);
        if (tapline9Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition expectedTapline9 =
                ArcRelayEntrantPlaylistDefinition.CreateHistoricalV26();
            if (string.Equals(
                    tapline9Version.DefinitionFingerprint,
                    UnpublishedMistakenTapline9DefinitionFingerprint,
                    StringComparison.Ordinal))
            {
                await RepairUnpublishedTapline9Async(
                    tapline9Version,
                    expectedTapline9,
                    cancellationToken);
            }
            expectedTapline9.Validate(playlist, tapline9Version);
        }
        PlaylistVersion? tapline10Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline10Version,
                cancellationToken);
        if (tapline10Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV27()
                .Validate(playlist, tapline10Version);
        }
        PlaylistVersion? tapline11Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline11Version,
                cancellationToken);
        if (tapline11Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV28()
                .Validate(playlist, tapline11Version);
        }
        PlaylistVersion? tapline12Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline12Version,
                cancellationToken);
        if (tapline12Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV29()
                .Validate(playlist, tapline12Version);
        }
        PlaylistVersion? tapline13Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline13Version,
                cancellationToken);
        if (tapline13Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV30()
                .Validate(playlist, tapline13Version);
        }
        PlaylistVersion? tapline14Version = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.Tapline14Version,
                cancellationToken);
        if (tapline14Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV31()
                .Validate(playlist, tapline14Version);
        }
        PlaylistVersion? completeObservationVersion =
            await db.PlaylistVersions.SingleOrDefaultAsync(value =>
                value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition
                    .CompleteObservationVersion,
                cancellationToken);
        if (completeObservationVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV32()
                .Validate(playlist, completeObservationVersion);
        }
        PlaylistVersion? tapline15Version =
            await db.PlaylistVersions.SingleOrDefaultAsync(value =>
                value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition
                    .BroadloomInitialVersion,
                cancellationToken);
        if (tapline15Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV33()
                .Validate(playlist, tapline15Version);
        }
        PlaylistVersion? broadloom2Version =
            await db.PlaylistVersions.SingleOrDefaultAsync(value =>
                value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition
                    .Broadloom2Version,
                cancellationToken);
        if (broadloom2Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV34()
                .Validate(playlist, broadloom2Version);
        }
        PlaylistVersion? broadloom3Version =
            await db.PlaylistVersions.SingleOrDefaultAsync(value =>
                value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition
                    .Broadloom3Version,
                cancellationToken);
        if (broadloom3Version is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV35()
                .Validate(playlist, broadloom3Version);
        }
        PlaylistVersion? recoveryVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.RecoveryVersion,
                cancellationToken);
        if (recoveryVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV5()
                .Validate(playlist, recoveryVersion);
        }
        PlaylistVersion? forwardVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.ForwardVersion,
                cancellationToken);
        if (forwardVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV4()
                .Validate(playlist, forwardVersion);
        }
        PlaylistVersion? version = await db.PlaylistVersions.SingleOrDefaultAsync(
            value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.Version,
            cancellationToken);
        if (version is null)
        {
            version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = ArcRelayEntrantPlaylistDefinition.Version,
                GameModeId = expected.Match.Rules.GameMode.ModeId,
                RulesetId = expected.Match.Rules.RulesetId,
                MatchFormatId = expected.Match.Format.FormatId,
                MapPoolId = expected.Match.Map.Id,
                SeriesPolicyId = ArcRelayEntrantPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId = ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = expected.AdmissionPolicyId,
                ExecutionPolicyId = expected.ExecutionPolicyId,
                ExecutionEngineVersion = expected.ExecutionEngineVersion,
                CanonicalDefinition = expected.CanonicalDefinition,
                DefinitionFingerprint = expected.DefinitionFingerprint,
                Provenance = expected.Provenance,
                Visibility = ArcRelayEntrantPlaylistDefinition.Visibility,
            };
            db.PlaylistVersions.Add(version);
            await db.SaveChangesAsync(cancellationToken);
        }
        expected.Validate(playlist, version);

        Season? season = await db.Seasons.SingleOrDefaultAsync(
            value => value.Key == ArcRelayLadderPolicy.SeasonKey,
            cancellationToken);
        if (season is null)
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            season = new Season
            {
                Key = ArcRelayLadderPolicy.SeasonKey,
                DisplayName = ArcRelayLadderPolicy.SeasonName,
                Number = ArcRelayLadderPolicy.SeasonNumber,
                Status = SeasonStatus.Scheduled,
                StartsAt = now,
                EndsAt = now.AddDays(
                    ArcRelayLadderPolicy.SeasonDurationDays),
                RatingSeedPolicyId =
                    ArcRelayLadderPolicy.RatingSeedPolicyId,
                CreatedAt = now,
            };
            db.Seasons.Add(season);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (season.Number != ArcRelayLadderPolicy.SeasonNumber ||
            !string.Equals(season.DisplayName,
                ArcRelayLadderPolicy.SeasonName,
                StringComparison.Ordinal) ||
            !string.Equals(season.RatingSeedPolicyId,
                ArcRelayLadderPolicy.RatingSeedPolicyId,
                StringComparison.Ordinal) ||
            season.StartsAt is null || season.EndsAt is null ||
            season.EndsAt.Value - season.StartsAt.Value !=
                TimeSpan.FromDays(ArcRelayLadderPolicy.SeasonDurationDays))
        {
            throw new InvalidOperationException(
                "Arc Relay Season 1 contradicts its registered definition.");
        }

        Ladder? ladder = await db.Ladders.SingleOrDefaultAsync(
            value => value.PlaylistVersionId == version.Id &&
                value.SeasonId == season.Id,
            cancellationToken);
        if (ladder is null)
        {
            ladder = new Ladder
            {
                PlaylistVersionId = version.Id,
                SeasonId = season.Id,
                Status = LadderStatus.Draft,
                RatingPolicyId = ArcRelayEloV1.Id,
                IsListed = false,
                AwardsAchievements = false,
            };
            db.Ladders.Add(ladder);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (!string.Equals(ladder.RatingPolicyId, ArcRelayEloV1.Id,
                StringComparison.Ordinal) || ladder.AwardsAchievements)
        {
            throw new InvalidOperationException(
                "Arc Relay Season 1 ladder contradicts its registered policy.");
        }

        if (season.Status == SeasonStatus.Open &&
            ladder.Status == LadderStatus.Draft)
        {
            await StartFreshOpenRatingBookAsync(
                playlist, season, ladder, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await seasons.AdvanceAsync(cancellationToken);
        ladder = await db.Ladders.AsNoTracking().SingleAsync(
            value => value.Id == ladder.Id, cancellationToken);
        return new ArcRelayEntrantSeedResult(version, ladder);
    }

    private async Task StartFreshOpenRatingBookAsync(
        Playlist playlist,
        Season season,
        Ladder current,
        CancellationToken cancellationToken)
    {
        var candidates = await (
            from ladder in db.Ladders
            join version in db.PlaylistVersions
                on ladder.PlaylistVersionId equals version.Id
            where version.PlaylistId == playlist.Id &&
                version.Version < ArcRelayEntrantPlaylistDefinition.Version &&
                ladder.SeasonId == season.Id
            select new { Ladder = ladder, version.Version })
            .ToArrayAsync(cancellationToken);
        var open = candidates
            .Where(value => value.Ladder.Status == LadderStatus.Open)
            .ToArray();
        if (open.Length != 1)
        {
            throw new InvalidOperationException(
                "A fresh Arc Relay rating book can start only from one open " +
                "prior immutable ladder.");
        }
        int latestRegisteredVersion = candidates.Max(value => value.Version);
        if (open[0].Version != latestRegisteredVersion)
        {
            throw new InvalidOperationException(
                "The open Arc Relay rating book cannot skip a newer registered " +
                $"intermediate ladder (open v{open[0].Version}, latest " +
                $"registered v{latestRegisteredVersion}).");
        }
        Ladder previous = open[0].Ladder;
        if (await db.ArcRelayEntrantRatings.AnyAsync(
                value => value.LadderId == current.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"The draft Arc Relay v{ArcRelayEntrantPlaylistDefinition.Version} " +
                "ladder already contains ratings.");
        }

        Guid[] entrants = await db.ArcRelayEntrants
            .Where(value => value.LadderOptedIn)
            .OrderBy(value => value.Id)
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);
        foreach (Guid entrantId in entrants)
        {
            db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
            {
                EntrantId = entrantId,
                LadderId = current.Id,
            });
        }

        previous.Status = LadderStatus.Closed;
        previous.IsListed = false;
        current.Status = LadderStatus.Open;
        current.IsListed = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RepairUnpublishedTapline9Async(
        PlaylistVersion version,
        ArcRelayEntrantPlaylistDefinition expected,
        CancellationToken cancellationToken)
        => await RepairUnpublishedCurrentAsync(
            version,
            expected,
            UnpublishedMistakenTapline9DefinitionFingerprint,
            cancellationToken);

    private async Task RepairUnpublishedCurrentAsync(
        PlaylistVersion version,
        ArcRelayEntrantPlaylistDefinition expected,
        string repairableFingerprint,
        CancellationToken cancellationToken)
    {
        bool hasMatchHistory = await db.Matches.AnyAsync(
                value => value.PlaylistVersionId == version.Id,
                cancellationToken)
            || await db.MatchSets.AnyAsync(
                value => value.PlaylistVersionId == version.Id,
                cancellationToken);
        if (hasMatchHistory)
        {
            throw new InvalidOperationException(
                "The unpublished Arc Relay playlist definition has "
                + "match history and cannot be corrected in place.");
        }

        // Owner-authorized correction of the unpublished local identity.
        // PostgreSQL normally rejects every PlaylistVersion mutation. Open
        // that guard only around this exact fingerprint-gated row, retain its
        // identity/rating book, then restore the guard before validation.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"PlaylistVersions\" DISABLE TRIGGER "
            + "\"TR_PlaylistVersions_Immutable\";",
            cancellationToken);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "PlaylistVersions"
                SET "GameModeId" = {expected.Match.Rules.GameMode.ModeId},
                    "RulesetId" = {expected.Match.Rules.RulesetId},
                    "MatchFormatId" = {expected.Match.Format.FormatId},
                    "MapPoolId" = {expected.Match.Map.Id},
                    "SeriesPolicyId" = {ArcRelayEntrantPlaylistDefinition.SeriesPolicyId},
                    "MatchmakingPolicyId" = {ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId},
                    "AdmissionPolicyId" = {expected.AdmissionPolicyId},
                    "ExecutionPolicyId" = {expected.ExecutionPolicyId},
                    "ExecutionEngineVersion" = {expected.ExecutionEngineVersion},
                    "CanonicalDefinition" = CAST({expected.CanonicalDefinition} AS jsonb),
                    "DefinitionFingerprint" = {expected.DefinitionFingerprint},
                    "Provenance" = CAST({expected.Provenance} AS jsonb),
                    "Visibility" = {ArcRelayEntrantPlaylistDefinition.Visibility}
                WHERE "Id" = {version.Id}
                  AND "DefinitionFingerprint" = {repairableFingerprint};
                """, cancellationToken);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"PlaylistVersions\" ENABLE TRIGGER "
                + "\"TR_PlaylistVersions_Immutable\";",
                cancellationToken);
        }
        await db.Entry(version).ReloadAsync(cancellationToken);
    }
}

public sealed record ArcRelayEntrantSeedResult(
    PlaylistVersion PlaylistVersion,
    Ladder Ladder);
