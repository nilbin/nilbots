using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One production-shaped generation-3 Frontline Labs contract. The values are
/// an experimental mechanics arm, not a balance or ranked-play verdict. It is
/// deliberately a new rules/map generation and does not reinterpret or mutate
/// the frozen <c>frontline-alpha-1</c> contract.
/// </summary>
public static class FrontlineLabsDefinition
{
    public const string PlaylistKey = "frontline-labs";
    public const string RulesetId = "frontline-labs-1";
    public const string MapId = "frontline-labs-01";
    public const string MatchFormatId =
        HeadToHeadMatchFormatDefinition.Id;

    private const string PrimeFormId = "prime-mobile";
    private const string ChildFormId = "child-mobile";
    private const string ReplicaFormId = "replica-mobile";
    private const string TurretFormId = "turret";
    private const string GroundMovementId = "ground";
    private const string MobileVisionId = "mobile-vision";
    private const string TurretVisionId = "turret-vision";
    private const string MobileAttackId = "mobile-bolt";
    private const string TurretAttackId = "turret-bolt";
    private const string MobilizeActionId = "mobilize";
    private const string PrimeLifecycleId = "prime-respawn";
    private const string ChildLifecycleId = "child-ready";
    private const string FabricationSourceRoleId =
        "fabrication-source";
    private const string FabricationOutputRoleId =
        "fabrication-output";
    private const string RemoteFabricationSourceRegionId =
        "fabrication-source-anywhere";

    public static ActorResolvedMatchDefinition Create() =>
        CreateResolved(
            RulesetId,
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false);

    /// <summary>
    /// Creates a local-only, content-identified capture-threshold arm without
    /// reinterpreting the immutable hosted <see cref="RulesetId"/> contract.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateCaptureThresholdExperiment(int captureThreshold)
    {
        if (captureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(captureThreshold),
                captureThreshold,
                "Capture threshold must be positive.");
        }

        return CreateResolved(
            $"{RulesetId}-experiment-capture-{captureThreshold}",
            captureThreshold,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false);
    }

    /// <summary>
    /// Creates a local-only capture-gain phase arm. Hosted v1 remains static;
    /// the candidate publishes its complete schedule in the resolved contract.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateCaptureGainPhaseExperiment(
            int startsAtTick,
            int gainPerSoleTeamTick)
    {
        if (startsAtTick <= 0 || startsAtTick >= 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startsAtTick),
                startsAtTick,
                "The phase must start after tick zero and before MaxTicks.");
        }
        if (gainPerSoleTeamTick <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gainPerSoleTeamTick),
                gainPerSoleTeamTick,
                "Capture gain must be positive.");
        }

        return CreateResolved(
            $"{RulesetId}-experiment-gain-t{startsAtTick}-{gainPerSoleTeamTick}",
            captureThreshold: 15,
            captureGainSchedule:
            [
                new(
                    "opening",
                    startsAtTick: 0,
                    gainPerSoleTeamTick: 1),
                new(
                    "late-escalation",
                    startsAtTick,
                    gainPerSoleTeamTick),
            ],
            enableMobilize: false,
            remoteFabrication: false);
    }

    /// <summary>
    /// Creates a local-only action-contract arm in which a turret may return
    /// once to child-mobile without allowing an Anchor healing loop.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateMobilizeExperiment() =>
        CreateResolved(
            $"{RulesetId}-experiment-mobilize",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: true,
            remoteFabrication: false);

    /// <summary>
    /// Creates a local-only fabrication arm in which an explicit Fabricate
    /// action may queue a Ready child from any walkable source position. The
    /// child still appears on the participant's protected output pad and the
    /// action still consumes one Prime decision.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateRemoteFabricationExperiment() =>
        CreateResolved(
            $"{RulesetId}-experiment-remote-fabrication",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: true);

    /// <summary>
    /// Creates a local-only objective-control arm in which the positive form
    /// weight difference between teams determines capture pressure.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateNetControlExperiment() =>
        CreateResolved(
            $"{RulesetId}-experiment-net-control",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            controlPolicy:
                FrontlineCaptureDefinition.ControlPolicyKind
                    .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral);

    /// <summary>
    /// Creates a local-only duel-depth arm. Mobile attacks may remain
    /// straight or commit one private 45-degree bend after one to four tiles;
    /// initial aim offsets and repeated bends are unavailable.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateOneBendShotsExperiment(
            FrontlineLabsDuelMapArm mapArm =
                FrontlineLabsDuelMapArm.Current) =>
        CreateResolved(
            $"{RulesetId}-experiment-one-bend-shots",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            oneBendShots: true,
            duelMapArm: mapArm);

    /// <summary>
    /// Creates a local-only progression arm. Each team's child slots create
    /// their first mobile lives automatically at ticks 120 and 260, then use
    /// ordinary automatic respawn. One-bend shots remain enabled so this arm
    /// can be compared directly with the duel-depth map experiments.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateAutomaticCompanionsExperiment(
            FrontlineLabsDuelMapArm mapArm =
                FrontlineLabsDuelMapArm.Current) =>
        CreateResolved(
            $"{RulesetId}-experiment-one-bend-auto-companions",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            oneBendShots: true,
            duelMapArm: mapArm,
            automaticCompanions: true);

    private static ActorResolvedMatchDefinition CreateResolved(
        string rulesetId,
        int captureThreshold,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            captureGainSchedule,
        bool enableMobilize,
        bool remoteFabrication,
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy =
            FrontlineCaptureDefinition.ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
        bool oneBendShots = false,
        FrontlineLabsDuelMapArm duelMapArm =
            FrontlineLabsDuelMapArm.Current,
        bool automaticCompanions = false)
    {
        ActorRulesDefinition rules = CreateRules(
            rulesetId,
            captureThreshold,
            captureGainSchedule,
            enableMobilize,
            remoteFabrication,
            controlPolicy,
            oneBendShots,
            automaticCompanions);
        ActorMapDefinition map = CreateMap(
            remoteFabrication,
            duelMapArm,
            automaticCompanions);
        PublicMatchTopology topology = CreateTopology();
        InitialDeploymentDefinition deployment =
            CreateInitialDeployment();

        return new ActorResolvedMatchDefinition(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            deployment,
            CreateLifecycleAssignments(automaticCompanions),
            automaticCompanions
                ? []
                : CreateParticipantRegionAssignments(remoteFabrication),
            new FrontlineActorModeMapBindingDefinition(
                [
                    "frontline-position-0",
                    "frontline-position-1",
                    "frontline-position-2",
                    "frontline-position-3",
                    "frontline-position-4",
                ],
                [
                    new FrontlineTeamAdvanceDefinition(
                        0,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                    new FrontlineTeamAdvanceDefinition(
                        1,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardLowerIndex),
                ]),
            CreateCapabilityVersions());
    }

    private static ActorMatchCapabilityVersions CreateCapabilityVersions() =>
        new(
            contractProfileId: "generic-actor-match-2",
            runtimeProtocolVersion: "1.0",
            runtimeConfigurationVersion: "1.0",
            runtimeContractVersion: 2,
            matchStartSchemaVersion: 2,
            observationSchemaVersion: 2,
            decisionSchemaVersion: 2,
            matchContractSchemaVersion: 2);

    private static ActorRulesDefinition CreateRules(
        string rulesetId,
        int captureThreshold,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            captureGainSchedule,
        bool enableMobilize,
        bool remoteFabrication,
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy,
        bool oneBendShots,
        bool automaticCompanions)
    {
        var movement = new ActorMovementProfileDefinition(
            GroundMovementId,
            ActorMovementLayer.Ground);
        ActorVisionProfileDefinition mobileVision = Vision(
            MobileVisionId,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: 1);
        ActorVisionProfileDefinition turretVision = Vision(
            TurretVisionId,
            ActorVisionShape.Omnidirectional,
            omnidirectionalProximityRange: 6);
        var projectile = new ActorProjectileDefinition(
            ActorProjectileMode.Discrete,
            damagePerHit: 1,
            maxTravelTiles: 8,
            ticksPerAdvance: 1,
            tilesPerAdvance: 2,
            launchTiles: 1,
            advancesOnLaunchTick: false,
            damageAppliedSimultaneously: true,
            diagonalCornersMustBeClear: true);
        var mobileAttack = new ActorAttackProfileDefinition(
            MobileAttackId,
            omnidirectionalAim: false,
            projectile,
            cooldownTicks: 2,
            maxEnergy: 0,
            attackEnergyCost: 0,
            energyRegenerationIntervalTicks: 0,
            energyRegenerationAmount: 0,
            ShotProgram(
                enabled: true,
                oneBendOnly: oneBendShots));
        var turretAttack = new ActorAttackProfileDefinition(
            TurretAttackId,
            omnidirectionalAim: true,
            projectile,
            cooldownTicks: 1,
            maxEnergy: 0,
            attackEnergyCost: 0,
            energyRegenerationIntervalTicks: 0,
            energyRegenerationAmount: 0,
            ShotProgram(
                enabled: false,
                oneBendOnly: false));
        ActorTransitionWindupDefinition anchorWindup = Windup(
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate);
        ActorTransitionWindupDefinition splitWindup = Windup(
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration);
        string[] turretActions = enableMobilize
            ? ["wait", PublicActionIds.ShootDirection, MobilizeActionId]
            : ["wait", PublicActionIds.ShootDirection];
        var actions = new List<ActorActionDefinition>
        {
            new(
                "wait",
                0,
                ActorActionKind.Wait,
                []),
            new(
                "move",
                1,
                ActorActionKind.Movement,
                [ActorActionParameterKind.Direction]),
            new(
                "rotate",
                2,
                ActorActionKind.Rotation,
                [ActorActionParameterKind.Direction]),
            new(
                "shoot",
                4,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ShotProgram]),
            new(
                "transform",
                PublicActionCodes.Transform,
                ActorActionKind.SameLifeTransition,
                [ActorActionParameterKind.FormTarget]),
            new(
                PublicActionIds.ShootDirection,
                PublicActionCodes.ShootDirection,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ProjectileHeading]),
        };
        if (!automaticCompanions)
        {
            actions.Add(
                new ActorActionDefinition(
                    "fabricate",
                    PublicActionCodes.Fabricate,
                    ActorActionKind.Fabrication,
                    [ActorActionParameterKind.UnitTarget]));
            actions.Add(
                new ActorActionDefinition(
                    "split",
                    103,
                    ActorActionKind.Replication,
                    []));
        }
        if (enableMobilize)
        {
            actions.Add(
                new ActorActionDefinition(
                    MobilizeActionId,
                    104,
                    ActorActionKind.SameLifeTransition,
                    []));
        }
        var sameLifeTransitions =
            new List<ActorSameLifeTransitionDefinition>
            {
                new ActorFormTransitionDefinition(
                    "anchor-child",
                    "transform",
                    ChildFormId,
                    TurretFormId,
                    anchorWindup,
                    ActorSameLifeTransitionDefinition.MemoryContinuityKind
                        .PreservePrivateMemory,
                    new ActorSameLifeHealthDefinition(
                        ActorSameLifeHealthDefinition.HealthPolicyKind
                            .AddFlatCappedToTargetMaximum,
                        flatHealthGain: 2),
                    ActorSameLifeCombatStateDefinition
                        .PreserveWithoutRefillV1,
                    new ActorSameLifePlacementDefinition(
                        ActorSameLifePlacementDefinition
                            .PositionContinuityKind.SameOccupiedGroundTile,
                        ActorSameLifePlacementDefinition
                            .LegalityEvaluationKind
                            .QueueAndCompletionTileTags,
                        requiredTileTags: [],
                        forbiddenTileTags:
                        [
                            ActorMapTileTagDefinition.TileTagKind
                                .TransitionPlacementForbidden,
                        ],
                        ActorSameLifePlacementDefinition
                            .FailedCompletionKind
                            .CancelAndRemainInSourceForm),
                    irreversibleForLife: !enableMobilize),
            };
        if (enableMobilize)
        {
            sameLifeTransitions.Add(
                new ActorFormTransitionDefinition(
                    "mobilize-child",
                    MobilizeActionId,
                    TurretFormId,
                    ChildFormId,
                    anchorWindup,
                    ActorSameLifeTransitionDefinition.MemoryContinuityKind
                        .PreservePrivateMemory,
                    new ActorSameLifeHealthDefinition(
                        ActorSameLifeHealthDefinition.HealthPolicyKind
                            .PreserveCurrentCappedToTargetMaximum,
                        flatHealthGain: 0),
                    ActorSameLifeCombatStateDefinition
                        .PreserveWithoutRefillV1,
                    new ActorSameLifePlacementDefinition(
                        ActorSameLifePlacementDefinition
                            .PositionContinuityKind.SameOccupiedGroundTile,
                        ActorSameLifePlacementDefinition
                            .LegalityEvaluationKind
                            .QueueAndCompletionTileTags,
                        requiredTileTags: [],
                        forbiddenTileTags: [],
                        ActorSameLifePlacementDefinition
                            .FailedCompletionKind
                            .CancelAndRemainInSourceForm),
                    irreversibleForLife: true));
        }

        return new ActorRulesDefinition(
            rulesetId,
            new ActorRulesLimits(
                maxTicks: 500,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            new ActorSeedMechanicsDefinition(
                rulesetId,
                ActorSeedMechanicsDefinition.SeedDerivationKind
                    .MatchSeedProfileTeamUnitLifeMix64V1,
                ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                    .PerStableUnitMonotonicStartingAtZero,
                ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                    .FreshRuntimePerLife,
                ActorSeedMechanicsDefinition.PrivateMemoryKind
                    .IsolatedPerRuntime),
            new FrontlineGameModeDefinition(
                new FrontlineVictoryDefinition(
                    pushesToBreach: 3,
                    [
                        new ScoreRankingDefinition(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                frontlinePositionCount: 5,
                new FrontlineCaptureDefinition(
                    threshold: captureThreshold,
                    gainPerSoleTeamTick: 1,
                    decayAmount: 1,
                    decayIntervalTicks: 2,
                    redeployPauseTicks: 5,
                    gainSchedule: captureGainSchedule,
                    controlPolicy)),
            new ActorLifecycleDefinition(
                [
                    new ActorLifecycleProfileDefinition(
                        PrimeLifecycleId,
                        ActorLifecycleProfileDefinition
                            .DestructionPolicyKind.AutomaticRespawn,
                        delayTicks: 18,
                        automaticReturnFormId: PrimeFormId),
                    new ActorLifecycleProfileDefinition(
                        ChildLifecycleId,
                        automaticCompanions
                            ? ActorLifecycleProfileDefinition
                                .DestructionPolicyKind.AutomaticRespawn
                            : ActorLifecycleProfileDefinition
                                .DestructionPolicyKind
                                .ReadyForExplicitFabrication,
                        delayTicks: 30,
                        automaticReturnFormId:
                            automaticCompanions ? ChildFormId : null),
                ]),
            [
                new ActorFormDefinition(
                    PrimeFormId,
                    maxHealth: 3,
                    movement.Id,
                    mobileVision.Id,
                    mobileAttack.Id,
                    objectiveWeight: 1,
                    automaticCompanions
                        ? ["wait", "move", "rotate", "shoot"]
                        : [
                            "wait",
                            "move",
                            "rotate",
                            "shoot",
                            "fabricate",
                            "split",
                        ]),
                new ActorFormDefinition(
                    ChildFormId,
                    maxHealth: 3,
                    movement.Id,
                    mobileVision.Id,
                    mobileAttack.Id,
                    objectiveWeight: 1,
                    ["wait", "move", "rotate", "shoot", "transform"]),
                new ActorFormDefinition(
                    ReplicaFormId,
                    maxHealth: 3,
                    movement.Id,
                    mobileVision.Id,
                    mobileAttack.Id,
                    objectiveWeight: 1,
                    ["wait", "move", "rotate", "shoot"]),
                new ActorFormDefinition(
                    TurretFormId,
                    maxHealth: 5,
                    movement.Id,
                    turretVision.Id,
                    turretAttack.Id,
                    objectiveWeight: 0,
                    turretActions),
            ],
            [movement],
            [mobileVision, turretVision],
            [mobileAttack, turretAttack],
            actions,
            automaticCompanions
                ? []
                : [
                new BoundedChildFabricationDefinition(
                    "fabricate-child",
                    "fabricate",
                    [PrimeFormId],
                    ChildFormId,
                    FabricationSourceRoleId,
                    FabricationOutputRoleId,
                    requiredSourceTileTags:
                    remoteFabrication
                        ? []
                        : [
                            ActorMapTileTagDefinition.TileTagKind
                                .SpawnProtected,
                        ],
                    requiredOutputTileTags:
                    [
                        ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
                    ],
                    forbiddenOutputTileTags: [],
                    candidateOffsets: remoteFabrication
                        ? RemoteFabricationCandidateOffsets()
                        : FabricationCandidateOffsets(),
                    new ActorFabricationDelayDefinition(durationTicks: 1),
                    ActorActionRejectionResult.Blocked),
                ],
            sameLifeTransitions,
            automaticCompanions
                ? []
                : [
                new SplitReplicationTransitionDefinition(
                    "split-prime",
                    "split",
                    [PrimeFormId],
                    ReplicaFormId,
                    descendantCount: 2,
                    maxSourceGeneration: 0,
                    requireNoPriorSameLifeTransition: true,
                    new ActorReplicationHealthDefinition(
                        ActorReplicationHealthDefinition.DistributionKind
                            .DivideCurrentHealthEquallyFloor,
                        minimumHealthPerDescendant: 1,
                        ActorReplicationHealthDefinition.RemainderKind.Discard),
                    candidateOffsets:
                    [
                        new ActorRelativePositionOffset(0, -1),
                        new ActorRelativePositionOffset(0, 1),
                        new ActorRelativePositionOffset(-1, 0),
                        new ActorRelativePositionOffset(1, 0),
                    ],
                    splitWindup),
                ],
            new ActorTeamPerceptionDefinition(
                ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion),
            new ActorCollisionDefinition(
                actorsBlockWalls: true,
                actorsBlockActors: true,
                sameDestinationMovesBlockAll: true,
                swapMovesBlocked: true,
                followingVacatedActorAllowed: false,
                projectilesBlockMovement: true,
                movingOntoProjectileCausesHit: true,
                wallsConsumeProjectiles: true,
                projectilesIgnoreFiringLife: true,
                projectilesStopOnFirstEnemyActor: true,
                projectilesCollideWithProjectiles: false,
                ActorCollisionDefinition.AlliedProjectileContactKind
                    .PassThrough),
            new ActorTickResolutionDefinition(
                observationsUsePreTickState: true,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                ActorTickResolutionDefinition.CreateSupportedPhases()));
    }

    private static ActorVisionProfileDefinition Vision(
        string id,
        ActorVisionShape shape,
        int omnidirectionalProximityRange) =>
        new(
            id,
            range: 6,
            ActorVisionDistanceMetric.Chebyshev,
            shape,
            omnidirectionalProximityRange,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 8,
            hearingBearingSectors: 8,
            ActorHearingBearingModel
                .EightOctantsStrictTwoToOneCardinalV1,
            hearingDistanceBandUpperBounds: [2, 5],
            loudEventKinds:
            [
                ActorAudibleEventKind.Destruction,
                ActorAudibleEventKind.Damage,
                ActorAudibleEventKind.Attack,
            ]);

    private static ActorShotProgramDefinition ShotProgram(
        bool enabled,
        bool oneBendOnly) =>
        new(
            enabled,
            headingSectors: 8,
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
            bendStepSectors: 1,
            minInitialAimSteps: enabled && !oneBendOnly ? -1 : 0,
            maxInitialAimSteps: enabled && !oneBendOnly ? 1 : 0,
            new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            maxBendAfterTiles: enabled ? 4 : 1,
            minBendEveryTiles: 1,
            maxBendEveryTiles: enabled && !oneBendOnly ? 3 : 1,
            minBendCount: 1,
            maxBendCount: enabled && !oneBendOnly ? 3 : 1,
            launchTiles: 1,
            payloadOptional: enabled,
            defaultProgram: new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult: enabled
                ? ActorActionRejectionResult.Rejected
                : null,
            unsupportedPayloadResult: ActorActionRejectionResult.Blocked,
            diagonalCornersMustBeClear: true);

    private static ActorTransitionWindupDefinition Windup(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            completion) =>
        new(
            durationTicks: 1,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            completion,
            ActorTransitionWindupDefinition.PlacementReferenceKind
                .QueueTimePose);

    private static ImmutableArray<ActorRelativePositionOffset>
        FabricationCandidateOffsets() =>
        (
            from forward in Enumerable.Range(-2, 5)
            from right in Enumerable.Range(-2, 5)
            where forward != 0 || right != 0
            orderby Math.Max(Math.Abs(forward), Math.Abs(right)),
                Math.Abs(forward) + Math.Abs(right),
                forward,
                right
            select new ActorRelativePositionOffset(forward, right)
        ).ToImmutableArray();

    private static ImmutableArray<ActorRelativePositionOffset>
        RemoteFabricationCandidateOffsets() =>
        (
            from forward in Enumerable.Range(-22, 45)
            from right in Enumerable.Range(-14, 29)
            where forward != 0 || right != 0
            orderby Math.Max(Math.Abs(forward), Math.Abs(right)),
                Math.Abs(forward) + Math.Abs(right),
                forward,
                right
            select new ActorRelativePositionOffset(forward, right)
        ).ToImmutableArray();

    private static ActorMapDefinition CreateMap(
        bool remoteFabrication,
        FrontlineLabsDuelMapArm duelMapArm,
        bool automaticCompanions) =>
        new(
            remoteFabrication
                ? $"{MapId}-remote-fabrication-experiment"
                : (duelMapArm, automaticCompanions) switch
                {
                    (FrontlineLabsDuelMapArm.Current, false) => MapId,
                    (FrontlineLabsDuelMapArm.ThinFronts, false) =>
                        $"{MapId}-thin-fronts-experiment",
                    (FrontlineLabsDuelMapArm
                        .OuterShoulderBypass, false) =>
                        $"{MapId}-outer-shoulder-bypass-experiment",
                    (FrontlineLabsDuelMapArm.Current, true) =>
                        $"{MapId}-auto-companions",
                    (FrontlineLabsDuelMapArm.ThinFronts, true) =>
                        $"{MapId}-thin-fronts-auto-companions",
                    (FrontlineLabsDuelMapArm
                        .OuterShoulderBypass, true) =>
                        $"{MapId}-outer-shoulder-auto-companions",
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(duelMapArm),
                        duelMapArm,
                        "Unknown Frontline Labs duel map arm."),
                },
            version: 1,
            MapTileRows(duelMapArm),
            [
                Spawn("team-0-prime", 2, 7, Direction.East),
                Spawn("team-1-prime", 20, 7, Direction.West),
                .. AutomaticCompanionSpawns(automaticCompanions),
            ],
            [
                .. ObjectiveRegions(duelMapArm),
                Region(
                    "team-0-home-pad",
                    [(1, 6), (2, 6), (1, 7), (2, 7), (1, 8), (2, 8)]),
                Region(
                    "team-1-home-pad",
                    [
                        (20, 6),
                        (21, 6),
                        (20, 7),
                        (21, 7),
                        (20, 8),
                        (21, 8),
                    ]),
                .. RemoteFabricationRegions(remoteFabrication),
            ],
            [
                new ActorMapTileTagDefinition(
                    "anchor-forbidden",
                    ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden,
                    AnchorForbiddenTiles(duelMapArm)),
                new ActorMapTileTagDefinition(
                    "protected-home-pads",
                    ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
                    [
                        new Position(1, 6),
                        new Position(2, 6),
                        new Position(1, 7),
                        new Position(2, 7),
                        new Position(1, 8),
                        new Position(2, 8),
                        new Position(20, 6),
                        new Position(21, 6),
                        new Position(20, 7),
                        new Position(21, 7),
                        new Position(20, 8),
                        new Position(21, 8),
                    ]),
            ]);

    private static ImmutableArray<string> MapTileRows(
        FrontlineLabsDuelMapArm duelMapArm)
    {
        ImmutableArray<string> rows =
        [
            "#######################",
            "#.....................#",
            "#..##.....#.#.....##..#",
            "#.........#.#.........#",
            "#...#......#......#...#",
            "#....#.....#.....#....#",
            "#....#..##...##..#....#",
            "#.....................#",
            "#....#..##...##..#....#",
            "#....#.....#.....#....#",
            "#....#.....#.....#....#",
            "#.........#.#.........#",
            "#..##.....#.#.....##..#",
            "#.....................#",
            "#######################",
        ];
        if (duelMapArm != FrontlineLabsDuelMapArm.OuterShoulderBypass)
            return rows;

        return rows
            .SetItem(6, "#....#...#...#...#....#")
            .SetItem(8, "#....#...#...#...#....#");
    }

    private static ImmutableArray<ActorMapRegionDefinition>
        ObjectiveRegions(FrontlineLabsDuelMapArm duelMapArm) =>
        duelMapArm == FrontlineLabsDuelMapArm.ThinFronts
            ?
            [
                Objective(
                    "frontline-position-0",
                    [(4, 8), (4, 9), (4, 10)]),
                Objective(
                    "frontline-position-1",
                    [(7, 4), (7, 5), (7, 6)]),
                Objective(
                    "frontline-position-2",
                    [(11, 6), (11, 7), (11, 8)]),
                Objective(
                    "frontline-position-3",
                    [(15, 4), (15, 5), (15, 6)]),
                Objective(
                    "frontline-position-4",
                    [(18, 8), (18, 9), (18, 10)]),
            ]
            :
            [
                Objective(
                    "frontline-position-0",
                    [(3, 8), (4, 8), (3, 9), (4, 9)]),
                Objective(
                    "frontline-position-1",
                    [(6, 5), (7, 5), (6, 6), (7, 6)]),
                Objective(
                    "frontline-position-2",
                    [
                        (10, 7),
                        (11, 7),
                        (12, 7),
                        (10, 8),
                        (11, 8),
                        (12, 8),
                    ]),
                Objective(
                    "frontline-position-3",
                    [(15, 5), (16, 5), (15, 6), (16, 6)]),
                Objective(
                    "frontline-position-4",
                    [(18, 8), (19, 8), (18, 9), (19, 9)]),
            ];

    private static ImmutableArray<ActorMapRegionDefinition>
        RemoteFabricationRegions(bool enabled) =>
        enabled
            ? [
                Region(
                    RemoteFabricationSourceRegionId,
                    WalkableMapTiles()),
            ]
            : [];

    private static IReadOnlyList<(int X, int Y)> WalkableMapTiles()
    {
        ImmutableArray<string> rows = MapTileRows(
            FrontlineLabsDuelMapArm.Current);
        return (
            from y in Enumerable.Range(0, rows.Length)
            from x in Enumerable.Range(0, rows[y].Length)
            where rows[y][x] != '#'
            select (X: x, Y: y)
        ).ToArray();
    }

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        int x,
        int y,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(
                id,
                new Position(x, y),
                facing),
            [ActorMovementLayer.Ground]);

    private static ImmutableArray<ActorMapSpawnAnchorDefinition>
        AutomaticCompanionSpawns(bool enabled) =>
        enabled
            ? [
                Spawn("team-0-child-1", 1, 6, Direction.East),
                Spawn("team-0-child-2", 1, 8, Direction.East),
                Spawn("team-1-child-1", 21, 6, Direction.West),
                Spawn("team-1-child-2", 21, 8, Direction.West),
            ]
            : [];

    private static ActorMapRegionDefinition Objective(
        string id,
        IReadOnlyList<(int X, int Y)> tiles) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.Objective,
            Positions(tiles));

    private static ActorMapRegionDefinition Region(
        string id,
        IReadOnlyList<(int X, int Y)> tiles) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.TransitionPlacement,
            Positions(tiles));

    private static ImmutableArray<Position> Positions(
        IReadOnlyList<(int X, int Y)> tiles) =>
        tiles.Select(tile => new Position(tile.X, tile.Y))
            .ToImmutableArray();

    private static ImmutableArray<Position> AnchorForbiddenTiles(
        FrontlineLabsDuelMapArm duelMapArm) =>
    [
        new(1, 1), new(2, 1), new(20, 1), new(21, 1),
        new(1, 2), new(2, 2), new(20, 2), new(21, 2),
        new(1, 3), new(2, 3), new(3, 3), new(19, 3), new(20, 3), new(21, 3),
        new(1, 4), new(2, 4), new(3, 4), new(19, 4), new(20, 4), new(21, 4),
        new(1, 5), new(2, 5), new(3, 5), new(4, 5), new(6, 5), new(7, 5),
        new(15, 5), new(16, 5), new(18, 5), new(19, 5), new(20, 5), new(21, 5),
        new(1, 6), new(2, 6), new(3, 6), new(4, 6), new(6, 6), new(7, 6),
        new(15, 6), new(16, 6), new(18, 6), new(19, 6), new(20, 6), new(21, 6),
        new(1, 7), new(2, 7), new(3, 7), new(4, 7), new(5, 7), new(6, 7),
        new(7, 7), new(8, 7), new(9, 7), new(10, 7), new(11, 7), new(12, 7),
        new(13, 7), new(14, 7), new(15, 7), new(16, 7), new(17, 7),
        new(18, 7), new(19, 7), new(20, 7), new(21, 7),
        new(1, 8), new(2, 8), new(3, 8), new(4, 8), new(10, 8), new(11, 8),
        new(12, 8), new(18, 8), new(19, 8), new(20, 8), new(21, 8),
        new(1, 9), new(2, 9), new(3, 9), new(4, 9), new(18, 9), new(19, 9),
        new(20, 9), new(21, 9),
        new(1, 10), new(2, 10), new(3, 10), new(4, 10),
        new(18, 10), new(19, 10), new(20, 10), new(21, 10),
        new(1, 11), new(2, 11), new(3, 11), new(4, 11),
        new(18, 11), new(19, 11), new(20, 11), new(21, 11),
        new(1, 12), new(2, 12), new(5, 12), new(17, 12), new(20, 12),
        new(21, 12),
        new(1, 13), new(2, 13), new(6, 13), new(16, 13), new(20, 13),
        new(21, 13),
        .. ShoulderBypassForbiddenTiles(duelMapArm),
    ];

    private static ImmutableArray<Position> ShoulderBypassForbiddenTiles(
        FrontlineLabsDuelMapArm duelMapArm) =>
        duelMapArm == FrontlineLabsDuelMapArm.OuterShoulderBypass
            ?
            [
                new Position(8, 6),
                new Position(14, 6),
                new Position(8, 8),
                new Position(14, 8),
            ]
            : [];

    private static PublicMatchTopology CreateTopology() =>
        new()
        {
            Teams = [new PublicScoringTeam(0), new PublicScoringTeam(1)],
            Participants =
            [
                new PublicParticipant(0, 0),
                new PublicParticipant(1, 1),
            ],
            UnitSlots =
            [
                new PublicUnitSlot(0, 0, 0),
                new PublicUnitSlot(0, 1, 0),
                new PublicUnitSlot(0, 2, 0),
                new PublicUnitSlot(1, 0, 1),
                new PublicUnitSlot(1, 1, 1),
                new PublicUnitSlot(1, 2, 1),
            ],
            InitialLives =
            [
                new PublicInitialLife(0, 0, 0, PrimeFormId),
                new PublicInitialLife(1, 0, 0, PrimeFormId),
            ],
        };

    private static InitialDeploymentDefinition CreateInitialDeployment() =>
        new(
            [
                new InitialSpawnDefinition(
                    "team-0-prime",
                    new Position(2, 7),
                    Direction.East),
                new InitialSpawnDefinition(
                    "team-1-prime",
                    new Position(20, 7),
                    Direction.West),
            ],
            [
                new InitialLifeDeployment(
                    0,
                    0,
                    0,
                    PrimeFormId,
                    "team-0-prime"),
                new InitialLifeDeployment(
                    1,
                    0,
                    0,
                    PrimeFormId,
                    "team-1-prime"),
            ]);

    private static ImmutableArray<
        ActorUnitSlotLifecycleAssignmentDefinition>
        CreateLifecycleAssignments(bool automaticCompanions) =>
    [
        PrimeAssignment(0, "team-0-prime"),
        ChildAssignment(
            0,
            1,
            unlockTick: 120,
            automaticCompanions
                ? "team-0-child-1"
                : null),
        ChildAssignment(
            0,
            2,
            unlockTick: 260,
            automaticCompanions
                ? "team-0-child-2"
                : null),
        PrimeAssignment(1, "team-1-prime"),
        ChildAssignment(
            1,
            1,
            unlockTick: 120,
            automaticCompanions
                ? "team-1-child-1"
                : null),
        ChildAssignment(
            1,
            2,
            unlockTick: 260,
            automaticCompanions
                ? "team-1-child-2"
                : null),
    ];

    private static ActorUnitSlotLifecycleAssignmentDefinition PrimeAssignment(
        int teamId,
        string spawnId) =>
        new(
            teamId,
            unitId: 0,
            PrimeLifecycleId,
            initialGeneration: 0,
            allowedFormIds: [PrimeFormId, ReplicaFormId],
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.ActiveAtTickZero,
            unlockTick: null,
            assignedRespawnSpawnId: spawnId);

    private static ActorUnitSlotLifecycleAssignmentDefinition ChildAssignment(
        int teamId,
        int unitId,
        int unlockTick,
        string? automaticSpawnId) =>
        new(
            teamId,
            unitId,
            ChildLifecycleId,
            initialGeneration: automaticSpawnId is null ? null : 0,
            allowedFormIds: [ChildFormId, ReplicaFormId, TurretFormId],
            automaticSpawnId is null
                ? ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick
                : ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind
                    .DormantAutomaticActivationAtTick,
            unlockTick,
            assignedRespawnSpawnId: automaticSpawnId);

    private static ImmutableArray<
        ActorParticipantRegionAssignmentDefinition>
        CreateParticipantRegionAssignments(
            bool remoteFabrication) =>
    [
        new ActorParticipantRegionAssignmentDefinition(
            0,
            FabricationSourceRoleId,
            remoteFabrication
                ? RemoteFabricationSourceRegionId
                : "team-0-home-pad",
            Direction.East),
        new ActorParticipantRegionAssignmentDefinition(
            0,
            FabricationOutputRoleId,
            "team-0-home-pad",
            Direction.East),
        new ActorParticipantRegionAssignmentDefinition(
            1,
            FabricationSourceRoleId,
            remoteFabrication
                ? RemoteFabricationSourceRegionId
                : "team-1-home-pad",
            Direction.West),
        new ActorParticipantRegionAssignmentDefinition(
            1,
            FabricationOutputRoleId,
            "team-1-home-pad",
            Direction.West),
    ];
}
