namespace BotArena.App.Notifications;

/// <summary>
/// What a push says, per kind.
/// <para>
/// Server-side rather than client-side because a push has to render before the app is
/// running — there is nothing to ask. That makes this the one place where notification
/// prose is written by the server, and it should stay terse: the in-app toast is where the
/// game celebrates, with the bot's sprite and its own colours. A banner cannot do that and
/// should not try.
/// </para>
/// <para>
/// The <c>data</c> payload carries what the app needs to open the right screen on tap,
/// using ids rather than a route so the client owns its own navigation.
/// </para>
/// </summary>
public static class PushCopy
{
    public static (string Title, string Body, Dictionary<string, string> Data)? For(
        UserNotificationPayload payload) =>
        payload switch
        {
            MatchChallengedPayload challenge => (
                $"{challenge.BotName} was challenged",
                $"{challenge.ChallengerName} on {challenge.MapId} — watch it live.",
                new Dictionary<string, string>
                {
                    ["kind"] = UserNotificationKinds.MatchChallenged,
                    ["matchId"] = challenge.MatchId.ToString(),
                }),

            MatchSettledPayload match => (
                $"{match.BotName} {Verb(match.Outcome)}",
                $"against {match.OpponentName} on {match.MapId}.",
                new Dictionary<string, string>
                {
                    ["kind"] = UserNotificationKinds.MatchSettled,
                    ["matchId"] = match.MatchId.ToString(),
                }),

            SetSettledPayload set => (
                // The rating delta belongs in the title: it is the thing the player came
                // for, and a banner may be truncated to its first line.
                $"{set.BotName} {Verb(set.Outcome)} {Signed(set.RatingChange)}",
                $"{set.Score}–{set.OpponentScore} against {set.OpponentName}.",
                new Dictionary<string, string>
                {
                    ["kind"] = UserNotificationKinds.SetSettled,
                    ["matchSetId"] = set.MatchSetId.ToString(),
                }),

            // Entitlements are deliberately not pushed. An unlock is a reward to come back
            // to, not news with a deadline, and the toast delivers it far better than a
            // banner can — see DECISIONS #118.
            _ => null,
        };

    private static string Verb(string outcome) =>
        outcome switch { "Win" => "won", "Loss" => "lost", _ => "drew" };

    private static string Signed(double ratingChange) =>
        $"{(ratingChange >= 0 ? "+" : "")}{Math.Round(ratingChange)}";
}
