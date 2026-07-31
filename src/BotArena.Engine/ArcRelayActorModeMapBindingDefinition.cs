using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Resolves Arc Relay's stable Well order and participant-relative home facts
/// onto format-3 map regions.
/// </summary>
public sealed record ArcRelayActorModeMapBindingDefinition
    : ActorModeMapBindingDefinition
{
    public ArcRelayActorModeMapBindingDefinition(
        IReadOnlyList<string> orderedWellRegionIds,
        string reactorRegionRoleId,
        string homePadRegionRoleId)
    {
        ArgumentNullException.ThrowIfNull(orderedWellRegionIds);
        string[] wells = [.. orderedWellRegionIds];
        if (wells.Length == 0
            || wells.Any(string.IsNullOrWhiteSpace)
            || wells.Distinct(StringComparer.Ordinal).Count() != wells.Length)
        {
            throw new ArgumentException(
                "Arc Relay must bind unique non-blank Well region IDs.",
                nameof(orderedWellRegionIds));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(reactorRegionRoleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(homePadRegionRoleId);
        if (string.Equals(
                reactorRegionRoleId,
                homePadRegionRoleId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Arc Relay reactor and home-pad roles must differ.",
                nameof(homePadRegionRoleId));
        }

        OrderedWellRegionIds = wells.ToImmutableArray();
        ReactorRegionRoleId = reactorRegionRoleId;
        HomePadRegionRoleId = homePadRegionRoleId;
    }

    public override ActorModeMapBindingDefinitionKind Kind =>
        ActorModeMapBindingDefinitionKind.ArcRelay;
    public ImmutableArray<string> OrderedWellRegionIds { get; }
    public string ReactorRegionRoleId { get; }
    public string HomePadRegionRoleId { get; }
}
