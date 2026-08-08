using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One tick's reading of the active objective, as the capture kernel consumes
/// it. Every control policy reads <see cref="DenialWeightByTeam"/> — the
/// positive objective weight each scoring team has standing on the region,
/// which is what "objective weight" has always meant. The capture channel
/// additionally reads which of that weight held its tile and how much hostile
/// damage landed on the region, and no other policy may carry either.
/// </summary>
public sealed record FrontlineObjectivePresence
{
    /// <summary>
    /// A plain presence reading, for every control policy that resolves from
    /// objective weight alone.
    /// </summary>
    public FrontlineObjectivePresence(
        IReadOnlyDictionary<int, int> denialWeightByTeam)
    {
        ArgumentNullException.ThrowIfNull(denialWeightByTeam);
        DenialWeightByTeam = denialWeightByTeam
            .ToImmutableSortedDictionary();
        StationaryClaimWeightByTeam =
            ImmutableSortedDictionary<int, int>.Empty;
        HostileDamageOnObjectiveByTeam =
            ImmutableSortedDictionary<int, long>.Empty;
        IsChannelReading = false;
    }

    /// <summary>
    /// A channel reading. <paramref name="stationaryClaimWeightByTeam"/> is
    /// the subset of each team's objective weight whose position at the end
    /// of this tick equals its position at the end of the previous tick — a
    /// life with no previous position counts as stationary — and
    /// <paramref name="hostileDamageOnObjectiveByTeam"/> is the actual health
    /// removed this tick from that team's bodies standing on the region.
    /// </summary>
    public FrontlineObjectivePresence(
        IReadOnlyDictionary<int, int> denialWeightByTeam,
        IReadOnlyDictionary<int, int> stationaryClaimWeightByTeam,
        IReadOnlyDictionary<int, long> hostileDamageOnObjectiveByTeam)
    {
        ArgumentNullException.ThrowIfNull(denialWeightByTeam);
        ArgumentNullException.ThrowIfNull(stationaryClaimWeightByTeam);
        ArgumentNullException.ThrowIfNull(hostileDamageOnObjectiveByTeam);

        DenialWeightByTeam = denialWeightByTeam
            .ToImmutableSortedDictionary();
        StationaryClaimWeightByTeam = stationaryClaimWeightByTeam
            .ToImmutableSortedDictionary();
        HostileDamageOnObjectiveByTeam = hostileDamageOnObjectiveByTeam
            .ToImmutableSortedDictionary();
        IsChannelReading = true;
    }

    /// <summary>
    /// Positive objective weight per team, counting every body on the region
    /// whether it moved or not. Denial never requires stillness.
    /// </summary>
    public ImmutableSortedDictionary<int, int> DenialWeightByTeam { get; }

    /// <summary>
    /// The stationary part of that weight per team. Empty on a non-channel
    /// reading, where it has no meaning.
    /// </summary>
    public ImmutableSortedDictionary<int, int> StationaryClaimWeightByTeam
    {
        get;
    }

    /// <summary>
    /// Actual health removed this tick from each team's bodies standing on
    /// the region, by a hostile source. Empty on a non-channel reading.
    /// </summary>
    public ImmutableSortedDictionary<int, long>
        HostileDamageOnObjectiveByTeam
    { get; }

    /// <summary>
    /// True when the reading carries the channel's two extra facts. The
    /// kernel requires this to agree with the declared control policy, so a
    /// caller can never silently hand a channel reading to a policy that
    /// would ignore it.
    /// </summary>
    public bool IsChannelReading { get; }
}
