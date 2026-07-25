using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BotArena.App.Notifications;

[Authorize]
public sealed class UserNotificationsHub : Hub
{
    public const string ClientMethod = "notification";
}
