using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Versioned, local-only resolved contracts used to qualify gameplay
/// capabilities before artifacts may contribute balance evidence. A probe is
/// an ordinary generic match contract; bots receive no hidden scenario API.
/// </summary>
public static class FrontlineLabsQualificationDefinition
{
    public const string SuiteId = "frontline-qualification-1";
    public const string FoundationSuiteId = "frontline-qualification-2";
    public const int FoundationSuiteVersion = 2;
    public const string FoundationProfileId =
        "frontline-h2h-one-bend-auto-foundation-1";
    public const string FundamentalsSuiteId = "frontline-qualification-3";
    public const int FundamentalsSuiteVersion = 1;
    public const string FundamentalsProfileId =
        "frontline-duel-depth-union-t2-v1";
    public const string TacticalSuiteId = "frontline-qualification-4";
    public const int TacticalSuiteVersion = 1;
    public const string TacticalProfileId =
        "frontline-duel-depth-union-t3-v1";
    public const string PositionalSuiteId = "frontline-qualification-5";
    public const int PositionalSuiteVersion = 1;
    public const string PositionalProfileId =
        "frontline-duel-depth-union-t4-v1";

    /// <summary>
    /// THE MIND SUITES (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c>
    /// §6.2). Parallel IDs on parallel profiles, running the SAME T1-T4 probe
    /// contracts against a participant-scoped controller.
    ///
    /// <para>The probe contracts are untouched on purpose: almost every probe
    /// is already one-body, and a mind commanding one body is a perfectly good
    /// subject for direct fire, evasion and objective pathing. Only the
    /// runner's bot HOSTING changes, which is the cheapest possible answer and
    /// the one that keeps the existing reference artifacts comparable.</para>
    ///
    /// <para>The IDs are parallel rather than shared because a profile ID is a
    /// PRE-REGISTRATION in this project. A mind T4 and a per-life T4 are not
    /// the same evidence — the same tier was earned driving a different
    /// machine — and balance evidence must never have to guess which one it is
    /// holding.</para>
    /// </summary>
    public const string MindFundamentalsSuiteId =
        "frontline-mind-qualification-3";

    /// <inheritdoc cref="MindFundamentalsSuiteId"/>
    public const string MindFundamentalsProfileId =
        "frontline-mind-union-t2-v1";

    /// <inheritdoc cref="MindFundamentalsSuiteId"/>
    public const string MindTacticalSuiteId =
        "frontline-mind-qualification-4";

    /// <inheritdoc cref="MindFundamentalsSuiteId"/>
    public const string MindTacticalProfileId =
        "frontline-mind-union-t3-v1";

    /// <inheritdoc cref="MindFundamentalsSuiteId"/>
    public const string MindPositionalSuiteId =
        "frontline-mind-qualification-5";

    /// <inheritdoc cref="MindFundamentalsSuiteId"/>
    public const string MindPositionalProfileId =
        "frontline-mind-union-t4-v1";

    /// <summary>
    /// The T2 competency that REPLACES the documented <c>respawn-reorient</c>
    /// requirement, which is vacuous under the mind: a mind's memory does not
    /// reset when a body dies, so "resumes mode-directed play after a fresh
    /// life with isolated memory" measures nothing.
    ///
    /// <para>What replaces it measures the same thing and is strictly harder:
    /// when the body executing a task is destroyed, the mind RESUMES THE TASK
    /// WITH ANOTHER BODY. Per-life, that was not merely hard — the plan had no
    /// carrier at all, because the body holding it was the body that died.
    /// </para>
    /// </summary>
    public const string BodyHandoffProbeId = "body-handoff";

    /// <summary>
    /// The T4 mind-native probe: the #187/#188 set-piece, and the thing the
    /// mind exists to make authorable. Keep a body still on the objective with
    /// another own body adjacent on the threat axis, for K consecutive ticks,
    /// from both assignments.
    /// </summary>
    public const string EscortIntegrityProbeId = "escort-integrity";

    /// <summary>
    /// Consecutive ticks of intact escort that <c>escort-integrity</c>
    /// requires, and the window inside which <c>body-handoff</c> must see the
    /// task resumed.
    /// </summary>
    public const int EscortIntegrityTicks = 6;

    /// <inheritdoc cref="EscortIntegrityTicks"/>
    public const int BodyHandoffWindowTicks = 12;
    public const string EntryProbeId = "entry-initiative";
    public const string ContractAutoDeterminismProbeId =
        "contract-auto-determinism";
    public const string ContractMatrixProbeId = "contract-matrix";
    public const string AutomaticLifeCycleProbeId =
        "automatic-life-cycle";
    public const string ObjectivePathProbeId = "objective-path";
    public const string DirectFireProbeId = "direct-fire";
    public const string StraightEvadeProbeId = "straight-evade";
    public const string ManualFabricationProbeId = "manual-fabrication";
    public const string WallTerminatedBendProbeId =
        "wall-terminated-bend";
    public const string StrictCornerProbeId = "strict-corner";
    public const string CadenceParityProbeId = "cadence-parity";
    public const string CooldownWindowProbeId = "cooldown-window";
    public const string LocalFormSafetyProbeId = "local-form-safety";
    public const string SuppressionChokeProbeId = "suppression-choke";
    public const string PredictionChamberProbeId = "prediction-chamber";
    public const string FrontRotationProbeId = "front-rotation";
    public const string MapHoldoutProbeId = "map-holdout";

    private const string Team0SpawnId =
        "qualification-team-0-prime";
    private const string Team1SpawnId =
        "qualification-team-1-prime";

    /// <summary>
    /// Starts the two Primes at the known six-tile central suppression
    /// approach and ends immediately before the first companion unlock. The
    /// rules retain the exact one-bend projectile semantics under a distinct
    /// qualification identity.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateEntryProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeLimits(
            source.Rules,
            $"{SuiteId}-{EntryProbeId}",
            maxTicks: 120);
        ActorMapDefinition map = new(
            $"{SuiteId}-{EntryProbeId}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(
                    Team0SpawnId,
                    new Position(8, 7),
                    Direction.East),
                Spawn(
                    Team1SpawnId,
                    new Position(14, 7),
                    Direction.West),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        InitialDeploymentDefinition deployment = new(
            [
                new InitialSpawnDefinition(
                    Team0SpawnId,
                    new Position(8, 7),
                    Direction.East),
                new InitialSpawnDefinition(
                    Team1SpawnId,
                    new Position(14, 7),
                    Direction.West),
            ],
            [
                .. source.InitialDeployment.Lives.Select(life =>
                    new InitialLifeDeployment(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId,
                        life.FormId,
                        life.TeamId == 0
                            ? Team0SpawnId
                            : Team1SpawnId)),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] lifecycleAssignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId == 0)
                .Select(assignment =>
                new ActorUnitSlotLifecycleAssignmentDefinition(
                    assignment.TeamId,
                    assignment.UnitId,
                    assignment.LifecycleProfileId,
                    assignment.InitialGeneration,
                    assignment.AllowedFormIds,
                    assignment.InitialAvailability,
                    assignment.UnlockTick,
                    assignment.UnitId == 0
                        ? assignment.TeamId == 0
                            ? Team0SpawnId
                            : Team1SpawnId
                        : assignment.AssignedRespawnSpawnId)),
        ];
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId == 0)
                .ToImmutableArray(),
            InitialLives = source.Topology.InitialLives
                .Where(life => life.UnitId == 0)
                .ToImmutableArray(),
        };

        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            lifecycleAssignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// First immutable component of the v2 foundation profile. The ordinary
    /// automatic-companion contract is shortened to one unlock and given an
    /// unreachable capture threshold so every conforming artifact observes
    /// the same full topology window. No tier is awarded by this component.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateContractAutoDeterminismProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment();
        var frontline = (FrontlineGameModeDefinition)
            source.Rules.GameMode;
        FrontlineCaptureDefinition capture = frontline.Capture;
        var probeMode = new FrontlineGameModeDefinition(
            frontline.FrontlineVictory,
            frontline.ScoreCatalog,
            frontline.FrontlinePositionCount,
            new FrontlineCaptureDefinition(
                threshold: 1000,
                capture.GainPerSoleTeamTick,
                capture.DecayAmount,
                capture.DecayIntervalTicks,
                capture.RedeployPauseTicks,
                capture.GainSchedule,
                capture.ControlPolicy));
        var rules = new ActorRulesDefinition(
            $"{FoundationSuiteId}-{ContractAutoDeterminismProbeId}",
            new ActorRulesLimits(
                maxTicks: 130,
                source.Rules.Limits.RuntimeFaults),
            source.Rules.SeedMechanics,
            probeMode,
            source.Rules.Lifecycle,
            source.Rules.Forms,
            source.Rules.MovementProfiles,
            source.Rules.VisionProfiles,
            source.Rules.AttackProfiles,
            source.Rules.Actions,
            source.Rules.FabricationTransitions,
            source.Rules.SameLifeTransitions,
            source.Rules.ReplicationTransitions,
            source.Rules.TeamPerception,
            source.Rules.Collisions,
            source.Rules.TickResolution);

        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId != 2)
                .ToImmutableArray(),
        };
        ActorUnitSlotLifecycleAssignmentDefinition[] lifecycleAssignments =
        [
            .. source.LifecycleAssignments.Where(assignment =>
                assignment.UnitId != 2),
        ];

        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            topology,
            source.InitialDeployment,
            lifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// T1 contract/count/identity probe for the union profile. It uses
    /// non-default participant IDs and exposes all three stable unit slots,
    /// with automatic children accelerated only for this local contract.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateContractMatrixProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{FundamentalsSuiteId}-{ContractMatrixProbeId}",
            maxTicks: 24,
            captureThreshold: 1000,
            removePopulationActions: false);
        IReadOnlyDictionary<int, int> participantIds =
            new Dictionary<int, int>
            {
                [0] = 7,
                [1] = 19,
            };
        var topology = source.Topology with
        {
            Participants = source.Topology.Participants
                .Select(participant => new PublicParticipant(
                    participantIds[participant.ParticipantId],
                    participant.TeamId))
                .ToImmutableArray(),
            UnitSlots = source.Topology.UnitSlots
                .Select(slot => new PublicUnitSlot(
                    slot.TeamId,
                    slot.UnitId,
                    participantIds[slot.ControllerParticipantId]))
                .ToImmutableArray(),
        };
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments.Select(assignment =>
            {
                if (assignment.UnitId == 0)
                    return assignment;
                int unlockTick = assignment.UnitId == 1 ? 4 : 8;
                return new ActorUnitSlotLifecycleAssignmentDefinition(
                    assignment.TeamId,
                    assignment.UnitId,
                    assignment.LifecycleProfileId,
                    assignment.InitialGeneration,
                    assignment.AllowedFormIds,
                    assignment.InitialAvailability,
                    unlockTick,
                    assignment.AssignedRespawnSpawnId);
            }),
        ];
        ActorParticipantRegionAssignmentDefinition[] regions =
        [
            .. source.ParticipantRegionAssignments.Select(assignment =>
                new ActorParticipantRegionAssignmentDefinition(
                    participantIds[assignment.ParticipantId],
                    assignment.RegionRoleId,
                    assignment.MapRegionId,
                    assignment.Facing)),
        ];
        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            topology,
            source.InitialDeployment,
            assignments,
            regions,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// T2 objective-path micro-scenario. The tested team starts at the
    /// two-step central approach while the passive controller is placed in a
    /// distant map corner. The active objective and every route fact remain
    /// ordinary contract data.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateObjectivePathProbe(
        int testedTeamId)
    {
        if (testedTeamId is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(testedTeamId));
        Position team0 = testedTeamId == 0
            ? new Position(8, 7)
            : new Position(2, 1);
        Position team1 = testedTeamId == 1
            ? new Position(14, 7)
            : new Position(20, 1);
        return CreatePrimeOnlyProbe(
            ObjectivePathProbeId,
            maxTicks: 24,
            team0,
            testedTeamId == 0 ? Direction.East : Direction.South,
            team1,
            testedTeamId == 1 ? Direction.West : Direction.South);
    }

    /// <summary>
    /// T2 clear-lane attack micro-scenario. Both assignments see the same
    /// open range-six line and an inert target.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateDirectFireProbe() =>
        CreatePrimeOnlyProbe(
            DirectFireProbeId,
            maxTicks: 20,
            new Position(8, 7),
            Direction.East,
            new Position(14, 7),
            Direction.West);

    /// <summary>
    /// T2 straight-projectile evasion micro-scenario. A framework controller
    /// fires once down the open central line and then waits.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateStraightEvadeProbe() =>
        CreatePrimeOnlyProbe(
            StraightEvadeProbeId,
            maxTicks: 12,
            new Position(8, 7),
            Direction.East,
            new Position(14, 7),
            Direction.West);

    /// <summary>
    /// T2 explicit lifecycle half of the union profile. One child slot is
    /// Ready at tick zero while each Prime is still on its declared local
    /// fabrication source. The passive opponent cannot interfere.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateManualFabricationProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{FundamentalsSuiteId}-{ManualFabricationProbeId}",
            maxTicks: 20,
            captureThreshold: 1000,
            removePopulationActions: false);
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId != 2)
                .ToImmutableArray(),
        };
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId != 2)
                .Select(assignment =>
                {
                    if (assignment.UnitId != 1)
                        return assignment;
                    return new ActorUnitSlotLifecycleAssignmentDefinition(
                        assignment.TeamId,
                        assignment.UnitId,
                        assignment.LifecycleProfileId,
                        initialGeneration: null,
                        assignment.AllowedFormIds,
                        ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.DormantUnlockAtTick,
                        unlockTick: 0,
                        assignedRespawnSpawnId: null);
                }),
        ];
        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            topology,
            source.InitialDeployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// T3 positive geometry probe. The tested Prime sees an off-axis target
    /// that cannot be reached by a legal straight or aim-only shot. A
    /// one-bend program after the first tile hits; the opposite bend enters
    /// the central lower pocket and terminates against its declared wall.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateWallTerminatedBendProbe(int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                WallTerminatedBendProbeId,
                maxTicks: 12,
                new Position(8, 7),
                Direction.East,
                "prime-mobile",
                new Position(12, 6),
                Direction.South,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                WallTerminatedBendProbeId,
                maxTicks: 12,
                new Position(10, 6),
                Direction.South,
                "prime-mobile",
                new Position(14, 7),
                Direction.West,
                "prime-mobile");
    }

    /// <summary>
    /// T3 negative strict-corner probe. The tested Prime holds the central
    /// objective and sees an off-axis opponent through a clear supercover
    /// line. A lax one-bend preview appears to hit after three tiles, but the
    /// authoritative strict diagonal is blocked by the centre-column wall.
    /// The preceding positive bend probe prevents "never curve" from gaming
    /// this selective-safety case.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateStrictCornerProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                StrictCornerProbeId,
                maxTicks: 8,
                new Position(10, 7),
                Direction.North,
                "prime-mobile",
                new Position(9, 3),
                Direction.South,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                StrictCornerProbeId,
                maxTicks: 8,
                new Position(13, 3),
                Direction.South,
                "prime-mobile",
                new Position(12, 7),
                Direction.North,
                "prime-mobile");
    }

    /// <summary>
    /// T3 cadence pair. The geometry is identical while the declared mobile
    /// projectile range is either three (ends one tile short) or four
    /// (reaches the objective occupant). The artifact must read the resolved
    /// remaining-range semantics rather than assume even speed-two steps.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateCadenceParityProbe(
        int testedTeamId,
        int projectileRange)
    {
        ValidateTestedTeam(testedTeamId);
        if (projectileRange is not (3 or 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectileRange));
        }
        ActorResolvedMatchDefinition probe = testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                $"{CadenceParityProbeId}-range-{projectileRange}",
                maxTicks: 8,
                new Position(11, 7),
                Direction.East,
                "prime-mobile",
                new Position(15, 7),
                Direction.West,
                "prime-mobile",
                mapIdentityId: CadenceParityProbeId)
            : CreateTacticalSingleLifeProbe(
                $"{CadenceParityProbeId}-range-{projectileRange}",
                maxTicks: 8,
                new Position(7, 7),
                Direction.East,
                "prime-mobile",
                new Position(11, 7),
                Direction.West,
                "prime-mobile",
                mapIdentityId: CadenceParityProbeId);
        return WithMobileProjectileRange(probe, projectileRange);
    }

    /// <summary>
    /// T3 tempo probe. The tested Prime begins two steps from the central
    /// objective while the controller commits its one allowed opening shot.
    /// The declared cooldown creates a bounded window for useful movement or
    /// damage.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateCooldownWindowProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                CooldownWindowProbeId,
                maxTicks: 10,
                new Position(8, 7),
                Direction.East,
                "prime-mobile",
                new Position(14, 7),
                Direction.North,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                CooldownWindowProbeId,
                maxTicks: 10,
                new Position(8, 7),
                Direction.North,
                "prime-mobile",
                new Position(14, 7),
                Direction.West,
                "prime-mobile");
    }

    /// <summary>
    /// T3 local commitment probe. The tested child already supplies positive
    /// weight on the active objective. Transforming into the available
    /// weight-zero turret is locally dominated by continuing to control it
    /// against a passive distant opponent.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateLocalFormSafetyProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                LocalFormSafetyProbeId,
                maxTicks: 8,
                new Position(11, 8),
                Direction.North,
                "child-mobile",
                new Position(20, 1),
                Direction.South,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                LocalFormSafetyProbeId,
                maxTicks: 8,
                new Position(2, 1),
                Direction.South,
                "prime-mobile",
                new Position(11, 8),
                Direction.North,
                "child-mobile");
    }

    /// <summary>
    /// T4 suppression state. The tested Prime already occupies the active
    /// objective at one end of the six-tile central lane. Advancing gives up
    /// useful ground, while straight fire reaches the passive lane target and
    /// every one-bend alternative is consumed by the approach walls.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateSuppressionChokeProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        Position team0 = new(8, 7);
        Position team1 = new(14, 7);
        Position[] objective = testedTeamId == 0
            ? [new Position(7, 7), new Position(8, 7)]
            : [new Position(14, 7), new Position(15, 7)];
        return CreatePositionalSingleLifeProbe(
            SuppressionChokeProbeId,
            maxTicks: 12,
            captureThreshold: 1000,
            FrontlineLabsDuelMapArm.Current,
            team0,
            Direction.East,
            team1,
            Direction.West,
            objective,
            mapVariantId: $"tested-team-{testedTeamId}");
    }

    /// <summary>
    /// T4 entry state under the current wall topology. A repeated straight
    /// pressure controller makes waiting for a last-moment response retreat
    /// forever; useful play enters the central prediction chamber early.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreatePositionalEntryProbe(int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return CreatePositionalSingleLifeProbe(
            EntryProbeId,
            maxTicks: 60,
            captureThreshold: 1000,
            FrontlineLabsDuelMapArm.Current,
            new Position(8, 7),
            Direction.East,
            new Position(14, 7),
            Direction.West);
    }

    /// <summary>
    /// T4 prediction-chamber state. A range-four straight projectile leaves a
    /// safe lateral response that preserves active-objective occupancy; a
    /// positional policy should prefer it to an equally safe concession.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreatePredictionChamberProbe(int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreatePositionalSingleLifeProbe(
                PredictionChamberProbeId,
                maxTicks: 8,
                captureThreshold: 1000,
                FrontlineLabsDuelMapArm.Current,
                new Position(12, 7),
                Direction.West,
                new Position(8, 7),
                Direction.East)
            : CreatePositionalSingleLifeProbe(
                PredictionChamberProbeId,
                maxTicks: 8,
                captureThreshold: 1000,
                FrontlineLabsDuelMapArm.Current,
                new Position(14, 7),
                Direction.West,
                new Position(10, 7),
                Direction.East);
    }

    /// <summary>
    /// T4 front-state probe. The tested Prime begins on the centre objective,
    /// completes a short declared capture, then must leave the obsolete region
    /// and establish control on the newly active objective.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateFrontRotationProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreatePositionalSingleLifeProbe(
                FrontRotationProbeId,
                maxTicks: 40,
                captureThreshold: 6,
                FrontlineLabsDuelMapArm.Current,
                new Position(11, 7),
                Direction.East,
                new Position(20, 13),
                Direction.West)
            : CreatePositionalSingleLifeProbe(
                FrontRotationProbeId,
                maxTicks: 40,
                captureThreshold: 6,
                FrontlineLabsDuelMapArm.Current,
                new Position(2, 13),
                Direction.East,
                new Position(11, 7),
                Direction.West);
    }

    /// <summary>
    /// T4 public map holdout. It repeats the mirrored pressure-entry task on
    /// the content-identified thin-front objective topology.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateMapHoldoutProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return CreatePositionalSingleLifeProbe(
            MapHoldoutProbeId,
            maxTicks: 60,
            captureThreshold: 1000,
            FrontlineLabsDuelMapArm.ThinFronts,
            new Position(8, 7),
            Direction.East,
            new Position(14, 7),
            Direction.West);
    }

    /// <summary>
    /// T2 <c>body-handoff</c>: a two-body probe in which the body doing the
    /// job dies.
    ///
    /// <para>The tested team starts one body ON the central objective in an
    /// open lane, facing a pressure controller that fires down that lane every
    /// tick, and a second body two steps behind it. Bolts carry lethal damage,
    /// so the exchange resolves in a few ticks and the question the probe
    /// exists to ask arrives immediately: <b>the body holding the point is
    /// gone — does the plan survive it?</b></para>
    ///
    /// <para>Per-life this was structurally unanswerable. The plan lived in the
    /// dying body's private memory, its successor started empty, and its
    /// sibling could only re-derive an intention from a frozen observation that
    /// never contained one. A mind simply still has the plan.</para>
    /// </summary>
    public static ActorResolvedMatchDefinition CreateBodyHandoffProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        Position tested = testedTeamId == 0
            ? new Position(10, 7)
            : new Position(12, 7);
        Position reserve = testedTeamId == 0
            ? new Position(8, 7)
            : new Position(14, 7);
        Position controller = testedTeamId == 0
            ? new Position(14, 7)
            : new Position(8, 7);
        Direction testedFacing =
            testedTeamId == 0 ? Direction.East : Direction.West;
        Direction controllerFacing =
            testedTeamId == 0 ? Direction.West : Direction.East;
        ActorResolvedMatchDefinition probe = CreateHandoffProbe(
            BodyHandoffProbeId,
            maxTicks: 40,
            testedTeamId,
            tested,
            testedFacing,
            reserve,
            controller,
            controllerFacing,
            [
                new Position(10, 7),
                new Position(11, 7),
                new Position(12, 7),
            ]);
        // Lethal bolts, so the loss the probe is about is a matter of a few
        // ticks rather than of luck. It applies to both sides, which is the
        // only symmetric way to do it.
        return WithMobileProjectileDamage(probe, damagePerHit: 99);
    }

    /// <summary>
    /// T4 <c>escort-integrity</c>: hold the point still with a body beside the
    /// holder on the axis the damage comes from, for
    /// <see cref="EscortIntegrityTicks"/> consecutive ticks, from both
    /// assignments.
    ///
    /// <para>Three own slots against a passive controller. Nothing here needs
    /// a new mechanism: it is the escorted channel exactly as the game already
    /// plays it, and it is on the T4 list because the last per-life cohort
    /// measured escorts as the intended play its authors could barely execute
    /// — every body had to independently derive who was holding, who was
    /// screening, and on which side, and any disagreement cost a tick of
    /// stationary claim.</para>
    /// </summary>
    public static ActorResolvedMatchDefinition CreateEscortIntegrityProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        Position tested = testedTeamId == 0
            ? new Position(10, 7)
            : new Position(12, 7);
        Position reserve = testedTeamId == 0
            ? new Position(8, 7)
            : new Position(14, 7);
        Position controller = testedTeamId == 0
            ? new Position(20, 1)
            : new Position(2, 1);
        return CreateHandoffProbe(
            EscortIntegrityProbeId,
            maxTicks: 30,
            testedTeamId,
            tested,
            testedTeamId == 0 ? Direction.East : Direction.West,
            reserve,
            controller,
            Direction.South,
            [
                new Position(10, 7),
                new Position(11, 7),
                new Position(12, 7),
            ]);
    }

    /// <summary>
    /// The shared two-versus-one contract behind the mind-native probes: the
    /// tested participant owns units 0 and 1 (both live at tick zero), the
    /// controller owns exactly one, and the active objective is an explicit
    /// three-tile stretch of the central lane so the geometry is stated rather
    /// than inherited.
    /// </summary>
    private static ActorResolvedMatchDefinition CreateHandoffProbe(
        string probeId,
        int maxTicks,
        int testedTeamId,
        Position testedPosition,
        Direction testedFacing,
        Position reservePosition,
        Position controllerPosition,
        Direction controllerFacing,
        IReadOnlyCollection<Position> activeObjective)
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{MindFundamentalsSuiteId}-{probeId}-t{testedTeamId}",
            maxTicks,
            captureThreshold: 1000,
            removePopulationActions: true);
        int controllerTeamId = 1 - testedTeamId;
        // Short spawn IDs on purpose: a canonical semantic ID is capped at 64
        // bytes, and the suite ID alone spends 30 of them.
        const string SpawnPrefix = "fmq";
        string testedSpawnId = $"{SpawnPrefix}-{probeId}-tested";
        string reserveSpawnId = $"{SpawnPrefix}-{probeId}-reserve";
        string controllerSpawnId = $"{SpawnPrefix}-{probeId}-controller";
        // Respawn anchors sit OFF the objective on purpose. A slot's respawn
        // anchor is reserved while the slot is away, and a reservation refuses
        // entry — so an anchor on the point would make the point unenterable
        // for the very body the probe is asking to take it over.
        string testedRespawnSpawnId =
            $"{SpawnPrefix}-{probeId}-tested-return";
        string controllerRespawnSpawnId =
            $"{SpawnPrefix}-{probeId}-controller-return";
        Position testedRespawn = new(
            testedTeamId == 0 ? 2 : 20,
            7);
        Position controllerRespawn = new(
            testedTeamId == 0 ? 20 : 2,
            7);
        ImmutableArray<ActorMapRegionDefinition> regions =
        [
            .. source.Map.Regions.Select(region =>
                region.RegionId == "frontline-position-2"
                    ? new ActorMapRegionDefinition(
                        region.RegionId,
                        region.Kind,
                        [.. activeObjective])
                    : region),
        ];
        var map = new ActorMapDefinition(
            $"{MindFundamentalsSuiteId}-{probeId}-t{testedTeamId}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(testedSpawnId, testedPosition, testedFacing),
                Spawn(reserveSpawnId, reservePosition, testedFacing),
                Spawn(
                    controllerSpawnId,
                    controllerPosition,
                    controllerFacing),
                Spawn(
                    testedRespawnSpawnId,
                    testedRespawn,
                    testedFacing),
                Spawn(
                    controllerRespawnSpawnId,
                    controllerRespawn,
                    controllerFacing),
            ],
            regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            [
                new InitialSpawnDefinition(
                    testedSpawnId,
                    testedPosition,
                    testedFacing),
                new InitialSpawnDefinition(
                    reserveSpawnId,
                    reservePosition,
                    testedFacing),
                new InitialSpawnDefinition(
                    controllerSpawnId,
                    controllerPosition,
                    controllerFacing),
            ],
            [
                new InitialLifeDeployment(
                    testedTeamId,
                    0,
                    0,
                    "prime-mobile",
                    testedSpawnId),
                new InitialLifeDeployment(
                    testedTeamId,
                    1,
                    0,
                    "child-mobile",
                    reserveSpawnId),
                new InitialLifeDeployment(
                    controllerTeamId,
                    0,
                    0,
                    "prime-mobile",
                    controllerSpawnId),
            ]);
        // Every slot keeps its declared lifecycle profile and its declared
        // allowed forms; only availability, generation and spawn move. A
        // narrowed form list would drop the anchor route's target form, and a
        // respawn spawn on a non-automatic profile is refused outright — both
        // are the validator keeping the contract honest rather than obstacles.
        ActorUnitSlotLifecycleAssignmentDefinition Assignment(
            int teamId,
            int unitId,
            string spawnId)
        {
            ActorUnitSlotLifecycleAssignmentDefinition declared =
                source.LifecycleAssignments.First(assignment =>
                    assignment.TeamId == teamId
                    && assignment.UnitId == unitId);
            return new ActorUnitSlotLifecycleAssignmentDefinition(
                teamId,
                unitId,
                declared.LifecycleProfileId,
                // A tick-zero active slot must declare the generation its
                // initial life carries; all of these are first lives.
                initialGeneration: 0,
                declared.AllowedFormIds,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                declared.AssignedRespawnSpawnId is null
                    ? null
                    : teamId == testedTeamId
                        ? testedRespawnSpawnId
                        : controllerRespawnSpawnId);
        }

        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            Assignment(testedTeamId, 0, testedSpawnId),
            Assignment(testedTeamId, 1, reserveSpawnId),
            Assignment(controllerTeamId, 0, controllerSpawnId),
        ];
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot =>
                    slot.UnitId == 0
                    || slot.TeamId == testedTeamId && slot.UnitId == 1)
                .ToImmutableArray(),
            InitialLives =
            [
                new PublicInitialLife(testedTeamId, 0, 0, "prime-mobile"),
                new PublicInitialLife(testedTeamId, 1, 0, "child-mobile"),
                new PublicInitialLife(
                    controllerTeamId,
                    0,
                    0,
                    "prime-mobile"),
            ],
        };
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition WithMobileProjectileDamage(
        ActorResolvedMatchDefinition source,
        int damagePerHit)
    {
        ActorAttackProfileDefinition[] attacks =
        [
            .. source.Rules.AttackProfiles.Select(profile =>
            {
                ActorProjectileDefinition projectile = profile.Projectile;
                return new ActorAttackProfileDefinition(
                    profile.Id,
                    profile.OmnidirectionalAim,
                    new ActorProjectileDefinition(
                        projectile.Mode,
                        damagePerHit,
                        projectile.MaxTravelTiles,
                        projectile.TicksPerAdvance,
                        projectile.TilesPerAdvance,
                        projectile.LaunchTiles,
                        projectile.AdvancesOnLaunchTick,
                        projectile.DamageAppliedSimultaneously,
                        projectile.DiagonalCornersMustBeClear),
                    profile.CooldownTicks,
                    profile.MaxEnergy,
                    profile.AttackEnergyCost,
                    profile.EnergyRegenerationIntervalTicks,
                    profile.EnergyRegenerationAmount,
                    profile.ShotProgram);
            }),
        ];
        ActorRulesDefinition rules = source.Rules;
        var lethalRules = new ActorRulesDefinition(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles,
            rules.VisionProfiles,
            attacks,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            lethalRules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition CreatePrimeOnlyProbe(
        string probeId,
        int maxTicks,
        Position team0Position,
        Direction team0Facing,
        Position team1Position,
        Direction team1Facing)
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{FundamentalsSuiteId}-{probeId}",
            maxTicks,
            captureThreshold: 1000,
            removePopulationActions: true);
        string team0SpawnId =
            $"{FundamentalsSuiteId}-{probeId}-team-0";
        string team1SpawnId =
            $"{FundamentalsSuiteId}-{probeId}-team-1";
        var map = new ActorMapDefinition(
            $"{FundamentalsSuiteId}-{probeId}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(team0SpawnId, team0Position, team0Facing),
                Spawn(team1SpawnId, team1Position, team1Facing),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            [
                new InitialSpawnDefinition(
                    team0SpawnId,
                    team0Position,
                    team0Facing),
                new InitialSpawnDefinition(
                    team1SpawnId,
                    team1Position,
                    team1Facing),
            ],
            [
                new InitialLifeDeployment(
                    0,
                    0,
                    0,
                    "prime-mobile",
                    team0SpawnId),
                new InitialLifeDeployment(
                    1,
                    0,
                    0,
                    "prime-mobile",
                    team1SpawnId),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId == 0)
                .Select(assignment =>
                    new ActorUnitSlotLifecycleAssignmentDefinition(
                        assignment.TeamId,
                        assignment.UnitId,
                        assignment.LifecycleProfileId,
                        assignment.InitialGeneration,
                        assignment.AllowedFormIds,
                        assignment.InitialAvailability,
                        assignment.UnlockTick,
                        assignment.TeamId == 0
                            ? team0SpawnId
                            : team1SpawnId)),
        ];
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId == 0)
                .ToImmutableArray(),
            InitialLives = source.Topology.InitialLives
                .Where(life => life.UnitId == 0)
                .ToImmutableArray(),
        };
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition
        CreateTacticalSingleLifeProbe(
            string probeId,
            int maxTicks,
            Position team0Position,
            Direction team0Facing,
            string team0FormId,
            Position team1Position,
            Direction team1Facing,
            string team1FormId,
            string? mapIdentityId = null)
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{TacticalSuiteId}-{probeId}",
            maxTicks,
            captureThreshold: 1000,
            removePopulationActions: true);
        string mapIdentity = mapIdentityId ?? probeId;
        string team0SpawnId =
            $"{TacticalSuiteId}-{mapIdentity}-team-0";
        string team1SpawnId =
            $"{TacticalSuiteId}-{mapIdentity}-team-1";
        var map = new ActorMapDefinition(
            $"{TacticalSuiteId}-{mapIdentity}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(team0SpawnId, team0Position, team0Facing),
                Spawn(team1SpawnId, team1Position, team1Facing),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            [
                new InitialSpawnDefinition(
                    team0SpawnId,
                    team0Position,
                    team0Facing),
                new InitialSpawnDefinition(
                    team1SpawnId,
                    team1Position,
                    team1Facing),
            ],
            [
                new InitialLifeDeployment(
                    0,
                    0,
                    0,
                    team0FormId,
                    team0SpawnId),
                new InitialLifeDeployment(
                    1,
                    0,
                    0,
                    team1FormId,
                    team1SpawnId),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId == 0)
                .Select(assignment =>
                {
                    string initialForm = assignment.TeamId == 0
                        ? team0FormId
                        : team1FormId;
                    string[] allowedForms = initialForm switch
                    {
                        "child-mobile" =>
                        [
                            "prime-mobile",
                            "child-mobile",
                            "turret",
                        ],
                        "turret" =>
                        [
                            "prime-mobile",
                            "turret",
                        ],
                        _ => ["prime-mobile"],
                    };
                    return new ActorUnitSlotLifecycleAssignmentDefinition(
                        assignment.TeamId,
                        assignment.UnitId,
                        assignment.LifecycleProfileId,
                        assignment.InitialGeneration,
                        allowedForms,
                        assignment.InitialAvailability,
                        assignment.UnlockTick,
                        assignment.TeamId == 0
                            ? team0SpawnId
                            : team1SpawnId);
                }),
        ];
        var topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId == 0)
                .ToImmutableArray(),
            InitialLives =
            [
                new PublicInitialLife(0, 0, 0, team0FormId),
                new PublicInitialLife(1, 0, 0, team1FormId),
            ],
        };
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition
        CreatePositionalSingleLifeProbe(
            string probeId,
            int maxTicks,
            int captureThreshold,
            FrontlineLabsDuelMapArm mapArm,
            Position team0Position,
            Direction team0Facing,
            Position team1Position,
            Direction team1Facing,
            IReadOnlyCollection<Position>? activeObjectiveOverride = null,
            string? mapVariantId = null)
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment(mapArm);
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{PositionalSuiteId}-{probeId}",
            maxTicks,
            captureThreshold,
            removePopulationActions: true);
        string team0SpawnId =
            $"{PositionalSuiteId}-{probeId}-team-0";
        string team1SpawnId =
            $"{PositionalSuiteId}-{probeId}-team-1";
        ImmutableArray<ActorMapRegionDefinition> regions =
        [
            .. source.Map.Regions.Select(region =>
                activeObjectiveOverride is not null
                && region.RegionId == "frontline-position-2"
                    ? new ActorMapRegionDefinition(
                        region.RegionId,
                        region.Kind,
                        activeObjectiveOverride.ToImmutableArray())
                    : region),
        ];
        var map = new ActorMapDefinition(
            $"fq5-{probeId}-{MapArmId(mapArm)}" +
            $"{(mapVariantId is null ? "" : $"-{mapVariantId}")}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(team0SpawnId, team0Position, team0Facing),
                Spawn(team1SpawnId, team1Position, team1Facing),
            ],
            regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            [
                new InitialSpawnDefinition(
                    team0SpawnId,
                    team0Position,
                    team0Facing),
                new InitialSpawnDefinition(
                    team1SpawnId,
                    team1Position,
                    team1Facing),
            ],
            [
                new InitialLifeDeployment(
                    0,
                    0,
                    0,
                    "prime-mobile",
                    team0SpawnId),
                new InitialLifeDeployment(
                    1,
                    0,
                    0,
                    "prime-mobile",
                    team1SpawnId),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId == 0)
                .Select(assignment =>
                    new ActorUnitSlotLifecycleAssignmentDefinition(
                        assignment.TeamId,
                        assignment.UnitId,
                        assignment.LifecycleProfileId,
                        assignment.InitialGeneration,
                        ["prime-mobile"],
                        assignment.InitialAvailability,
                        assignment.UnlockTick,
                        assignment.TeamId == 0
                            ? team0SpawnId
                            : team1SpawnId)),
        ];
        var topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId == 0)
                .ToImmutableArray(),
            InitialLives =
            [
                new PublicInitialLife(0, 0, 0, "prime-mobile"),
                new PublicInitialLife(1, 0, 0, "prime-mobile"),
            ],
        };
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static string MapArmId(FrontlineLabsDuelMapArm mapArm) =>
        mapArm switch
        {
            FrontlineLabsDuelMapArm.Current => "current",
            FrontlineLabsDuelMapArm.ThinFronts => "thin-fronts",
            FrontlineLabsDuelMapArm.OuterShoulderBypass =>
                "outer-shoulder-bypass",
            _ => throw new ArgumentOutOfRangeException(
                nameof(mapArm)),
        };

    private static ActorResolvedMatchDefinition WithMobileProjectileRange(
        ActorResolvedMatchDefinition source,
        int maxTravelTiles)
    {
        ActorAttackProfileDefinition[] attacks =
        [
            .. source.Rules.AttackProfiles.Select(profile =>
            {
                if (profile.Id != "mobile-bolt")
                    return profile;
                ActorProjectileDefinition projectile = profile.Projectile;
                return new ActorAttackProfileDefinition(
                    profile.Id,
                    profile.OmnidirectionalAim,
                    new ActorProjectileDefinition(
                        projectile.Mode,
                        projectile.DamagePerHit,
                        maxTravelTiles,
                        projectile.TicksPerAdvance,
                        projectile.TilesPerAdvance,
                        projectile.LaunchTiles,
                        projectile.AdvancesOnLaunchTick,
                        projectile.DamageAppliedSimultaneously,
                        projectile.DiagonalCornersMustBeClear),
                    profile.CooldownTicks,
                    profile.MaxEnergy,
                    profile.AttackEnergyCost,
                    profile.EnergyRegenerationIntervalTicks,
                    profile.EnergyRegenerationAmount,
                    profile.ShotProgram);
            }),
        ];
        ActorRulesDefinition rules = source.Rules;
        var rangedRules = new ActorRulesDefinition(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles,
            rules.VisionProfiles,
            attacks,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            rangedRules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static void ValidateTestedTeam(int testedTeamId)
    {
        if (testedTeamId is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(testedTeamId));
    }

    private static ActorRulesDefinition WithProbeMode(
        ActorRulesDefinition source,
        string rulesetId,
        int maxTicks,
        int captureThreshold,
        bool removePopulationActions)
    {
        var frontline = (FrontlineGameModeDefinition)source.GameMode;
        FrontlineCaptureDefinition capture = frontline.Capture;
        var mode = new FrontlineGameModeDefinition(
            frontline.FrontlineVictory,
            frontline.ScoreCatalog,
            frontline.FrontlinePositionCount,
            new FrontlineCaptureDefinition(
                captureThreshold,
                capture.GainPerSoleTeamTick,
                capture.DecayAmount,
                capture.DecayIntervalTicks,
                capture.RedeployPauseTicks,
                capture.GainSchedule,
                capture.ControlPolicy));
        HashSet<string> removedActionIds = removePopulationActions
            ? ["fabricate", "split"]
            : [];
        return new ActorRulesDefinition(
            rulesetId,
            new ActorRulesLimits(maxTicks, source.Limits.RuntimeFaults),
            source.SeedMechanics,
            mode,
            source.Lifecycle,
            source.Forms.Select(form =>
                removedActionIds.Count == 0
                    ? form
                    : new ActorFormDefinition(
                        form.Id,
                        form.MaxHealth,
                        form.MovementProfileId,
                        form.VisionProfileId,
                        form.AttackProfileId,
                        form.ObjectiveWeight,
                        form.AllowedActionIds.Where(actionId =>
                            !removedActionIds.Contains(actionId)))),
            source.MovementProfiles,
            source.VisionProfiles,
            source.AttackProfiles,
            source.Actions.Where(action =>
                !removedActionIds.Contains(action.Id)),
            removePopulationActions
                ? []
                : source.FabricationTransitions,
            source.SameLifeTransitions,
            removePopulationActions
                ? []
                : source.ReplicationTransitions,
            source.TeamPerception,
            source.Collisions,
            source.TickResolution);
    }

    private static ActorRulesDefinition WithProbeLimits(
        ActorRulesDefinition source,
        string rulesetId,
        int maxTicks) =>
        new(
            rulesetId,
            new ActorRulesLimits(
                maxTicks,
                source.Limits.RuntimeFaults),
            source.SeedMechanics,
            source.GameMode,
            source.Lifecycle,
            source.Forms.Select(form =>
                form.Id == "prime-mobile"
                    ? new ActorFormDefinition(
                        form.Id,
                        form.MaxHealth,
                        form.MovementProfileId,
                        form.VisionProfileId,
                        form.AttackProfileId,
                        form.ObjectiveWeight,
                        form.AllowedActionIds.Where(actionId =>
                            actionId is not ("fabricate" or "split")))
                    : form),
            source.MovementProfiles,
            source.VisionProfiles,
            source.AttackProfiles,
            source.Actions.Where(action =>
                action.Id is not ("fabricate" or "split")),
            [],
            source.SameLifeTransitions,
            [],
            source.TeamPerception,
            source.Collisions,
            source.TickResolution);

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        Position position,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(id, position, facing),
            ImmutableArray.Create(ActorMovementLayer.Ground));
}
