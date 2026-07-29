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
    /// programs fires straight through the parameterless attack action —
    /// unless the arm's bend envelope is universal, which hands every mobile
    /// gun the same grammar at its own depth.</summary>
    public required bool OneBendShotPrograms { get; init; }

    /// <summary>
    /// How far this class's mobile gun may fly before its one bend. Depth is
    /// the identity: the striker keeps the full 1–4 envelope it was measured
    /// on, and a class that gains the grammar in a universal-bend arm gets the
    /// shallower half the skill-shot forensics justified — option richness
    /// rather than raw power (owner ruling, the slate's "Current kit").
    /// </summary>
    public required int MobileMaxBendAfterTiles { get; init; }

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

    /// <summary>
    /// The one candidate skill this class owns in the pre-registered kit
    /// (<c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>). Skills are opt-in
    /// per arm; a cell only carries a skill token when the owning class is
    /// actually in it, so an arm identity stays a function of its contract.
    /// </summary>
    public FrontlineLabsSkillKit Skill { get; init; } =
        FrontlineLabsSkillKit.None;

    /// <summary>
    /// Windup ticks entering the class's stance. A longer entry than exit is
    /// the deliberate shape: committing is the punishable public window, and
    /// leaving must stay cheap enough that the stance is a cycle rather than a
    /// self-brick.
    /// </summary>
    public int StanceEntryWindupTicks { get; init; }

    /// <summary>Windup ticks returning from the class's stance.</summary>
    public int StanceExitWindupTicks { get; init; }

    /// <summary>Projectiles the volley stance's gun launches per attack.</summary>
    public int VolleyProjectileCount { get; init; }

    /// <summary>Fire cadence of the volley stance's gun, in ticks.</summary>
    public int VolleyCooldownTicks { get; init; }

    /// <summary>
    /// Unlock ticks for the extra unit slots this class receives in a
    /// five-slot arm, in slot order. Empty for every class that does not own
    /// <see cref="FrontlineLabsSkillKit.FabricatorFiveSlots"/>.
    /// </summary>
    public ImmutableArray<int> ExtraChildUnlockTicks { get; init; } = [];

    /// <summary>
    /// Rebuild delay for the extra slots. Deliberately slower than
    /// <see cref="ChildRebuildDelayTicks"/>: the slate killed SURGE because
    /// faster replacement of free bodies accelerates the convicted loop, so
    /// extra slot COUNT must not become extra slot TEMPO.
    /// </summary>
    public int ExtraChildRebuildDelayTicks { get; init; }

    public string PrimeFormId => $"{Id}-prime";
    public string ChildFormId => $"{Id}-child";
    public string PrimeTurretFormId => $"{Id}-prime-turret";
    public string ChildTurretFormId => $"{Id}-child-turret";

    /// <summary>
    /// Stance forms are per source form, exactly like the turret forms and for
    /// the same reason: the parameterless Mobilize must resolve a single
    /// return target (DECISIONS #154).
    /// </summary>
    public string PrimeStanceFormId => $"{Id}-prime-{StanceToken}";

    public string ChildStanceFormId => $"{Id}-child-{StanceToken}";

    public string StanceAttackProfileId => $"{Id}-volley";
    public string MobileVisionProfileId => $"{Id}-vision";
    public string MobileAttackProfileId => $"{Id}-bolt";
    public string PrimeLifecycleProfileId => $"{Id}-prime-respawn";
    public string ChildLifecycleProfileId => $"{Id}-child-ready";
    public string ExtraChildLifecycleProfileId => $"{Id}-late-child-ready";

    private string StanceToken => Skill switch
    {
        FrontlineLabsSkillKit.StrikerVolley => "volley-stance",
        FrontlineLabsSkillKit.BulwarkAegisShell => "aegis-shell",
        _ => throw new InvalidOperationException(
            $"Class '{Id}' owns no same-life stance skill."),
    };

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
        MobileMaxBendAfterTiles = 4,
        MayAnchor = false,
        PrimeAnchorWindupTicks = 0,
        ChildAnchorWindupTicks = 0,
        TurretMaxHealth = 0,
        ExplicitForwardFabrication = false,
        FirstChildUnlockTick = 120,
        SecondChildUnlockTick = 260,
        ChildRebuildDelayTicks = 30,
        // VOLLEY: entry 2 / exit 1, three damage-1 bolts, cadence 5 against
        // the mobile gun's 2. Zoning, not a wipe — the fan must soften
        // striker-vs-fabricator toward the cycle-magnitude band rather than
        // deepen the game's most lopsided edge.
        Skill = FrontlineLabsSkillKit.StrikerVolley,
        StanceEntryWindupTicks = 2,
        StanceExitWindupTicks = 1,
        VolleyProjectileCount = 3,
        VolleyCooldownTicks = 5,
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
        MobileMaxBendAfterTiles = 2,
        MayAnchor = true,
        PrimeAnchorWindupTicks = 3,
        ChildAnchorWindupTicks = 1,
        TurretMaxHealth = 7,
        ExplicitForwardFabrication = false,
        FirstChildUnlockTick = 120,
        SecondChildUnlockTick = 260,
        ChildRebuildDelayTicks = 30,
        // AEGIS SHELL: entry 1 / exit 1. Tenure is not clock-bounded; it is
        // priced by the form — immobile, no gun — while objective weight
        // stays 1, so a shelled bulwark still holds ground it cannot shoot
        // from. Blanks poke, loses to flanks and multi-angle bodies.
        Skill = FrontlineLabsSkillKit.BulwarkAegisShell,
        StanceEntryWindupTicks = 1,
        StanceExitWindupTicks = 1,
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
        MobileMaxBendAfterTiles = 2,
        MayAnchor = false,
        PrimeAnchorWindupTicks = 0,
        ChildAnchorWindupTicks = 0,
        TurretMaxHealth = 0,
        ExplicitForwardFabrication = true,
        FirstChildUnlockTick = 60,
        SecondChildUnlockTick = 180,
        ChildRebuildDelayTicks = 15,
        // FIVE SLOTS: the extra two slots continue this class's own 120-tick
        // unlock cadence (60, 180, 300, 420) rather than inventing a second
        // schedule, so the fifth body cannot reach the field before 84% of a
        // 500-tick match has elapsed — a late-game ceiling that has to be
        // defended, not an opening advantage. Their rebuild delay is the
        // non-fabricator baseline (30, double this class's 15): the slate
        // killed SURGE for accelerating replacement, so the arm buys COUNT
        // without buying TEMPO.
        Skill = FrontlineLabsSkillKit.FabricatorFiveSlots,
        ExtraChildUnlockTicks = [300, 420],
        ExtraChildRebuildDelayTicks = 30,
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
