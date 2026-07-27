namespace BotArena.Engine;

/// <summary>
/// Complete public contract for creating and recreating Frontline child lives.
/// Dynamic action masks expose current availability; this definition exposes
/// the stable rules that produce those masks and resolve a submitted action.
/// </summary>
public sealed record PublicFrontlineFabricationDefinition(
    bool Enabled,
    string ActionId,
    int FabricatorUnitId,
    string FabricatorFormId,
    PublicFrontlineFabricationTargetPolicy TargetPolicy,
    PublicFrontlineFabricationActivationRegion ActivationRegion,
    bool ConsumesTick,
    int SpawnDelayTicks,
    PublicFrontlineFabricationCapacityEvaluation CapacityEvaluation,
    PublicFrontlineFabricationSpawnRegion SpawnRegion,
    PublicFrontlineFabricationSpawnSelection SpawnSelection,
    PublicFrontlineFabricationSpawnFacing SpawnFacing,
    PublicActionRejectionResult UnavailableSpawnResult,
    bool RequiresExplicitRefabricationAfterRebuild);

public enum PublicFrontlineFabricationTargetPolicy
{
    OwnReadyChildSlot = 0,
}

public enum PublicFrontlineFabricationActivationRegion
{
    OwnProtectedSpawnPad = 0,
}

public enum PublicFrontlineFabricationSpawnRegion
{
    OwnProtectedSpawnPadExcludingPrimeSpawn = 0,
}

/// <summary>
/// Action masks expose valid attempt targets without pre-filtering pad
/// capacity. Capacity is evaluated after joint movement in the explicit
/// QueueFabrications phase, where no available tile produces the public
/// unavailable-spawn result.
/// </summary>
public enum PublicFrontlineFabricationCapacityEvaluation
{
    PostMovementDuringQueueFabrications = 0,
}

public enum PublicFrontlineFabricationSpawnSelection
{
    FirstUnoccupiedUnreservedCanonicalYThenX = 0,
}

public enum PublicFrontlineFabricationSpawnFacing
{
    OwnPrimeSpawnFacing = 0,
}
