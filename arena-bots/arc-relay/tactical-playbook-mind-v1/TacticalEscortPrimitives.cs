using BotArena.Sdk;

/// <summary>
/// The follower substrate for escorted orders — pure geometry, no arena
/// state. A formation puts a body on a slot measured off the DESTINATION,
/// which is why a trailing slot ends up standing on the leader's way back
/// out of a corridor. A follower instead reads the LEADER: where it is and
/// which way it is about to step. Everything specific to a posture lives in
/// one named function behind <see cref="DesiredTile"/>, and everything
/// specific to "how many followers" lives in the caller's ordinal — so
/// screen/flank postures and tank/utility escorts join by extending the
/// switch and the list, not by rewriting the mechanism.
/// </summary>
internal static class TacticalEscortPrimitives
{
    /// <summary>
    /// Where follower <paramref name="ordinal"/> (0-based, in the order's
    /// declared precedence) wants to stand this tick.
    /// </summary>
    /// <param name="leader">The leader's tile right now.</param>
    /// <param name="leaderStep">The tile the leader is about to step into,
    /// or null when it is not moving.</param>
    /// <param name="leaderFacing">Fallback heading when the leader is
    /// standing still: a stationary body's back is behind its face.</param>
    internal static Position DesiredTile(
        string posture,
        Position leader,
        Position? leaderStep,
        Direction leaderFacing,
        int ordinal,
        int leash) => posture switch
        {
            "trail" => Trail(leader, leaderStep, leaderFacing, ordinal, leash),
            _ => throw new InvalidDataException(
                $"Unknown escort posture '{posture}'."),
        };

    /// <summary>
    /// Behind the leader along its own line of travel: follower 0 one tile
    /// back, follower 1 two tiles back, and so on up to the leash. "Behind"
    /// is measured against where the leader is GOING, so the instant it
    /// turns around, the whole file's wanted ground turns around with it and
    /// nobody is left standing in the doorway.
    /// </summary>
    private static Position Trail(
        Position leader,
        Position? leaderStep,
        Direction leaderFacing,
        int ordinal,
        int leash)
    {
        (int dx, int dy) = leaderStep is Position step
            && step != leader
            ? (step.X - leader.X, step.Y - leader.Y)
            : leaderFacing switch
            {
                Direction.North => (0, -1),
                Direction.South => (0, 1),
                Direction.East => (1, 0),
                _ => (-1, 0),
            };
        int back = Math.Min(ordinal + 1, Math.Max(leash, 1));
        return leader.Offset(-dx * back, -dy * back);
    }
}
