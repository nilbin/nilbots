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
    public const string EntryProbeId = "entry-initiative";
    public const string ContractAutoDeterminismProbeId =
        "contract-auto-determinism";

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
