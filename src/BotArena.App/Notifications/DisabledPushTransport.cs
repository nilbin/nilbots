namespace BotArena.App.Notifications;

/// <summary>
/// The transport for a deployment that does not send push at all.
/// <para>
/// The default, and deliberately so. Local development, tests and the CLI-facing roles
/// have no business reaching out to Expo, and a misconfigured production would otherwise
/// discover its push setup by sending real notifications to real phones.
/// </para>
/// <para>
/// It reports every message as failed rather than sent. Recording a delivery that never
/// happened would make the records lie, and "failed, push disabled" is the truth — the
/// notification itself was still written and still arrives in-app.
/// </para>
/// </summary>
public sealed class DisabledPushTransport : IPushTransport
{
    public Task<IReadOnlyList<PushResult>> SendAsync(
        IReadOnlyList<PushMessage> messages,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PushResult>>(
            messages
                .Select(message => new PushResult(
                    message.PushToken,
                    Ok: false,
                    Error: "push transport disabled",
                    // Not dead — the token is fine, this deployment simply does not send.
                    // Dropping registrations here would empty the table on every restart
                    // of a server whose push config had not been switched on yet.
                    TokenIsDead: false))
                .ToList());
}
