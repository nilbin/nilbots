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
        string homePadRegionRoleId,
        IReadOnlyList<ArcRelayWorkshopRegionBindingDefinition>? workshops = null)
    {
        ArcRelayWorkshopRegionBindingDefinition[] workshopBindings =
            [.. workshops ?? []];
        if (workshopBindings.Any(value => value is null)
            || workshopBindings.Select(value => value.WorkshopId)
                .Distinct(StringComparer.Ordinal).Count()
                != workshopBindings.Length)
        {
            throw new ArgumentException(
                "Workshop bindings must be non-null with unique workshop ids.",
                nameof(workshops));
        }
        Workshops = workshopBindings.ToImmutableArray();
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

    /// <summary>Per-Workshop region binding; empty for historical maps.</summary>
    public ImmutableArray<ArcRelayWorkshopRegionBindingDefinition> Workshops
    { get; }
}

/// <summary>
/// Binds one veterancy Workshop id onto its exact single-tile training
/// region and its explicitly authored contest region. Colocated with the
/// Arc Relay binding it belongs to (the Replay.cs record pattern).
/// </summary>
public sealed record ArcRelayWorkshopRegionBindingDefinition
{
    public ArcRelayWorkshopRegionBindingDefinition(
        string workshopId,
        string trainRegionId,
        string contestRegionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workshopId);
        ArgumentException.ThrowIfNullOrWhiteSpace(trainRegionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contestRegionId);
        if (string.Equals(trainRegionId, contestRegionId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A Workshop's training and contest regions must differ.",
                nameof(contestRegionId));
        }
        WorkshopId = workshopId;
        TrainRegionId = trainRegionId;
        ContestRegionId = contestRegionId;
    }

    public string WorkshopId { get; }
    public string TrainRegionId { get; }
    public string ContestRegionId { get; }
}
