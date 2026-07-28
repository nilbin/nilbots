using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One pre-registered Frontline Labs class chassis: a data-only stat and
/// capability block that the classes experiment arm expands into per-team
/// forms, profiles, routes, and lifecycle assignments (DECISIONS #153).
/// The values are experiment candidates, never a balance verdict. Movement
/// and projectile kinematics (one tile per tick, projectile speed two,
/// damage one) are deliberately identical across classes so the exact duel
/// analysis keeps its parity structure; classes differ in durability,
/// vision, fire tempo, shot language, anchor play, and fabrication
/// economics only.
/// </summary>
public sealed record FrontlineLabsClassDefinition
{
    public required string Id { get; init; }
    public required int PrimeMaxHealth { get; init; }
    public required int ChildMaxHealth { get; init; }
    public required ActorVisionShape MobileVisionShape { get; init; }
    public required int MobileVisionRange { get; init; }
    public required int MobileOmnidirectionalProximityRange { get; init; }
    public required int MobileCooldownTicks { get; init; }
    public required int MobileMaxTravelTiles { get; init; }

    /// <summary>One private bend (the duel-depth envelope). A class without
    /// programs fires straight through the parameterless attack action.</summary>
    public required bool OneBendShotPrograms { get; init; }

    public required int TurretMaxHealth { get; init; }
    public required bool TurretMayMobilize { get; init; }
    public required int FirstChildUnlockTick { get; init; }
    public required int SecondChildUnlockTick { get; init; }
    public required int ChildRebuildDelayTicks { get; init; }

    public string PrimeFormId => $"{Id}-prime";
    public string ChildFormId => $"{Id}-child";
    public string ReplicaFormId => $"{Id}-replica";
    public string TurretFormId => $"{Id}-turret";
    public string MobileVisionProfileId => $"{Id}-vision";
    public string MobileAttackProfileId => $"{Id}-bolt";
    public string PrimeLifecycleProfileId => $"{Id}-prime-respawn";
    public string ChildLifecycleProfileId => $"{Id}-child-ready";

    /// <summary>The reference chassis: the duel-depth one-bend arm as a class.
    /// Prediction duels through private shot commitments.</summary>
    public static FrontlineLabsClassDefinition Striker { get; } = new()
    {
        Id = "striker",
        PrimeMaxHealth = 3,
        ChildMaxHealth = 3,
        MobileVisionShape = ActorVisionShape.FacingQuadrant,
        MobileVisionRange = 6,
        MobileOmnidirectionalProximityRange = 1,
        MobileCooldownTicks = 2,
        MobileMaxTravelTiles = 8,
        OneBendShotPrograms = true,
        TurretMaxHealth = 5,
        TurretMayMobilize = false,
        FirstChildUnlockTick = 120,
        SecondChildUnlockTick = 260,
        ChildRebuildDelayTicks = 30,
    };

    /// <summary>Durable short-sighted holder: straight suppressive fire on a
    /// slow cadence, tougher bodies, and reversible turret commitment.</summary>
    public static FrontlineLabsClassDefinition Bulwark { get; } = new()
    {
        Id = "bulwark",
        PrimeMaxHealth = 5,
        ChildMaxHealth = 4,
        MobileVisionShape = ActorVisionShape.Omnidirectional,
        MobileVisionRange = 4,
        MobileOmnidirectionalProximityRange = 4,
        MobileCooldownTicks = 3,
        MobileMaxTravelTiles = 6,
        OneBendShotPrograms = false,
        TurretMaxHealth = 7,
        TurretMayMobilize = true,
        FirstChildUnlockTick = 120,
        SecondChildUnlockTick = 260,
        ChildRebuildDelayTicks = 30,
    };

    /// <summary>Fragile economy engine: ordinary guns on a weak prime, but
    /// companions unlock earlier and rebuild faster.</summary>
    public static FrontlineLabsClassDefinition Fabricator { get; } = new()
    {
        Id = "fabricator",
        PrimeMaxHealth = 2,
        ChildMaxHealth = 3,
        MobileVisionShape = ActorVisionShape.FacingQuadrant,
        MobileVisionRange = 6,
        MobileOmnidirectionalProximityRange = 1,
        MobileCooldownTicks = 2,
        MobileMaxTravelTiles = 7,
        OneBendShotPrograms = false,
        TurretMaxHealth = 5,
        TurretMayMobilize = false,
        FirstChildUnlockTick = 60,
        SecondChildUnlockTick = 180,
        ChildRebuildDelayTicks = 15,
    };

    public static ImmutableArray<FrontlineLabsClassDefinition> All { get; } =
        [Bulwark, Fabricator, Striker];

    public static FrontlineLabsClassDefinition Parse(string id) =>
        All.FirstOrDefault(entry => entry.Id == id)
        ?? throw new ArgumentException(
            $"Unknown Frontline Labs class '{id}'. Known classes: "
            + string.Join(", ", All.Select(entry => entry.Id))
            + ".",
            nameof(id));
}
