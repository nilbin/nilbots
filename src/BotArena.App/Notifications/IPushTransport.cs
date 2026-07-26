namespace BotArena.App.Notifications;

/// <summary>What a push looks like by the time the transport sees it.</summary>
/// <param name="PushToken">The device to reach.</param>
/// <param name="Title">One line. A push can be plain — the in-app toast is where the game celebrates.</param>
/// <param name="Body">One more line.</param>
/// <param name="Data">Carried through to the app so a tap can open the right screen.</param>
public sealed record PushMessage(
    string PushToken,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

/// <param name="Ok">Whether this device accepted it.</param>
/// <param name="Error">Transport-reported reason, for the delivery record.</param>
/// <param name="TokenIsDead">
/// The token will never work again — uninstalled, or rotated. The registration should be
/// removed rather than retried, or the account fans out to it forever.
/// </param>
public sealed record PushResult(string PushToken, bool Ok, string? Error, bool TokenIsDead);

/// <summary>
/// Sending a push, without saying to whom.
/// <para>
/// The interface exists so the choice of transport stays one class. Expo's push service is
/// what ships — it removes certificate and key management and is one API for both
/// platforms — but it puts a third party in the delivery path, so moving to APNs and FCM
/// directly is a change of implementation rather than of design. Make that move when
/// per-message priority or delivery telemetry starts mattering, not before.
/// </para>
/// </summary>
public interface IPushTransport
{
    Task<IReadOnlyList<PushResult>> SendAsync(
        IReadOnlyList<PushMessage> messages,
        CancellationToken cancellationToken);
}
