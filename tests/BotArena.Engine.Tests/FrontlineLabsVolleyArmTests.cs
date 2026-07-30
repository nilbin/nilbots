using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the salvo (#182): damage-2 fan on its own slot-scoped entry
/// cooldown (the first consumer of #181), the shared gun counter dropped
/// to the floor, inert-omitted without a striker, cast cells
/// byte-identical, and the surf identities inside the budget.
/// </summary>
public sealed class FrontlineLabsVolleyArmTests
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
        FrontlineLabsVolleyArm volley)
    {
        bool fabricator =
            teamZero.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
            || teamOne.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots;
        return FrontlineLabsDefinition.CreatePendulumExperiment(
            Keel,
            (teamZero, teamOne),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: WholeKit,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: fabricator
                ? FrontlineLabsFiveSlotVariant.Wane
                : FrontlineLabsFiveSlotVariant.Full,
            stanceGround: FrontlineLabsStanceGroundArm.Open,
            aim: FrontlineLabsAimArm.Offset,
            cooldown: FrontlineLabsCooldownArm.Ticking,
            volley: volley);
    }

    [Fact]
    public void SalvoRearmsTheFanAndPricesItOnTheRoute()
    {
        ActorResolvedMatchDefinition salvo = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsVolleyArm.Salvo);
        ActorAttackProfileDefinition fan = salvo.Rules.AttackProfiles
            .Single(profile => profile.Id
                == FrontlineLabsClassDefinition.Striker
                    .StanceAttackProfileId);
        ActorAttackProfileDefinition mobile = salvo.Rules.AttackProfiles
            .Single(profile => profile.Id
                == FrontlineLabsClassDefinition.Striker
                    .MobileAttackProfileId);

        // Every bolt deals 2; kinematics otherwise stay the class bolt.
        Assert.Equal(2, fan.Projectile.DamagePerHit);
        Assert.Equal(1, mobile.Projectile.DamagePerHit);
        Assert.Equal(
            mobile.Projectile.MaxTravelTiles,
            fan.Projectile.MaxTravelTiles);
        Assert.Equal(
            mobile.Projectile.TilesPerAdvance,
            fan.Projectile.TilesPerAdvance);
        // The fan stops taxing the shared counter (floor cadence)...
        Assert.Equal(1, fan.CooldownTicks);
        // ...and its frequency moves to the entry route (#181).
        foreach (string route in new[]
                 {
                     "volley-striker-prime",
                     "volley-striker-child",
                 })
        {
            Assert.Equal(
                8,
                salvo.Rules.SameLifeTransitions
                    .Single(item => item.TransitionId == route)
                    .CooldownTicks);
        }
        // The shell's entries stay cooldown-free.
        Assert.Equal(
            0,
            salvo.Rules.SameLifeTransitions
                .Single(item => item.TransitionId == "shell-bulwark-prime")
                .CooldownTicks);
    }

    [Fact]
    public void CastIsTheMeasuredCellByteForByte()
    {
        ActorResolvedMatchDefinition cast = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsVolleyArm.Cast);
        ActorResolvedMatchDefinition measured =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                stanceGround: FrontlineLabsStanceGroundArm.Open,
                aim: FrontlineLabsAimArm.Offset,
                cooldown: FrontlineLabsCooldownArm.Ticking);
        Assert.Equal(measured.Rules.RulesetId, cast.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(measured),
            ActorContractFingerprint.ComputeMatch(cast));
    }

    [Fact]
    public void TheSurfIdentitiesFitAndSalvoInertOmitsWithoutAStriker()
    {
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in new[]
                 {
                     (FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsClassDefinition.Striker),
                     (FrontlineLabsClassDefinition.Fabricator,
                         FrontlineLabsClassDefinition.Striker),
                     (FrontlineLabsClassDefinition.Striker,
                         FrontlineLabsClassDefinition.Striker),
                 })
        {
            string id = Arm(zero, one, FrontlineLabsVolleyArm.Salvo)
                .Rules.RulesetId;
            Assert.Contains("-surf-", id, StringComparison.Ordinal);
            Assert.True(id.Length <= 64, $"{id} is {id.Length}");
        }
        // No striker: salvo is inert-omitted and the mirror stays tide.
        ActorResolvedMatchDefinition mirror = Arm(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsVolleyArm.Salvo);
        Assert.Contains(
            "-tide-",
            mirror.Rules.RulesetId,
            StringComparison.Ordinal);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(
                Arm(
                    FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsVolleyArm.Cast)),
            ActorContractFingerprint.ComputeMatch(mirror));
    }
}
