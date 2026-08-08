using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the aim arm (DECISIONS #173): the offset level restores ±1-sector
/// initial aim on every class's mobile gun and nothing else, straight is
/// byte-identical to the measured cells, specials stay untouched, and the
/// adopted-game composite `sail` (rig + aim) plus `sail-wane` stay inside
/// the canonical budget.
/// </summary>
public sealed class FrontlineLabsAimArmTests
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
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsAimArm aim,
        FrontlineLabsFiveSlotVariant fiveSlots =
            FrontlineLabsFiveSlotVariant.Full) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            Keel,
            (teamZero, teamOne),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: WholeKit,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: fiveSlots,
            aim: aim);

    private static ActorAttackProfileDefinition Attack(
        ActorResolvedMatchDefinition definition,
        string profileId) =>
        definition.Rules.AttackProfiles
            .Single(profile => profile.Id == profileId);

    [Fact]
    public void OffsetRestoresTheDiagonalLaunchOnEveryMobileGun()
    {
        ActorResolvedMatchDefinition offset = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsAimArm.Offset);
        ActorResolvedMatchDefinition straight = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsAimArm.Straight);

        foreach (FrontlineLabsClassDefinition entry in new[]
                 {
                     FrontlineLabsClassDefinition.Bulwark,
                     FrontlineLabsClassDefinition.Striker,
                 })
        {
            ActorShotProgramDefinition with = Attack(
                offset,
                entry.MobileAttackProfileId).ShotProgram;
            ActorShotProgramDefinition without = Attack(
                straight,
                entry.MobileAttackProfileId).ShotProgram;
            Assert.Equal(-1, with.MinInitialAimSteps);
            Assert.Equal(1, with.MaxInitialAimSteps);
            Assert.Equal(0, without.MinInitialAimSteps);
            Assert.Equal(0, without.MaxInitialAimSteps);
            // The one-bend rule survives the aim arm.
            Assert.Equal(1, with.MaxBendCount);
            Assert.Equal(without.MaxBendAfterTiles, with.MaxBendAfterTiles);
        }
    }

    [Fact]
    public void TheSpecialsStayUntouched()
    {
        ActorResolvedMatchDefinition offset = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsAimArm.Offset);
        // The volley aims by facing; its program stays disabled.
        Assert.False(Attack(
            offset,
            FrontlineLabsClassDefinition.Striker.StanceAttackProfileId)
            .ShotProgram.Enabled);
        // The turret aims absolutely; its program stays disabled.
        Assert.False(Attack(offset, "turret-bolt").ShotProgram.Enabled);
    }

    [Fact]
    public void StraightIsTheMeasuredCellByteForByte()
    {
        ActorResolvedMatchDefinition straight = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsAimArm.Straight);
        ActorResolvedMatchDefinition measured =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);

        Assert.Equal(measured.Rules.RulesetId, straight.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(measured),
            ActorContractFingerprint.ComputeMatch(straight));
    }

    [Fact]
    public void TheAdoptedGameCompositeStaysInsideTheBudget()
    {
        string sail = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsAimArm.Offset).Rules.RulesetId;
        Assert.Contains("-sail-", sail, StringComparison.Ordinal);
        Assert.DoesNotContain("-rig-", sail, StringComparison.Ordinal);
        Assert.True(sail.Length <= 64, $"{sail} is {sail.Length}");

        // The whole tuned game is one registered identity, `crew`, and it
        // must fit the worst cell — the fabricator mirror.
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in new[]
                 {
                     (FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsClassDefinition.Fabricator),
                     (FrontlineLabsClassDefinition.Fabricator,
                         FrontlineLabsClassDefinition.Fabricator),
                 })
        {
            string crew = Arm(
                zero,
                one,
                FrontlineLabsAimArm.Offset,
                FrontlineLabsFiveSlotVariant.Wane).Rules.RulesetId;
            Assert.Contains("-crew-", crew, StringComparison.Ordinal);
            Assert.DoesNotContain("-wane-", crew, StringComparison.Ordinal);
            Assert.True(crew.Length <= 64, $"{crew} is {crew.Length}");
        }
    }

    [Fact]
    public void AnAimArmRefusesACellWithoutClasses()
    {
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                aim: FrontlineLabsAimArm.Offset));
    }
}
