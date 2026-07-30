using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the stance-ground arm (DECISIONS #171 tuning, round 3): the free
/// level empties the forbidden tag kind on exactly the VOLLEY and AEGIS
/// SHELL entry routes, turret anchors keep the tag, the strict level is
/// byte-identical to the measured cells, identities stay inside the
/// canonical budget (wane + free registered as `berth`), and the refusal
/// names its constraint.
/// </summary>
public sealed class FrontlineLabsStanceGroundTests
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
        (FrontlineLabsClassDefinition, FrontlineLabsClassDefinition) pair,
        FrontlineLabsStanceGroundArm ground,
        FrontlineLabsFiveSlotVariant fiveSlots =
            FrontlineLabsFiveSlotVariant.Full) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            Keel,
            pair,
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: WholeKit,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: fiveSlots,
            stanceGround: ground);

    private static (FrontlineLabsClassDefinition, FrontlineLabsClassDefinition)
        BulwarkFabricator =>
        (FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Fabricator);

    private static ImmutableArray<
            ActorMapTileTagDefinition.TileTagKind>
        ForbiddenTags(ActorResolvedMatchDefinition definition, string routeId)
        =>
        definition.Rules.SameLifeTransitions
            .Single(route => route.TransitionId == routeId)
            .Placement.ForbiddenTileTags;

    [Fact]
    public void FreeEmptiesExactlyTheSkillStanceEntries()
    {
        ActorResolvedMatchDefinition free = Arm(
            BulwarkFabricator,
            FrontlineLabsStanceGroundArm.Free);

        Assert.Empty(ForbiddenTags(free, "shell-bulwark-prime"));
        Assert.Empty(ForbiddenTags(free, "shell-bulwark-child"));
        // Turret anchor routes keep the tag: fortress-on-point stays closed.
        Assert.Contains(
            ActorMapTileTagDefinition.TileTagKind.TransitionPlacementForbidden,
            ForbiddenTags(free, "anchor-bulwark-prime"));
        Assert.Contains(
            ActorMapTileTagDefinition.TileTagKind.TransitionPlacementForbidden,
            ForbiddenTags(free, "anchor-bulwark-child"));

        ActorResolvedMatchDefinition striker = Arm(
            (FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker),
            FrontlineLabsStanceGroundArm.Free);
        Assert.Empty(ForbiddenTags(striker, "volley-striker-prime"));
        Assert.Empty(ForbiddenTags(striker, "volley-striker-child"));
    }

    [Fact]
    public void StrictIsTheMeasuredCellByteForByte()
    {
        ActorResolvedMatchDefinition measured = Arm(
            BulwarkFabricator,
            FrontlineLabsStanceGroundArm.Strict);
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                BulwarkFabricator,
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);

        Assert.Equal(baseline.Rules.RulesetId, measured.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(baseline),
            ActorContractFingerprint.ComputeMatch(measured));
    }

    [Fact]
    public void TheIdentitiesStayInsideTheBudget()
    {
        string free = Arm(
            BulwarkFabricator,
            FrontlineLabsStanceGroundArm.Free).Rules.RulesetId;
        Assert.Contains("-free-", free, StringComparison.Ordinal);
        Assert.True(free.Length <= 64, $"{free} is {free.Length}");

        // wane + free is the registered composite `berth`.
        string berth = Arm(
            BulwarkFabricator,
            FrontlineLabsStanceGroundArm.Free,
            FrontlineLabsFiveSlotVariant.Wane).Rules.RulesetId;
        Assert.Contains("-berth-", berth, StringComparison.Ordinal);
        Assert.DoesNotContain("-wane-", berth, StringComparison.Ordinal);
        Assert.True(berth.Length <= 64, $"{berth} is {berth.Length}");

        string wane = Arm(
            BulwarkFabricator,
            FrontlineLabsStanceGroundArm.Strict,
            FrontlineLabsFiveSlotVariant.Wane).Rules.RulesetId;
        string[] all = [free, berth, wane];
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AStanceGroundArmRefusesACellWithoutAStance()
    {
        ArgumentException refusal = Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Fabricator),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                stanceGround: FrontlineLabsStanceGroundArm.Free));
        Assert.Contains("VOLLEY", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(
            "AEGIS SHELL",
            refusal.Message,
            StringComparison.Ordinal);
    }
}
