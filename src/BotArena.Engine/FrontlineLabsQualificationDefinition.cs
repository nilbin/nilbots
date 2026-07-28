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
    public const string EntryProbeId = "entry-initiative";

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
