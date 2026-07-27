namespace BotArena.Engine;

/// <summary>
/// Reproducible joint-damage semantics. Contacts are discovered from one
/// immutable phase snapshot, then applied in a canonical accounting order so
/// health removal, lethal attribution, events, and scores cannot depend on
/// collection iteration order.
/// </summary>
public sealed record ActorDamageResolutionDefinition
{
    public static ActorDamageResolutionDefinition CanonicalJointV1 { get; } =
        new(
            ContactBatchKind
                .CollectAllValidContactsBeforeAnyHealthMutation,
            PerTargetApplicationOrderKind
                .AttributedSourceTeamUnitLifeThenProjectileIdThenContactOrdinalUnattributedLast,
            ProjectileIdentityAssignmentKind
                .MatchWideInt64StartingAtZeroCanonicalSourceActorThenAttackOrdinal,
            ContactOrdinalAssignmentKind
                .MovementDestinationThenExistingTraversalThenNewLaunchPathSubsteps,
            HealthApplicationKind
                .SequentialActualHealthRemovedCappedToRemainingHealth,
            DestructionAttributionKind
                .FirstOrderedContactReducingPositiveHealthToZero,
            EventOrderKind
                .TargetTeamUnitLifeThenPerTargetApplicationOrder);

    public ActorDamageResolutionDefinition(
        ContactBatchKind contactBatch,
        PerTargetApplicationOrderKind perTargetApplicationOrder,
        ProjectileIdentityAssignmentKind projectileIdentityAssignment,
        ContactOrdinalAssignmentKind contactOrdinalAssignment,
        HealthApplicationKind healthApplication,
        DestructionAttributionKind destructionAttribution,
        EventOrderKind eventOrder)
    {
        if (!Enum.IsDefined(contactBatch))
            throw new ArgumentOutOfRangeException(nameof(contactBatch));
        if (!Enum.IsDefined(perTargetApplicationOrder))
        {
            throw new ArgumentOutOfRangeException(
                nameof(perTargetApplicationOrder));
        }
        if (!Enum.IsDefined(projectileIdentityAssignment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectileIdentityAssignment));
        }
        if (!Enum.IsDefined(contactOrdinalAssignment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(contactOrdinalAssignment));
        }
        if (!Enum.IsDefined(healthApplication))
            throw new ArgumentOutOfRangeException(nameof(healthApplication));
        if (!Enum.IsDefined(destructionAttribution))
        {
            throw new ArgumentOutOfRangeException(
                nameof(destructionAttribution));
        }
        if (!Enum.IsDefined(eventOrder))
            throw new ArgumentOutOfRangeException(nameof(eventOrder));

        ContactBatch = contactBatch;
        PerTargetApplicationOrder = perTargetApplicationOrder;
        ProjectileIdentityAssignment = projectileIdentityAssignment;
        ContactOrdinalAssignment = contactOrdinalAssignment;
        HealthApplication = healthApplication;
        DestructionAttribution = destructionAttribution;
        EventOrder = eventOrder;
    }

    public ContactBatchKind ContactBatch { get; }
    public PerTargetApplicationOrderKind PerTargetApplicationOrder { get; }
    public ProjectileIdentityAssignmentKind ProjectileIdentityAssignment
    {
        get;
    }
    public ContactOrdinalAssignmentKind ContactOrdinalAssignment { get; }
    public HealthApplicationKind HealthApplication { get; }
    public DestructionAttributionKind DestructionAttribution { get; }
    public EventOrderKind EventOrder { get; }

    public enum ContactBatchKind
    {
        CollectAllValidContactsBeforeAnyHealthMutation = 0,
    }

    public enum PerTargetApplicationOrderKind
    {
        AttributedSourceTeamUnitLifeThenProjectileIdThenContactOrdinalUnattributedLast
            = 0,
    }

    public enum ProjectileIdentityAssignmentKind
    {
        /// <summary>
        /// Projectile IDs are checked signed 64-bit integers, monotonically
        /// assigned from zero. Same-tick launches are ordered by source
        /// team/unit/life identity and then that actor's attack ordinal.
        /// </summary>
        MatchWideInt64StartingAtZeroCanonicalSourceActorThenAttackOrdinal = 0,
    }

    public enum ContactOrdinalAssignmentKind
    {
        /// <summary>
        /// Contact ordinals follow the collision phase categories and the
        /// exact tile-entry order within each projectile path.
        /// </summary>
        MovementDestinationThenExistingTraversalThenNewLaunchPathSubsteps = 0,
    }

    public enum HealthApplicationKind
    {
        SequentialActualHealthRemovedCappedToRemainingHealth = 0,
    }

    public enum DestructionAttributionKind
    {
        FirstOrderedContactReducingPositiveHealthToZero = 0,
    }

    public enum EventOrderKind
    {
        TargetTeamUnitLifeThenPerTargetApplicationOrder = 0,
    }
}
