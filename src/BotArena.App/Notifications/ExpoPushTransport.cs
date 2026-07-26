using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BotArena.App.Notifications;

/// <summary>
/// Sends through Expo's push service.
/// <para>
/// One HTTP call for both platforms, and no APNs certificates or FCM keys to provision and
/// rotate — the setup cost avoided is the reason this is worth a third party in the
/// delivery path at this size (DECISIONS #118).
/// </para>
/// <para>
/// Expo answers 200 with a per-message ticket even when individual messages failed, so the
/// status code says nothing about delivery. The ticket array is the result, positionally
/// matched to the request, and <c>DeviceNotRegistered</c> in it is the signal to drop a
/// registration rather than keep retrying it.
/// </para>
/// </summary>
public sealed class ExpoPushTransport(HttpClient http, ILogger<ExpoPushTransport> logger)
    : IPushTransport
{
    /// <summary>Expo's documented cap per request.</summary>
    private const int BatchSize = 100;

    public async Task<IReadOnlyList<PushResult>> SendAsync(
        IReadOnlyList<PushMessage> messages,
        CancellationToken cancellationToken)
    {
        var results = new List<PushResult>(messages.Count);
        foreach (PushMessage[] batch in messages.Chunk(BatchSize))
            results.AddRange(await SendBatchAsync(batch, cancellationToken));
        return results;
    }

    private async Task<IReadOnlyList<PushResult>> SendBatchAsync(
        PushMessage[] batch,
        CancellationToken cancellationToken)
    {
        var payload = batch.Select(message => new ExpoMessage(
            message.PushToken,
            message.Title,
            message.Body,
            message.Data)).ToArray();

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/--/api/v2/push/send", payload, cancellationToken);
        }
        catch (HttpRequestException failure)
        {
            // The whole batch is unresolved, not dead. Throwing asks the job to retry,
            // which is exactly what a durable job is for; marking the tokens dead here
            // would delete good registrations because Expo had a bad minute.
            logger.LogWarning(failure, "Expo push request failed");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Expo push returned {Status}",
                (int)response.StatusCode);
            throw new HttpRequestException($"Expo push returned {(int)response.StatusCode}.");
        }

        ExpoResponse? body = await response.Content
            .ReadFromJsonAsync<ExpoResponse>(cancellationToken);
        IReadOnlyList<ExpoTicket> tickets = body?.Data ?? [];

        return batch.Select((message, index) =>
        {
            // A short array means Expo told us less than we sent. Treating the missing
            // ones as delivered would silently lose them, so they are failures and the job
            // retries; the delivery record keeps the retry from re-sending the rest.
            ExpoTicket? ticket = index < tickets.Count ? tickets[index] : null;
            if (ticket is null)
                return new PushResult(message.PushToken, false, "no ticket returned", false);
            if (ticket.Status == "ok")
                return new PushResult(message.PushToken, true, null, false);
            bool dead = ticket.Details?.Error == "DeviceNotRegistered";
            return new PushResult(message.PushToken, false, ticket.Message ?? ticket.Status, dead);
        }).ToList();
    }

    private sealed record ExpoMessage(
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("data")] IReadOnlyDictionary<string, string> Data);

    private sealed record ExpoResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ExpoTicket>? Data);

    private sealed record ExpoTicket(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("details")] ExpoTicketDetails? Details);

    private sealed record ExpoTicketDetails(
        [property: JsonPropertyName("error")] string? Error);
}
