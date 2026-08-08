using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>One scoring team's public reactor and charge state.</summary>
public sealed record ArcRelayReactorState(
    int TeamId,
    Position Position,
    int ChargePips,
    int IntegritySegments)
{
    /// <summary>
    /// Filled Threefold socket well ids, in the contract's canonical well
    /// order. Empty outside threefold rulesets, where charge is generic;
    /// under threefold, <see cref="ChargePips"/> equals this count.
    /// </summary>
    public ImmutableArray<string> FilledSocketWellIds { get; init; } = [];

    public bool Equals(ArcRelayReactorState? other) =>
        other is not null
        && TeamId == other.TeamId
        && Position == other.Position
        && ChargePips == other.ChargePips
        && IntegritySegments == other.IntegritySegments
        && FilledSocketWellIds.SequenceEqual(
            other.FilledSocketWellIds, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TeamId);
        hash.Add(Position);
        hash.Add(ChargePips);
        hash.Add(IntegritySegments);
        foreach (string wellId in FilledSocketWellIds)
            hash.Add(wellId, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
