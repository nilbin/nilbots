using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

public static class FrontlineLabsEndpoints
{
    public static void MapFrontlineLabs(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/labs");

        group.MapGet("/", async (
            FrontlineLabsSettings settings,
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!settings.Enabled)
            {
                return Results.Ok(
                    new LabsCatalogResponse(false, []));
            }

            FrontlineLabsPlaylistDefinition expected =
                FrontlineLabsPlaylistDefinition.Create();
            var persisted = await (
                    from version in db.PlaylistVersions.AsNoTracking()
                    join playlist in db.Playlists.AsNoTracking()
                        on version.PlaylistId equals playlist.Id
                    where playlist.Key ==
                          FrontlineLabsPlaylistDefinition.PlaylistKey
                          && version.Version ==
                          FrontlineLabsPlaylistDefinition.Version
                    select new { Playlist = playlist, Version = version })
                .SingleOrDefaultAsync(cancellationToken);
            if (persisted is null)
            {
                throw new InvalidOperationException(
                    "Frontline Labs is enabled but its immutable playlist " +
                    "identity has not been seeded. Run the migrate role.");
            }

            expected.Validate(persisted.Playlist, persisted.Version);
            MatchFormatDefinition format = expected.Match.Format;
            return Results.Ok(new LabsCatalogResponse(
                true,
                [
                    new LabsPlaylistResponse(
                        persisted.Version.Id,
                        persisted.Playlist.Key,
                        persisted.Playlist.DisplayName,
                        persisted.Version.Version,
                        persisted.Version.GameModeId,
                        persisted.Version.RulesetId,
                        persisted.Version.MatchFormatId,
                        format.ParticipantCount,
                        format.ScoringTeamCount,
                        format.ParticipantsPerTeam,
                        persisted.Version.AdmissionPolicyId),
                ]));
        }).Produces<LabsCatalogResponse>();

        group.MapPost("/matches", async (
            CreateLabsMatchRequest request,
            ClaimsPrincipal principal,
            FrontlineLabsSettings settings,
            AppDbContext db,
            MatchAdmissionService admission,
            MatchParticipantSnapshotFactory snapshots,
            UnrankedMatchLimits unrankedLimits,
            TimeProvider timeProvider,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (!settings.Enabled)
                return Results.NotFound();
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();

            FrontlineLabsPlaylistDefinition expected =
                FrontlineLabsPlaylistDefinition.Create();
            int participantCount =
                expected.Match.Format.ParticipantCount;
            Guid[] entrantIds = request.EntrantBotIds?.ToArray() ?? [];
            if (entrantIds.Length != participantCount ||
                entrantIds.Any(id => id == Guid.Empty) ||
                entrantIds.Distinct().Count() != entrantIds.Length)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Labs entrants.",
                    detail:
                        $"This playlist requires exactly {participantCount} distinct bots.");
            }

            await using var admissionScope =
                await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.TakeAdmissionLockAsync(
                AdmissionLocks.LabsMatchPool,
                cancellationToken);
            await db.Database.TakeAdmissionLockAsync(
                AdmissionLocks.Unranked(userId),
                cancellationToken);

            DateTime dayAgo =
                timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);
            int startedToday = await db.Matches.CountAsync(
                candidate =>
                    candidate.InitiatedByUserId == userId &&
                    candidate.MatchSetId == null &&
                    candidate.CreatedAt >= dayAgo,
                cancellationToken);
            if (startedToday >= unrankedLimits.AccountDailyLimit)
            {
                return Results.Problem(
                    $"Accounts may start at most " +
                    $"{unrankedLimits.AccountDailyLimit} unranked matches " +
                    "per 24 hours.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            PlaylistVersion? playlistVersion =
                await db.PlaylistVersions.SingleOrDefaultAsync(
                    candidate =>
                        candidate.Id == request.PlaylistVersionId,
                    cancellationToken);
            if (playlistVersion is null ||
                playlistVersion.Version !=
                    FrontlineLabsPlaylistDefinition.Version)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Labs playlist not found.",
                    detail:
                        "The requested Labs playlist is not available.");
            }
            Playlist playlist = await db.Playlists.SingleAsync(
                candidate =>
                    candidate.Id == playlistVersion.PlaylistId,
                cancellationToken);
            try
            {
                expected.Validate(playlist, playlistVersion);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Labs playlist not found.",
                    detail:
                        "The requested Labs playlist is not available.");
            }

            int labsStartedToday = await db.Matches.CountAsync(
                candidate =>
                    candidate.InitiatedByUserId == userId &&
                    candidate.CreatedAt >= dayAgo &&
                    candidate.PlaylistVersionId != null &&
                    db.PlaylistVersions.Any(version =>
                        version.Id == candidate.PlaylistVersionId &&
                        version.Visibility == PlaylistVisibilityIds.Labs),
                cancellationToken);
            if (labsStartedToday >= settings.AccountDailyLimit)
            {
                return Results.Problem(
                    $"Accounts may start at most " +
                    $"{settings.AccountDailyLimit} Labs matches per 24 hours.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            int accountActive = await db.Matches.CountAsync(
                candidate =>
                    candidate.InitiatedByUserId == userId &&
                    (candidate.Status == MatchStatus.Pending ||
                     candidate.Status == MatchStatus.Running) &&
                    candidate.PlaylistVersionId != null &&
                    db.PlaylistVersions.Any(version =>
                        version.Id == candidate.PlaylistVersionId &&
                        version.Visibility == PlaylistVisibilityIds.Labs),
                cancellationToken);
            if (accountActive >= settings.AccountActiveLimit)
            {
                return Results.Problem(
                    $"Accounts may have at most " +
                    $"{settings.AccountActiveLimit} active Labs matches at a time.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            int globalActive = await db.Matches.CountAsync(
                candidate =>
                    (candidate.Status == MatchStatus.Pending ||
                     candidate.Status == MatchStatus.Running) &&
                    candidate.PlaylistVersionId != null &&
                    db.PlaylistVersions.Any(version =>
                        version.Id == candidate.PlaylistVersionId &&
                        version.Visibility == PlaylistVisibilityIds.Labs),
                cancellationToken);
            if (globalActive >= settings.GlobalActiveLimit)
            {
                return Results.Problem(
                    "The Labs match pool is currently full. Try again later.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var admitted =
                new List<AdmittedMatchBot>(participantCount);
            for (int index = 0; index < entrantIds.Length; index++)
            {
                ApplicationResult<AdmittedMatchBot> candidate =
                    await admission.AdmitForProfileAsync(
                        entrantIds[index],
                        index == 0 ? userId : null,
                        expected.AdmissionPolicyId,
                        cancellationToken);
                if (!candidate.Succeeded)
                    return candidate.Error!.ToProblemDetails(http);
                admitted.Add(candidate.Value!);
            }

            long seed = request.Seed ?? Random.Shared.NextInt64();
            ActorResolvedMatchDefinition definition = expected.Match;
            var match = new Match
            {
                MapId = definition.Map.Id,
                MapVersion = definition.Map.Version,
                Seed = seed,
                InitiatedByUserId = userId,
                GameRulesVersion = definition.Rules.RulesetId,
                RuntimeConfigurationVersion =
                    definition.CapabilityVersions
                        .RuntimeConfigurationVersion,
                PlaylistVersionId = playlistVersion.Id,
            };
            PublicParticipant[] topologyParticipants =
                definition.Topology.Participants
                    .OrderBy(participant => participant.ParticipantId)
                    .ToArray();
            for (int index = 0;
                 index < admitted.Count;
                 index++)
            {
                PublicParticipant topologyParticipant =
                    topologyParticipants[index];
                MatchParticipant participant = snapshots.Create(
                    match.Id,
                    topologyParticipant.ParticipantId,
                    admitted[index]);
                participant.TeamId = topologyParticipant.TeamId;
                match.Participants.Add(participant);
            }

            db.Matches.Add(match);
            db.BackgroundJobs.Add(
                BackgroundJob.ExecuteGenericActorMatch(
                    match.Id,
                    FrontlineLabsPlaylistDefinition.PlaylistKey,
                    FrontlineLabsPlaylistDefinition.Version));
            await db.SaveChangesAsync(cancellationToken);
            await admissionScope.CommitAsync(cancellationToken);
            return Results.Ok(new CreatedMatchResponse(match.Id));
        }).Produces<CreatedMatchResponse>()
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Labs);
    }
}
