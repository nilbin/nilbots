using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Notifications;

public static class UserNotificationsEndpoints
{
    public static void MapUserNotifications(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes
            .MapGroup("/api/notifications")
            .RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            int? take,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();

            int limit = Math.Clamp(take ?? 20, 1, 50);
            List<UserNotification> notifications = await db.UserNotifications
                .AsNoTracking()
                .Where(notification =>
                    notification.UserId == userId &&
                    notification.ReadAt == null)
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .Take(limit)
                .ToListAsync(cancellationToken);
            notifications.Reverse();
            return Results.Ok(notifications.Select(
                UserNotificationContracts.ToResponse));
        });

        group.MapPost("/{notificationId:guid}/read", async (
            Guid notificationId,
            ClaimsPrincipal principal,
            AppDbContext db,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            await db.UserNotifications
                .Where(notification =>
                    notification.Id == notificationId &&
                    notification.UserId == userId &&
                    notification.ReadAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        notification => notification.ReadAt,
                        now),
                    cancellationToken);
            // Idempotent and non-enumerating: another account cannot use this
            // endpoint to discover whether a notification ID exists.
            return Results.NoContent();
        });
    }
}
