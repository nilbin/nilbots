using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the registered FIVE SLOTS tuning variants (DECISIONS #171): one
/// lever per variant, identity suffixes inside the canonical budget, the
/// Full arm byte-identical to the phase-2 measured cell, and the refusal
/// that keeps a variant out of cells that do not carry the skill.
/// </summary>
public sealed class FrontlineLabsFiveSlotVariantTests
{
    private const FrontlineLabsPendulumArm Keel =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ForwardRally
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    private const FrontlineLabsSkillKit WholeKit =
        FrontlineLabsSkillKit.StrikerVolley
        | FrontlineLabsSkillKit.BulwarkAegisShell
        | FrontlineLabsSkillKit.FabricatorFiveSlots;

    private static ActorResolvedMatchDefinition Arm(
        FrontlineLabsFiveSlotVariant variant) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            Keel,
            (FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Fabricator),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: WholeKit,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: variant);

    private static int[] FabricatorUnlocks(
        ActorResolvedMatchDefinition definition) =>
    [
        .. definition.LifecycleAssignments
            .Where(slot => slot.TeamId == 1 && slot.UnlockTick is not null)
            .Select(slot => slot.UnlockTick!.Value)
            .OrderBy(tick => tick),
    ];

    private static int RebuildDelay(
        ActorResolvedMatchDefinition definition,
        string profileId) =>
        definition.Rules.Lifecycle.Profiles
            .Single(profile => profile.ProfileId == profileId)
            .DelayTicks;

    [Fact]
    public void TheFullArmIsThePhaseTwoMeasuredCellByteForByte()
    {
        ActorResolvedMatchDefinition measured =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Fabricator),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);
        ActorResolvedMatchDefinition full = Arm(
            FrontlineLabsFiveSlotVariant.Full);

        Assert.Equal(measured.Rules.RulesetId, full.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(measured.Rules),
            ActorContractFingerprint.ComputeRules(full.Rules));
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(measured),
            ActorContractFingerprint.ComputeMatch(full));
    }

    [Fact]
    public void EveryVariantMintsItsOwnSuffixedIdentityInsideTheBudget()
    {
        string full = Arm(FrontlineLabsFiveSlotVariant.Full).Rules.RulesetId;
        foreach ((FrontlineLabsFiveSlotVariant variant, string token) in new[]
                 {
                     (FrontlineLabsFiveSlotVariant.Trim, "trim"),
                     (FrontlineLabsFiveSlotVariant.Boom, "boom"),
                     (FrontlineLabsFiveSlotVariant.Drag, "drag"),
                     (FrontlineLabsFiveSlotVariant.Moor, "moor"),
                     (FrontlineLabsFiveSlotVariant.Wane, "wane"),
                 })
        {
            string id = Arm(variant).Rules.RulesetId;
            Assert.NotEqual(full, id);
            Assert.Contains($"-{token}-", id, StringComparison.Ordinal);
            Assert.True(id.Length <= 64, $"{id} is {id.Length} characters");
        }
    }

    [Fact]
    public void EveryVariantChangesTheMatchFingerprint()
    {
        string[] fingerprints =
        [
            .. new[]
                {
                    FrontlineLabsFiveSlotVariant.Full,
                    FrontlineLabsFiveSlotVariant.Trim,
                    FrontlineLabsFiveSlotVariant.Boom,
                    FrontlineLabsFiveSlotVariant.Drag,
                    FrontlineLabsFiveSlotVariant.Moor,
                    FrontlineLabsFiveSlotVariant.Wane,
                }
                .Select(variant =>
                    ActorContractFingerprint.ComputeMatch(Arm(variant))),
        ];
        Assert.Equal(
            fingerprints.Length,
            fingerprints.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TrimDropsExactlyTheFifthSlot()
    {
        ActorResolvedMatchDefinition full = Arm(
            FrontlineLabsFiveSlotVariant.Full);
        ActorResolvedMatchDefinition trim = Arm(
            FrontlineLabsFiveSlotVariant.Trim);

        // Team 1 is the fabricator in this canonical pair.
        Assert.Equal([60, 180, 300, 420], FabricatorUnlocks(full));
        Assert.Equal([60, 180, 300], FabricatorUnlocks(trim));
        // The bulwark side is untouched by every variant.
        int[] BulwarkUnlocks(ActorResolvedMatchDefinition definition) =>
        [
            .. definition.LifecycleAssignments
                .Where(slot => slot.TeamId == 0
                    && slot.UnlockTick is not null)
                .Select(slot => slot.UnlockTick!.Value)
                .OrderBy(tick => tick),
        ];
        Assert.Equal(BulwarkUnlocks(full), BulwarkUnlocks(trim));
    }

    [Fact]
    public void BoomSwingsTheExtraScheduleLateOnTheSameCadence()
    {
        Assert.Equal(
            [60, 180, 360, 480],
            FabricatorUnlocks(Arm(FrontlineLabsFiveSlotVariant.Boom)));
    }

    [Fact]
    public void DragPutsTheOrdinaryChildrenOnTheBaselineRebuildClock()
    {
        string childProfile = FrontlineLabsClassDefinition.Fabricator
            .ChildLifecycleProfileId;
        string extraProfile = FrontlineLabsClassDefinition.Fabricator
            .ExtraChildLifecycleProfileId;
        ActorResolvedMatchDefinition full = Arm(
            FrontlineLabsFiveSlotVariant.Full);
        ActorResolvedMatchDefinition drag = Arm(
            FrontlineLabsFiveSlotVariant.Drag);

        Assert.Equal(15, RebuildDelay(full, childProfile));
        Assert.Equal(30, RebuildDelay(drag, childProfile));
        // The extra slots already rebuild on the baseline clock; drag
        // changes nothing about them, and boom/trim change no rebuild
        // clock at all.
        Assert.Equal(30, RebuildDelay(full, extraProfile));
        Assert.Equal(30, RebuildDelay(drag, extraProfile));
        Assert.Equal(
            15,
            RebuildDelay(
                Arm(FrontlineLabsFiveSlotVariant.Boom),
                childProfile));
        Assert.Equal(
            15,
            RebuildDelay(
                Arm(FrontlineLabsFiveSlotVariant.Trim),
                childProfile));
        // Drag leaves the schedule untouched.
        Assert.Equal(FabricatorUnlocks(full), FabricatorUnlocks(drag));
    }

    [Fact]
    public void TheCompositesCarryBothMeasuredLevers()
    {
        ActorResolvedMatchDefinition moor = Arm(
            FrontlineLabsFiveSlotVariant.Moor);
        ActorResolvedMatchDefinition wane = Arm(
            FrontlineLabsFiveSlotVariant.Wane);
        string childProfile = FrontlineLabsClassDefinition.Fabricator
            .ChildLifecycleProfileId;

        Assert.Equal([60, 180, 300], FabricatorUnlocks(moor));
        Assert.Equal([60, 180, 300], FabricatorUnlocks(wane));
        Assert.Equal(30, RebuildDelay(moor, childProfile));
        Assert.Equal(22, RebuildDelay(wane, childProfile));
        // Four slots means the trim topology profiles, not new ones.
        Assert.Equal(
            FrontlineLabsDefinition.TrimAsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(moor.Topology));
        Assert.Equal(
            FrontlineLabsDefinition.TrimAsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(wane.Topology));
    }

    [Fact]
    public void TrimMintsItsOwnTopologyProfiles()
    {
        Assert.Equal(
            FrontlineLabsDefinition.TrimAsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                Arm(FrontlineLabsFiveSlotVariant.Trim).Topology));
        Assert.Equal(
            FrontlineLabsDefinition.TrimMirrorTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                FrontlineLabsDefinition.CreatePendulumExperiment(
                    Keel,
                    (FrontlineLabsClassDefinition.Fabricator,
                        FrontlineLabsClassDefinition.Fabricator),
                    movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                    skills: WholeKit,
                    bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                    fiveSlots: FrontlineLabsFiveSlotVariant.Trim).Topology));
        // The other variants keep the five-slot shapes.
        Assert.Equal(
            FrontlineLabsDefinition.AsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                Arm(FrontlineLabsFiveSlotVariant.Boom).Topology));
        Assert.Equal(
            FrontlineLabsDefinition.AsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                Arm(FrontlineLabsFiveSlotVariant.Drag).Topology));
    }

    [Fact]
    public void AVariantRefusesACellWithoutTheSkill()
    {
        ArgumentException refusal = Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                fiveSlots: FrontlineLabsFiveSlotVariant.Trim));
        // The refusal names its constraint (the tooling-trap lesson).
        Assert.Contains(
            "FIVE SLOTS",
            refusal.Message,
            StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Fabricator),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                fiveSlots: FrontlineLabsFiveSlotVariant.Drag));
    }
}
