using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The capture channel driven live through whole scripted matches on the real
/// arm. The kernel tests pin the arithmetic; these pin that the SESSION
/// derives the right facts from the world — which bodies held their tile, and
/// which of the tick's damage landed on the objective — and that every history
/// they produce survives the chronology validator, which re-derives both from
/// the recorded document rather than trusting it.
/// <para>The A/B is the memo's headline claim: one script, run with and
/// without a body standing on the firing line OFF the objective. With the
/// screen the channel completes in 8; without it the same bolts take the work
/// straight back off.</para>
/// </summary>
public sealed class FrontlineLabsCaptureChannelSessionTests
{
    private const int Channeler = 0;
    private const int Poker = 1;

    /// <summary>The centre objective, which is where every probe happens.</summary>
    private static readonly ImmutableHashSet<Position> CentreObjective =
    [
        new(10, 7), new(11, 7), new(12, 7),
        new(10, 8), new(11, 8), new(12, 8),
    ];

    /// <summary>The channeling tile, on the objective and on the open row.</summary>
    private static readonly Position ChannelTile = new(11, 7);

    /// <summary>
    /// The screening tile: on the poker's firing line, one tile east of the
    /// objective's eastern edge, so a bolt aimed down row 7 stops here.
    /// </summary>
    private static readonly Position ScreenTile = new(13, 7);

    /// <summary>Where the poker parks: row 7, inside its gun's travel of 8.</summary>
    private static readonly Position PokeTile = new(16, 7);

    /// <summary>
    /// The column the channeling child drops onto the open row from. Its
    /// pad's own exit east is the PRIME's authored spawn, which is
    /// permanently reserved against its own children, so it walks the
    /// northern shoulder to here first.
    /// </summary>
    private const int ShoulderX = 4;

    /// <summary>
    /// The tick the poker opens fire. It is after the channeling child's
    /// unlock (120) and its walk, so the whole exchange happens with every
    /// body already in place and nothing else moving.
    /// </summary>
    private const int FireFromTick = 130;

    [Fact]
    public void AScreenedStationaryChannelerCompletesInEightTicks()
    {
        TickRecord[] run = Read(Run(screen: true));
        TickRecord[] window = ChannelWindow(run);

        // Eight ticks, one point each, and the eighth takes the objective.
        Assert.Equal(8, window.Length);
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(Channeler, window[index].ClaimingTeamId);
            Assert.Equal(index + 1, window[index].CaptureProgress);
        }
        Assert.Equal(2, window[0].ActivePositionIndex);
        Assert.Equal(3, window[^1].ActivePositionIndex);
        Assert.Equal(0, window[^1].CaptureProgress);

        // And the screen really was eating bolts through that window: the
        // damage landed on a channeling-team body OFF the objective, and cost
        // the channel nothing.
        Assert.Contains(
            window,
            record => record.Damage.Any(hit =>
                hit.TeamId == Channeler
                && !CentreObjective.Contains(hit.Position)));
        Assert.DoesNotContain(
            window,
            record => record.Damage.Any(hit =>
                hit.TeamId == Channeler
                && CentreObjective.Contains(hit.Position)));
    }

    /// <summary>
    /// The same script with the screen left at home. Every bolt that reaches
    /// the channeler reverts exactly its damage from the run's work, so the
    /// tick nets zero, and the capture the screened run completed in eight
    /// ticks does not happen at all.
    /// </summary>
    [Fact]
    public void WithoutTheScreenTheSameBoltsRevertTheChannelersWork()
    {
        TickRecord[] run = Read(Run(screen: false));
        TickRecord[] window = ChannelWindow(run);

        Assert.Equal(8, window.Length);
        int hits = 0;
        for (int index = 0; index < 8; index++)
        {
            TickRecord record = window[index];
            long onObjective = record.Damage
                .Where(hit =>
                    hit.TeamId == Channeler
                    && CentreObjective.Contains(hit.Position))
                .Sum(hit => (long)hit.Amount);
            int before = index == 0 ? 0 : window[index - 1].CaptureProgress;
            // Gain lands first, the revert second, and the revert is floored
            // at the run's start — which is zero here, because the run began
            // on a neutral objective.
            Assert.Equal(
                (int)Math.Max(0, before + 1 - onObjective),
                record.CaptureProgress);
            if (onObjective > 0)
                hits++;
        }

        // The screened run took the objective on exactly this tick; the same
        // eight ticks under fire leave the front where it was, one point of
        // work short for every point of health the poker removed.
        Assert.True(hits >= 3, $"expected sustained poking, saw {hits} hits");
        Assert.Equal(2, window[7].ActivePositionIndex);
        Assert.Equal(8 - hits, window[7].CaptureProgress);
        Assert.Equal(Channeler, window[7].ClaimingTeamId);
    }

    /// <summary>
    /// Stillness is what pays. A body that oscillates between two tiles of
    /// the SAME objective region is on the point the whole time — it would
    /// deny an enemy — but it never adds a single point to a claim.
    /// </summary>
    [Fact]
    public void AChannelerThatChangesTileContributesNothingThatTick()
    {
        TickRecord[] run = Read(
            Run(screen: false, oscillate: true, poke: false));
        TickRecord[] onPoint = run
            .Where(record => record.Tick >= 20)
            .Where(record => record.ChannelerBodiesOnObjective > 0)
            .ToArray();

        Assert.True(
            onPoint.Length >= 40,
            $"the probe never held the point, saw {onPoint.Length} ticks");
        Assert.All(onPoint, record =>
        {
            Assert.Null(record.ClaimingTeamId);
            Assert.Equal(0, record.CaptureProgress);
        });
    }

    /// <summary>
    /// The impossible channel histories, refused. Each forgery below is a
    /// self-consistent recorded boundary; the only thing wrong with it is
    /// that the bodies and damage the SAME document recorded could not have
    /// produced it. The validator re-derives stillness from the previous
    /// boundary's positions and the interrupt from the recorded Damage facts,
    /// so none of the three can reconcile.
    /// </summary>
    [Fact]
    public void TheValidatorRejectsImpossibleChannelHistories()
    {
        GenericActorMatchChannelForgery screened = Forgery(Run(screen: true));
        GenericActorMatchChannelForgery still =
            Forgery(Run(screen: false, oscillate: true, poke: false));

        // Gain past what the reading supports: one stationary body against an
        // empty objective buys exactly one point, capped or not.
        Reject(screened, progress => progress + 1);
        // A revert with no damage fact behind it.
        Reject(screened, progress => Math.Max(1, progress - 1));
        // And gain credited to a body the document also recorded moving.
        Reject(still, _ => 1);
    }

    private static void Reject(
        GenericActorMatchChannelForgery source,
        Func<int, int> forgeProgress)
    {
        GenericActorMatchTickFrame frame = source.Frame;
        var mode = (GenericActorRuntimeObservation.ModeObservationState
            .Frontline)frame.PostState.Mode;
        var forged = new GenericActorRuntimeObservation.ModeObservationState
            .Frontline(
                mode.ModeId,
                mode.ActivePositionIndex,
                mode.ClaimingTeamId ?? Channeler,
                forgeProgress(mode.CaptureProgress),
                mode.DecayTicksElapsed,
                mode.ControlResumesAtTick,
                mode.HoldOwnerTeamId,
                mode.HoldEndsAtTick,
                mode.SecondaryOwnerTeamId,
                mode.SecondaryClaimProgress);
        var forgedFrame = new GenericActorMatchTickFrame(
            frame.TickStart,
            frame.ActorTurns,
            frame.Events,
            frame.Traversals,
            new GenericActorWorldSnapshot(
                source.Definition,
                frame.PostState.NextTick,
                frame.PostState.NextProjectileId,
                frame.PostState.Participants,
                frame.PostState.Slots,
                frame.PostState.ActiveLives,
                frame.PostState.PendingReplications,
                frame.PostState.Projectiles,
                frame.PostState.Scoreboard,
                forged));

        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            GenericFrontlineChronologyEvidence.Validate(
                source.Definition,
                source.Chronology.InitialFrame,
                [
                    .. source.Chronology.Ticks.Select(item =>
                        item.Tick == frame.Tick ? forgedFrame : item),
                ],
                null));
        Assert.Contains(
            "Frontline",
            failure.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The chronology plus the one channeling tick every forgery rewrites.
    /// </summary>
    private static GenericActorMatchChannelForgery Forgery(
        GenericActorMatchChronology chronology)
    {
        int target = FireFromTick + 3;
        return new GenericActorMatchChannelForgery(
            chronology,
            (ActorResolvedMatchDefinition)chronology.Descriptor.Definition,
            chronology.Ticks.Single(frame => frame.Tick == target));
    }

    private sealed record GenericActorMatchChannelForgery(
        GenericActorMatchChronology Chronology,
        ActorResolvedMatchDefinition Definition,
        GenericActorMatchTickFrame Frame);

    /// <summary>
    /// The eight ticks starting from the first one on which the channeling
    /// team gains anything at all. Located rather than hard-coded, so the
    /// probe stays honest if a walk gets a tick longer.
    /// </summary>
    private static TickRecord[] ChannelWindow(TickRecord[] run)
    {
        int start = Array.FindIndex(
            run,
            record => record.ClaimingTeamId == Channeler
                && record.CaptureProgress > 0);
        Assert.True(start >= 0, "the probe never started a claim");
        return run
            .Skip(start)
            .TakeWhile(record =>
                record.ActivePositionIndex == 2
                || record.CaptureProgress == 0)
            .Take(8)
            .ToArray();
    }

    private static TickRecord[] Read(GenericActorMatchChronology chronology)
    {
        var records = new List<TickRecord>();
        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            var control = (GenericActorRuntimeObservation.ModeObservationState
                .Frontline)frame.PostState.Mode;
            records.Add(new TickRecord(
                frame.Tick,
                control.ClaimingTeamId,
                control.CaptureProgress,
                control.ActivePositionIndex,
                frame.PostState.ActiveLives.Count(life =>
                    life.ActorId.TeamId == Channeler
                    && CentreObjective.Contains(life.Position)),
                [
                    .. FrontlineLabsSkillArmTestFixture.Damages(frame)
                        .Select(payload => new DamageRecord(
                            payload.TargetActorId.TeamId,
                            payload.Position,
                            payload.Amount)),
                ]));
        }
        return [.. records];
    }

    /// <summary>
    /// The scripted match. Team 0 is a bulwark (a 5-HP prime screen and a
    /// 4-HP child channeler); team 1 is a striker, which parks inside its
    /// gun's travel and fires down the open centre row from
    /// <see cref="FireFromTick"/>. The only thing that varies between runs is
    /// whether the prime walks onto the firing line or stays home.
    /// </summary>
    private static GenericActorMatchChronology Run(
        bool screen,
        bool oscillate = false,
        bool poke = true)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.EnemySoleDecay,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                capture: FrontlineLabsCaptureArm.Channel);
        return FrontlineLabsSkillArmTestFixture.Run(
            definition,
            (start, observation) => start.ActorId.TeamId == Poker
                ? Poke(observation, poke)
                : Hold(observation, screen, oscillate));
    }

    /// <summary>
    /// The striker: walk to the firing tile, then hold and fire straight west
    /// down row 7 from the agreed tick. Its cooldown does the spacing.
    /// </summary>
    private static GenericActorRuntimeDecision Poke(
        GenericActorRuntimeObservation observation,
        bool poke)
    {
        if (observation.Self.ActorId.UnitId != 0)
            return GenericDeathmatchSessionTestFixture.Wait();
        if (FrontlineLabsSkillArmTestFixture.WalkTo(observation, PokeTile)
            is { } step)
        {
            return step;
        }
        return poke
            && observation.Tick >= FireFromTick
            && FrontlineLabsSkillArmTestFixture.Allows(observation, "shoot")
                ? GenericDeathmatchSessionTestFixture.Shoot()
                : GenericDeathmatchSessionTestFixture.Wait();
    }

    /// <summary>
    /// The bulwark: unit 0 is the screen (or stays home), unit 1 is the
    /// channeler. Later lives hold wherever they arrive, so a death does not
    /// silently re-run the script.
    /// <para>The channeler takes the northern shoulder out of its pad because
    /// the prime's authored spawn is permanently reserved against its own
    /// children — walking straight down the row is not a legal route.</para>
    /// </summary>
    private static GenericActorRuntimeDecision Hold(
        GenericActorRuntimeObservation observation,
        bool screen,
        bool oscillate)
    {
        if (observation.Self.ActorId.LifeId != 0)
            return GenericDeathmatchSessionTestFixture.Wait();
        if (observation.Self.ActorId.UnitId == 0)
        {
            return screen
                && FrontlineLabsSkillArmTestFixture.WalkTo(
                    observation,
                    ScreenTile) is { } step
                ? step
                : GenericDeathmatchSessionTestFixture.Wait();
        }
        if (observation.Self.ActorId.UnitId != 1)
            return GenericDeathmatchSessionTestFixture.Wait();
        Position self = observation.Self.Position;
        if (self.X < ShoulderX)
            return GenericDeathmatchSessionTestFixture.Move(Direction.East);
        if (self.Y < ChannelTile.Y)
            return GenericDeathmatchSessionTestFixture.Move(Direction.South);
        if (self.X < ChannelTile.X)
            return GenericDeathmatchSessionTestFixture.Move(Direction.East);
        // Both tiles are on the SAME objective region, so an oscillating body
        // never leaves the point — it only ever stops being still.
        return oscillate
            ? GenericDeathmatchSessionTestFixture.Move(
                observation.Self.Position == ChannelTile
                    ? Direction.South
                    : Direction.North)
            : GenericDeathmatchSessionTestFixture.Wait();
    }

    private sealed record TickRecord(
        int Tick,
        int? ClaimingTeamId,
        int CaptureProgress,
        int ActivePositionIndex,
        int ChannelerBodiesOnObjective,
        ImmutableArray<DamageRecord> Damage);

    private sealed record DamageRecord(
        int TeamId,
        Position Position,
        int Amount);
}
