using System.Collections.Concurrent;
using BotArena.App.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BotArena.App.Notifications;

public sealed record PostgresNotificationOptions(string ConnectionString);

/// <summary>
/// PostgreSQL NOTIFY crosses the process boundary between match/compile workers
/// and every web node. Each web node then sends the durable notification to its
/// own connected SignalR clients. Missed realtime events remain in the inbox.
/// </summary>
public sealed class PostgresNotificationListener(
    PostgresNotificationOptions options,
    IServiceScopeFactory scopeFactory,
    IHubContext<UserNotificationsHub> hub,
    ILogger<PostgresNotificationListener> logger)
    : BackgroundService
{
    public const string Channel = "nilbots_user_notifications";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Notification listener disconnected; reconnecting");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var pending = new ConcurrentQueue<Guid>();
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        connection.Notification += (_, args) =>
        {
            if (Guid.TryParse(args.Payload, out Guid notificationId))
                pending.Enqueue(notificationId);
        };
        await connection.OpenAsync(cancellationToken);
        await using (NpgsqlCommand listen = connection.CreateCommand())
        {
            listen.CommandText = $"LISTEN {Channel}";
            await listen.ExecuteNonQueryAsync(cancellationToken);
        }
        logger.LogInformation("Notification listener connected");

        while (!cancellationToken.IsCancellationRequested)
        {
            await connection.WaitAsync(cancellationToken);
            while (pending.TryDequeue(out Guid notificationId))
                await BroadcastAsync(notificationId, cancellationToken);
        }
    }

    private async Task BroadcastAsync(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserNotification? notification = await db.UserNotifications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == notificationId,
                cancellationToken);
        if (notification is null)
            return;

        await hub.Clients
            .User(notification.UserId.ToString())
            .SendAsync(
                UserNotificationsHub.ClientMethod,
                UserNotificationContracts.ToResponse(notification),
                cancellationToken);
    }
}
