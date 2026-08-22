using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The neutral veterancy Workshop contract, driven at the mode seam so no
/// mind can hide a timing bug: the tick-25 unlock boundary, the 15 exact
/// qualifying ticks, the single XP grant through the ordinary ladder, the
/// historical 45-tick dormancy, current repeatable channels, per-Workshop
/// independence, every reset cause, identity hygiene across lives and teams,
/// and the profile gating that keeps historical rules bytes untouched.
/// </summary>
public sealed class ArcRelayVeterancyWorkshopTests
{
    private const int UnlockTick = 25;
    private const int ChannelTicks = 15;
    private const int DormancyTicks = 45;
    private static readonly Position NorthTile = new(18, 4);
    private static readonly Position SouthTile = new(18, 22);

    [Fact]
    public void Broadloom1DeclaresTheWorkshopContract()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Broadloom1);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);

        Assert.Equal("arc-relay-broadloom-1", definition.Rules.RulesetId);
        Assert.Equal("arc-relay-broadloom-01", definition.Map.Id);
        Assert.Equal(37, definition.Map.Width);
        Assert.Equal(27, definition.Map.Height);
        ArcRelayVeterancyWorkshopRules rules =
            Assert.IsType<ArcRelayVeterancyWorkshopRules>(
                mode.VeterancyWorkshops);
        Assert.True(rules.WorkshopIds.SequenceEqual(["north", "south"]));
        Assert.Equal(UnlockTick, rules.UnlockTick);
        Assert.Equal(ChannelTicks, rules.RequiredChannelTicks);
        Assert.Equal(1, rules.XpAward);
        Assert.Equal(DormancyTicks, rules.DormancyTicks);

        // Proof 18: the exact training tiles refuse signature placement, so
        // walls, mines and sentries can never land on them.
        ActorMapTileTagDefinition forbidden = definition.Map.TileTags.Single(
            value => value.TagId == "arc-required-tiles");
        Assert.Contains(NorthTile, forbidden.Tiles);
        Assert.Contains(SouthTile, forbidden.Tiles);

        // Proof 17: both rotational sides receive identical opportunity —
        // the south geometry is the exact 180-degree image of the north.
        Position Rotate(Position value) => new(36 - value.X, 26 - value.Y);
        Assert.Equal(SouthTile, Rotate(NorthTile));
        ImmutableHashSet<Position> northContest = definition.Map.Regions
            .Single(value =>
                value.RegionId == "veterancy-workshop-north-contest")
            .Tiles.ToImmutableHashSet();
        ImmutableHashSet<Position> southContest = definition.Map.Regions
            .Single(value =>
                value.RegionId == "veterancy-workshop-south-contest")
            .Tiles.ToImmutableHashSet();
        Assert.Equal(
            southContest,
            northContest.Select(Rotate).ToImmutableHashSet());
        Assert.Contains(NorthTile, northContest);
    }

    [Fact]
    public void Broadloom3RemovesDormancyWithoutRewritingBroadloom2()
    {
        ArcRelayVeterancyWorkshopRules historical = Assert.IsType<
            ArcRelayVeterancyWorkshopRules>(Assert.IsType<
                ArcRelayGameModeDefinition>(ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.Broadloom2)
                    .Rules.GameMode).VeterancyWorkshops);
        ArcRelayVeterancyWorkshopRules current = Assert.IsType<
            ArcRelayVeterancyWorkshopRules>(Assert.IsType<
                ArcRelayGameModeDefinition>(ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.Broadloom3)
                    .Rules.GameMode).VeterancyWorkshops);

        Assert.Equal(DormancyTicks, historical.DormancyTicks);
        Assert.Equal(0, current.DormancyTicks);
        Assert.Equal("arc-relay-broadloom-3",
            ArcRelayLoopProfile.Broadloom3.RulesetId);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.Broadloom2).Rules),
            ActorContractFingerprint.ComputeRules(ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.Broadloom3).Rules));
    }

    [Fact]
    public void HistoricalProfilesStayWorkshopFreeAndByteIdentical()
    {
        ActorResolvedMatchDefinition tapline = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline14);
        ActorResolvedMatchDefinition broadloom = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Broadloom1);

        Assert.Null(Assert.IsType<ArcRelayGameModeDefinition>(
            tapline.Rules.GameMode).VeterancyWorkshops);
        string taplineCanonical =
            ActorContractManifestSerializer.ToCanonicalJson(tapline);
        Assert.DoesNotContain("veterancyWorkshops", taplineCanonical);
        string broadloomCanonical =
            ActorContractManifestSerializer.ToCanonicalJson(broadloom);
        Assert.Contains("\"veterancyWorkshops\"", broadloomCanonical);
        Assert.Contains("\"requiredChannelTicks\":15", broadloomCanonical);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(tapline.Rules),
            ActorContractFingerprint.ComputeRules(broadloom.Rules));
    }

    [Fact]
    public void TrainingUnlocksExactlyAtTick25()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity trainee = ActorIdentity.FromTeamUnitLife(0, 3, 0);

        // Standing on the tile before the unlock builds nothing and stays
        // publicly dormant with the unlock as its one countdown.
        GenericActorModeTickResult early = Tick(
            driver, UnlockTick - 1, Life(trainee, NorthTile));
        Assert.Empty(WorkshopFacts(early));
        ArcRelayVeterancyWorkshopState dormant = Workshop(driver, "north");
        Assert.Equal(ArcRelayVeterancyWorkshopPhase.Dormant, dormant.Phase);
        Assert.Equal(UnlockTick, dormant.ReadyAtTick);
        Assert.Equal(0, dormant.ProgressTicks);

        // The unlock tick itself both publishes readiness and counts as the
        // first qualifying tick for an already-still body.
        GenericActorModeTickResult unlock = Tick(
            driver, UnlockTick, Life(trainee, NorthTile));
        Assert.Single(Facts(unlock).OfType<ArcRelayEvent.WorkshopReady>(),
            value => value.WorkshopId == "north");
        Assert.Single(
            Facts(unlock).OfType<ArcRelayEvent.WorkshopTrainingStarted>());
        ArcRelayVeterancyWorkshopState training = Workshop(driver, "north");
        Assert.Equal(ArcRelayVeterancyWorkshopPhase.Training, training.Phase);
        Assert.Equal(1, training.ProgressTicks);
        Assert.Equal(ChannelTicks, training.RequiredTicks);
        Assert.Equal(0, training.TrainingTeamId);
        Assert.Null(training.ReadyAtTick);
    }

    [Fact]
    public void FifteenExactQualifyingTicksCompleteOnceAndGrantOneXp()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity trainee = ActorIdentity.FromTeamUnitLife(0, 3, 0);

        int completionTick = UnlockTick + ChannelTicks - 1;
        for (int tick = UnlockTick; tick < completionTick; tick++)
        {
            GenericActorModeTickResult step = Tick(
                driver, tick, Life(trainee, NorthTile));
            Assert.Empty(Facts(step)
                .OfType<ArcRelayEvent.WorkshopTrainingCompleted>());
            Assert.Equal(
                tick - UnlockTick + 1,
                Workshop(driver, "north").ProgressTicks);
        }
        GenericActorModeTickResult completed = Tick(
            driver, completionTick, Life(trainee, NorthTile));
        ArcRelayEvent.WorkshopTrainingCompleted completion = Assert.Single(
            Facts(completed)
                .OfType<ArcRelayEvent.WorkshopTrainingCompleted>());
        Assert.Equal("north", completion.WorkshopId);
        Assert.Equal(trainee, completion.TraineeActorId);
        Assert.Equal(1, completion.XpAward);
        Assert.Equal(
            completionTick + 1 + DormancyTicks,
            completion.ReadyAgainTick);
        // One grant of exactly 1 XP: below the 2-XP level threshold, so no
        // level-up yet — the ladder is the ordinary veterancy ladder.
        Assert.Empty(Facts(completed).OfType<ArcRelayEvent.LeveledUp>());
        Assert.Equal(1, driver.VeterancyLevel(trainee));

        ArcRelayVeterancyWorkshopState dormant = Workshop(driver, "north");
        Assert.Equal(ArcRelayVeterancyWorkshopPhase.Dormant, dormant.Phase);
        Assert.Equal(0, dormant.ProgressTicks);
        Assert.Equal(completion.ReadyAgainTick, dormant.ReadyAtTick);
    }

    [Fact]
    public void DormancyHoldsFor45TicksAndReopensOnTheExactBoundary()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity trainee = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        int completionTick = CompleteOneSession(driver, trainee);
        int readyAgain = completionTick + 1 + DormancyTicks;

        for (int tick = completionTick + 1; tick < readyAgain; tick++)
        {
            GenericActorModeTickResult held = Tick(
                driver, tick, Life(trainee, NorthTile));
            Assert.Empty(WorkshopFacts(held));
            Assert.Equal(
                ArcRelayVeterancyWorkshopPhase.Dormant,
                Workshop(driver, "north").Phase);
            Assert.Equal(0, Workshop(driver, "north").ProgressTicks);
        }
        GenericActorModeTickResult reopened = Tick(
            driver, readyAgain, Life(trainee, NorthTile));
        Assert.Single(
            Facts(reopened).OfType<ArcRelayEvent.WorkshopReady>());
        Assert.Equal(1, Workshop(driver, "north").ProgressTicks);

        // A second full session levels the bot: 1 + 1 XP at 2 XP per level.
        for (int tick = readyAgain + 1;
             tick < readyAgain + ChannelTicks; tick++)
        {
            Tick(driver, tick, Life(trainee, NorthTile));
        }
        Assert.Equal(2, driver.VeterancyLevel(trainee));
    }

    [Fact]
    public void ZeroDormancyCompletesEightBackToBackChannelsIn120Ticks()
    {
        ArcRelayActorMatchModeDriver driver = Driver(
            ArcRelayLoopProfile.Broadloom3);
        ActorIdentity trainee = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        var completionTicks = new List<int>();

        for (int tick = UnlockTick;
             tick < UnlockTick + ChannelTicks * 8;
             tick++)
        {
            GenericActorModeTickResult step = Tick(
                driver, tick, Life(trainee, NorthTile));
            ArcRelayEvent.WorkshopTrainingCompleted? completion = Facts(step)
                .OfType<ArcRelayEvent.WorkshopTrainingCompleted>()
                .SingleOrDefault();
            if (completion is not null)
            {
                completionTicks.Add(tick);
                Assert.Equal(tick + 1, completion.ReadyAgainTick);
            }
        }

        Assert.Equal(
            Enumerable.Range(1, 8)
                .Select(session => UnlockTick + ChannelTicks * session - 1),
            completionTicks);
        Assert.Equal(5, driver.VeterancyLevel(trainee));
        GenericActorModeTickResult capped = Tick(
            driver,
            UnlockTick + ChannelTicks * 8,
            Life(trainee, NorthTile));
        Assert.Empty(Facts(capped).OfType<ArcRelayEvent.LeveledUp>());
        Assert.Equal(
            ArcRelayVeterancyWorkshopPhase.Ready,
            Workshop(driver, "north").Phase);
    }

    [Fact]
    public void WorkshopsOperateIndependently()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity north = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        ActorIdentity south = ActorIdentity.FromTeamUnitLife(1, 3, 0);

        // Both channels advance in the same ticks; interrupting the south
        // channel leaves the north channel untouched.
        for (int tick = UnlockTick; tick < UnlockTick + 5; tick++)
        {
            Tick(driver, tick, Life(north, NorthTile), Life(south, SouthTile));
        }
        Assert.Equal(5, Workshop(driver, "north").ProgressTicks);
        Assert.Equal(5, Workshop(driver, "south").ProgressTicks);

        GenericActorModeTickResult southLeft = Tick(
            driver, UnlockTick + 5, Life(north, NorthTile));
        ArcRelayEvent.WorkshopTrainingInterrupted interrupted = Assert.Single(
            Facts(southLeft)
                .OfType<ArcRelayEvent.WorkshopTrainingInterrupted>());
        Assert.Equal("south", interrupted.WorkshopId);
        Assert.Equal(5, interrupted.ProgressLost);
        Assert.Equal(6, Workshop(driver, "north").ProgressTicks);
        Assert.Equal(0, Workshop(driver, "south").ProgressTicks);
    }

    [Fact]
    public void EnemyContestResetsWithoutTouchingTheTrainingTile()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity trainee = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        Advance(driver, trainee, ticks: 8);

        // (16,7) is antechamber ground inside the authored contest region —
        // not the training tile — and a living enemy there stops everything.
        ActorIdentity raider = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        GenericActorModeTickResult contested = Tick(
            driver,
            UnlockTick + 8,
            Life(trainee, NorthTile),
            Life(raider, new Position(16, 7)));
        ArcRelayEvent.WorkshopTrainingInterrupted interrupted = Assert.Single(
            Facts(contested)
                .OfType<ArcRelayEvent.WorkshopTrainingInterrupted>());
        Assert.Equal(
            ArcRelayEvent.WorkshopInterruptionKind.Contested,
            interrupted.Reason);
        Assert.Equal(8, interrupted.ProgressLost);
        Assert.Equal(
            ArcRelayVeterancyWorkshopPhase.Contested,
            Workshop(driver, "north").Phase);

        // Friendly presence in the same region is not a contest, and extra
        // friendly bodies never accelerate progress (proof 9).
        ActorIdentity guard = ActorIdentity.FromTeamUnitLife(0, 4, 0);
        Tick(driver, UnlockTick + 9,
            Life(trainee, NorthTile), Life(guard, new Position(16, 7)));
        Tick(driver, UnlockTick + 10,
            Life(trainee, NorthTile), Life(guard, new Position(16, 7)));
        Assert.Equal(2, Workshop(driver, "north").ProgressTicks);
        Assert.Equal(
            ArcRelayVeterancyWorkshopPhase.Training,
            Workshop(driver, "north").Phase);
    }

    [Theory]
    [InlineData("moved")]
    [InlineData("damaged")]
    [InlineData("combat")]
    public void MovementDamageAndCombatVerbsResetProgress(string cause)
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity trainee = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        Advance(driver, trainee, ticks: 6);

        GenericActorModeTickResult reset = driver.ApplyJointTick(
            World(Life(trainee, NorthTile)),
            new GenericActorModeTickInput(
                UnlockTick + 6,
                [],
                [],
                movedOrDisplacedActors: cause == "moved"
                    ? new HashSet<ActorIdentity> { trainee }
                    : null,
                combatActingActors: cause == "combat"
                    ? new HashSet<ActorIdentity> { trainee }
                    : null,
                damagedActors: cause == "damaged"
                    ? new HashSet<ActorIdentity> { trainee }
                    : null));
        ArcRelayEvent.WorkshopTrainingInterrupted interrupted = Assert.Single(
            Facts(reset)
                .OfType<ArcRelayEvent.WorkshopTrainingInterrupted>());
        Assert.Equal(6, interrupted.ProgressLost);
        Assert.Equal(
            cause switch
            {
                "moved" => ArcRelayEvent.WorkshopInterruptionKind.Moved,
                "damaged" => ArcRelayEvent.WorkshopInterruptionKind.Damaged,
                _ => ArcRelayEvent.WorkshopInterruptionKind.ActedInCombat,
            },
            interrupted.Reason);
        Assert.Equal(0, Workshop(driver, "north").ProgressTicks);

        // A quiet Wait (absent from every fact set) resumes from zero on the
        // very next tick — no permanent claimant, no reservation (proof 8).
        Tick(driver, UnlockTick + 7, Life(trainee, NorthTile));
        Assert.Equal(1, Workshop(driver, "north").ProgressTicks);
    }

    [Fact]
    public void DeathAndRejoinCannotInheritAndTeamsNeverAlias()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity firstLife = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        Advance(driver, firstLife, ticks: 9);

        // The trainee dies: the channel resets with the death spelled out.
        GenericActorModeTickResult died = driver.ApplyJointTick(
            World(),
            new GenericActorModeTickInput(
                UnlockTick + 9,
                [],
                [
                    new FrontlineScrapDestruction(
                        firstLife, NorthTile, SourceActorId: null),
                ]));
        ArcRelayEvent.WorkshopTrainingInterrupted interrupted = Assert.Single(
            Facts(died)
                .OfType<ArcRelayEvent.WorkshopTrainingInterrupted>());
        Assert.Equal(
            ArcRelayEvent.WorkshopInterruptionKind.Died, interrupted.Reason);

        // The slot's next life starts from zero: identity is team + slot +
        // life, so nothing carries over — and its arrival tick is a move,
        // which the session reports, so nothing counts that tick either.
        ActorIdentity secondLife = ActorIdentity.FromTeamUnitLife(0, 3, 1);
        driver.ApplyJointTick(
            World(Life(secondLife, NorthTile)),
            new GenericActorModeTickInput(
                UnlockTick + 10,
                [],
                [],
                movedOrDisplacedActors:
                    new HashSet<ActorIdentity> { secondLife }));
        Assert.Equal(0, Workshop(driver, "north").ProgressTicks);
        Tick(driver, UnlockTick + 11, Life(secondLife, NorthTile));
        Assert.Equal(1, Workshop(driver, "north").ProgressTicks);

        // The same unit id on the OTHER team trains the south Workshop at
        // the same time without any aliasing.
        ActorIdentity mirrored = ActorIdentity.FromTeamUnitLife(1, 3, 1);
        Tick(driver, UnlockTick + 12,
            Life(secondLife, NorthTile), Life(mirrored, SouthTile));
        Assert.Equal(2, Workshop(driver, "north").ProgressTicks);
        Assert.Equal(1, Workshop(driver, "south").ProgressTicks);
        Assert.Equal(0, Workshop(driver, "north").TrainingTeamId);
        Assert.Equal(1, Workshop(driver, "south").TrainingTeamId);
    }

    [Fact]
    public void MaxLevelBodiesCannotOpenAChannel()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity veteran = ActorIdentity.FromTeamUnitLife(0, 3, 0);

        // Feed kill XP through the ordinary ladder until the cap: 2 XP per
        // level, max level 5, so 8 XP saturates it.
        for (int victim = 0; victim < 8; victim++)
        {
            driver.HandleDestructions(
                UnlockTick - 10,
                [
                    new FrontlineScrapDestruction(
                        ActorIdentity.FromTeamUnitLife(1, 0, victim),
                        new Position(20, 6),
                        veteran),
                ]);
        }
        Assert.Equal(5, driver.VeterancyLevel(veteran));

        Tick(driver, UnlockTick, Life(veteran, NorthTile));
        Tick(driver, UnlockTick + 1, Life(veteran, NorthTile));
        Assert.Equal(0, Workshop(driver, "north").ProgressTicks);
        Assert.Equal(
            ArcRelayVeterancyWorkshopPhase.Ready,
            Workshop(driver, "north").Phase);
    }

    private static int CompleteOneSession(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity trainee)
    {
        int completionTick = UnlockTick + ChannelTicks - 1;
        for (int tick = UnlockTick; tick <= completionTick; tick++)
            Tick(driver, tick, Life(trainee, NorthTile));
        Assert.Equal(
            ArcRelayVeterancyWorkshopPhase.Dormant,
            Workshop(driver, "north").Phase);
        return completionTick;
    }

    private static void Advance(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity trainee,
        int ticks)
    {
        for (int tick = UnlockTick; tick < UnlockTick + ticks; tick++)
            Tick(driver, tick, Life(trainee, NorthTile));
        Assert.Equal(ticks, Workshop(driver, "north").ProgressTicks);
    }

    private static GenericActorModeTickResult Tick(
        ArcRelayActorMatchModeDriver driver,
        int tick,
        params GenericActorModeActiveLife[] lives) =>
        driver.ApplyJointTick(
            World(lives),
            new GenericActorModeTickInput(tick, [], []));

    private static ArcRelayActorMatchModeDriver Driver(
        ArcRelayLoopProfile? profile = null) => new(
        ArcRelayH0Definition.Create(
            loopProfile: profile ?? ArcRelayLoopProfile.Broadloom1),
        matchSeed: 20_260_822UL);

    private static ArcRelayVeterancyWorkshopState Workshop(
        ArcRelayActorMatchModeDriver driver,
        string workshopId) =>
        Assert.IsType<GenericActorModeState.ArcRelay>(driver.State)
            .State.Workshops.Single(value => value.WorkshopId == workshopId);

    private static ArcRelayEvent[] Facts(
        GenericActorModeTickResult result) =>
        result.ModeEvents.Select(value => Assert.IsType<
                GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                value.Payload).Fact)
            .ToArray();

    private static ArcRelayEvent[] WorkshopFacts(
        GenericActorModeTickResult result) =>
        Facts(result).Where(value => value is
                ArcRelayEvent.WorkshopReady
                or ArcRelayEvent.WorkshopTrainingStarted
                or ArcRelayEvent.WorkshopTrainingInterrupted
                or ArcRelayEvent.WorkshopTrainingCompleted)
            .ToArray();

    private static GenericActorModeActiveLife Life(
        ActorIdentity actor,
        Position position) =>
        new(actor, "arc-body-longshot", position, health: 30);

    private static GenericActorModeWorldView World(
        params GenericActorModeActiveLife[] lives) =>
        new(
            new Dictionary<int, long>
            {
                [0] = lives.Where(value => value.ActorId.TeamId == 0)
                    .Sum(value => value.Health),
                [1] = lives.Where(value => value.ActorId.TeamId == 1)
                    .Sum(value => value.Health),
            },
            [0, 1],
            lives.ToImmutableArray());
}
