using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One form assembled from named movement, vision, attack, and action
/// capabilities. Cross-catalog reference validation belongs to the resolved
/// rules aggregate.
/// </summary>
public sealed record ActorFormDefinition
{
    public ActorFormDefinition(
        string id,
        int maxHealth,
        string movementProfileId,
        string visionProfileId,
        string? attackProfileId,
        int objectiveWeight,
        IEnumerable<string> allowedActionIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(movementProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(visionProfileId);
        if (attackProfileId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(attackProfileId);
        ArgumentNullException.ThrowIfNull(allowedActionIds);
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth));
        if (objectiveWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(objectiveWeight));

        ImmutableArray<string> actionIds = allowedActionIds
            .Select(value => value)
            .ToImmutableArray();
        if (actionIds.IsEmpty || actionIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "A form must allow at least one non-blank action ID.",
                nameof(allowedActionIds));
        }
        if (actionIds.Distinct(StringComparer.Ordinal).Count()
            != actionIds.Length)
        {
            throw new ArgumentException(
                "Allowed action IDs cannot contain duplicates.",
                nameof(allowedActionIds));
        }

        Id = id;
        MaxHealth = maxHealth;
        MovementProfileId = movementProfileId;
        VisionProfileId = visionProfileId;
        AttackProfileId = attackProfileId;
        ObjectiveWeight = objectiveWeight;
        AllowedActionIds = actionIds
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public string Id { get; }
    public int MaxHealth { get; }
    public string MovementProfileId { get; }
    public string VisionProfileId { get; }
    public string? AttackProfileId { get; }
    public int ObjectiveWeight { get; }
    public ImmutableArray<string> AllowedActionIds { get; }
}
