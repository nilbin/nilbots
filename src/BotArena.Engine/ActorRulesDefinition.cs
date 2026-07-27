using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete serializer-neutral generation-3 actor rules. Match format,
/// topology, deployment, and map bindings remain separate match inputs.
/// </summary>
public sealed record ActorRulesDefinition
{
    public const int CurrentSchemaVersion = 3;

    public ActorRulesDefinition(
        string rulesetId,
        ActorRulesLimits limits,
        ActorSeedMechanicsDefinition seedMechanics,
        GameModeDefinition gameMode,
        ActorLifecycleDefinition lifecycle,
        IEnumerable<ActorFormDefinition> forms,
        IEnumerable<ActorMovementProfileDefinition> movementProfiles,
        IEnumerable<ActorVisionProfileDefinition> visionProfiles,
        IEnumerable<ActorAttackProfileDefinition> attackProfiles,
        IEnumerable<ActorActionDefinition> actions,
        IEnumerable<ActorFabricationTransitionDefinition>
            fabricationTransitions,
        IEnumerable<ActorSameLifeTransitionDefinition> sameLifeTransitions,
        IEnumerable<ActorReplicationTransitionDefinition>
            replicationTransitions,
        ActorTeamPerceptionDefinition teamPerception,
        ActorCollisionDefinition collisions,
        ActorTickResolutionDefinition tickResolution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesetId);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(seedMechanics);
        ArgumentNullException.ThrowIfNull(gameMode);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(forms);
        ArgumentNullException.ThrowIfNull(movementProfiles);
        ArgumentNullException.ThrowIfNull(visionProfiles);
        ArgumentNullException.ThrowIfNull(attackProfiles);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(fabricationTransitions);
        ArgumentNullException.ThrowIfNull(sameLifeTransitions);
        ArgumentNullException.ThrowIfNull(replicationTransitions);
        ArgumentNullException.ThrowIfNull(teamPerception);
        ArgumentNullException.ThrowIfNull(collisions);
        ArgumentNullException.ThrowIfNull(tickResolution);

        ActorFormDefinition[] formSnapshot = [.. forms];
        ActorMovementProfileDefinition[] movementSnapshot =
            [.. movementProfiles];
        ActorVisionProfileDefinition[] visionSnapshot = [.. visionProfiles];
        ActorAttackProfileDefinition[] attackSnapshot = [.. attackProfiles];
        ActorActionDefinition[] actionSnapshot = [.. actions];
        ActorFabricationTransitionDefinition[] fabricationSnapshot =
            [.. fabricationTransitions];
        ActorSameLifeTransitionDefinition[] sameLifeSnapshot =
            [.. sameLifeTransitions];
        ActorReplicationTransitionDefinition[] replicationSnapshot =
            [.. replicationTransitions];

        ActorRulesDefinitionValidator.ValidateComponents(
            rulesetId,
            limits,
            seedMechanics,
            gameMode,
            lifecycle,
            formSnapshot,
            movementSnapshot,
            visionSnapshot,
            attackSnapshot,
            actionSnapshot,
            fabricationSnapshot,
            sameLifeSnapshot,
            replicationSnapshot);

        RulesetId = rulesetId;
        Limits = limits;
        SeedMechanics = seedMechanics;
        GameMode = gameMode;
        Lifecycle = lifecycle;
        Forms = formSnapshot
            .OrderBy(form => form.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        MovementProfiles = movementSnapshot
            .OrderBy(profile => profile.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        VisionProfiles = visionSnapshot
            .OrderBy(profile => profile.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        AttackProfiles = attackSnapshot
            .OrderBy(profile => profile.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        Actions = actionSnapshot
            .OrderBy(action => action.Code)
            .ThenBy(action => action.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        FabricationTransitions = fabricationSnapshot
            .OrderBy(
                transition => transition.TransitionId,
                StringComparer.Ordinal)
            .ToImmutableArray();
        SameLifeTransitions = sameLifeSnapshot
            .OrderBy(
                transition => transition.TransitionId,
                StringComparer.Ordinal)
            .ToImmutableArray();
        ReplicationTransitions = replicationSnapshot
            .OrderBy(
                transition => transition.TransitionId,
                StringComparer.Ordinal)
            .ToImmutableArray();
        TeamPerception = teamPerception;
        Collisions = collisions;
        TickResolution = tickResolution;
    }

    public int SchemaVersion => CurrentSchemaVersion;
    public string RulesetId { get; }
    public ActorRulesLimits Limits { get; }
    public ActorSeedMechanicsDefinition SeedMechanics { get; }
    public GameModeDefinition GameMode { get; }
    public ActorLifecycleDefinition Lifecycle { get; }
    public ImmutableArray<ActorFormDefinition> Forms { get; }
    public ImmutableArray<ActorMovementProfileDefinition> MovementProfiles
    {
        get;
    }
    public ImmutableArray<ActorVisionProfileDefinition> VisionProfiles
    {
        get;
    }
    public ImmutableArray<ActorAttackProfileDefinition> AttackProfiles
    {
        get;
    }
    public ImmutableArray<ActorActionDefinition> Actions { get; }
    public ImmutableArray<ActorFabricationTransitionDefinition>
        FabricationTransitions { get; }
    public ImmutableArray<ActorSameLifeTransitionDefinition>
        SameLifeTransitions { get; }
    public ImmutableArray<ActorReplicationTransitionDefinition>
        ReplicationTransitions { get; }
    public ActorTeamPerceptionDefinition TeamPerception { get; }
    public ActorCollisionDefinition Collisions { get; }
    public ActorTickResolutionDefinition TickResolution { get; }
}
