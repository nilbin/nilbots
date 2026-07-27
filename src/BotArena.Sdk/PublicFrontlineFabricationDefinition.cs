namespace BotArena.Sdk;

/// <summary>Complete public contract for creating Frontline child lives.</summary>
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
