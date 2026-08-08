using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the cooldown-clock arm (DECISIONS #180): ticking advances the
/// cooldown through unarmed forms in real play, frozen cells stay
/// byte-identical to every measured contract, and the tide identity fits
/// the worst cell.
/// </summary>
public sealed class FrontlineLabsCooldownArmTests
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
        FrontlineLabsCooldownArm cooldown,
        (FrontlineLabsClassDefinition, FrontlineLabsClassDefinition)? pair =
            null) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            Keel,
            pair ?? (FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: WholeKit,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: pair is { } p
                && (p.Item1.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
                    || p.Item2.Skill
                        == FrontlineLabsSkillKit.FabricatorFiveSlots)
                ? FrontlineLabsFiveSlotVariant.Wane
                : FrontlineLabsFiveSlotVariant.Full,
            stanceGround: FrontlineLabsStanceGroundArm.Open,
            aim: FrontlineLabsAimArm.Offset,
            cooldown: cooldown);

    [Fact]
    public void TheClockRunsInsideAShellUnderTicking()
    {
        // Team 0's prime fires once where it stands (cooldown 3) and
        // shells IN PLACE on the very next legal tick — under the open
        // ground a stance is legal anywhere, so the probe needs no
        // walking (the naive walk helper is facing-blind and faults
        // under facing-locked). Under the frozen clock the cooldown it
        // carried into the shell survives; under ticking it reaches
        // zero inside.
        string shell = FrontlineLabsClassDefinition.Bulwark
            .PrimeStanceFormId;

        (int MinInside, int TicksInside) Probe(
            FrontlineLabsCooldownArm cooldown)
        {
            bool fired = false;
            GenericActorMatchChronology chronology =
                FrontlineLabsSkillArmTestFixture.Run(
                    Arm(cooldown),
                    (_, observation) =>
                    {
                        if (observation.Self.ActorId.TeamId != 0
                            || observation.Self.ActorId.UnitId != 0)
                        {
                            return GenericDeathmatchSessionTestFixture
                                .Wait();
                        }
                        if (observation.Self.FormId == shell)
                        {
                            return GenericDeathmatchSessionTestFixture
                                .Wait();
                        }
                        // Under the universal bend the bulwark's gun is the
                        // programmed `shoot` (payload optional = the default
                        // straight program), not `shoot-straight`.
                        if (!fired
                            && FrontlineLabsSkillArmTestFixture.Allows(
                                observation,
                                "shoot"))
                        {
                            fired = true;
                            return new GenericActorRuntimeDecision(
                                "shoot", 4, [], null);
                        }
                        return fired
                            && FrontlineLabsSkillArmTestFixture.Allows(
                                observation,
                                "transform")
                            ? GenericDeathmatchSessionTestFixture
                                .Transform(shell)
                            : GenericDeathmatchSessionTestFixture.Wait();
                    });

            int minInside = int.MaxValue;
            int ticksInside = 0;
            var trace = new List<string>();
            foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
            {
                GenericActorWorldSnapshot.LifeSnapshot? life = frame
                    .PostState.ActiveLives
                    .SingleOrDefault(item =>
                        item.ActorId.TeamId == 0
                        && item.ActorId.UnitId == 0);
                if (life is not null && trace.Count < 30)
                {
                    trace.Add(
                        $"t{frame.TickStart.Tick}:{life.FormId}@"
                        + $"({life.Position.X},{life.Position.Y})cd"
                        + $"{life.Cooldown}");
                }
                if (life is null || life.FormId != shell)
                    continue;
                ticksInside++;
                minInside = Math.Min(minInside, life.Cooldown);
            }
            if (ticksInside == 0)
            {
                string first = chronology.Ticks.Length == 0
                    ? "NO TICKS"
                    : string.Join(",", chronology.Ticks[0].PostState
                        .ActiveLives.Select(item =>
                            $"{item.ActorId.TeamId}/{item.ActorId.UnitId}"
                            + $":{item.FormId}"));
                throw new InvalidOperationException(
                    $"ticks={chronology.Ticks.Length} lives0=[{first}] "
                    + "trace: " + string.Join(" ", trace));
            }
            return (minInside, ticksInside);
        }

        (int frozenMin, int frozenTicks) = Probe(
            FrontlineLabsCooldownArm.Frozen);
        (int tickingMin, int tickingTicks) = Probe(
            FrontlineLabsCooldownArm.Ticking);

        Assert.True(
            frozenTicks > 10 && tickingTicks > 10,
            $"the probe never stood in the shell long enough to measure "
            + $"(frozen {frozenTicks} ticks, ticking {tickingTicks})");
        // The frozen clock keeps the carried cooldown forever; the ticking
        // clock runs it to zero inside the same shell.
        Assert.True(
            frozenMin > 0,
            $"frozen clock leaked: min cooldown in shell was {frozenMin}");
        Assert.True(
            tickingMin == 0,
            $"ticking clock froze at {tickingMin}; arm kind = "
            + Arm(FrontlineLabsCooldownArm.Ticking)
                .Rules.TickResolution.CooldownClock);
    }

    [Fact]
    public void FrozenIsTheMeasuredCellByteForByte()
    {
        ActorResolvedMatchDefinition frozen = Arm(
            FrontlineLabsCooldownArm.Frozen);
        ActorResolvedMatchDefinition measured =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Bulwark),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                stanceGround: FrontlineLabsStanceGroundArm.Open,
                aim: FrontlineLabsAimArm.Offset);

        Assert.Equal(measured.Rules.RulesetId, frozen.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(measured),
            ActorContractFingerprint.ComputeMatch(frozen));
    }

    [Fact]
    public void TheTickingIdentitiesFitTheBudget()
    {
        string bvb = Arm(FrontlineLabsCooldownArm.Ticking).Rules.RulesetId;
        Assert.Contains("-tick-", bvb, StringComparison.Ordinal);
        Assert.True(bvb.Length <= 64, $"{bvb} is {bvb.Length}");

        string tide = Arm(
            FrontlineLabsCooldownArm.Ticking,
            (FrontlineLabsClassDefinition.Fabricator,
                FrontlineLabsClassDefinition.Fabricator)).Rules.RulesetId;
        Assert.Contains("-tide-", tide, StringComparison.Ordinal);
        Assert.DoesNotContain("-deck-", tide, StringComparison.Ordinal);
        Assert.True(tide.Length <= 64, $"{tide} is {tide.Length}");

        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(
                Arm(FrontlineLabsCooldownArm.Ticking)),
            ActorContractFingerprint.ComputeMatch(
                Arm(FrontlineLabsCooldownArm.Frozen)));
    }
}
