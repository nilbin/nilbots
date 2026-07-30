using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the open game's turret cycle IN PLAY (DECISIONS #176): a real
/// session accepts anchor → mobilize → anchor on one life, the chronology
/// validator accepts the produced history, and the ratio-floored health
/// arithmetic holds at full health across the cycle. Contract-level facts
/// are pinned in FrontlineLabsStanceGroundTests; this is the live proof
/// no wave-4 bot could give (their doctrine never re-anchors).
/// </summary>
public sealed class FrontlineLabsTurretCycleTests
{
    private const FrontlineLabsPendulumArm Keel =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ForwardRally
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    [Fact]
    public void ASingleLifeCyclesTheTurretTwiceAndHealthMapsLosslesslyAtFull()
    {
        string turret = FrontlineLabsClassDefinition.Bulwark
            .PrimeTurretFormId;
        string mobile = FrontlineLabsClassDefinition.Bulwark.PrimeFormId;
        ActorResolvedMatchDefinition arm =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Bulwark),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: FrontlineLabsSkillKit.BulwarkAegisShell,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                stanceGround: FrontlineLabsStanceGroundArm.Open);

        // Team 0's prime alternates transform-to-turret and mobilize-back
        // forever; everyone else waits. Under the historical rule the
        // second anchor would be refused; under open it must complete.
        GenericActorMatchChronology chronology =
            FrontlineLabsSkillArmTestFixture.Run(
                arm,
                (_, observation) =>
                {
                    if (observation.Self.ActorId.TeamId != 0
                        || observation.Self.ActorId.UnitId != 0)
                    {
                        return GenericDeathmatchSessionTestFixture.Wait();
                    }
                    if (observation.Self.FormId == turret)
                    {
                        return FrontlineLabsSkillArmTestFixture.Allows(
                            observation,
                            "mobilize")
                            ? FrontlineLabsSkillArmTestFixture.Mobilize()
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(
                            turret)
                        : GenericDeathmatchSessionTestFixture.Wait();
                });

        ImmutableArray<
                GenericActorRuntimeObservation.EventPayload.FormTransition>
            completions =
            [
                .. chronology.Ticks
                    .SelectMany(frame =>
                        FrontlineLabsSkillArmTestFixture.Transitions(
                            frame,
                            GenericActorRuntimeObservation.EventKind
                                .FormTransitionCompleted))
                    .Where(item => item.ActorId.TeamId == 0
                        && item.ActorId.UnitId == 0),
            ];
        int anchors = completions.Count(item => item.ToFormId == turret);
        int mobilizes = completions.Count(item => item.ToFormId == mobile);
        // The cycle really cycled: at least two full round trips on the
        // same life (no combat in this probe, so the life never changes).
        Assert.True(
            anchors >= 2 && mobilizes >= 2,
            $"expected a repeating cycle, saw {anchors} anchors and "
            + $"{mobilizes} mobilizes");

        // Full health maps losslessly in both directions: the bulwark
        // prime (5) stands in its 7-max turret at exactly 7 and returns
        // to exactly 5, tick after tick, cycle after cycle. Nobody shoots
        // in this probe, so any deviation is the health policy leaking —
        // a heal exploit would read 7 as mobile, a preserve-capped bug
        // would read 5 as turret after the first return.
        int turretTicks = 0;
        int mobileTicks = 0;
        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            GenericActorWorldSnapshot.LifeSnapshot? life = frame.PostState
                .ActiveLives
                .SingleOrDefault(item =>
                    item.ActorId.TeamId == 0 && item.ActorId.UnitId == 0);
            if (life is null)
                continue;
            if (life.FormId == turret)
            {
                turretTicks++;
                Assert.Equal(7, life.Health);
            }
            else if (life.FormId == mobile)
            {
                mobileTicks++;
                Assert.Equal(5, life.Health);
            }
        }
        Assert.True(
            turretTicks > 0 && mobileTicks > 0,
            "the probe never stood in both forms");
    }
}
