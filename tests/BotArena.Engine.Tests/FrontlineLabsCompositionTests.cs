using BotArena.Engine;

namespace BotArena.Engine.Tests;

/// <summary>
/// Slot-scoped COMPOSITIONS: chassis identity moves from the participant to
/// the slot, mono-class becomes the special case, and the registered mixed
/// presets each answer one pre-registered question
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §9).
/// </summary>
public class FrontlineLabsCompositionTests
{
    private static ActorResolvedMatchDefinition Cell(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsChassisArm chassis = FrontlineLabsChassisArm.Unified,
        (FrontlineLabsComposition TeamZero,
            FrontlineLabsComposition TeamOne)? compositions = null) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ContestMajority
                | FrontlineLabsPendulumArm.EnemySoleDecay,
            (teamZero, teamOne),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: FrontlineLabsSkillKit.StrikerVolley
                | FrontlineLabsSkillKit.BulwarkAegisShell
                | FrontlineLabsSkillKit.FabricatorFiveSlots,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            stanceGround: FrontlineLabsStanceGroundArm.Open,
            aim: FrontlineLabsAimArm.Offset,
            cooldown: FrontlineLabsCooldownArm.Ticking,
            volley: FrontlineLabsVolleyArm.Salvo,
            capture: FrontlineLabsCaptureArm.Channel,
            economy: FrontlineLabsEconomyArm.Scrap,
            roster: FrontlineLabsRosterArm.Legion,
            horizon: FrontlineLabsHorizonArm.Long,
            chassis: chassis,
            compositions: compositions);

    [Fact]
    public void EveryRegisteredTokenIsOneWordInsideTheProfileIdBudget()
    {
        Assert.Equal(
            FrontlineLabsComposition.All.Select(entry => entry.Token),
            GenericMindContractReservations.RegisteredCompositionTokens);
        Assert.Equal(
            FrontlineLabsComposition.All
                .Where(entry => entry.IsMixed)
                .Select(entry => entry.Token),
            GenericMindContractReservations
                .RegisteredMixedCompositionTokens);
        Assert.All(
            FrontlineLabsComposition.All,
            entry => Assert.True(
                entry.Token.Length
                <= GenericMindContractReservations
                    .MaxCompositionTokenLength));
    }

    [Fact]
    public void MonoCompositionsWriteNoPerSlotChassisAtAll()
    {
        ActorResolvedMatchDefinition declared = Cell(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            compositions: (
                FrontlineLabsComposition.Bulwark,
                FrontlineLabsComposition.Striker));
        ActorResolvedMatchDefinition undeclared = Cell(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker);

        // The load-bearing property: naming a mono composition and naming
        // nothing are the same contract, byte for byte.
        Assert.All(
            declared.Topology.UnitSlots,
            slot => Assert.Null(slot.ClassId));
        Assert.Equal(
            ActorMatchCanonicalWriter.SerializeTopology(
                undeclared.Topology,
                includeFingerprint: true),
            ActorMatchCanonicalWriter.SerializeTopology(
                declared.Topology,
                includeFingerprint: true));
        Assert.Equal(
            undeclared.Rules.RulesetId,
            declared.Rules.RulesetId);
    }

    [Fact]
    public void AMixedCompositionPublishesItsChassisPerSlot()
    {
        ActorResolvedMatchDefinition mixed = Cell(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Fabricator,
            compositions: (
                FrontlineLabsComposition.Warden,
                FrontlineLabsComposition.Spearhead));

        // The team's declared identity is the COMPOSITION token; the slot's
        // is its CHASSIS.
        Assert.Equal(
            ["warden", "spearhead"],
            mixed.Topology.Teams.Select(team => team.ClassId));
        Assert.Equal(
            "bulwark",
            mixed.Topology.UnitSlots
                .Single(slot => slot.TeamId == 0 && slot.UnitId == 0)
                .ClassId);
        // warden cycles fabricator, striker over its companion slots.
        Assert.Equal(
            "fabricator",
            mixed.Topology.UnitSlots
                .Single(slot => slot.TeamId == 0 && slot.UnitId == 1)
                .ClassId);
        Assert.Equal(
            "striker",
            mixed.Topology.UnitSlots
                .Single(slot => slot.TeamId == 0 && slot.UnitId == 2)
                .ClassId);
        // The registered pairing keys the TOPOLOGY profile, never the
        // ruleset: two compositions playing the same mechanics share a rules
        // fingerprint.
        Assert.Equal(
            FrontlineLabsDefinition
                .LegionSpearheadVersusWardenTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(mixed.Topology));
        Assert.DoesNotContain("warden", mixed.Rules.RulesetId);
        Assert.DoesNotContain("spearhead", mixed.Rules.RulesetId);
    }

    [Fact]
    public void AFabricatingCompositionBuildsItsOwnArmyRatherThanCopies()
    {
        ActorResolvedMatchDefinition mixed = Cell(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Fabricator,
            compositions: (
                FrontlineLabsComposition.Warden,
                FrontlineLabsComposition.Spearhead));

        // spearhead leads with a fabricator, so every slot is BUILT, and each
        // one is built as the chassis it declares.
        Assert.Equal(
            "striker-body",
            mixed.LifecycleAssignments
                .Single(entry => entry.TeamId == 1 && entry.UnitId == 1)
                .FabricationOutputFormId);
        Assert.Equal(
            "bulwark-body",
            mixed.LifecycleAssignments
                .Single(entry => entry.TeamId == 1 && entry.UnitId == 2)
                .FabricationOutputFormId);
        // warden fields a fabricator as a COMPANION, so its army is built
        // too — which is the whole question the token exists to ask.
        Assert.All(
            mixed.LifecycleAssignments.Where(entry => entry.TeamId == 0),
            entry => Assert.NotNull(entry.FabricationOutputFormId));
    }

    [Fact]
    public void ACompositionMustAgreeWithTheClassItDepartsFrom()
    {
        Assert.Throws<ArgumentException>(() =>
            Cell(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Fabricator,
                compositions: (
                    FrontlineLabsComposition.Spearhead,
                    FrontlineLabsComposition.Warden)));
    }

    [Fact]
    public void AMixedCompositionNeedsTheUnifiedChassisAndTheLegionRoster()
    {
        // On the split chassis a class's verbs live on its PRIME form, so a
        // chassis carried by a companion slot would arrive without them.
        Assert.Throws<ArgumentException>(() =>
            Cell(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Fabricator,
                chassis: FrontlineLabsChassisArm.Split,
                compositions: (
                    FrontlineLabsComposition.Warden,
                    FrontlineLabsComposition.Spearhead)));

        // The registered mixed profiles are legion shapes.
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Fabricator),
                chassis: FrontlineLabsChassisArm.Unified,
                compositions: (
                    FrontlineLabsComposition.Warden,
                    FrontlineLabsComposition.Spearhead)));
    }

    [Fact]
    public void ACompositionCellPitsADeclaredArmyAgainstADeclaredArmy()
    {
        Assert.Throws<ArgumentException>(() =>
            Cell(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Fabricator,
                compositions: (
                    FrontlineLabsComposition.Warden,
                    FrontlineLabsComposition.Fabricator)));
    }
}
