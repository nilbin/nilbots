namespace BotArena.App.Matches;

/// <summary>
/// When a match stops withholding its result.
/// <para>
/// One implementation because two would drift: the announcement job is scheduled from
/// this, and the job re-checks against <see cref="Match.BroadcastComplete"/> on arrival.
/// If the two disagreed, every announcement would either fire early — publishing a winner
/// mid-replay — or bounce off the guard and retry until it gave up.
/// </para>
/// </summary>
public static class BroadcastSchedule
{
    /// <summary>
    /// Announcements are scheduled slightly past the boundary. The job's due time is
    /// compared against PostgreSQL's clock while <see cref="Match.BroadcastComplete"/>
    /// uses the application's, and landing exactly on the edge would make a sub-second
    /// skew between them enough to arrive early and bounce.
    /// </summary>
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The instant <paramref name="match"/> becomes fully public, or null when it has no
    /// broadcast to wait for — an unfinished match, or a legacy row with no broadcast
    /// start, which <see cref="Match.PresentationTick"/> already treats as fully visible.
    /// </summary>
    public static DateTime? CompletesAt(Match match)
    {
        if (match.BroadcastStartedAt is not DateTime start)
            return null;
        double ticksPerSecond = Math.Max(0.001, match.PresentationTicksPerSecond);
        // BroadcastComplete wants PresentationTick strictly past EndTick, so the whole of
        // the last tick has to elapse before the result is public. A null EndTick is a
        // valid zero-tick generic result and becomes public at the broadcast start, not
        // during its countdown.
        double presentedTickCount = match.EndTick is int endTick
            ? endTick + 1.0
            : 0;
        return start.AddSeconds(presentedTickCount / ticksPerSecond);
    }

    /// <summary>When the announcement job for <paramref name="match"/> should become due.</summary>
    public static DateTime? AnnounceAt(Match match) => CompletesAt(match) + Margin;
}
