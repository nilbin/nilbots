using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Notifications;

public sealed record RegisterDeviceRequest(string PushToken, string DeviceId, string Platform);

public sealed record NotificationPreferenceResponse(string Kind, bool PushEnabled);

public sealed record UpdateNotificationPreferenceRequest(string Kind, bool PushEnabled);

/// <summary>
/// Where a phone says "push to me", and which of those pushes it wants.
/// </summary>
public static class DeviceEndpoints
{
    /// <summary>Kinds a client may express a push preference for — those a push exists for.</summary>
    private static readonly string[] PushableKinds =
    [
        UserNotificationKinds.MatchChallenged,
        UserNotificationKinds.MatchSettled,
        UserNotificationKinds.SetSettled,
    ];

    public static void MapDevices(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/devices").RequireAuthorization();

        /// Register or refresh. Called on sign-in and on every launch: Expo rotates
        /// tokens, and a stale one is indistinguishable from a live one until a send fails.
        group.MapPut("/", async (
            RegisterDeviceRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.PushToken)
                || string.IsNullOrWhiteSpace(request.DeviceId))
            {
                return Results.BadRequest("A push token and device id are required.");
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            // The same physical device signing into a different account must not leave the
            // previous account pushing to it. The token is the address, so whoever holds it
            // last owns it — anything else delivers one player's results to another.
            await db.DeviceRegistrations
                .Where(device => device.PushToken == request.PushToken && device.UserId != userId)
                .ExecuteDeleteAsync(cancellationToken);

            DeviceRegistration? existing = await db.DeviceRegistrations.SingleOrDefaultAsync(
                device => device.UserId == userId && device.DeviceId == request.DeviceId,
                cancellationToken);

            if (existing is null)
            {
                db.DeviceRegistrations.Add(new DeviceRegistration
                {
                    UserId = userId,
                    PushToken = request.PushToken,
                    DeviceId = request.DeviceId,
                    Platform = request.Platform,
                    CreatedAt = now,
                    LastSeenAt = now,
                });
            }
            else
            {
                // A reinstall keeps the device id and mints a new token; updating in place
                // is what stops the account accumulating dead tokens.
                existing.PushToken = request.PushToken;
                existing.Platform = request.Platform;
                existing.LastSeenAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        /// Sign-out, or a player turning push off entirely for this device.
        group.MapDelete("/{deviceId}", async (
            string deviceId,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            await db.DeviceRegistrations
                .Where(device => device.UserId == userId && device.DeviceId == deviceId)
                .ExecuteDeleteAsync(cancellationToken);
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);
    }

    public static void MapNotificationPreferences(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/notifications/preferences").RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();

            // Stored as exceptions, returned as a complete picture: a client should not
            // have to know that an absent row means on.
            Dictionary<string, bool> overrides = await db.NotificationPreferences
                .Where(preference => preference.UserId == userId)
                .ToDictionaryAsync(
                    preference => preference.Kind,
                    preference => preference.PushEnabled,
                    cancellationToken);

            return Results.Ok(PushableKinds
                .Select(kind => new NotificationPreferenceResponse(
                    kind,
                    !overrides.TryGetValue(kind, out bool enabled) || enabled))
                .ToList());
        }).Produces<IReadOnlyList<NotificationPreferenceResponse>>();

        group.MapPut("/", async (
            UpdateNotificationPreferenceRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            if (!PushableKinds.Contains(request.Kind))
                return Results.BadRequest($"'{request.Kind}' is not a pushable notification kind.");

            NotificationPreference? preference = await db.NotificationPreferences
                .SingleOrDefaultAsync(
                    row => row.UserId == userId && row.Kind == request.Kind,
                    cancellationToken);

            if (preference is null)
            {
                db.NotificationPreferences.Add(new NotificationPreference
                {
                    UserId = userId,
                    Kind = request.Kind,
                    PushEnabled = request.PushEnabled,
                    UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
                });
            }
            else
            {
                preference.PushEnabled = request.PushEnabled;
                preference.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            }

            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);
    }
}
