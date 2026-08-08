using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Diagnostic projection of the values that actually differ between probe
/// variants. Every field is already public contract data the artifact
/// receives at MatchStart; the report repeats it beside the metrics so an
/// author never has to replay evidence to learn which map, attack profile,
/// or start position a case used.
/// </summary>
internal sealed record FrontlineLabsQualificationScenario(
    string ProbeId,
    string VariantId,
    string RulesetId,
    string MapId,
    string MapArm,
    int MaxTicks,
    int CaptureThreshold,
    int FrontlinePositionCount,
    int InitialActivePositionIndex,
    string? InitialActiveObjectiveRegionId,
    IReadOnlyList<Position> InitialActiveObjectiveTiles,
    FrontlineLabsQualificationScenarioActor Bot,
    FrontlineLabsQualificationScenarioActor Controller)
{
    public static FrontlineLabsQualificationScenario Resolve(
        ActorResolvedMatchDefinition definition,
        string probeId,
        string variantId,
        string mapArm,
        string controller,
        int botTeamId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var frontline =
            definition.Rules.GameMode as FrontlineGameModeDefinition;
        var binding = definition.ModeMapBinding
            as FrontlineActorModeMapBindingDefinition;
        int positionCount = frontline?.FrontlinePositionCount ?? 0;
        int activeIndex = positionCount / 2;
        string? activeRegionId =
            binding is not null
            && activeIndex >= 0
            && activeIndex < binding.OrderedObjectiveRegionIds.Length
                ? binding.OrderedObjectiveRegionIds[activeIndex]
                : null;
        Position[] activeTiles = activeRegionId is null
            ? []
            :
            [
                .. definition.Map.Regions
                    .Single(region => region.RegionId == activeRegionId)
                    .Tiles,
            ];
        return new FrontlineLabsQualificationScenario(
            probeId,
            variantId,
            definition.Rules.RulesetId,
            definition.Map.Id,
            mapArm,
            definition.Rules.Limits.MaxTicks,
            frontline?.Capture.Threshold ?? 0,
            positionCount,
            activeIndex,
            activeRegionId,
            activeTiles,
            ResolveActor(
                definition,
                botTeamId,
                "tested-artifact",
                activeTiles),
            ResolveActor(
                definition,
                1 - botTeamId,
                controller,
                activeTiles));
    }

    private static FrontlineLabsQualificationScenarioActor ResolveActor(
        ActorResolvedMatchDefinition definition,
        int teamId,
        string role,
        IReadOnlyList<Position> activeObjectiveTiles)
    {
        InitialLifeDeployment life = definition.InitialDeployment.Lives
            .First(item => item.TeamId == teamId);
        InitialSpawnDefinition spawn = definition.InitialDeployment.Spawns
            .Single(item => item.SpawnId == life.SpawnId);
        ActorFormDefinition form = definition.Rules.Forms
            .Single(item => item.Id == life.FormId);
        ActorAttackProfileDefinition? attack =
            form.AttackProfileId is string attackProfileId
                ? definition.Rules.AttackProfiles
                    .Single(item => item.Id == attackProfileId)
                : null;
        return new FrontlineLabsQualificationScenarioActor(
            role,
            teamId,
            definition.Topology.Participants
                .Single(participant => participant.TeamId == teamId)
                .ParticipantId,
            spawn.Position,
            spawn.Facing.ToString().ToLowerInvariant(),
            life.FormId,
            form.ObjectiveWeight,
            activeObjectiveTiles.Contains(spawn.Position),
            [.. form.AllowedActionIds],
            attack is null
                ? null
                : new FrontlineLabsQualificationScenarioAttack(
                    attack.Id,
                    attack.Projectile.MaxTravelTiles,
                    attack.Projectile.TilesPerAdvance,
                    attack.Projectile.TicksPerAdvance,
                    attack.Projectile.LaunchTiles,
                    attack.CooldownTicks,
                    attack.Projectile.DamagePerHit,
                    attack.OmnidirectionalAim,
                    attack.ShotProgram.Enabled,
                    attack.ShotProgram.MaxBendCount),
            [
                .. definition.LifecycleAssignments
                    .Where(assignment => assignment.TeamId == teamId)
                    .OrderBy(assignment => assignment.UnitId)
                    .Select(assignment =>
                        new FrontlineLabsQualificationScenarioUnitSlot(
                            assignment.UnitId,
                            CamelCase(
                                assignment.InitialAvailability.ToString()),
                            assignment.UnlockTick,
                            [.. assignment.AllowedFormIds])),
            ]);
    }

    private static string CamelCase(string value) =>
        value.Length == 0
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
}

/// <summary>One side's resolved starting facts for a qualification case.</summary>
internal sealed record FrontlineLabsQualificationScenarioActor(
    string Role,
    int TeamId,
    int ParticipantId,
    Position StartPosition,
    string StartFacing,
    string StartFormId,
    int StartObjectiveWeight,
    bool StartsOnActiveObjective,
    IReadOnlyList<string> StartFormActionIds,
    FrontlineLabsQualificationScenarioAttack? Attack,
    IReadOnlyList<FrontlineLabsQualificationScenarioUnitSlot> UnitSlots);

/// <summary>Resolved attack-profile values of one side's starting form.</summary>
internal sealed record FrontlineLabsQualificationScenarioAttack(
    string ProfileId,
    int MaxTravelTiles,
    int TilesPerAdvance,
    int TicksPerAdvance,
    int LaunchTiles,
    int CooldownTicks,
    int DamagePerHit,
    bool OmnidirectionalAim,
    bool ShotProgramEnabled,
    int MaxBendCount);

/// <summary>Resolved lifecycle availability of one stable unit slot.</summary>
internal sealed record FrontlineLabsQualificationScenarioUnitSlot(
    int UnitId,
    string InitialAvailability,
    int? UnlockTick,
    IReadOnlyList<string> AllowedFormIds);
