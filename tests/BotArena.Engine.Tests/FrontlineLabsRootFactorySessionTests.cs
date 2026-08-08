namespace BotArena.Engine.Tests;

/// <summary>
/// THE ROOT FACTORY, driven live (DECISIONS #194). Under one chassis per class
/// every fabricator body is placed by the verb, so a participant that loses its
/// last body could never place another — the owner ruled that the HOME BASE
/// acts as the root factory instead. These pin that the session actually does
/// it: one body, at home, after the class's own delay, and only while the
/// participant truly holds nothing.
/// </summary>
public sealed class FrontlineLabsRootFactorySessionTests
{
    /// <summary>
    /// A unified fabricator against a bulwark that never stops firing. The
    /// fabricator spends nothing on its verb, so its opening body is the only
    /// body it will ever have — and the base is what puts the next one on the
    /// board.
    /// </summary>
    private static ActorResolvedMatchDefinition Cell() =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ContestMajority
                | FrontlineLabsPendulumArm.EnemySoleDecay,
            (FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Fabricator),
            chassis: FrontlineLabsChassisArm.Unified);

    /// <summary>
    /// The bulwark holds its pad and fires straight down the open row; the
    /// fabricator walks into it and never builds anything. That is a total
    /// loss on a repeating clock, which is exactly the state the bootstrap
    /// exists to answer.
    /// </summary>
    private static GenericActorRuntimeDecision Script(
        GenericActorRuntimeStart start,
        GenericActorRuntimeObservation observation) =>
        start.ActorId.TeamId == 0
            ? FrontlineLabsSkillArmTestFixture.ShootStraight()
            : GenericDeathmatchSessionTestFixture.Move(Direction.West);

    private static GenericActorMatchChronology Run() =>
        FrontlineLabsSkillArmTestFixture.Run(Cell(), Script);

    /// <summary>
    /// Every base seed in the run, with the tick it landed on. A seed is an
    /// ordinary life-spawn event carrying the new spawn reason, so nothing new
    /// had to be subscribed to.
    /// </summary>
    private static (int Tick,
        GenericActorRuntimeObservation.EventPayload.LifeSpawned Payload)[]
        Seeds(GenericActorMatchChronology run) =>
        [
            .. run.Ticks
                .SelectMany(frame => frame.TickStart.Events
                    .Concat(frame.Events)
                    .Select(item => (frame.Tick, item.Payload)))
                .Select(entry => (
                    entry.Tick,
                    Payload: entry.Payload
                        as GenericActorRuntimeObservation.EventPayload
                            .LifeSpawned))
                .Where(entry =>
                    entry.Payload is not null
                    && entry.Payload.Reason
                    == GenericActorRuntimeStart.SpawnReason.RootFactorySeed)
                .Select(entry => (entry.Tick, entry.Payload!)),
        ];

    [Fact]
    public void TheBaseSeedsExactlyOneBodyAtHomeWhenAParticipantIsWiped()
    {
        GenericActorMatchChronology run = Run();
        (int Tick,
            GenericActorRuntimeObservation.EventPayload.LifeSpawned Payload)[]
            seeds = Seeds(run);

        // The bootstrap fired at all — a fabricator that never spends its verb
        // is a participant that WILL be wiped on this cell.
        Assert.NotEmpty(seeds);
        Assert.All(
            seeds,
            seed =>
            {
                // Only the class whose bodies are built can be bootstrapped.
                Assert.Equal(1, seed.Payload.ActorId.TeamId);
                // At HOME, on the slot the authored PrimeSpawn pad now
                // reserves as an ordinary home spawn.
                Assert.Equal(0, seed.Payload.ActorId.UnitId);
                Assert.Equal(new Position(20, 7), seed.Payload.Position);
                Assert.Equal(
                    FrontlineLabsClassDefinition.Fabricator.UnifiedFormId,
                    seed.Payload.FormId);
                // A fresh lineage: the structure is not a parent.
                Assert.Equal(0, seed.Payload.Generation);
                Assert.Null(seed.Payload.ParentActorId);
                Assert.Null(seed.Payload.SourceTransitionId);
                Assert.Null(seed.Payload.SourceOperationId);
            });
        // One body, never two: the base is a floor under a wipe, not a
        // second army.
        Assert.All(
            seeds.GroupBy(seed => seed.Tick),
            group => Assert.Single(group));
    }

    [Fact]
    public void TheSeedArrivesOnTheClassesOwnRebuildClockAfterATotalLoss()
    {
        GenericActorMatchChronology run = Run();
        int delay = FrontlineLabsClassDefinition.Fabricator
            .UnifiedLifecycleDelayTicks;

        foreach ((int tick, _) in Seeds(run))
        {
            // The last tick whose POST-state still held a body; the body died
            // during the tick after it, so the ordinary destruction grammar —
            // destroyed tick + 1 + profile delay — lands two past it.
            int lastHeld = run.Ticks
                .Where(frame =>
                    frame.Tick < tick
                    && frame.PostState.ActiveLives.Any(life =>
                        life.ActorId.TeamId == 1))
                .Select(frame => frame.Tick)
                .DefaultIfEmpty(-1)
                .Max();
            Assert.Equal(lastHeld + 2 + delay, tick);

            // And the participant genuinely held nothing right up to it.
            Assert.All(
                run.Ticks.Where(frame =>
                    frame.Tick > lastHeld && frame.Tick < tick),
                frame => Assert.DoesNotContain(
                    frame.PostState.ActiveLives,
                    life => life.ActorId.TeamId == 1));
        }
    }

    [Fact]
    public void TheSplitChassisDeclaresNoBootstrapAndSeedsNothing()
    {
        GenericActorMatchChronology run =
            FrontlineLabsSkillArmTestFixture.Run(
                FrontlineLabsDefinition.CreatePendulumExperiment(
                    FrontlineLabsPendulumArm.StickyFrontline
                        | FrontlineLabsPendulumArm.ContestMajority
                        | FrontlineLabsPendulumArm.EnemySoleDecay,
                    (FrontlineLabsClassDefinition.Bulwark,
                        FrontlineLabsClassDefinition.Fabricator)),
                Script);

        // On the measured shape the prime respawns by itself, so the base has
        // nothing to answer and the arm writes no seed at all.
        Assert.Empty(Seeds(run));
    }
}
