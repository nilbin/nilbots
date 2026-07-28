namespace BotArena.Engine;

/// <summary>
/// Authoritative raw Deathmatch counters for one scoring team. Active health
/// is a terminal world-state snapshot and therefore is not persisted here.
/// </summary>
public sealed record DeathmatchTeamScore
{
    public DeathmatchTeamScore(
        int teamId,
        long kills,
        long deaths,
        long damageDealt)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        if (kills < 0)
            throw new ArgumentOutOfRangeException(nameof(kills));
        if (deaths < 0)
            throw new ArgumentOutOfRangeException(nameof(deaths));
        if (damageDealt < 0)
            throw new ArgumentOutOfRangeException(nameof(damageDealt));

        TeamId = teamId;
        Kills = kills;
        Deaths = deaths;
        DamageDealt = damageDealt;
    }

    public int TeamId { get; }
    public long Kills { get; }
    public long Deaths { get; }
    public long DamageDealt { get; }
}
