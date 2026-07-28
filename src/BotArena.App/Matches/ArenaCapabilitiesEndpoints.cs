using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;

namespace BotArena.App.Matches;

public static class ArenaCapabilitiesEndpoints
{
    public static void MapArenaCapabilities(
        this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/arena", async (
            ClaimsPrincipal principal,
            MatchExecutionSettings matchSettings,
            ArenaAllowanceService allowances,
            MatchPlayabilityService playability,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();

            (
                ArenaAllowanceResponse unranked,
                RankedArenaAllowanceResponse ranked
            ) = await allowances.ProjectAsync(
                userId,
                cancellationToken);
            IReadOnlyList<MatchPlayabilityResponse> bots =
                await playability.ProjectAsync(
                    userId,
                    cancellationToken);

            DuelArenaDefinition duel =
                DuelArenaDefinition.Official;
            var format = new DuelArenaFormatResponse(
                matchSettings.MatchRules.RulesVersion,
                BotContractProfiles.LegacyDuel,
                new ArenaUnrankedFormatResponse(
                    DuelArenaDefinition.UnrankedGamesPerMatch,
                    duel.DefaultUnrankedMapId),
                new ArenaRankedFormatResponse(
                    DuelMirrored6V1.GameCount,
                    DuelMirrored6V1.MapPairCount,
                    DuelMirrored6V1.UsesMirroredSlots,
                    duel.RankedMapPool,
                    RankedMatchmaking.PoolSize));
            return Results.Ok(new ArenaCapabilitiesResponse(
                format,
                unranked,
                ranked,
                bots));
        }).Produces<ArenaCapabilitiesResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
