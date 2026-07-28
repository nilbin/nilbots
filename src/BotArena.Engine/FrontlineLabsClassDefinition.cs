using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One pre-registered Frontline Labs class chassis: a data-only stat and
/// capability block that the classes experiment arm expands into per-team
/// forms, profiles, routes, and lifecycle assignments (DECISIONS #153/#154).
/// The values are experiment candidates, never a balance verdict. Movement
/// and projectile kinematics (one tile per tick, projectile speed two,
/// damage one) are deliberately identical across classes so the exact duel
/// analysis keeps its parity structure. Each class carries exactly one
/// exclusive verb family: Striker bends shots, Bulwark fortifies
/// (reversibly, class-wide), Fabricator forward-fabricates explicitly while
/// the other classes receive companions automatically. Split is absent from
/// every class arm and reserved for a future swarm class (ablation debt).
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

    /// <summary>Class-wide fortification: prime and children may Anchor into
    /// per-source turret forms and Mobilize back once per life. The prime's
    /// longer windup makes its commitment a readable, punishable window.</summary>
    public required bool MayAnchor { get; init; }

    public required int PrimeAnchorWindupTicks { get; init; }
    public required int ChildAnchorWindupTicks { get; init; }
    public required int TurretMaxHealth { get; init; }

    /// <summary>The Fabricator's exclusive verb: explicitly queue a child
    /// that materializes beside the prime in the field (never on a protected
    /// pad). Classes without it receive companions automatically at their
    /// unlock ticks — manual pad fabrication is a dominant chore, not a
    /// strategy (DECISIONS #154).</summary>
    public required bool ExplicitForwardFabrication { get; init; }

    public required int FirstChildUnlockTick { get; init; }
    public required int SecondChildUnlockTick { get; init; }
    public required int ChildRebuildDelayTicks { get; init; }

    public string PrimeFormId => $"{Id}-prime";
    public string ChildFormId => $"{Id}-child";
    public string PrimeTurretFormId => $"{Id}-prime-turret";
    public string ChildTurretFormId => $"{Id}-child-turret";
    public string MobileVisionProfileId => $"{Id}-vision";
    public string MobileAttackProfileId => $"{Id}-bolt";
    public string PrimeLifecycleProfileId => $"{Id}-prime-respawn";
    public string ChildLifecycleProfileId => $"{Id}-child-ready";

    /// <summary>The reference chassis: the duel-depth one-bend arm as a class.
    /// Prediction duels through private shot commitments; companions arrive
    /// automatically.</summary>
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
        MayAnchor = false,
        PrimeAnchorWindupTicks = 0,
        ChildAnchorWindupTicks = 0,
        TurretMaxHealth = 0,
        ExplicitForwardFabrication = false,
        FirstChildUnlockTick = 120,
        SecondChildUnlockTick = 260,
        ChildRebuildDelayTicks = 30,
    };

    /// <summary>Durable short-sighted fortifier: straight suppressive fire on
    /// a slow cadence, tougher bodies, and the exclusive reversible Anchor —
    /// prime included, behind a longer visible windup.</summary>
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
        MayAnchor = true,
        PrimeAnchorWindupTicks = 3,
        ChildAnchorWindupTicks = 1,
        TurretMaxHealth = 7,
        ExplicitForwardFabrication = false,
        FirstChildUnlockTick = 120,
        SecondChildUnlockTick = 260,
        ChildRebuildDelayTicks = 30,
    };

    /// <summary>Fragile economy engine: the only class that fabricates, and
    /// its fabrication is a real decision — earlier, faster, field-placed
    /// children bought with combat actions. Lowest floor, highest ceiling.</summary>
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
        MayAnchor = false,
        PrimeAnchorWindupTicks = 0,
        ChildAnchorWindupTicks = 0,
        TurretMaxHealth = 0,
        ExplicitForwardFabrication = true,
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
